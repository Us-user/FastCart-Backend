# syntax=docker/dockerfile:1
# Reproducible multi-stage build for the FastCart API (§4.2, §9). Built remotely by Render.

# ---- build stage -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so `restore` is cached until a .csproj changes.
COPY src/FastCart.Api/FastCart.Api.csproj                     src/FastCart.Api/
COPY src/FastCart.Application/FastCart.Application.csproj      src/FastCart.Application/
COPY src/FastCart.Domain/FastCart.Domain.csproj               src/FastCart.Domain/
COPY src/FastCart.Infrastructure/FastCart.Infrastructure.csproj src/FastCart.Infrastructure/
RUN dotnet restore src/FastCart.Api/FastCart.Api.csproj

# Copy the rest of the source and publish a release build.
COPY src/ src/
RUN dotnet publish src/FastCart.Api/FastCart.Api.csproj -c Release -o /app --no-restore

# ---- runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./

# The runtime image defaults to the Production environment. Render injects $PORT and
# expects the service to listen on it; fall back to 8080 for a local `docker run`.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "export ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080}; exec dotnet FastCart.Api.dll"]
