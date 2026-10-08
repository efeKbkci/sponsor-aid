# handler.py
import events_pb2
from .config import PopulationRegistryConfig
from .services.extractor import PopulationRegistryExtractor
from .services.sanitizer import PopulationRegistrySanitizer

class PopulationRegistryHandler:
    def __init__(self):
        self.config = PopulationRegistryConfig()
        self.extractor = PopulationRegistryExtractor()
        self.sanitizer = PopulationRegistrySanitizer(self.config.INPUT_DATE_FORMAT, self.config.OUTPUT_DATE_FORMAT)

    def process(self, task) -> events_pb2.WorkerResultMessage:
        file_path = task.file_path
        try:
            raw_table = self.extractor.extract(file_path)
            people = []

            for row_index, row in enumerate(raw_table):
                if not row:
                    continue

                # SIRA hücresi rakam değilse (örneğin başlık satırı ise) atla
                sira_hucresi = str(row[0]).strip() if row[0] else ""
                if not sira_hucresi.isdigit():
                    continue

                # Tablomuzun 13 hücreden (0'dan 12'ye) oluştuğunu garanti altına alıyoruz
                if len(row) <= self.config.COL_EVENTS:
                    continue

                try:
                    death_date = self.sanitizer.parse_death_date(row[self.config.COL_EVENTS])
                    person_fields = {
                        "kinship": row[self.config.COL_KINSHIP].strip(),
                        "id_number": row[self.config.COL_ID_NUMBER].strip(),
                        "first_name": row[self.config.COL_FIRST_NAME].strip(),
                        "last_name": row[self.config.COL_LAST_NAME].strip(),
                        "birth_date": self.sanitizer.parse_birth_date(
                            row[self.config.COL_BIRTH_INFO]
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

# Uygulama Giriş Noktası
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