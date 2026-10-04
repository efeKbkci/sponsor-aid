namespace SponsorAid.Application.Events;

// Bu sınıf Worker'ın dinleyeceği RabbitMQ kuyruğuna (Örn: document-processing-queue) atılır.
public record DocumentProcessingIntegrationEvent(
    string RegistrationKey,
    string FilePath // İndirilip diske/MinIO'ya kaydedilen dosyanın tam yolu
);