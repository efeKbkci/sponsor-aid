using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;

public class ContinueRegistrationHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier) : IRequestHandler<ContinueRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;

    public async Task Handle(ContinueRegistrationCommand request, CancellationToken cancellationToken)
    {
        bool isValidKey = request.RegistrationKey.Length == 6 && request.RegistrationKey.All(char.IsAsciiLetterOrDigit);
        if (!isValidKey)
        {
            await _notifier.SendMessageAsync(request.ChatId, "⚠️ Takip kodunun 6 karakterli olduğundan ve yalnızca harf/rakam içerdiğinden emin olun.");
            return;
        }

        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        // 1. KAYIT BULUNAMADI KONTROLÜ
        if (draft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, $"⚠️ '<b>{request.RegistrationKey}</b>' takip koduna ait aktif bir başvuru kaydı bulunamadı.");
            return;
        }

        // 2. ZATEN BİTMİŞ KAYIT KONTROLÜ
        if (draft.CurrentStep == RegistrationStep.Completed)
        {
            await _notifier.SendMessageAsync(request.ChatId, $"✅ '<b>{request.RegistrationKey}</b>' takip kodlu başvuru zaten başarıyla tamamlanmıştır. Yeni bir kayıt için /start yazabilirsiniz.");
            return;
        }

        // 3. YENİ SOHBETE SAHİPLENDİRME VE KİLİT ÇÖZME
        // Kullanıcı başka sohbete geçtiği için ChatId güncellenir
        draft.ChatId = request.ChatId;

        // Eğer önceki oturumda sunucuda bir işlem yarım kaldıysa güvenli adıma geri çekilir
        draft.RevertProcessingState();

        await _draftRepository.SaveAsync(draft, cancellationToken);

        // 4. KULLANICIYA KALDIĞI ADIMI HATIRLAT
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
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Anket soruları aşamasındasınız. Kaldığınız sorudan devam etmek için lütfen herhangi bir mesaj yazıp gönderiniz.");
                break;

            default:
                await _notifier.SendMessageAsync(draft.ChatId, baseMessage + "Kaldığınız yerden devam etmek için lütfen bir mesaj gönderiniz.");
                break;
        }
    }
}