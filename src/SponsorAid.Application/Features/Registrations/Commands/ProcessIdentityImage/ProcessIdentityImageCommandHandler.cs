using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

/// <summary>
/// Stores identity card photos locally and enqueues an asynchronous OCR task.
/// </summary>
public class ProcessIdentityImageCommandHandler(IFileStorageService fileStorage, IEventPublisher eventPublisher)
    : IRequestHandler<ProcessIdentityImageCommand>
{
    public async Task Handle(ProcessIdentityImageCommand request, CancellationToken cancellationToken)
    {
        string savedFilePath = await fileStorage.DownloadAndSaveAsync(request.FileId, request.Extension);

        var task = new ProcessImageTask
        {
            RegistrationKey = request.RegistrationKey,
            FilePath = savedFilePath,
            ImageSide = request.ImageSide == "front" ? ImageSide.Front : ImageSide.Back
        };

        var envelope = new WorkerTaskEnvelope
        {
            CommandType = CommandType.ProcessImage,
            ImageTask = task
        };

        // Publishes task envelope to message queue so background workers handle OCR without blocking chat requests.
        await eventPublisher.PublishAsync("document-processing-queue", envelope);
    }
}
