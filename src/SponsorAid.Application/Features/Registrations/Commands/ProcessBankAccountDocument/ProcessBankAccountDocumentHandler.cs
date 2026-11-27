using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBankAccountDocument;

/// <summary>
/// Stores an uploaded bank account document locally and publishes an extraction task to the background worker queue.
/// </summary>
public class ProcessBankAccountDocumentHandler(
    IFileStorageService fileStorage,
    IEventPublisher eventPublisher) : IRequestHandler<ProcessBankAccountDocumentCommand>
{
    private readonly IFileStorageService _fileStorage = fileStorage;
    private readonly IEventPublisher _eventPublisher = eventPublisher;

    public async Task Handle(ProcessBankAccountDocumentCommand request, CancellationToken cancellationToken)
    {
        string savedFilePath = await _fileStorage.DownloadAndSaveAsync(request.FileId, request.Extension);

        var task = new ProcessBankAccountDocTask
        {
            RegistrationKey = request.RegistrationKey,
            FilePath = savedFilePath
        };

        var envelope = new WorkerTaskEnvelope
        {
            CommandType = CommandType.ProcessBankAccountDoc,
            BankAccountDocTask = task
        };

        await _eventPublisher.PublishAsync("document-processing-queue", envelope);
    }
}
