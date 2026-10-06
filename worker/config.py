import os

# RabbitMQ Ayarları
RABBITMQ_HOST = os.environ.get('RABBITMQ_HOST', 'localhost')
QUEUE_RECEIVE = 'document-processing-queue'  # C#'ın fotoğrafı yollayacağı kuyruk
QUEUE_SEND = 'document-result-queue'         # İşçinin sonucu (PDF'i) yollayacağı kuyruk