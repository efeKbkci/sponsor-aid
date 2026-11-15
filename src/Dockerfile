# Multi-stage Dockerfile for the SponsorAid ASP.NET Core API.
# Separates SDK compilation from the final runtime image to minimize container size.

# Stage 1: Build and publish using the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy protobuf contracts and project definitions first to maximize Docker layer cache.
COPY contracts/ ./contracts/
COPY src/SponsorAid.Api/*.csproj ./src/SponsorAid.Api/
COPY src/SponsorAid.Application/*.csproj ./src/SponsorAid.Application/
COPY src/SponsorAid.Domain/*.csproj ./src/SponsorAid.Domain/
COPY src/SponsorAid.Infrastructure/*.csproj ./src/SponsorAid.Infrastructure/

# Restores package dependencies before copying source files to avoid re-downloading on every code change.
RUN dotnet restore src/SponsorAid.Api/SponsorAid.Api.csproj

# Copy remaining source code and publish release binaries.
COPY src/ ./src/
RUN dotnet publish src/SponsorAid.Api/SponsorAid.Api.csproj -c Release -o /app/publish --no-restore

# Stage 2: Minimal runtime image for production execution.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Installs Kerberos GSSAPI library required by PostgreSQL authentication providers on Linux.
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

# Enables full culture support and configures Turkish locale for string casing rules.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LC_ALL=tr_TR.UTF-8
ENV LANG=tr_TR.UTF-8

# Copies only compiled application outputs from the build container.
COPY --from=build /app/publish .

# Creates shared folder for document uploads mounted by docker-compose volumes.
RUN mkdir -p /app/wwwroot/uploads

ENTRYPOINT ["dotnet", "SponsorAid.Api.dll"]
