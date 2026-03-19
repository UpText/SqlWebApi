# syntax=docker/dockerfile:1

ARG TARGETPLATFORM

# -------------------------
# Build stage
# -------------------------
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore "swa.csproj"
RUN dotnet publish "swa.csproj" -c Debug -o /app/publish


# -------------------------
# Runtime stage
# -------------------------
FROM --platform=$TARGETPLATFORM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

WORKDIR /home/site/wwwroot

# Azure Functions environment
ENV DOTNET_EnableDiagnostics=1
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
ENV AzureWebJobsStorage=UseDevelopmentStorage=true
ENV AzureWebJobsScriptRoot=/home/site/wwwroot

# -----------------------------------
# Install Rider Remote Debugger (RRD)
# -----------------------------------
USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates unzip \
    && rm -rf /var/lib/apt/lists/*

ENV RRD_ROOT=/opt/JetBrains/RiderRemoteDebugger

RUN mkdir -p ${RRD_ROOT} \
    && curl -fL "https://data.services.jetbrains.com/products/download?code=RRD&platform=linux64" \
       -o /tmp/rrd.zip \
    && unzip -q /tmp/rrd.zip -d ${RRD_ROOT} \
    && rm -f /tmp/rrd.zip

# -----------------------------------

EXPOSE 80

COPY --from=build /app/publish .
