# Daha güncel ve yamalanmış bir taban imaj kullanıyoruz
FROM python:3.12-slim-bookworm

# Güvenlik için 'root' olmayan yetkisiz bir kullanıcı oluşturuyoruz
RUN useradd -m sponsorworker

# Konteyner içindeki ana çalışma dizinini belirliyoruz
WORKDIR /app

# Sistemi güncelleyip gerekli kütüphaneleri kuruyor ve önbelleği temizliyoruz
# --no-install-recommends parametresi gereksiz paketlerin yüklenmesini (saldırı yüzeyini) engeller
RUN apt-get update && apt-get upgrade -y && apt-get install -y --no-install-recommends \
    libglib2.0-0 libsm6 libxext6 libxrender-dev \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Tüm uygulama dosyalarının sahipliğini oluşturduğumuz yetkisiz kullanıcıya devrediyoruz
RUN chown -R sponsorworker:sponsorworker /app

# Sistem bu satırdan itibaren 'root' yetkisini bırakır ve kısıtlı kullanıcıya geçer
USER sponsorworker

# Konteyner ayağa kalktığında çalışacak ana betik
CMD ["python", "main.py"]