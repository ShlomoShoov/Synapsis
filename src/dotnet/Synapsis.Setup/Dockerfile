# The setup server: prepares every store once, then ends. Build context: the repository root.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/dotnet/Directory.Build.props src/dotnet/
COPY src/dotnet/Synapsis.Data/ src/dotnet/Synapsis.Data/
COPY src/dotnet/Synapsis.Setup/ src/dotnet/Synapsis.Setup/
RUN dotnet restore src/dotnet/Synapsis.Setup/Synapsis.Setup.csproj
RUN dotnet publish src/dotnet/Synapsis.Setup/Synapsis.Setup.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Synapsis.Setup.dll"]
