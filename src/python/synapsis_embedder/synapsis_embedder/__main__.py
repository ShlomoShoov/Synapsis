"""python -m synapsis_embedder: serve the embedder on $PORT (default 8090)."""
import os

import uvicorn

from .app import create_app

if __name__ == "__main__":
    uvicorn.run(create_app(), host="0.0.0.0", port=int(os.environ.get("PORT", "8090")), log_level="info")
