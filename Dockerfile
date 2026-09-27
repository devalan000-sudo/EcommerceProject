# syntax=docker/dockerfile:1
FROM node:22-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/src ./src
COPY frontend/public ./public
COPY frontend/index.html frontend/vite.config.js ./
ENV VITE_DEMO_CATALOG=false VITE_API_URL=/api
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/Catalog.Api/Catalog.Api.csproj backend/Catalog.Api/
RUN dotnet restore backend/Catalog.Api/Catalog.Api.csproj
COPY backend/Catalog.Api/ backend/Catalog.Api/
COPY --from=frontend /src/frontend/dist/ frontend/dist/
RUN dotnet publish backend/Catalog.Api/Catalog.Api.csproj -c Release --no-restore -p:UseAppHost=false -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
RUN mkdir -p /var/lib/catalog/keys && chown -R app:app /var/lib/catalog
COPY --from=build /out/ ./
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD ["dotnet", "Catalog.Api.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
