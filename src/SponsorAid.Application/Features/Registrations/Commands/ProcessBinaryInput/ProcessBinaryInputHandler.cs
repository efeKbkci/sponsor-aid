using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;
using SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;

public class ProcessBinaryInputHandler(
    IDraftStateRepository draftRepository, 
    IMediator mediator, IUserNotifier notifier) : IRequestHandler<ProcessBinaryInputCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IMediator _mediator = mediator;
    private readonly IUserNotifier _notifier = notifier;

    public async Task Handle(ProcessBinaryInputCommand request, CancellationToken cancellationToken)
    {
        var activeDraft = await _draftRepository.GetActiveByChatIdAsync(request.ChatId, cancellationToken);

        if (activeDraft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, "⚠️ Aktif bir başvurunuz bulunamadı. Lütfen önce <b>/new</b> yazarak kayıt başlatınız.");
            return;
        }

        switch (activeDraft.CurrentStep)
        {
            case RegistrationStep.WaitingForPopulationRegistry:
                activeDraft.SetStep(RegistrationStep.ProcessingPopulationRegistry);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, "⏳ Nüfus belgeniz okunuyor, lütfen bekleyiniz...");

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "PopulationRegistry"), cancellationToken);
                break;

            case RegistrationStep.WaitingForResidence:
                activeDraft.SetStep(RegistrationStep.ProcessingResidence);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, "⏳ İkametgah belgeniz işleniyor, lütfen bekleyiniz...");

                await _mediator.Send(new ProcessOfficialDocumentCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "Residence"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityFront:
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityFront);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, "⏳ Kimlik ön yüzünüz yapay zeka ile işleniyor...");

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "front"), cancellationToken);
                break;

            case RegistrationStep.WaitingForIdentityBack:
                activeDraft.SetStep(RegistrationStep.ProcessingIdentityBack);
                await _draftRepository.SaveAsync(activeDraft, cancellationToken);
                await _notifier.SendMessageAsync(request.ChatId, "⏳ Kimlik arka yüzünüz yapay zeka ile işleniyor...");

                await _mediator.Send(new ProcessIdentityImageCommand(
                    activeDraft.RegistrationKey, request.FileId, request.FileSize, request.Extension, "back"), cancellationToken);
                break;

            // Kilit Altındaki Asenkron Durumlar
            case RegistrationStep.ProcessingPopulationRegistry:
            case RegistrationStep.ProcessingResidence:
            case RegistrationStep.ProcessingIdentityFront:
            case RegistrationStep.ProcessingIdentityBack:
            case RegistrationStep.MergingDocuments:
                await _notifier.SendMessageAsync(request.ChatId, "⚠️ Şu an sistem bir önceki belgeniz üzerinde çalışıyor. Lütfen işlemin bitmesini bekleyiniz.");
                break;

            // Belge Beklenmeyen Durumlar (Anket, Onay vb.)
            default:
                await _notifier.SendMessageAsync(request.ChatId, "⚠️ Şu an belge yükleme aşamasında değilsiniz. Lütfen metin olarak cevap veriniz veya seçenek butonlarını kullanınız.");
                break;
        }
    }
}
