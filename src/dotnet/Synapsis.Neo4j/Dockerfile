# The Neo4j consumer worker. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/dotnet/Directory.Build.props src/dotnet/
COPY src/dotnet/Synapsis.Data/ src/dotnet/Synapsis.Data/
COPY src/dotnet/Synapsis.Neo4j/ src/dotnet/Synapsis.Neo4j/
RUN dotnet restore src/dotnet/Synapsis.Neo4j/Synapsis.Neo4j.csproj
RUN dotnet publish src/dotnet/Synapsis.Neo4j/Synapsis.Neo4j.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Synapsis.Neo4j.dll"]
