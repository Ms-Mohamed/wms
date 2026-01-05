# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first
COPY WMS.API/WMS.API.csproj WMS.API/
COPY WMS.Business/WMS.Business.csproj WMS.Business/
COPY WMS.Data/WMS.Data.csproj WMS.Data/

# Restore dependencies
RUN dotnet restore WMS.API/WMS.API.csproj

# Copy the rest of the source code
COPY . .

# Build and Publish
WORKDIR /src/WMS.API
RUN dotnet publish -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WMS.API.dll"]
