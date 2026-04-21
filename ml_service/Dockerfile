FROM python:3.11-slim

WORKDIR /app

# Install Python dependencies first (layer cache)
COPY requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy ML service source code
# (built from context: ./ml_service, so paths are relative to that folder)
COPY . ./

# models/ and datasets/ are NOT baked in — they are bind-mounted at runtime
# via docker-compose volumes so the container always uses the latest artifacts.
# Expected mounts:
#   ./models    -> /app/models    (read-only)
#   ./datasets  -> /app/datasets  (read-only)

ENV PYTHONUNBUFFERED=1
ENV MODELS_DIR=/app/models
ENV DATASETS_DIR=/app/datasets/hospital

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
