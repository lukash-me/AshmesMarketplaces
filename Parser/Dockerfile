# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    PATH="/opt/ashmes-parser-venv/bin:${PATH}"

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        fonts-liberation \
        libasound2t64 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libcairo2 \
        libcups2 \
        libdbus-1-3 \
        libdrm2 \
        libgbm1 \
        libglib2.0-0 \
        libgtk-3-0 \
        libnss3 \
        libpango-1.0-0 \
        libu2f-udev \
        libx11-6 \
        libxcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxext6 \
        libxfixes3 \
        libxkbcommon0 \
        libxrandr2 \
        libxshmfence1 \
        python3 \
        python3-venv \
        python3-pip \
        unzip \
        wget \
        xdg-utils \
    && rm -rf /var/lib/apt/lists/*

COPY Parser/requirements.txt Parser/requirements.txt
RUN python3 -m venv /opt/ashmes-parser-venv \
    && pip install --upgrade pip \
    && pip install -r Parser/requirements.txt \
    && python -m playwright install --with-deps chromium \
    && rm -rf /var/lib/apt/lists/*

COPY Backend/AshmesMarketplaces.sln Backend/
COPY Backend/AshmesMarketplaces.Application/AshmesMarketplaces.Application.csproj Backend/AshmesMarketplaces.Application/
COPY Backend/AshmesMarketplaces.DataAccess/AshmesMarketplaces.DataAccess.csproj Backend/AshmesMarketplaces.DataAccess/
COPY Backend/AshmesMarketplaces.Domain/AshmesMarketplaces.Domain.csproj Backend/AshmesMarketplaces.Domain/
COPY Backend/AshmesMarketplaces.ParserIngestionCli/AshmesMarketplaces.ParserIngestionCli.csproj Backend/AshmesMarketplaces.ParserIngestionCli/
RUN dotnet restore Backend/AshmesMarketplaces.ParserIngestionCli/AshmesMarketplaces.ParserIngestionCli.csproj

COPY Backend/ Backend/
COPY Parser/ Parser/

RUN dotnet build Backend/AshmesMarketplaces.ParserIngestionCli/AshmesMarketplaces.ParserIngestionCli.csproj \
    -c Release \
    --no-restore

CMD ["python", "Parser/app/cycle_runner.py", "--help"]
