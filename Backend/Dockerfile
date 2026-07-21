# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/AshmesMarketplaces.sln Backend/
COPY Backend/AshmesMarketplaces.API/AshmesMarketplaces.API.csproj Backend/AshmesMarketplaces.API/
COPY Backend/AshmesMarketplaces.Application/AshmesMarketplaces.Application.csproj Backend/AshmesMarketplaces.Application/
COPY Backend/AshmesMarketplaces.DataAccess/AshmesMarketplaces.DataAccess.csproj Backend/AshmesMarketplaces.DataAccess/
COPY Backend/AshmesMarketplaces.Domain/AshmesMarketplaces.Domain.csproj Backend/AshmesMarketplaces.Domain/
COPY Backend/AshmesMarketplaces.Infrastructure/AshmesMarketplaces.Infrastructure.csproj Backend/AshmesMarketplaces.Infrastructure/
COPY Backend/AshmesMarketplaces.ParserIngestionCli/AshmesMarketplaces.ParserIngestionCli.csproj Backend/AshmesMarketplaces.ParserIngestionCli/

RUN dotnet restore Backend/AshmesMarketplaces.API/AshmesMarketplaces.API.csproj

COPY Backend/ Backend/

RUN dotnet publish Backend/AshmesMarketplaces.API/AshmesMarketplaces.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    && rm -f /app/publish/appsettings.Development.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AshmesMarketplaces.API.dll"]
