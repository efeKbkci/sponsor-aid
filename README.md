# SponsorAid — Event-Driven Document Verification & AI Aid Intake Pipeline

**SponsorAid** is a distributed, polyglot backend and AI document processing system that automates social aid and orphan sponsorship intake workflows through a conversational interface. It replaces manual, paper-heavy NGO review processes with an end-to-end pipeline featuring **custom-trained YOLOv8 computer vision models ($99.5\%\text{ mAP@50}$, $5.2\text{ ms}$ latency)**, **neural semantic intent matching (BERT)**, **autonomous government portal verification**, **spatial PDF table extraction**, and **out-of-band cryptographic archiving**.

## 📌 Executive Summary & Access Notice

> **🔒 Security & Live Instance Policy:**
>
> Because this system processes sensitive Personally Identifiable Information (PII)—including national identity cards, family registry records, and financial data—and interfaces directly with official government verification portals, **the live Telegram bot is restricted to authorized personnel**, and custom-trained YOLO model weights are excluded from public version control.
>
> **✅ Zero-Setup Local Verification:**
>
> Reviewers can verify the entire 13-stage state machine and 31-step user journey locally without external API keys using the automated **Testcontainers** integration suite (`dotnet test` and `pytest`). See [Testing & Verification](#-testing--verification).

## 🏗️ System Architecture

SponsorAid decouples real-time conversational state management (**ASP.NET Core 10**) from compute-intensive Computer Vision, NLP, OCR, and PDF parsing workloads (**Python 3.12**) using **RabbitMQ** and binary **Protocol Buffers** (`proto3`) contracts.

<img width="6240" height="2080" alt="image" src="https://github.com/user-attachments/assets/d2c70676-158d-453b-86b1-29e568e4f1ff" />

## ⚡ Key Engineering & AI Highlights

### 1. Custom YOLOv8 Model Training, Dataset Curation & Computer Vision Pipeline

To reliably process unconstrained smartphone photos of national ID cards submitted by applicants, I curated a custom dataset from scratch and trained two specialized **Ultralytics YOLOv8** models using **Roboflow**:

* **Privacy-Preserving Data Collection & Domain Randomization:** Physically masked sensitive PII fields on a sample ID card using black tape and captured $\sim 150$ raw photographs. To prevent background memorization and overfitting, samples were systematically varied across **natural sunlight vs. indoor artificial lighting**, **high vs. low contrast**, and **complex patterned/textured vs. plain backgrounds**.
* **Polygon Annotation & $3\times$ Augmentation:** Annotated precise polygon segmentation boundaries in Roboflow and applied data augmentation pipelines to expand the training set to **$\sim 450$ labeled images**.
* **Dual-Model Inference Pipeline ($99.5\%\text{ mAP@50}$, $5.2\text{ ms}$ Latency):**
  1. **Instance Segmentation ([`image_processor.py`](worker/microservices/id_processing/services/image_processor.py)):** Custom YOLOv8-seg model (`id_segmenter.pt`) achieves **$99.5\%\text{ mAP@50}$** with **$5.2\text{ ms}$ inference latency**. It isolates the card's polygon mask (`conf=0.5`) and crops an axis-aligned bounding box—deliberately avoiding perspective warp interpolation artifacts that degrade downstream OCR accuracy.
  2. **Orientation Classification ([`image_rotator.py`](worker/microservices/id_processing/services/image_rotator.py)):** Reused the curated dataset to train a secondary YOLOv8 classification model (`id_orientation_cls.pt`). After normalizing portrait images via $90^\circ$ rotation, the classifier detects upside-down (`reversed`) cards and flips them $180^\circ$ prior to text extraction.
* **ROI-Scoped OCR ([`ocr_service.py`](worker/microservices/id_processing/services/ocr_service.py)):** Slices the aligned card image to the top $50\%$ for front sides and the bottom $50\%$ (MRZ zone) for back sides before calling Azure Computer Vision Read API—cutting payload size in half and eliminating false-positive numeric matches.
* **Super-Resolution QR Recovery ([`scanner.py`](worker/microservices/barcode_extraction/services/scanner.py)):** Employs OpenCV's `WeChatQRCode` engine backed by Caffe detection and super-resolution neural networks to decode 16-character verification barcodes from blurry or low-resolution document photos.
* **Automated A4 Composite Archiving ([`pdf_generator.py`](worker/microservices/id_processing/services/pdf_generator.py)):** Resizes and centers verified front and back card images vertically onto a standardized A4 PDF canvas in memory.

### 2. Neural Semantic Matching with In-Memory LRU Matrix Caching

* During the 17-question survey and natural-language `/update` flow, users frequently reply with unstructured free text (e.g., *"he worked at a construction site"* or *"I typed my bank account wrong"*).
* [`matcher.py`](worker/microservices/semantic_matching/services/matcher.py) hosts a fine-tuned Turkish BERT NLI sentence transformer (`emrecan/bert-base-turkish-cased-mean-nli-stsb-tr`) exposed via FastAPI ([`handler.py`](worker/microservices/semantic_matching/handler.py)).
* **Zero-Recomputation Matrix Cache:** To avoid re-encoding static candidate catalogs on every request, normalized PyTorch embedding tensors are stored in an in-memory `OrderedDict` **LRU cache** (`_max_cache_size = 10`). Warm queries compute cosine similarity against cached candidate matrices in milliseconds under `torch.no_grad()`.

### 3. Per-Session Concurrency Control & Self-Healing State Machine (.NET 10)

* **Race Condition Prevention:** Users in chat interfaces frequently send rapid, overlapping messages or multiple attachments simultaneously. [`RequestSerializationBehavior.cs`](src/SponsorAid.Application/Behaviors/RequestSerializationBehavior.cs) implements a MediatR pipeline behavior backed by a `ConcurrentDictionary<string, SemaphoreSlim>` that serializes execution per `RegistrationKey` or `ChatId` without blocking unrelated users.
* **Automatic State Rollback:** [`ExceptionHandlingBehavior.cs`](src/SponsorAid.Application/Behaviors/ExceptionHandlingBehavior.cs) intercepts domain and infrastructure exceptions across the pipeline, automatically invokes `draft.RevertProcessingState()` to roll back in-flight processing steps (e.g., `ProcessingResidence` $\rightarrow$ `WaitingForResidence`), persists the recovered state to PostgreSQL, and sends a localized diagnostic prompt while returning `HTTP 200 OK` to prevent infinite webhook retry loops.

### 4. Cross-Language Binary Contracts with Protocol Buffers & RabbitMQ

* **Strict Schema Evolution:** Instead of error-prone untyped JSON strings between C# and Python, asynchronous tasks and worker results are defined in [`contracts/events.proto`](contracts/events.proto) using `oneof` discriminated unions (`WorkerTaskEnvelope` and `WorkerResultMessage`).
* **Resilient Message Consumption:** [`RabbitMqEventPublisher.cs`](src/SponsorAid.Infrastructure/Services/RabbitMqEventPublisher.cs) and [`WorkerResultConsumerService.cs`](src/SponsorAid.Infrastructure/Services/WorkerResultConsumerService.cs) use durable queues, persistent delivery modes, connection retry loops, `prefetch_count=1` load balancing in [`worker/main.py`](worker/main.py), and explicit manual `BasicAck`/`BasicNack` handling to prevent task loss or poison-message loops.

### 5. Autonomous 5-Step Government Portal Verification

* When a user uploads a smartphone photo of an official document rather than a digital PDF, the worker extracts the verification QR/barcode and passes it back to the API.
* [`EGovermentDocumentService.cs`](src/SponsorAid.Infrastructure/Services/EGovermentDocumentService.cs) emulates a complete 5-step browser verification flow against the official government portal (`turkiye.gov.tr`). It maintains an isolated per-request `CookieContainer` over pooled `SocketsHttpHandler` connections, scrapes dynamic anti-forgery `data-token` attributes via regex at each step, injects randomized humanized delays ($400\text{--}1200\text{ ms}$) to respect rate limits, and downloads the authentic government-signed PDF for downstream parsing.

### 6. Geometric & Spatial PDF Parsing

* **Population Registry Extraction:** [`population_registry_parsing/services/extractor.py`](worker/microservices/population_registry_parsing/services/extractor.py) locates the table header anchor (`SIRA`), crops out government letterheads/seals above the anchor coordinate (`top - 2`), and extracts tabular family records using vector line geometry.
* **Coordinate-Bucketed Address Scraping:** [`residence_parsing/services/extractor.py`](worker/microservices/residence_parsing/services/extractor.py) locates the 10-digit spatial address code anchor, dynamically detects the bottom table border edge, and reconstructs multi-line addresses by quantizing word Y-coordinates into 4-pixel buckets (`round(w['top'] / 4)`).

### 7. Rich Domain Model & Out-of-Band Cryptographic Dispatch

* **Algorithmic Value Objects:** Core domain invariants are enforced at instantiation in `SponsorAid.Domain`:
  * [`IdNumber.cs`](src/SponsorAid.Domain/ValueObjects/IdNumber.cs): Validates 11-digit national IDs using official Mod-10 and Mod-11 check digit formulas.
  * [`Iban.cs`](src/SponsorAid.Domain/ValueObjects/Iban.cs): Validates 26-character bank accounts using ISO 7064 **Mod-97** arithmetic over `BigInteger` ($\text{IBAN} \bmod 97 \equiv 1$).
  * [`Child.cs`](src/SponsorAid.Domain/Entities/Child.cs) & [`RegistrationDraftState.cs`](src/SponsorAid.Application/Models/RegistrationDraftState.cs): Automatically filters family registry records for deceased father verification and child age eligibility ($< 13$ years old).
* **Zero-Data-Retention & Split-Channel Delivery:** Upon final confirmation, [`FinalizeRegistrationHandler.cs`](src/SponsorAid.Application/Features/Registrations/Commands/FinalizeRegistration/FinalizeRegistrationHandler.cs) packages all JSON metadata and verified PDFs into an **AES-256 encrypted ZIP archive** ([`SharpZipLibSecureArchiveService.cs`](src/SponsorAid.Infrastructure/Services/SharpZipLibSecureArchiveService.cs)). [`RegistrationDispatchService.cs`](src/SponsorAid.Infrastructure/Services/RegistrationDispatchService.cs) sends the encrypted ZIP via SMTP email while transmitting the 16-character cryptographic password over a separate out-of-band Telegram admin channel, then immediately wipes all local files and database draft records.

## 🛠️ Tech Stack

| Layer | Technologies |
| ----- | ----- |
| **AI, Computer Vision & NLP** | Python 3.12, Ultralytics YOLOv8 (Custom Instance Segmentation & Orientation Classification — $99.5\%\text{ mAP@50}$, $5.2\text{ ms}$), Roboflow (Dataset Curation & Augmentation), SentenceTransformers (PyTorch / Turkish BERT NLI), OpenCV (`wechat_qrcode` + Caffe Super-Resolution), Azure Computer Vision OCR |
| **Backend API** | C# 13, .NET 10 (`ASP.NET Core`), Clean Architecture, MediatR (CQRS), Entity Framework Core 10 |
| **Database & Messaging** | PostgreSQL 16 (`jsonb` state persistence), RabbitMQ 3, Google Protocol Buffers (`proto3`), FastAPI |
| **Document & Security** | `pdfplumber` (Spatial PDF Geometry), Pillow, SharpZipLib (AES-256 ZIP encryption), `RandomNumberGenerator`, MailKit (STARTTLS SMTP) |
| **Testing & DevOps** | xUnit, Testcontainers (PostgreSQL), `WebApplicationFactory`, Moq, FluentAssertions, `pytest`, Docker Compose, GitHub Actions CI/CD, GitHub Container Registry (GHCR) |

## 🧪 Testing & Verification

The project includes comprehensive automated tests that validate both the .NET state machine and Python AI microservices without requiring live Telegram tokens or external government portal access.

### 1. .NET End-to-End Integration & Concurrency Tests

Powered by **Testcontainers** ([`SponsorAidTestFixture.cs`](test/SponsorAid.Tests/SponsorAidTestFixture.cs)), the test runner automatically spins up an isolated `postgres:16-alpine` Docker container, applies EF Core migrations, and hosts an in-memory `WebApplicationFactory<Program>`.

* [**`RegistrationJourneyTests.cs`**](test/SponsorAid.Tests/RegistrationJourneyTests.cs)**:** Simulates a complete **31-step end-to-end user journey** from `/new` webhook initiation, invalid/valid Mod-10/11 ID checks, document uploads, barcode e-Government fallback, multi-child age filtering, front/back ID card OCR & PDF merging, semantic survey matching, to final AES-256 package dispatch and disk cleanup.
* [**`RegistrationUpdateJourneyTests.cs`**](test/SponsorAid.Tests/RegistrationUpdateJourneyTests.cs)**:** Verifies natural-language field updates (`/update`), premature update rejection, cancellation recovery, and state resumption.
* [**`RequestSerializationBehaviorTests.cs`**](test/SponsorAid.Tests/RequestSerializationBehaviorTests.cs)**:** Dispatches 5 concurrent requests with identical keys and verifies via `Interlocked` counters that peak concurrency remains strictly at `1`.
* [**`CancelRegistrationHandlerTests.cs`**](test/SponsorAid.Tests/CancelRegistrationHandlerTests.cs)**:** Unit tests verifying complete physical file cleanup and draft deletion upon cancellation.

```bash
# Prerequisites: .NET 10 SDK and Docker Desktop running (for Testcontainers)
dotnet test src/SponsorAid.slnx -c Release --verbosity normal
```

### 2. Python Semantic Matcher & LRU Cache Tests

Located in [`worker/tests/semantic_matching/`](worker/tests/semantic_matching/), this suite tests the real Turkish BERT model via `FastAPI TestClient`:

* [**`test_semantic_matcher_unit.py`**](worker/tests/semantic_matching/test_semantic_matcher_unit.py)**:** Benchmarks cold vs. warm query execution times to verify in-memory embedding matrix caching and validates LRU eviction when candidate catalogs exceed capacity (`10`).
* [**`test_semantic_matcher_integration.py`**](worker/tests/semantic_matching/test_semantic_matcher_integration.py)**:** Verifies cosine similarity thresholds ($\ge 0.82$), out-of-distribution query rejection (`None`), and whitespace input gates.

```bash
cd worker
pip install -r requirements.txt pytest httpx --extra-index-url https://download.pytorch.org/whl/cpu
pytest tests/ -v
```

## 📂 Repository Structure

```text
├── .github/workflows/ci.yml        # Automated build, test (.NET + pytest), GHCR push & SSH deploy
├── contracts/
│   └── events.proto                # Shared C# & Python Protocol Buffers schema
├── src/
│   ├── SponsorAid.Domain/          # Entities, Enums, Domain Exceptions & Value Objects (IdNumber, Iban)
│   ├── SponsorAid.Application/     # CQRS Commands/Handlers, MediatR Behaviors & State Machine Models
│   ├── SponsorAid.Infrastructure/  # EF Core PostgreSQL, RabbitMQ, e-Gov Scraper, AES-256 Zip & Telegram
│   └── SponsorAid.Api/             # Webhook Controller, DI Registration & Localization Setup
├── test/
│   └── SponsorAid.Tests/           # Testcontainers E2E Journeys & Unit Test Suites
└── worker/
    ├── main.py                     # RabbitMQ Protobuf Consumer Loop & FastAPI Server
    ├── microservices/
    │   ├── barcode_extraction/     # OpenCV WeChatQRCode + Caffe Super-Resolution
    │   ├── id_processing/          # Custom YOLOv8 Segmentation/Rotation, Azure OCR & A4 PDF Generator
    │   ├── population_registry_parsing/ # Anchor-based PDF Cropping & Vector Table Extraction
    │   ├── residence_parsing/      # Spatial Coordinate Quantization & Address Sanitizer
    │   └── semantic_matching/      # Turkish BERT Embeddings & LRU Matrix Cache
    └── tests/                      # Pytest Unit & FastAPI Integration Tests
```
