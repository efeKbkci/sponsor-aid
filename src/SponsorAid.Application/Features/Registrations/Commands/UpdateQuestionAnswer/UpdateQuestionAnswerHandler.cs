using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;
using SponsorAid.Application.Features.Registrations.Common;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using static System.Net.Mime.MediaTypeNames;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;

public class UpdateQuestionAnswerHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    IMediator mediator,
    ISemanticMatcher semanticMatcher,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<UpdateQuestionAnswerCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly IMediator _mediator = mediator;
    private readonly ISemanticMatcher _semanticMatcher = semanticMatcher;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;
    private string _targetKey = string.Empty;

    public async Task Handle(UpdateQuestionAnswerCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        // -- Güncelleme işlemine izin verilip verilmeyeceğini kontrol et -- //

        if (draft == null)
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_NoActiveRegistration"]);
            return;
        }

        // 1. Durum Kontrolü: Kullanıcı anket aşamasına gelmiş mi?
        if (draft.CurrentStep < RegistrationStep.WaitingForQuestions)
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_RegistrationNotInQuestionnaire"]);
            return;
        }

        // 2. Eğer başvuru önceden tamamlanmışsa (Completed) güncellemeye izin verilmez.
        if (draft.CurrentStep == RegistrationStep.Completed)
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_CompletedRegistrationCannotBeUpdated"]);
            return;
        }

        // -- Kullanıcının hangi soruyu güncellemek istediğini öğren -- //

        // Bayrak = 1 olduğu sürece girdiler bu sınıfa yönlendirilir.
        draft.IsWaitingForUpdateFieldSelection = true; 
        await _draftRepository.SaveAsync(draft, cancellationToken);

        // Kullanıcı güncelle butonuna basmıştır. Şimdi alan bilgisi sorulacak.
        if (string.IsNullOrWhiteSpace(request.UserInput))
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Prompt_WhichInformationToUpdate"]);
            return;
        }

        // -- Kullanıcının hangi soruyu güncellemek istediğini öğrendikten sonra işlemi başlat -- //
        if (!string.IsNullOrWhiteSpace(request.UserInput))
        {
            var userInput = request.UserInput.Trim().Replace('İ', 'i');
            if (userInput.Equals("iptal", StringComparison.OrdinalIgnoreCase))
            {
                draft.IsWaitingForUpdateFieldSelection = false;
                await draftRepository.SaveAsync(draft, cancellationToken);
                await notifier.SendMessageAsync(request.ChatId, _localizer["Info_UpdateCancelled"]);
                return;
            }

            var targetMatch = await _semanticMatcher.FindBestMatchAsync(
                request.UserInput, 
                RegistrationSemanticCatalog.UpdateTargets, 
                0.8f, 
                cancellationToken
            );

            if (targetMatch == null)
            {
                await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_UpdateFieldNotFound"]);
                return;
            }

            // Güncellenmek istenen alan tespit edildi. 
            else
            {
                _targetKey = targetMatch.Key;
                draft.IsWaitingForUpdateFieldSelection = false;
                await draftRepository.SaveAsync(draft, cancellationToken);
            }
            
        }
        else
        {
            await _notifier.SendMessageAsync(request.ChatId, _localizer["Error_UpdateFieldRequired"]);
            return;
        }

        // -- Güncelleme işlemi başlatılıyor -- //
        var (success, messageKey, questionLabelKey) = draft.ResetQuestionAnswer(_targetKey);
        var message = success
            ? _localizer[messageKey, _localizer[questionLabelKey].Value].Value
            : questionLabelKey.Length == 0
                ? _localizer[messageKey].Value
                : _localizer[messageKey, _localizer[questionLabelKey].Value].Value;
        if (!success)
        {
            await _notifier.SendMessageAsync(request.ChatId, message);
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