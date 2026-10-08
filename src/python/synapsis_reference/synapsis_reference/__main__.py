"""python -m synapsis_reference serve | seed

serve  start the stand-in API on $PORT (default 8080), seeding the schema packages on first start
seed   register the seed packages that MongoDB does not have yet, print the result and exit"""
from __future__ import annotations

import json
import logging
import os
import sys


def main(argv: list[str]) -> int:
    logging.basicConfig(level=os.environ.get("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(name)s %(message)s")
    command = argv[1] if len(argv) > 1 else "serve"
    if command == "serve":
        import uvicorn
        from .app import create_app
        uvicorn.run(create_app(), host="0.0.0.0", port=int(os.environ.get("PORT", "8080")), log_level="info",
                    timeout_keep_alive=30)
        return 0
    if command == "seed":
        from .app import Services
        from .settings import Settings
        services = Services(Settings.from_env())
        services.registry_store.ensure_indexes()
        print(json.dumps(services.registry_store.seed(services.settings.seed_dir), indent=1, default=str))
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
