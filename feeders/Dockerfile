# Synapsis feeders: one image, every source. Build context: the repository root (for deploy/certs/).
FROM python:3.12.7-slim-bookworm

# Extra CA certificates for networks that inspect TLS: drop .crt files into deploy/certs/ (empty by default).
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates \
 && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY feeders/requirements.txt feeders/requirements.txt
ENV PIP_CERT=/etc/ssl/certs/ca-certificates.crt
RUN pip install --no-cache-dir -r feeders/requirements.txt
COPY feeders/ feeders/

ENV FEEDER_STATE_DIR=/state \
    FEEDER_CACHE_DIR=/cache \
    SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt \
    PYTHONUNBUFFERED=1
WORKDIR /app/feeders
ENTRYPOINT ["python", "-m", "synapsis_feeders"]
CMD ["list"]
