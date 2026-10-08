FROM python:3.12-slim-bookworm

ENV PYTHONUNBUFFERED=1

# RUN useradd -m sponsorworker

WORKDIR /app

RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    && rm -rf /var/lib/apt/lists/*

# 1. Önce sadece PyTorch ve Torchvision'ın hafif CPU sürümlerini kurun (~150-200 MB)
RUN pip install --no-cache-dir torch torchvision --index-url https://download.pytorch.org/whl/cpu

# 2. Ardından projenin diğer gereksinimlerini kurun
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt && \
    pip install --no-cache-dir --force-reinstall --no-deps opencv-contrib-python-headless==4.10.0.84

COPY . .

RUN mkdir -p /app/wwwroot/uploads 
    # chown -R sponsorworker:sponsorworker /app

# USER sponsorworker -> Güvenlik açısından riskli

CMD ["python", "main.py"]