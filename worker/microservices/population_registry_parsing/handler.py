import events_pb2
from .config import PopulationRegistryConfig
from .services.extractor import PopulationRegistryExtractor
from .services.sanitizer import PopulationRegistrySanitizer


class PopulationRegistryHandler:
    def __init__(self):
        self.config = PopulationRegistryConfig()
        self.extractor = PopulationRegistryExtractor()
        self.sanitizer = PopulationRegistrySanitizer(
            self.config.INPUT_DATE_FORMAT, self.config.OUTPUT_DATE_FORMAT
        )

    @staticmethod
    def _normalize_header(text: str | None) -> str:
        if not text:
            return ""
        cleaned = " ".join(str(text).split())
        return cleaned.replace("İ", "I").replace("ı", "I").upper()

    def _resolve_column_spans(self, raw_table: list) -> dict[str, tuple[int, int]] | None:
        """Başlık satırından her sütunun (start, end) aralığını dinamik olarak çözer."""
        for row in raw_table[:3]:
            if not row:
                continue
            normalized = [self._normalize_header(c) for c in row]
            if not any("SIRA" in h for h in normalized):
                continue

            non_empty_indices = [i for i, h in enumerate(normalized) if h]
            spans: dict[str, tuple[int, int]] = {}

            for pos, col_idx in enumerate(non_empty_indices):
                next_idx = non_empty_indices[pos + 1] if pos + 1 < len(non_empty_indices) else len(row)
                header = normalized[col_idx]

                if "SIRA" in header:
                    spans["sira"] = (col_idx, next_idx)
                elif "YAKINLIK" in header:
                    spans["kinship"] = (col_idx, next_idx)
                elif "KIMLIK" in header:
                    spans["id_number"] = (col_idx, next_idx)
                elif header == "ADI":
                    spans["first_name"] = (col_idx, next_idx)
                elif "SOYADI" in header:
                    spans["last_name"] = (col_idx, next_idx)
                elif "DOĞUM" in header or "DOGUM" in header:
                    spans["birth_info"] = (col_idx, next_idx)
                elif "OLAYLAR" in header:
                    spans["events"] = (col_idx, len(row))

            required = {"sira", "kinship", "id_number", "first_name", "last_name", "birth_info", "events"}
            if required.issubset(spans.keys()):
                return spans

        return None

    @staticmethod
    def _read_span(row: list, span: tuple[int, int]) -> str:
        start, end = span
        parts = [str(cell).strip() for cell in row[start:end] if cell and str(cell).strip()]
        return "\n".join(parts)

    def process(self, task) -> events_pb2.WorkerResultMessage:
        file_path = task.file_path
        try:
            raw_table = self.extractor.extract(file_path)
            col_spans = self._resolve_column_spans(raw_table)

            # Başlık bulunamazsa config.py'daki sabit indekslere düş
            if col_spans is None:
                col_spans = {
                    "sira": (0, 1),
                    "kinship": (self.config.COL_KINSHIP, self.config.COL_KINSHIP + 1),
                    "id_number": (self.config.COL_ID_NUMBER, self.config.COL_ID_NUMBER + 1),
                    "first_name": (self.config.COL_FIRST_NAME, self.config.COL_FIRST_NAME + 1),
                    "last_name": (self.config.COL_LAST_NAME, self.config.COL_LAST_NAME + 1),
                    "birth_info": (self.config.COL_BIRTH_INFO, self.config.COL_BIRTH_INFO + 1),
                    "events": (self.config.COL_EVENTS, self.config.COL_EVENTS + 1),
                }

            people = []
            for row_index, row in enumerate(raw_table):
                if not row:
                    continue

                sira_hucresi = self._read_span(row, col_spans["sira"])
                if not sira_hucresi.isdigit():
                    continue

                try:
                    events_text = self._read_span(row, col_spans["events"])
                    death_date = self.sanitizer.parse_death_date(events_text)

                    person_fields = {
                        "kinship": self._read_span(row, col_spans["kinship"]),
                        "id_number": self._read_span(row, col_spans["id_number"]),
                        "first_name": self._read_span(row, col_spans["first_name"]),
                        "last_name": self._read_span(row, col_spans["last_name"]),
                        "birth_date": self.sanitizer.parse_birth_date(
                            self._read_span(row, col_spans["birth_info"])
                        ),
                    }
                    if death_date is not None:
                        person_fields["death_date"] = death_date

                    person = events_pb2.Person(**person_fields)
                    if not person.id_number or not person.first_name:
                        raise ValueError("Zorunlu alanlardan biri (TC veya İsim) eksik okundu.")

                    people.append(person)
                except IndexError as error:
                    raise ValueError(
                        f"Kritik Hata: {row_index}. satırda beklenmeyen sütun eksikliği."
                    ) from error
                except Exception as error:
                    raise ValueError(
                        f"Kritik Hata: {row_index}. satır işlenirken sorun oluştu. "
                        f"Detay: {str(error)}"
                    ) from error

            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type,
                is_success=True,
                saved_file_path=file_path,
                population_data=events_pb2.PopulationRegistryData(people=people),
            )
        except Exception as error:
            return events_pb2.WorkerResultMessage(
                registration_key=task.registration_key,
                document_type=task.document_type,
                is_success=False,
                error_message=str(error),
                saved_file_path=file_path,
            )

# CLI entry point for standalone verification and debugging.
if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Nüfus kayıt belgesini işler.")
    parser.add_argument("file_path", help="İşlenecek belgenin yolu")
    parser.add_argument("--registration-key", default="local-test")
    args = parser.parse_args()

    handler = PopulationRegistryHandler()
    task = events_pb2.ProcessOfficialDocTask(
        registration_key=args.registration_key,
        file_path=args.file_path,
        document_type=events_pb2.DocumentType.DOCUMENT_TYPE_POPULATION_REGISTRY,
    )
    print(handler.process(task))
