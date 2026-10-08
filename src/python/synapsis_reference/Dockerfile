# Python stand-in for the C# API (development only). Build context: the repository root (it needs schemas/).
FROM python:3.12.7-slim-bookworm
COPY docker/certs/ /usr/local/share/ca-certificates/extra/
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY src/python/synapsis_reference/requirements.txt requirements.txt
ENV PIP_CERT=/etc/ssl/certs/ca-certificates.crt
RUN pip install --no-cache-dir -r requirements.txt
COPY schemas/ schemas/
COPY src/python/synapsis_reference/synapsis_reference/ synapsis_reference/
ENV SYNAPSIS_SCHEMAS_DIR=/app/schemas PORT=8080 PYTHONUNBUFFERED=1
EXPOSE 8080
CMD ["python", "-m", "synapsis_reference", "serve"]
