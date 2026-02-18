# Base image (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copia os arquivos .csproj e restaura
COPY ["FCG.Games.API/FCG.Games.API.csproj", "FCG.Games.API/"]
COPY ["FCG.Games.Domain/FCG.Games.Domain.csproj", "FCG.Games.Domain/"]
COPY ["FCG.Games.Infra/FCG.Games.Infra.csproj", "FCG.Games.Infra/"]
COPY ["FCG.Games.Services/FCG.Games.Services.csproj", "FCG.Games.Services/"]
RUN dotnet restore "FCG.Games.API/FCG.Games.API.csproj"

# Copia o código e build
COPY . .
WORKDIR "/src/FCG.Games.API"
RUN dotnet build "FCG.Games.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "FCG.Games.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FCG.Games.API.dll"]
