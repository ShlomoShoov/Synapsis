# The Elasticsearch consumer worker. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/dotnet/Directory.Build.props src/dotnet/
COPY src/dotnet/Synapsis.Data/ src/dotnet/Synapsis.Data/
COPY src/dotnet/Synapsis.Elasticsearch/ src/dotnet/Synapsis.Elasticsearch/
RUN dotnet restore src/dotnet/Synapsis.Elasticsearch/Synapsis.Elasticsearch.csproj
RUN dotnet publish src/dotnet/Synapsis.Elasticsearch/Synapsis.Elasticsearch.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Synapsis.Elasticsearch.dll"]
