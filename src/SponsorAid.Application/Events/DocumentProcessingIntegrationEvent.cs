namespace SponsorAid.Application.Events;

// Bu sınıf Worker'ın dinleyeceği RabbitMQ kuyruğuna (Örn: document-processing-queue) atılır.
public record BaseIntegrationEvent(
    string CommandType,
    string RegistrationKey
);

// 2. TEKİL BELGE İŞLEME EVENT'İ (Mevcut olanı güncelledik)
// Sadece kırpma veya gelecekteki OCR okuma işlemleri için
public record ProcessImageEvent(
    string RegistrationKey,
    string FilePath, // İndirilip diske/MinIO'ya kaydedilen dosyanın tam yolu
    string ImageSide // Örn: "front" veya "back"
) : BaseIntegrationEvent("ProcessImage", RegistrationKey);

// Sadece PDF birleştirme işlemine özel alanlar barındırır
public record MergeToPdfEvent(
    string RegistrationKey,
    string FrontFilePath,
    string BackFilePath
) : BaseIntegrationEvent("MergeToPdf", RegistrationKey);

public record ProcessDocumentEvent(
    string RegistrationKey,
    string FilePath // İndirilip diske/MinIO'ya kaydedilen dosyanın tam yolu
) : BaseIntegrationEvent("ProcessDocument", RegistrationKey);