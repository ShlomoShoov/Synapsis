# The C# Kafka relay worker. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates || true
WORKDIR /repo
COPY schemas/ schemas/
COPY src/dotnet/ src/dotnet/
RUN dotnet publish src/dotnet/Synapsis.KafkaRelay/Synapsis.KafkaRelay.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates || true
WORKDIR /app
COPY --from=build /app .
# The .NET Generic Host handles SIGINT as a graceful shutdown signal.  Make
# Docker Compose use it when stopping the relay so the host can cancel the
# change-stream loop and dispose the Kafka producer before the container exits.
STOPSIGNAL SIGINT
# Use CMD so a one-off `docker compose run kafka-relay <command>` replaces
# the service command instead of appending arguments to the relay process.
CMD ["dotnet", "Synapsis.KafkaRelay.dll"]
