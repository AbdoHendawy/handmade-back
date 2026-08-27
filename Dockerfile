# Handmade.Api — multi-stage production image (non-root).
# Build from repo root:
#   docker build -t handmade-api -f Dockerfile .
#
# Migrations are NOT applied on startup. Apply out-of-band before traffic:
#   dotnet ef database update --project src/Handmade.Infrastructure --startup-project src/Handmade.Api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props Handmade.sln ./
COPY src/Handmade.Domain/Handmade.Domain.csproj src/Handmade.Domain/
COPY src/Handmade.Application/Handmade.Application.csproj src/Handmade.Application/
COPY src/Handmade.Infrastructure/Handmade.Infrastructure.csproj src/Handmade.Infrastructure/
COPY src/Handmade.Api/Handmade.Api.csproj src/Handmade.Api/

RUN dotnet restore src/Handmade.Api/Handmade.Api.csproj

COPY src/Handmade.Domain/ src/Handmade.Domain/
COPY src/Handmade.Application/ src/Handmade.Application/
COPY src/Handmade.Infrastructure/ src/Handmade.Infrastructure/
COPY src/Handmade.Api/ src/Handmade.Api/

RUN dotnet publish src/Handmade.Api/Handmade.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 appgroup \
    && useradd --uid 10001 --gid appgroup --shell /usr/sbin/nologin --create-home appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Handmade.Api.dll"]
