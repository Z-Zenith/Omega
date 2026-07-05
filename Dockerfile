# --- Stage 1: Build Dependencies ---
FROM python:3.11-slim AS builder

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .

# Install dependencies into a virtual environment
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

RUN pip install --no-cache-dir -r requirements.txt

# --- Stage 2: Final Runtime ---
FROM python:3.11-slim AS runner

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PATH="/opt/venv/bin:$PATH"

# Copy virtual environment from builder stage
COPY --from=builder /opt/venv /opt/venv

# Create a non-privileged system user for security
RUN groupadd --gid 10001 appgroup && \
    useradd --uid 10001 --gid 10001 --shell /bin/bash --create-home appuser && \
    chown -R appuser:appgroup /app

# Copy application source code and tests
COPY --chown=appuser:appgroup src/ ./src/
COPY --chown=appuser:appgroup tests/ ./tests/

USER appuser

EXPOSE 8000

# Start Uvicorn serving FastAPI
CMD ["uvicorn", "src.main:app", "--host", "0.0.0.0", "--port", "8000"]
