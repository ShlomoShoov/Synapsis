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
ENTRYPOINT ["dotnet", "Synapsis.KafkaRelay.dll"]
