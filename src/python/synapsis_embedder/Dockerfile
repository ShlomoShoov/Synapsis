# synapsis-embedder: local text models behind HTTP. Models download into the 'models' volume on first start.
FROM python:3.12.7-slim-bookworm
COPY deploy/certs/ /usr/local/share/ca-certificates/extra/
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates && update-ca-certificates \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY src/python/synapsis_embedder/requirements.txt requirements.txt
ENV PIP_CERT=/etc/ssl/certs/ca-certificates.crt
RUN pip install --no-cache-dir -r requirements.txt
COPY src/python/synapsis_embedder/synapsis_embedder/ synapsis_embedder/
ENV PORT=8090 HF_HOME=/models/hf FASTEMBED_CACHE_PATH=/models/fastembed PYTHONUNBUFFERED=1
EXPOSE 8090
CMD ["python", "-m", "synapsis_embedder"]
