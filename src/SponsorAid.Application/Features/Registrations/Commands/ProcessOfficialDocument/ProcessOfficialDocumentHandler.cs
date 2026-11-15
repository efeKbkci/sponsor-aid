using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts; 
using SponsorAid.Domain.Exceptions;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;

/// <summary>
/// Validates official registration documents, stores them locally, and dispatches extraction tasks to the queue.
/// </summary>
public class ProcessBinaryDocumentCommandHandler : IRequestHandler<ProcessOfficialDocumentCommand>
{
    private readonly IFileStorageService _fileStorage;
    private readonly IEventPublisher _eventPublisher;

    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;

    public ProcessBinaryDocumentCommandHandler(
        IFileStorageService fileStorage,
        IEventPublisher eventPublisher)
    {
        _fileStorage = fileStorage;
        _eventPublisher = eventPublisher;
    }

    public async Task Handle(ProcessOfficialDocumentCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSize > MaxFileSizeInBytes)
        {
            throw new BusinessRuleException("Error_DocumentFileTooLarge");
        }

        var extension = request.Extension.ToLower();

        if (extension is not ("pdf" or "jpg" or "png" or "jpeg"))
        {
            throw new BusinessRuleException("Error_UnsupportedDocumentFileType");
        }

        string savedFilePath = await _fileStorage.DownloadAndSaveAsync(request.FileId, request.Extension);

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
