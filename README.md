# Synapsis

Two separate Docker Compose deployments: the **core** system and the demo **feeders**. Requirements: Docker 24 or
later with Compose v2, and at least 8 GB of memory for Docker.

## Run

1. The core: MongoDB, Elasticsearch, the embedder and the API on `http://localhost:8080`.

   ```bash
   cp deploy/core/.env.example deploy/core/.env        # first time only
   docker compose -f deploy/core/compose.yml up -d --build --wait
   curl -s localhost:8080/health
   ```

2. The feeders: demo data from 21 public sources. Fill in the keys in `deploy/feeders/.env` first.

   ```bash
   cp deploy/feeders/.env.example deploy/feeders/.env  # first time only
   docker compose -f deploy/feeders/compose.yml up -d --build
   ```

## Shut down

```bash
docker compose -f deploy/feeders/compose.yml down
docker compose -f deploy/core/compose.yml down
```

Add `-v` to either command to delete that deployment's data as well.
