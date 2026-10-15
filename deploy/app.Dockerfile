# Copyright (c) 2026 Bryan C. Weis and Common Consulting Company
# One image: the built console is copied into the API wwwroot. Same origin, no second host.
# Live API mode: the production bundle does not start MSW and calls this origin.

FROM node:22-bookworm-slim AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
COPY contracts/msw/ /src/contracts/msw/
COPY data/glossary.yaml /src/data/glossary.yaml
COPY data/fleet/ /src/data/fleet/
ARG VITE_USE_MSW=false
ARG VITE_API_BASE=
ENV VITE_USE_MSW=$VITE_USE_MSW
ENV VITE_API_BASE=$VITE_API_BASE
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json nuget.config ./
COPY src/ src/
COPY prompts/ prompts/
RUN dotnet restore src/Mps.Api/Mps.Api.csproj
RUN dotnet publish src/Mps.Api/Mps.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=api /app/publish .
COPY --from=web /src/web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Mps=Data Source=/app/data/mps.db
EXPOSE 8080
USER root
RUN set -eux; \
    id="${APP_UID:-1654}"; \
    mkdir -p /app/data; \
    chown "$id:0" /app/data; \
    chmod 775 /app/data
USER ${APP_UID:-1654}
ENTRYPOINT ["dotnet", "Mps.Api.dll"]
