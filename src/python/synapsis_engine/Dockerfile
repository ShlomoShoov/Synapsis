# synapsis-engine: deterministic patterns and forecasts. Reads MongoDB, writes conclusions through POST /ingest.
FROM python:3.12.7-slim-bookworm
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY src/python/synapsis_engine/requirements.txt requirements.txt
ENV PIP_CERT=/etc/ssl/certs/ca-certificates.crt
RUN pip install --no-cache-dir -r requirements.txt
COPY src/python/synapsis_engine/ ./
ENV PYTHONUNBUFFERED=1 ENGINE_DATA=/data
ENTRYPOINT ["python", "-m", "synapsis_engine"]
