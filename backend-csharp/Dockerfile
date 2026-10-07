# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer unless a csproj changes)
COPY WMS.API/WMS.API.csproj WMS.API/
COPY WMS.Business/WMS.Business.csproj WMS.Business/
COPY WMS.Data/WMS.Data.csproj WMS.Data/
RUN dotnet restore WMS.API/WMS.API.csproj

COPY WMS.API/ WMS.API/
COPY WMS.Business/ WMS.Business/
COPY WMS.Data/ WMS.Data/
WORKDIR /src/WMS.API
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage (non-root)
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "WMS.API.dll"]
