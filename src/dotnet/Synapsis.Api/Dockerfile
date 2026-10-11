# The C# ingestion server. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN update-ca-certificates
WORKDIR /repo
COPY src/dotnet/ src/dotnet/
RUN dotnet publish src/dotnet/Synapsis.Api/Synapsis.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
# curl answers the Compose health check.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Synapsis.Api.dll"]
