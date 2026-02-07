# Consultez https://aka.ms/customizecontainer pour savoir comment personnaliser votre conteneur de débogage et comment Visual Studio utilise ce Dockerfile pour générer vos images afin d'accélérer le débogage.

# Cet index est utilisé lors de l'exécution à partir de VS en mode rapide (par défaut pour la configuration de débogage)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443


# Cette phase est utilisée pour générer le projet de service
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj", "BlazorServerApp.ViewBlazor/"]
RUN dotnet restore "./BlazorServerApp.ViewBlazor/BlazorServerApp.ViewBlazor.csproj"
COPY . .
WORKDIR "/src/BlazorServerApp.ViewBlazor"
RUN dotnet build "./BlazorServerApp.ViewBlazor.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Cette étape permet de publier le projet de service à copier dans la phase finale
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BlazorServerApp.ViewBlazor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Cette phase est utilisée en production ou lors de l'exécution à partir de VS en mode normal (par défaut quand la configuration de débogage n'est pas utilisée)
FROM base AS final
WORKDIR /app

# Répertoire pour la base de données SQLite (volume persistant)
RUN mkdir -p /data
VOLUME /data

# Configuration par défaut (surchargeable via docker run -e ou docker-compose)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80
ENV ConnectionStrings__DefaultConnection="Data Source=/data/BlazorApp.db"
ENV OpenIdConnect__Authority="https://accounts.google.com"
ENV OpenIdConnect__ClientId=""
ENV OpenIdConnect__ClientSecret=""

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlazorServerApp.ViewBlazor.dll"]
