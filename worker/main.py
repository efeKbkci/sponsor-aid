import pika
import time
import threading
import uvicorn
from fastapi import FastAPI
from easyocr import Reader
import events_pb2
from config import RABBITMQ_HOST, RABBITMQ_PASSWORD, RABBITMQ_USER, QUEUE_RECEIVE, QUEUE_SEND
from microservices.id_processing.handler import IdProcessingHandler
from microservices.population_registry_parsing.handler import PopulationRegistryHandler
from microservices.residence_parsing.handler import ResidenceHandler
from microservices.barcode_extraction.handler import BarcodeExtractionHandler
from microservices.semantic_matching.handler import router as semantic_router

import gc
import ctypes

# 1. FastAPI Uygulaması
app = FastAPI(title="SponsorAid Worker Internal API")
app.include_router(semantic_router)

# 2. Servisleri Başlat (Mevcut mantık aynen korunur)
print("[*] Worker Başlatılıyor. Servisler ayağa kaldırılıyor...")
reader = Reader(['tr', 'en'], gpu=False, verbose=False)

id_service = IdProcessingHandler(reader)
population_registry_service = PopulationRegistryHandler()
residence_service = ResidenceHandler()
barcode_service = BarcodeExtractionHandler()

# 🚀 Başlangıçta modeller yüklenirken oluşan geçici kurulum çöplerini işletim sistemine iade et:
gc.collect()
try:
    ctypes.CDLL("libc.so.6").malloc_trim(0)
except Exception:
    pass

def route_event(ch, method, properties, body):
    envelope = events_pb2.WorkerTaskEnvelope()
    try:
        envelope.ParseFromString(body)
    except Exception as e:
        print(f"[❌] Protobuf okuma hatası: {e}")
        ch.basic_ack(delivery_tag=method.delivery_tag)
        return

    result_msg = None

    if envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_IMAGE:
        result_msg = id_service.process_image(envelope.image_task)
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_MERGE_PDF:
        result_msg = id_service.merge_to_pdf(envelope.merge_task)
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_OFFICIAL_DOC:
        task = envelope.official_doc_task
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
        result_msg = barcode_service.process(envelope.official_doc_task)
    else:
        print(f"[⚠️] Bilinmeyen komut tipi!")

    if result_msg:
        ch.basic_publish(
            exchange='',
            routing_key=QUEUE_SEND,
            body=result_msg.SerializeToString(),
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
    # RabbitMQ dinleyicisini arka planda thread olarak koştur
    mq_thread = threading.Thread(target=start_router, daemon=True)
    mq_thread.start()

    # FastAPI Uvicorn sunucusunu ana thread'de ayağa kaldır (Port: 8000)
    print("[*] 🌐 FastAPI HTTP Servisi 8000 portunda başlatılıyor...")
    uvicorn.run(app, host="0.0.0.0", port=8000)