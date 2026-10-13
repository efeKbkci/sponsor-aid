using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using System.Text.RegularExpressions;

namespace SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;

public class ContinueRegistrationHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IMediator mediator) : IRequestHandler<ContinueRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(ContinueRegistrationCommand request, CancellationToken cancellationToken)
    {
        // 1. Kod Gönderilmediyse (Menüden Tıklandıysa) -> Kayıtları Listele
        if (string.IsNullOrWhiteSpace(request.RegistrationKey))
        {
            var registrations = await _draftRepository.ListAllAsync(request.ChatId.ToString(), cancellationToken);
            var options = registrations.ToDictionary(
                draft => ContinueRegistrationCommand.SelectionCallbackPrefix + draft.RegistrationKey,
                draft => $"{draft.RegistrationKey} - {(string.IsNullOrWhiteSpace(draft.Guardian.Name) ? "İsimsiz Vasi" : draft.Guardian.Name)}");

            if (options.Count == 0)
            {
                await _notifier.SendMessageAsync(request.ChatId, "⚠️ Daha önce başlatılan bir kayıt bulunamadı.");
                return;
            }

            await _notifier.SendQuestionWithButtonsAsync(request.ChatId, "Devam etmek istediğiniz kaydı seçin:", options);
            return;
        }

        // 2. Kod Gönderildiyse -> Doğrudan Kaydı Yükle
        var registrationKey = request.RegistrationKey.Trim().ToUpperInvariant();
        var draft = await _draftRepository.GetAsync(registrationKey, cancellationToken);

        if (draft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, $"⚠️ '<b>{registrationKey}</b>' takip koduna ait aktif bir başvuru kaydı bulunamadı.");
            return;
        }

        if (draft.CurrentStep == RegistrationStep.Completed)
        {
            await _notifier.SendMessageAsync(request.ChatId, $"✅ '<b>{registrationKey}</b>' takip kodlu başvuru zaten başarıyla tamamlanmıştır. Yeni kayıt için menüyü kullanabilirsiniz.");
            return;
        }

        draft.ChatId = request.ChatId;
        draft.IsActive = true;
        draft.RevertProcessingState();
        await _draftRepository.SaveAsync(draft, cancellationToken);

        await SendResumePromptAsync(draft);
    }

    private async Task SendResumePromptAsync(RegistrationDraftState draft)
    {
        string baseMessage = $"🔄 <b>Kayıt Yüklendi! (Takip Kodu: {draft.RegistrationKey})</b>\n\n";

        switch (draft.CurrentStep)
        {
            case RegistrationStep.WaitingForGuardianId:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Lütfen kayıt işlemini başlatmak için **Vasi T.C. Kimlik Numarasını** 11 haneli olarak yazınız.");
                break;

            case RegistrationStep.WaitingForPopulationRegistry:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Lütfen e-Devlet'ten aldığınız <b>Vukuatlı Nüfus Kayıt Örneği</b> belgenizi PDF (veya net bir ekran görüntüsü) olarak gönderiniz.");
                break;

            case RegistrationStep.WaitingForResidence:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Lütfen e-Devlet'ten aldığınız <b>İkametgah (Yerleşim Yeri)</b> belgenizi PDF olarak gönderiniz.");
                break;

            case RegistrationStep.WaitingForIdentityFront:
                string targetFront = draft.CurrentDocumentTargetName == "Vasi" ? "Vasi" : $"{draft.CurrentDocumentTargetName} isimli çocuğunuz";
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + $"Lütfen **{targetFront}** için kimlik belgesinin <b>ÖN YÜZ</b> fotoğrafını gönderiniz.");
                break;

            case RegistrationStep.WaitingForIdentityBack:
                string targetBack = draft.CurrentDocumentTargetName == "Vasi" ? "Vasi" : $"{draft.CurrentDocumentTargetName} isimli çocuğunuz";
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + $"Lütfen **{targetBack}** için kimlik belgesinin <b>ARKA YÜZ</b> fotoğrafını gönderiniz.");
                break;

            case RegistrationStep.WaitingForQuestions:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Anket soruları aşamasındasınız.");
                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey));
                break;

            case RegistrationStep.WaitingForConfirmation:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Başvuru onay aşamasındasınız.");
                await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey));
                break;

            default:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Kaldığınız yerden devam etmek için lütfen bir mesaj gönderiniz.");
                break;
        }
    }
}