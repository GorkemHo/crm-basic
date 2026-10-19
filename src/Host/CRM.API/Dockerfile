# Build context: repository root.
#   docker build -f src/Host/CRM.API/Dockerfile -t crm-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY Directory.Build.props Directory.Packages.props ./
# Everything under src/, so newly added modules need no Dockerfile change.
COPY src/ src/

RUN dotnet publish src/Host/CRM.API/CRM.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root user provided by the base image.
USER $APP_UID

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CRM.API.dll"]
