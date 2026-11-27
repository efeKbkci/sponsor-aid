"""Azure Computer Vision Read API v3.2 client for image and scanned PDF OCR."""

import asyncio
import concurrent.futures
import os
import aiohttp

from ..config import BankAccountParsingConfig


class BankAccountOcrService:
    """Extracts full document text from images or scanned PDFs via Azure Vision Read API v3.2."""

    def __init__(self, config: BankAccountParsingConfig | None = None):
        self.config = config or BankAccountParsingConfig()
        self.endpoint = os.environ.get(self.config.AZURE_VISION_ENDPOINT_ENV)
        self.subscription_key = os.environ.get(self.config.AZURE_VISION_KEY_ENV)

        if not self.endpoint or not self.subscription_key:
            print("⚠️ UYARI: Azure Vision API anahtarları eksik! Lütfen .env dosyasını kontrol edin.")

    def extract_text(self, document_bytes: bytes) -> str:
        """Synchronously submit binary image or PDF bytes to Azure Read API and return extracted text."""
        if not document_bytes:
            raise ValueError("OCR için boş belge verisi gönderildi.")

        try:
            asyncio.get_running_loop()
        except RuntimeError:
            lines = asyncio.run(self._call_azure_read_api(document_bytes))
        else:
            with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
                lines = pool.submit(asyncio.run, self._call_azure_read_api(document_bytes)).result()

        return "\n".join(lines)

    async def _call_azure_read_api(self, document_bytes: bytes) -> list[str]:
        """Submit binary content to Azure Read API v3.2 and poll with bounded timeout."""
        endpoint = self.endpoint or os.environ.get(self.config.AZURE_VISION_ENDPOINT_ENV)
        subscription_key = self.subscription_key or os.environ.get(self.config.AZURE_VISION_KEY_ENV)

        if not endpoint or not subscription_key:
            raise ValueError("Sistemde Azure OCR anahtarları bulunamadı.")

        analyze_url = f"{endpoint.rstrip('/')}/vision/v3.2/read/analyze"
        headers = {
            "Ocp-Apim-Subscription-Key": subscription_key,
            "Content-Type": "application/octet-stream",
        }
        timeout = aiohttp.ClientTimeout(total=self.config.OCR_HTTP_TIMEOUT_SECONDS)

        async with aiohttp.ClientSession(timeout=timeout) as session:
            async with session.post(analyze_url, headers=headers, data=document_bytes) as response:
                response.raise_for_status()
                operation_url = response.headers.get("Operation-Location")

            if not operation_url:
                raise ValueError("Azure API'den operasyon doğrulama linki alınamadı.")

            poll_headers = {"Ocp-Apim-Subscription-Key": subscription_key}

            for _ in range(self.config.OCR_MAX_POLL_ATTEMPTS):
                await asyncio.sleep(self.config.OCR_POLL_INTERVAL_SECONDS)
                async with session.get(operation_url, headers=poll_headers) as poll_response:
                    poll_response.raise_for_status()
                    result = await poll_response.json()
                    status = result.get("status")

                    if status == "succeeded":
                        lines: list[str] = []
                        for read_result in result.get("analyzeResult", {}).get("readResults", []):
                            for line in read_result.get("lines", []):
                                text = line.get("text", "")
                                if text:
                                    lines.append(text)
                        return lines

                    if status == "failed":
                        raise ValueError("Azure OCR okuma işlemini başaramadı.")

        raise TimeoutError(
            f"Azure OCR işlemi zaman aşımına uğradı "
            f"({self.config.OCR_MAX_POLL_ATTEMPTS} deneme aşıldı)."
        )
