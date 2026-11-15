# Container image definition for the SponsorAid Python document processing worker.
FROM python:3.12-slim-bookworm

# Disables standard output buffering so logs appear in container monitors immediately.
ENV PYTHONUNBUFFERED=1

WORKDIR /app

# Installs shared system libraries required by OpenCV and computer vision libraries for headless image rendering.
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender-dev libgl1 \
    && rm -rf /var/lib/apt/lists/*

# Installs dependencies before copying code to preserve pip cache layers when application files change.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copies worker source code into the container.
COPY . .

# Prepares shared volume mount folder matching API document upload path.
RUN mkdir -p /app/wwwroot/uploads

# Starts the background queue consumer loop.
CMD ["python", "main.py"]
