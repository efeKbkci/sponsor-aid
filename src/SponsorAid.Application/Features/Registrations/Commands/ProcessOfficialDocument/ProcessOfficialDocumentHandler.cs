using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts; 

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;

/// <summary>
/// Stores official registration documents locally and dispatches extraction tasks to the queue.
/// </summary>
public class ProcessBinaryDocumentCommandHandler : IRequestHandler<ProcessOfficialDocumentCommand>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IEventPublisher _eventPublisher;

    public ProcessBinaryDocumentCommandHandler(
        IFileStorageService fileStorage,
        IEventPublisher eventPublisher)
    {
        _fileStorage = fileStorage;
        _eventPublisher = eventPublisher;
    }

    public async Task Handle(ProcessOfficialDocumentCommand request, CancellationToken cancellationToken)
    {
        var extension = (request.Extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

        string savedFilePath = await _fileStorage.DownloadAndSaveAsync(request.FileId, extension);

        var task = new ProcessOfficialDocTask
        {
            RegistrationKey = request.RegistrationKey,
            FilePath = savedFilePath,
            DocumentType = request.DocumentType == "PopulationRegistry"
                                    ? DocumentType.PopulationRegistry
                                    : DocumentType.Residence
        };

        // Scanned image documents require barcode extraction first, whereas PDF files contain full structured text.
        var envelope = new WorkerTaskEnvelope
        {
            CommandType = extension == "pdf" 
                                ? CommandType.ProcessOfficialDoc
                                : CommandType.ExtractBarcode,
            OfficialDocTask = task
        };

        // Offloads heavy document parsing and OCR tasks to background workers through the message queue.
        await _eventPublisher.PublishAsync("document-processing-queue", envelope);
    }
}
