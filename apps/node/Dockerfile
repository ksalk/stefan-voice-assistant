# =============================================================
# Stage 1 — Builder: compile native extensions
# =============================================================
FROM python:3.11-slim-bookworm AS builder

RUN apt-get update && apt-get install -y --no-install-recommends \
        gcc \
        pkg-config \
        portaudio19-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /build

COPY requirements.txt .
RUN pip install --no-cache-dir --prefix=/install -r requirements.txt
RUN PYTHONPATH=/install/lib/python3.11/site-packages python -c "import openwakeword; openwakeword.utils.download_models()"

# =============================================================
# Stage 2 — Runtime: lean image with only what's needed
# =============================================================
FROM python:3.11-slim-bookworm

RUN apt-get update && apt-get install -y --no-install-recommends \
        libasound2 \
        libportaudio2 \
        libsndfile1 \
        alsa-utils \
    && rm -rf /var/lib/apt/lists/*

# Copy installed Python packages from builder
COPY --from=builder /install /usr/local

WORKDIR /app

COPY src/ src/
COPY models/ models/
COPY .env.example .env.example

# Ensure recordings dir exists (can be volume-mounted)
RUN mkdir -p recordings

ENTRYPOINT ["python", "src/main.py"]
