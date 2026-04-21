FROM python:3.11-slim

WORKDIR /app

# Install Python dependencies
COPY ml_service/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Copy ML service source
COPY ml_service/ ./

# Copy model artifacts and dataset (relative to repo root)
COPY models/ ./models/
COPY datasets/ ./datasets/

ENV PYTHONUNBUFFERED=1

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
