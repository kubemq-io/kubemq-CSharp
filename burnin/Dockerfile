FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /app

# Copy SDK project
COPY src/KubeMQ.Sdk/KubeMQ.Sdk.csproj /app/src/KubeMQ.Sdk/
RUN dotnet restore /app/src/KubeMQ.Sdk/KubeMQ.Sdk.csproj

# Copy burnin project
COPY burnin/KubeMQ.Burnin.csproj /app/burnin/
RUN dotnet restore /app/burnin/KubeMQ.Burnin.csproj

# Copy source and build
COPY src/KubeMQ.Sdk/ /app/src/KubeMQ.Sdk/
COPY burnin/ /app/burnin/
RUN dotnet publish /app/burnin/KubeMQ.Burnin.csproj -c Release -o /app/out --no-restore

FROM --platform=$TARGETPLATFORM mcr.microsoft.com/dotnet/aspnet:8.0

RUN groupadd -r burnin && useradd -r -g burnin -s /bin/false burnin

WORKDIR /app
COPY --from=builder /app/out .

USER burnin
EXPOSE 8888

HEALTHCHECK --interval=15s --timeout=3s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8888/health || exit 1

ENTRYPOINT ["dotnet", "KubeMQ.Burnin.dll"]
CMD ["--config", "/etc/burnin/config.yaml"]
