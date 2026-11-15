FROM python:3.12-slim-bookworm

# Disable stdout/stderr buffering so worker print logs flush immediately in containers
ENV PYTHONUNBUFFERED=1

WORKDIR /app

# Native system libraries required by OpenCV and headless image processing
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    && rm -rf /var/lib/apt/lists/*

# Install locked Python dependencies (including CPU PyTorch) before copying source files to preserve pip layer cache on code-only edits
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Shared uploads directory mounted across services; non-root user is omitted to avoid volume permission conflicts
RUN mkdir -p /app/wwwroot/uploads

CMD ["python", "main.py"]