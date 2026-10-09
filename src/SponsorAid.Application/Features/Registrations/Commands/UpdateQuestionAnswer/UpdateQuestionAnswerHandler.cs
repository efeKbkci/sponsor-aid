using MediatR;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;

public class UpdateQuestionAnswerHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IMediator mediator) : IRequestHandler<UpdateQuestionAnswerCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IMediator _mediator = mediator;

    public async Task Handle(UpdateQuestionAnswerCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, "⚠️ Aktif kayıt bulunamadı.");
            return;
        }

        // 1. Durum Kontrolü: Kullanıcı anket aşamasına gelmiş mi?
        if (draft.CurrentStep < RegistrationStep.WaitingForQuestions)
        {
            await _notifier.SendMessageAsync(request.ChatId, "⚠️ Henüz soru-cevap aşamasına gelmediniz. Belge yükleme adımlarında güncelleme yapılamaz.");
            return;
        }

        // 2. Alanı sıfırla (Henüz cevaplanmadıysa veya geçersiz anahtarsa hata döner)
        var (success, message) = draft.ResetQuestionAnswer(request.QuestionKey);
        if (!success)
        {
            await _notifier.SendMessageAsync(request.ChatId, message);
            return;
        }

        // 3. Eğer başvuru önceden tamamlanmışsa (Completed) güncellemeye izin verilmez.
        if (draft.CurrentStep == RegistrationStep.Completed)
        {
            await _notifier.SendMessageAsync(request.ChatId, "⚠️ Başvuru zaten tamamlanmış. Güncellemeye izin verilmiyor.");
            return;
        }

        // 4. Eğer başvuru onay bekliyorsa, tekrar sorulacak sorulara geçiş yap.
        if (draft.CurrentStep == RegistrationStep.WaitingForConfirmation)
        {
            draft.SetStep(RegistrationStep.WaitingForQuestions);
        }

        await _draftRepository.SaveAsync(draft, cancellationToken);
        await _notifier.SendMessageAsync(request.ChatId, message);

        // 4. Doğrudan şelaleyi boş metinle tetikle -> Sistem sıfırlanan soruyu butonlu/mesajlı anında tekrar sorar!
        await _mediator.Send(new ProcessQuestionnaireAnswerCommand(draft.RegistrationKey), cancellationToken);
    }
}