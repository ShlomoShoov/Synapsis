# Synapsis feeders: one image, every source. Build context: the repository root (it needs schemas/).
FROM python:3.12.7-slim-bookworm

# Extra CA certificates for networks that inspect TLS: drop .crt files into docker/certs/ (empty by default).
COPY docker/certs/ /usr/local/share/ca-certificates/extra/
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates \
 && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY feeders/requirements.txt feeders/requirements.txt
ENV PIP_CERT=/etc/ssl/certs/ca-certificates.crt
RUN pip install --no-cache-dir -r feeders/requirements.txt
COPY schemas/ schemas/
COPY feeders/ feeders/

ENV SYNAPSIS_SCHEMAS_DIR=/app/schemas \
    FEEDER_STATE_DIR=/state \
    FEEDER_CACHE_DIR=/cache \
    SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt \
    PYTHONUNBUFFERED=1
WORKDIR /app/feeders
ENTRYPOINT ["python", "-m", "synapsis_feeders"]
CMD ["list"]
