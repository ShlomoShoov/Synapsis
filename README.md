# Synapsis

Two separate Docker Compose deployments: the **core** system and the demo **feeders**. Requirements: Docker 24 or
later with Compose v2, and at least 8 GB of memory for Docker.

## Run

1. The core: MongoDB, Elasticsearch, Kafka, Neo4j, the embedder, the setup (runs once, then ends), the C# API on
   `http://localhost:8080` (Swagger: `http://localhost:8080/swagger`), the relay, the indexer, the projector and the
   web client on `http://localhost:1897`.

   ```bash
   cp deploy/core/.env.example deploy/core/.env        # first time only, then set CORE_NEO4J_PASSWORD in it
   docker compose -f deploy/core/compose.yml up -d --build --wait
   curl -s localhost:8080/health
   ```

2. The feeders: demo data from 21 public sources. Fill in the keys in `deploy/feeders/.env` first.

   ```bash
   cp deploy/feeders/.env.example deploy/feeders/.env  # first time only
   docker compose -f deploy/feeders/compose.yml up -d --build
   ```

3. The read mock (optional): the Analyze routes (`/analyze/map`, `/analyze/graph`, `/records/{id}`) on
   `http://localhost:8081`, answered from a sample of real records, with no store.

   ```bash
   docker compose -f deploy/core/compose.yml --profile mock up -d --build mock-api
   curl -s "localhost:8081/analyze/map?zoom=2"
   ```

   Without Docker: `dotnet run --project src/dotnet/Synapsis.MockApi`. The sample is refreshed from the local
   MongoDB with `bash src/dotnet/Synapsis.MockApi/Sample/export-sample.sh`.

## Shut down

```bash
docker compose -f deploy/feeders/compose.yml down
docker compose -f deploy/core/compose.yml --profile mock down
```

Add `-v` to either command to delete that deployment's data as well.
