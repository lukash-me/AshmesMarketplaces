# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/AshmesMarketplaces.sln Backend/
COPY Backend/AshmesMarketplaces.AnalyticsWorker/AshmesMarketplaces.AnalyticsWorker.csproj Backend/AshmesMarketplaces.AnalyticsWorker/
COPY Backend/AshmesMarketplaces.Application/AshmesMarketplaces.Application.csproj Backend/AshmesMarketplaces.Application/
COPY Backend/AshmesMarketplaces.DataAccess/AshmesMarketplaces.DataAccess.csproj Backend/AshmesMarketplaces.DataAccess/
COPY Backend/AshmesMarketplaces.Domain/AshmesMarketplaces.Domain.csproj Backend/AshmesMarketplaces.Domain/

RUN dotnet restore Backend/AshmesMarketplaces.AnalyticsWorker/AshmesMarketplaces.AnalyticsWorker.csproj

COPY Backend/ Backend/

RUN dotnet publish Backend/AshmesMarketplaces.AnalyticsWorker/AshmesMarketplaces.AnalyticsWorker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AshmesMarketplaces.AnalyticsWorker.dll"]
