import pika
import json
from config import RABBITMQ_HOST, QUEUE_RECEIVE, QUEUE_SEND
from microservices.id_processing.handler import IdProcessingHandler

print("[*] Worker Başlatılıyor. Servisler ayağa kaldırılıyor...")

# Departmanları (Servisleri) Başlat
id_service = IdProcessingHandler()
# ileride eklenecek: document_service = DocumentParsingHandler()

def route_event(ch, method, properties, body):
    event = json.loads(body)
    command_type = event.get('CommandType')
    
    result_command = None

    # YÖNLENDİRME (ROUTING) MANTIĞI
    if command_type in ["ProcessImage", "MergeToPdf"]:
        result_command = id_service.handle_event(event)
        
    elif command_type == "ParseDocument":
        # result_command = document_service.handle_event(event)
        print("Belge okuma servisi henüz aktif değil.")
        
    else:
        print(f"[⚠️] Bilinmeyen komut tipi: {command_type}")

    # EĞER SERVİS BİR SONUÇ DÖNDÜYSE, BUNU C# API'SİNE GERİ YOLLA
    if result_command:
        ch.basic_publish(
            exchange='',
            routing_key=QUEUE_SEND,
            body=json.dumps(result_command),
            properties=pika.BasicProperties(delivery_mode=2)
        )

    # Mesajı kuyruktan güvenle sil
    ch.basic_ack(delivery_tag=method.delivery_tag)

def start_router():
    print(f"[*] RabbitMQ'ya bağlanılıyor ({RABBITMQ_HOST})...")
    connection = pika.BlockingConnection(pika.ConnectionParameters(host=RABBITMQ_HOST))
    channel = connection.channel()

    channel.queue_declare(queue=QUEUE_RECEIVE, durable=True)
    channel.queue_declare(queue=QUEUE_SEND, durable=True)

    channel.basic_qos(prefetch_count=1)
    channel.basic_consume(queue=QUEUE_RECEIVE, on_message_callback=route_event)

    print("[*] 🚦 Santral (Router) Hazır! Eventler bekleniyor...")
    channel.start_consuming()

if __name__ == '__main__':
    start_router()