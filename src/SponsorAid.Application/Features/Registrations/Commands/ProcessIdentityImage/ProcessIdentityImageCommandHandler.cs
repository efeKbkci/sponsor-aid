using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

public class ProcessIdentityImageCommandHandler(IFileStorageService fileStorage, IEventPublisher eventPublisher)
    : IRequestHandler<ProcessIdentityImageCommand>
{
    private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB
    public async Task Handle(ProcessIdentityImageCommand request, CancellationToken cancellationToken)
    {
        if (request.FileSize > MaxFileSizeInBytes)
        {
            throw new BusinessRuleException("Dosya boyutu çok büyük! Lütfen en fazla 5 MB boyutunda bir fotoğraf yükleyin.");
        }

        if (request.Extension.ToLower() is not ("jpg" or "png" or "jpeg"))
        {
            throw new BusinessRuleException("Kimlik fotoğrafınızın uzantısı sadece JPG, PNG veya JPEG olabilir.");
        }

        // 1. Fotoğrafı indir
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
            ImageTask = task // Zarfın içine görevi koyuyoruz
        };

        await eventPublisher.PublishAsync("document-processing-queue", envelope);
    }
}