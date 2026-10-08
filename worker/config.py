import os
from dotenv import load_dotenv

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
ENV_PATH = os.path.join(os.path.dirname(BASE_DIR), '.env')

# .env dosyası varsa yükle, yoksa ortam değişkenleriyle devam et
if os.path.exists(ENV_PATH):
    load_dotenv(ENV_PATH)

# RabbitMQ Ayarları (Varsayılan değerler Docker Compose ile ezilir)
RABBITMQ_HOST = os.environ.get('RABBITMQ_HOST', 'localhost')
RABBITMQ_USER = os.environ.get('RABBITMQ_USER', 'guest')
RABBITMQ_PASSWORD = os.environ.get('RABBITMQ_PASSWORD', 'guest')

QUEUE_RECEIVE = 'document-processing-queue'
QUEUE_SEND = 'document-result-queue'