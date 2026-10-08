import os
from dotenv import load_dotenv

# 1. worker klasörünün yolunu buluyoruz
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# 2. Bir üst dizine çıkıp .env dosyasının tam yolunu oluşturuyoruz
ENV_PATH = os.path.join(os.path.dirname(BASE_DIR), '.env')

# 3. .env dosyasını okuyup geçici olarak os.environ içine yüklüyoruz
load_dotenv(ENV_PATH)

# RabbitMQ Ayarları
RABBITMQ_HOST = os.environ.get('RABBITMQ_HOST', 'localhost')
RABBITMQ_USER = os.environ.get('RABBITMQ_USER', 'guest')         # 🚀 YENİ
RABBITMQ_PASSWORD = os.environ.get('RABBITMQ_PASSWORD', 'guest') # 🚀 YENİ

QUEUE_RECEIVE = 'document-processing-queue'  # C#'ın fotoğrafı yollayacağı kuyruk
QUEUE_SEND = 'document-result-queue'         # İşçinin sonucu (PDF'i) yollayacağı kuyruk