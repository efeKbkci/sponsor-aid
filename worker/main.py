import pika
import time
import threading
import uvicorn
from fastapi import FastAPI
import events_pb2
from config import RABBITMQ_HOST, RABBITMQ_PASSWORD, RABBITMQ_USER, QUEUE_RECEIVE, QUEUE_SEND
from microservices.id_processing.handler import IdProcessingHandler
from microservices.population_registry_parsing.handler import PopulationRegistryHandler
from microservices.residence_parsing.handler import ResidenceHandler
from microservices.barcode_extraction.handler import BarcodeExtractionHandler
from microservices.bank_account_parsing.handler import BankAccountParsingHandler
from microservices.semantic_matching.handler import router as semantic_router

# HTTP application for synchronous microservice endpoints such as semantic matching.
app = FastAPI(title="SponsorAid Worker Internal API")
app.include_router(semantic_router)

# Pre-initialize handlers at startup to keep machine learning models loaded in memory.
print("[*] Worker Başlatılıyor. Servisler ayağa kaldırılıyor...")

id_service = IdProcessingHandler()
population_registry_service = PopulationRegistryHandler()
residence_service = ResidenceHandler()
barcode_service = BarcodeExtractionHandler()
bank_account_service = BankAccountParsingHandler()

def route_event(ch, method, properties, body):
    """Deserialize incoming tasks from RabbitMQ and dispatch them to service handlers.

    Routes protobuf tasks by command type and publishes the result back to the send queue.
    """
    envelope = events_pb2.WorkerTaskEnvelope()
    try:
        envelope.ParseFromString(body)
    except Exception as e:
        print(f"[❌] Protobuf okuma hatası: {e}")
        # Acknowledge unreadable messages immediately to avoid poison pills blocking the queue.
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
    elif envelope.command_type == events_pb2.CommandType.COMMAND_TYPE_PROCESS_BANK_ACCOUNT_DOC:
        result_msg = bank_account_service.process(envelope.bank_account_doc_task)
    else:
        print(f"[⚠️] Bilinmeyen komut tipi!")

    if result_msg:
        # Delivery mode 2 persists the message to disk so messages survive broker restarts.
        ch.basic_publish(
            exchange='',
            routing_key=QUEUE_SEND,
            body=result_msg.SerializeToString(),
            properties=pika.BasicProperties(delivery_mode=2)
        )

    # Acknowledge the message only after processing completes to prevent task loss.
    ch.basic_ack(delivery_tag=method.delivery_tag)

def start_router():
    """Establish connection to RabbitMQ broker and listen for incoming tasks.

    Applies retry logic to handle startup races and limits prefetch to balance load.
    """
    print(f"[*] RabbitMQ'ya bağlanılıyor ({RABBITMQ_HOST})...")
    credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASSWORD)
    parameters = pika.ConnectionParameters(host=RABBITMQ_HOST, credentials=credentials)

    connection = None
    retries = 10
    # Retry loop handles container startup delays when RabbitMQ is not ready yet.
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

    # Limit unacknowledged messages to one to avoid overloading worker memory with heavy tasks.
    channel.basic_qos(prefetch_count=1)
    channel.basic_consume(queue=QUEUE_RECEIVE, on_message_callback=route_event)

    print("[*] 🚦 Santral (Router) Hazır! Eventler bekleniyor...")
    channel.start_consuming()

if __name__ == '__main__':
    # Run the RabbitMQ consumer in a background thread so the main thread can host the API.
    mq_thread = threading.Thread(target=start_router, daemon=True)
    mq_thread.start()

    # Run the HTTP server on the main thread to serve synchronous requests.
    print("[*] 🌐 FastAPI HTTP Servisi 8000 portunda başlatılıyor...")
    uvicorn.run(app, host="0.0.0.0", port=8000)
