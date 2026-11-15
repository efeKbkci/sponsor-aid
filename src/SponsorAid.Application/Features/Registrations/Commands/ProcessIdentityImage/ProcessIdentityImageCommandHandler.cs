using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

/// <summary>
/// Validates identity image constraints, stores the physical file, and enqueues an asynchronous OCR task.
/// </summary>
public class ProcessIdentityImageCommandHandler(IFileStorageService fileStorage, IEventPublisher eventPublisher)
    : IRequestHandler<ProcessIdentityImageCommand>
{
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024;
    public async Task Handle(ProcessIdentityImageCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSize > MaxFileSizeInBytes)
        {
            throw new BusinessRuleException("Error_IdentityImageTooLarge");
        }

        if (request.Extension.ToLower() is not ("jpg" or "png" or "jpeg"))
        {
            throw new BusinessRuleException("Error_UnsupportedIdentityImageType");
        }

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
