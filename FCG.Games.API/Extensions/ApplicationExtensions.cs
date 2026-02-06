using FCG.Games.API.Middlewares;
using FCG.Games.Infra.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FCG.Games.API.Extensions
{
    public static class ApplicationExtensions
    {
        public static void UseProjectConfiguration(this WebApplication app)
        {
            app.UseCustomSwagger();
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCustomMiddlewares();
            app.MapControllers();
            app.GenerateMigrations();
            app.MapHealthChecks("/health");
        }

        private static void UseCustomSwagger(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var swaggerEnabled = config["Swagger:Enabled"];
            if (!string.IsNullOrEmpty(swaggerEnabled) && swaggerEnabled.ToLower() == "true")
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
        }

        public static void UseCustomMiddlewares(this WebApplication app)
        {
            app.UseMiddleware<ExceptionMiddleware>();
        }

        private static void GenerateMigrations(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            dbContext.Database.Migrate();
        }
    }
}
