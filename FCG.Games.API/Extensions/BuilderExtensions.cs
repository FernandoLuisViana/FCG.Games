using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FCG.Games.API.Filters;
using FCG.Games.API.Messaging;
using FCG.Games.API.Middlewares;
using FCG.Games.Domain.Interfaces.Common;
using FCG.Games.Domain.Interfaces.Repositories;
using FCG.Games.Domain.Interfaces.Services;
using FCG.Games.Domain.Validators;
using FCG.Games.Infra.Contexts;
using FCG.Games.Infra.Logging;
using FCG.Games.Infra.Middleware;
using FCG.Games.Infra.Publishers;
using FCG.Games.Infra.Repository;
using FCG.Games.Services.Game;
using FCG.Games.Services.Order;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FCG.Games.API.Extensions
{
    public static class BuilderExtensions
    {
        public static void AddProjectServices(this WebApplicationBuilder builder)
        {
            builder.UseJsonFileConfiguration();
            builder.ConfigureDbContext();
            builder.ConfigureJwt();
            //builder.ConfigureLogMongo();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });
            builder.Services.AddEndpointsApiExplorer();
            builder.ConfigureSwagger();
            builder.ConfigureDI();
            builder.ConfigureHealthCheck();
            builder.ConfigureValidators();
            builder.ConfigureElasticSearch();
        }

        private static void UseJsonFileConfiguration(this WebApplicationBuilder builder)
        {
            var keysDirectoryPath = Path.Combine(AppContext.BaseDirectory, "dataprotection-keys");
            var keysDirectory = new DirectoryInfo(keysDirectoryPath);

            if (!keysDirectory.Exists)
                keysDirectory.Create();

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(keysDirectory)
                .SetApplicationName("FCG.Games");

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
        }

        private static void ConfigureDbContext(this WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("FCG")));
        }

        private static void ConfigureJwt(this WebApplicationBuilder builder)
        {
            var configuration = builder.Configuration.GetSection("Jwt");

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer = configuration["Issuer"],
                        ValidAudience = configuration["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration["Key"]!)
                        ),

                        RoleClaimType = ClaimTypes.Role,
                        NameClaimType = ClaimTypes.NameIdentifier
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
            });
        }

        private static void ConfigureSwagger(this WebApplicationBuilder builder)
        {
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "FCG.Games.API",
                    Version = "v1",
                    Description = "Web API ASP.NET Core - Microserviço de Jogos",
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            In = ParameterLocation.Header,
                            Scheme = "bearer"
                        },
                        Array.Empty<string>()
                    }
                });

                options.SchemaFilter<EnumSchemaFilter>();

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });
        }


        private static void ConfigureDI(this WebApplicationBuilder builder)
        {
            // Repositories
            builder.Services.AddScoped<IGameRepository, GameRepository>();
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
            builder.Services.AddScoped<IElasticSearchRepository, ElasticSearchRepository>();

            // Services
            builder.Services.AddScoped<IGameService, GameService>();
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IPromotionService, PromotionService>();

            // Publishers
            builder.Services.AddSingleton<IOrderEventPublisher, OrderEventPublisher>();

            // Observability
            builder.Services.AddScoped<ICorrelationIdGenerator, CorrelationIdGenerator>();
            builder.Services.AddScoped(typeof(IAppLogger<>), typeof(AppLogger<>));

            // Middlewares
            builder.Services.AddScoped<ExceptionMiddleware>();
            builder.Services.AddScoped(typeof(ValidationFilter<>));

            //Rabbit
            builder.Services.AddSingleton<RabbitPublisher>();

        }

        private static void ConfigureHealthCheck(this WebApplicationBuilder builder)
        {
            builder.Services.AddHealthChecks()
                .AddNpgSql(
                    builder.Configuration.GetConnectionString("FCG")!,
                    name: "postgresql",
                    timeout: TimeSpan.FromSeconds(5)
                );
        }

        private static void ConfigureValidators(this WebApplicationBuilder builder)
        {
            builder.Services.AddValidatorsFromAssembly(typeof(CreateGameRequestValidator).Assembly);
        }

        private static void ConfigureElasticSearch(this WebApplicationBuilder builder)
        {
            var configuration = builder.Configuration.GetSection("Elasticsearch");
            var url = configuration["Url"] ?? throw new InvalidOperationException("ElasticSearch Url not configured");
            var apiKey = configuration["ApiKey"] ?? throw new InvalidOperationException("ElasticSearch ApiKey not configured");

            builder.Services.AddSingleton(sp =>
            {
                var settings = new ElasticsearchClientSettings(new Uri(url))
                    .Authentication(new ApiKey(apiKey));

                return new ElasticsearchClient(settings);
            });
        }
    }
}
