# syntax=docker/dockerfile:1

# ---- Build stage ----
# Uses the full SDK image (large) only to compile — the final image below
# uses the much smaller ASP.NET runtime image instead.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first and restore separately from the rest of the source —
# Docker layer-caches this step, so `docker build` skips the (slow) NuGet
# restore entirely on subsequent builds unless a .csproj actually changed.
COPY DaftechCrm.sln ./
COPY src/DaftechCrm.Domain/DaftechCrm.Domain.csproj src/DaftechCrm.Domain/
COPY src/DaftechCrm.Application/DaftechCrm.Application.csproj src/DaftechCrm.Application/
COPY src/DaftechCrm.Infrastructure/DaftechCrm.Infrastructure.csproj src/DaftechCrm.Infrastructure/
COPY src/DaftechCrm.Api/DaftechCrm.Api.csproj src/DaftechCrm.Api/
RUN dotnet restore src/DaftechCrm.Api/DaftechCrm.Api.csproj

COPY src/ src/
RUN dotnet publish src/DaftechCrm.Api/DaftechCrm.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl isn't included in this base image since .NET 5 — installed here
# specifically so the HEALTHCHECK below has something to call.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

# Runs as a non-root user — the base image ships a built-in unprivileged
# "app" user (uid 1654) specifically for this purpose.
RUN mkdir -p /app/storage/uploads && chown -R app:app /app
USER app

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "DaftechCrm.Api.dll"]
