# Multi-stage build for the ASP.NET Core 8 Web API.
# Build context must be the repository root.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore only the WebApi project (+ its transitive project refs). Restoring the
# whole .sln would require the test projects under backend/tests, which are not
# copied into the runtime image.
COPY backend/Directory.Build.props backend/dotnet-tools.json ./backend/
COPY backend/src ./backend/src
RUN dotnet restore backend/src/ERP.WebApi/ERP.WebApi.csproj

# Publish the API
RUN dotnet publish backend/src/ERP.WebApi/ERP.WebApi.csproj \
    -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as a non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ERP.WebApi.dll"]
