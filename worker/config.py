"""Configuration module for the SponsorAid document processing worker service.

Loads environment variables and sets RabbitMQ message queue parameters.
"""

import os
from dotenv import load_dotenv

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
ENV_PATH = os.path.join(os.path.dirname(BASE_DIR), '.env')

# Loads local .env for manual testing while allowing container environments to inject their own variables.
if os.path.exists(ENV_PATH):
    load_dotenv(ENV_PATH)

# Message broker connection settings. Defaults support local standalone testing outside Docker.
RABBITMQ_HOST = os.environ.get('RABBITMQ_HOST', 'localhost')
RABBITMQ_USER = os.environ.get('RABBITMQ_USER', 'guest')
RABBITMQ_PASSWORD = os.environ.get('RABBITMQ_PASSWORD', 'guest')

# Queue names for receiving document tasks and dispatching extraction results back to the API.
QUEUE_RECEIVE = 'document-processing-queue'
QUEUE_SEND = 'document-result-queue'
