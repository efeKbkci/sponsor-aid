using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Events;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;

public class ProcessBinaryDocumentCommandHandler : IRequestHandler<ProcessBinaryDocumentCommand>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IEventPublisher _eventPublisher; // ARTIK TEKNOLOJİ BAĞIMSIZ!

    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;

    public ProcessBinaryDocumentCommandHandler(
        IFileStorageService fileStorage,
        IEventPublisher eventPublisher)
    {
        _fileStorage = fileStorage;
        _eventPublisher = eventPublisher;
    }

    public async Task Handle(ProcessBinaryDocumentCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSize > MaxFileSizeInBytes)
        {
            throw new ArgumentException("Dosya boyutu çok büyük! Lütfen en fazla 5 MB boyutunda bir belge yükleyin.");
        }

        if (request.Extension.ToLower() is not ("pdf" or "jpg" or "png" or "jpeg"))
        {
            throw new ArgumentException("Desteklenmeyen dosya türü! Lütfen sadece PDF, JPG, PNG veya JPEG dosyalarını yükleyin.");
        }

        string savedFilePath = await _fileStorage.DownloadAndSaveAsync(request.FileId, request.Extension);

        var message = new DocumentProcessingIntegrationEvent(
            RegistrationKey: request.RegistrationKey,
            FilePath: savedFilePath
        );

        // Handler sadece "Bu mesajı fırlat" der. Arka planda RabbitMQ mu var, Kafka mı var bilmez.
        await _eventPublisher.PublishAsync("document-processing-queue", message);
    }
}