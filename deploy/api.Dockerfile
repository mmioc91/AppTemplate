# The Api image (ADR 0033). Built from the repository root: docker build -f deploy/api.Dockerfile .
# The same image migrates the database as a deploy step (Database__MigrateAndExit=true, ADR 0029) and then serves.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props NuGet.Config BannedSymbols.txt .editorconfig ./
COPY src/ src/
RUN dotnet publish src/AppTemplate.Api/AppTemplate.Api.csproj --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The runtime image's own non-root user; the Api listens on 8080 inside the network, behind the proxy.
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AppTemplate.Api.dll"]
