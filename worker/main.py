import pika
import time
from easyocr import Reader
import events_pb2 # Ürettiğimiz Protobuf sınıfı
from config import RABBITMQ_HOST, RABBITMQ_PASSWORD, RABBITMQ_USER, QUEUE_RECEIVE, QUEUE_SEND
from microservices.id_processing.handler import IdProcessingHandler
from microservices.population_registry_parsing.handler import PopulationRegistryHandler
from microservices.residence_parsing.handler import ResidenceHandler
from microservices.barcode_extraction.handler import BarcodeExtractionHandler

print("[*] Worker Başlatılıyor. Servisler ayağa kaldırılıyor...")
print("[*] EasyOCR Modeli RAM'e yükleniyor (Bu işlem sadece açılışta 1 kez yapılır)...")
# 'tr' ve 'en' dillerini destekleyen okuyucuyu başlatıyoruz. GPU=False ile standart işlemcide bile çalışır.
reader = Reader(['tr', 'en'], gpu=False, verbose=False)

# Departmanları (Servisleri) Başlat
id_service = IdProcessingHandler(reader)
population_registry_service = PopulationRegistryHandler()
residence_service = ResidenceHandler()
barcode_service = BarcodeExtractionHandler()

def route_event(ch, method, properties, body):
    # 1. RabbitMQ'dan gelen byte'ları Zarf'a (Envelope) çevir
    envelope = events_pb2.WorkerTaskEnvelope()
    try:
        envelope.ParseFromString(body)
    except Exception as e:
        print(f"[❌] Protobuf okuma hatası: {e}")
        ch.basic_ack(delivery_tag=method.delivery_tag)
        return

    result_msg = None

    # 2. Zarfın üzerindeki Etikete (Enum) bakarak içindeki görevi ilgili metoda yolla
    if envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_IMAGE:
        result_msg = id_service.process_image(envelope.image_task)
        
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_MERGE_PDF:
        result_msg = id_service.merge_to_pdf(envelope.merge_task)
        
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_OFFICIAL_DOC:
        task = envelope.official_doc_task
        print(f"[*] Belge Servisi Çalıştı")

        if task.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_POPULATION_REGISTRY:
            result_msg = population_registry_service.process(task)
        elif task.document_type == events_pb2.DocumentType.DOCUMENT_TYPE_RESIDENCE:
            result_msg = residence_service.process(task)
        else:
            result_msg = events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type,
                is_success=False,
                error_message="Desteklenmeyen resmi belge türü.",
                saved_file_path=task.file_path,
            )
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_EXTRACT_BARCODE:
        task = envelope.official_doc_task
        print(f"[*] Barkod Servisi Çalıştı")
        result_msg = barcode_service.process(task)

    else:
        print(f"[⚠️] Bilinmeyen komut tipi!")

    # 3. Sonucu Byte'a (Binary) çevirip C#'a (RabbitMQ'ya) geri fırlat
    if result_msg:
        ch.basic_publish(
            exchange='',
            routing_key=QUEUE_SEND,
            body=result_msg.SerializeToString(), # DİKKAT: JSON dumps yerine SerializeToString kullanıyoruz!
            properties=pika.BasicProperties(delivery_mode=2)
        )

    ch.basic_ack(delivery_tag=method.delivery_tag)

def start_router():
    print(f"[*] RabbitMQ'ya bağlanılıyor ({RABBITMQ_HOST})...")
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)
    parameters = pika.ConnectionParameters(host=RABBITMQ_HOST, credentials=credentials)

    connection = None
    retries = 10
    while retries > 0:
        try:
            connection = pika.BlockingConnection(parameters)
            break
        except pika.exceptions.AMQPConnectionError:
            retries -= 1
            print(f"[!] RabbitMQ henüz hazır değil. 3 saniye sonra tekrar denenecek... (Kalan deneme: {retries})")
            time.sleep(3)

    if not connection:
        raise RuntimeError("RabbitMQ servisine bağlanılamadı. Süre aşımı!")

    channel = connection.channel()
    channel.queue_declare(queue=QUEUE_RECEIVE, durable=True)
    channel.queue_declare(queue=QUEUE_SEND, durable=True)

    channel.basic_qos(prefetch_count=1)
    channel.basic_consume(queue=QUEUE_RECEIVE, on_message_callback=route_event)

    print("[*] 🚦 Santral (Router) Hazır! Eventler bekleniyor...")
    channel.start_consuming()

if __name__ == '__main__':
    start_router()