# The read mock (GET /analyze/map, GET /analyze/graph, GET /records/{id}) over its sample. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates
WORKDIR /repo
COPY src/dotnet/ src/dotnet/
RUN dotnet publish src/dotnet/Synapsis.MockApi/Synapsis.MockApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8081
EXPOSE 8081
ENTRYPOINT ["dotnet", "Synapsis.MockApi.dll"]
