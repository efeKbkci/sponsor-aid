using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Common;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;

/// <summary>
/// Executes the questionnaire waterfall flow, validates incoming answers, and prompts for missing applicant details.
/// </summary>
public class ProcessQuestionnaireAnswerHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier,
    ISemanticMatcher semanticMatcher,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<ProcessQuestionnaireAnswerCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;
    private readonly ISemanticMatcher _semanticMatcher = semanticMatcher;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(ProcessQuestionnaireAnswerCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null) throw new KeyNotFoundException($"Draft with key '{request.RegistrationKey}' not found.");

        string text = request.AnswerText?.Trim() ?? string.Empty;

        if (draft.Guardian.ApplicantRole == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<ApplicantRole>(text, out var parsedRole))
            {
                draft.ApplyUpdates(new() { { "GuardianApplicantRole", text } });
                string label = _localizer[$"Option_ApplicantRole_{parsedRole}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_ApplicantRole"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Mother", _localizer["Option_ApplicantRole_Mother"] }, { "Guardian", _localizer["Option_ApplicantRole_Guardian"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_ApplicantRole"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.Gender == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<Gender>(text, out var parsedGender))
            {
                draft.ApplyUpdates(new() { { "GuardianGender", text } });
                string label = _localizer[$"Option_Gender_{parsedGender}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_Gender"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Female", _localizer["Option_Gender_Female"] }, { "Male", _localizer["Option_Gender_Male"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_Gender"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.MaritalStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<MaritalStatus>(text, out var parsedMaritalStatus))
            {
                draft.ApplyUpdates(new() { { "GuardianMaritalStatus", text } });
                string label = _localizer[$"Option_MaritalStatus_{parsedMaritalStatus}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_MaritalStatus"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Single", _localizer["Option_MaritalStatus_Single"] }, { "Married", _localizer["Option_MaritalStatus_Married"] }, { "Divorced", _localizer["Option_MaritalStatus_Divorced"] }, { "Widowed", _localizer["Option_MaritalStatus_Widowed"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_MaritalStatus"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.EducationLevel == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<EducationLevel>(text, out var parsedEducation))
            {
                draft.ApplyUpdates(new() { { "GuardianEducationLevel", text } });
                string label = _localizer[$"Option_EducationLevel_{parsedEducation}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_EducationLevel"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> {
                    { "Literate", _localizer["Option_EducationLevel_Literate"] }, { "PrimarySchool", _localizer["Option_EducationLevel_PrimarySchool"] }, { "MiddleSchool", _localizer["Option_EducationLevel_MiddleSchool"] },
                    { "HighSchool", _localizer["Option_EducationLevel_HighSchool"] }, { "AssociatesDegree", _localizer["Option_EducationLevel_AssociatesDegree"] }, { "BachelorsDegree", _localizer["Option_EducationLevel_BachelorsDegree"] }
                };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_EducationLevel"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.EducationStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<EducationStatus>(text, out var parsedEducationStatus))
            {
                draft.ApplyUpdates(new() { { "GuardianEducationStatus", text } });
                string label = _localizer[$"Option_EducationStatus_{parsedEducationStatus}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_EducationStatus"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> {
                    { "Graduated", _localizer["Option_EducationStatus_Graduated"] },
                    { "NeverAttended", _localizer["Option_EducationStatus_NeverAttended"] },
                    { "DroppedOut", _localizer["Option_EducationStatus_DroppedOut"] },
                    { "Student", _localizer["Option_EducationStatus_Student"] }
                };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_EducationStatus"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.DegreeOfKinship == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<DegreeOfKinship>(text, out var parsedKinship))
            {
                draft.ApplyUpdates(new() { { "GuardianDegreeOfKinship", text } });
                string label = _localizer[$"Option_DegreeOfKinship_{parsedKinship}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_DegreeOfKinship"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> {
                    { "Mother", _localizer["Option_DegreeOfKinship_Mother"] }, { "Grandmother", _localizer["Option_DegreeOfKinship_Grandmother"] }, { "Grandfather", _localizer["Option_DegreeOfKinship_Grandfather"] },
                    { "PaternalUncle", _localizer["Option_DegreeOfKinship_PaternalUncle"] }, { "MaternalUncle", _localizer["Option_DegreeOfKinship_MaternalUncle"] }, { "PaternalAunt", _localizer["Option_DegreeOfKinship_PaternalAunt"] },
                    { "MaternalAunt", _localizer["Option_DegreeOfKinship_MaternalAunt"] }, { "Sibling", _localizer["Option_DegreeOfKinship_Sibling"] }, { "Other", _localizer["Option_DegreeOfKinship_Other"] }
                };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_DegreeOfKinship"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (string.IsNullOrEmpty(draft.Guardian.Profession))
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianProfession", text } }); text = ""; }

            if (string.IsNullOrEmpty(draft.Guardian.Profession))
            {
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_GuardianProfession"]);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Guardian.PhoneNumber == null)
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianPhoneNumber", text } }); text = ""; }

            if (draft.Guardian.PhoneNumber == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_GuardianPhoneNumber"]);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.FamilyFinancial.FamilyMemberCount == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "FamilyMemberCount", text } }); text = ""; }

            if (draft.FamilyFinancial.FamilyMemberCount == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_FamilyMemberCount"]);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.Father.Profession == null)
        {
            bool isSemanticMatch = false;

            // Consults semantic matcher service to map free text input to standard profession enum keys.
            if (!string.IsNullOrEmpty(text) && !Enum.TryParse<FatherProfession>(text, out _))
            {
                var match = await _semanticMatcher.FindBestMatchAsync(text, RegistrationSemanticCatalog.FatherProfessions, 0.65f, cancellationToken);
                if (match != null)
                {
                    text = match.Key;
                    isSemanticMatch = true;
                }
            }

            if (!string.IsNullOrEmpty(text) && Enum.TryParse<FatherProfession>(text, out var parsedProfession))
            {
                draft.ApplyUpdates(new() { { "FatherProfession", text } });
                string label = _localizer[$"Option_FatherProfession_{parsedProfession}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_FatherProfession"], label);

                if (isSemanticMatch)
                {
                    await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_SemanticMatchSaved", label].Value);
                }

                text = "";
            }
            else
            {
                if (!string.IsNullOrEmpty(text))
                {
                    await _notifier.SendMessageAsync(draft.ChatId, _localizer["Error_InvalidAnswer"].Value);
                }

                var options = new Dictionary<string, string> { { "Worker", _localizer["Option_FatherProfession_Worker"] }, { "CivilServant", _localizer["Option_FatherProfession_CivilServant"] }, { "Farmer", _localizer["Option_FatherProfession_Farmer"] }, { "Freelancer", _localizer["Option_FatherProfession_Freelancer"] }, { "Other", _localizer["Option_FatherProfession_Other"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_FatherProfession"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken);
                return;
            }
        }

        if (draft.Father.DeathReason == null)
        {
            bool isSemanticMatch = false;

            // Resolves non-exact natural language phrases to standardized death reason categories.
            if (!string.IsNullOrEmpty(text) && !Enum.TryParse<FatherDeathReason>(text, out _))
            {
                var match = await _semanticMatcher.FindBestMatchAsync(text, RegistrationSemanticCatalog.FatherDeathReasons, 0.65f, cancellationToken);
                if (match != null)
                {
                    text = match.Key;
                    isSemanticMatch = true;
                }
            }

            if (!string.IsNullOrEmpty(text) && Enum.TryParse<FatherDeathReason>(text, out var parsedDeathReason))
            {
                draft.ApplyUpdates(new() { { "FatherDeathReason", text } });
                string label = _localizer[$"Option_FatherDeathReason_{parsedDeathReason}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_FatherDeathReason"], label);

                if (isSemanticMatch)
                {
                    await _notifier.SendMessageAsync(draft.ChatId, _localizer["Info_SemanticMatchSaved", label].Value);
                }

                text = "";
            }
            else
            {
                if (!string.IsNullOrEmpty(text))
                {
                    await _notifier.SendMessageAsync(draft.ChatId, _localizer["Error_InvalidAnswer"].Value);
                }

                var options = new Dictionary<string, string> { { "NaturalDeath", _localizer["Option_FatherDeathReason_NaturalDeath"] }, { "HeartAttack", _localizer["Option_FatherDeathReason_HeartAttack"] }, { "Cancer", _localizer["Option_FatherDeathReason_Cancer"] }, { "TrafficAccident", _localizer["Option_FatherDeathReason_TrafficAccident"] }, { "WorkAccident", _localizer["Option_FatherDeathReason_WorkAccident"] }, { "Earthquake", _localizer["Option_FatherDeathReason_Earthquake"] }, { "Other", _localizer["Option_FatherDeathReason_Other"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_FatherDeathReason"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken);
                return;
            }
        }

        if (draft.DisabledMember == null || draft.DisabledMember.Name == null)
        {
            if (text == "Yok") { draft.ApplyUpdates(new() { { "DisabledName", "Yok" } }); text = ""; }
            else if (text == "Var") { await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_DisabledMemberName"]); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            else if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledName", text } }); text = ""; }

            if (draft.DisabledMember?.Name == null)
            {
                var options = new Dictionary<string, string> { { "Var", _localizer["Option_Yes"] }, { "Yok", _localizer["Option_No"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_DisabledMemberExists"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.DisabledMember != null && draft.DisabledMember.Name != "Yok")
        {
            if (draft.DisabledMember.Diagnosis == null)
            {
                if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDiagnosis", text } }); text = ""; }
                if (draft.DisabledMember.Diagnosis == null) { await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_DisabledMemberDiagnosis"]); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            }
            if (draft.DisabledMember.DiseaseStatus == null)
            {
                if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDiseaseStatus", text } }); text = ""; }
                if (draft.DisabledMember.DiseaseStatus == null) { await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_DisabledMemberStatus"]); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            }
            if (draft.DisabledMember.TreatmentStatus == null)
            {
                if (!string.IsNullOrEmpty(text) && Enum.TryParse<TreatmentStatus>(text, out _)) { draft.ApplyUpdates(new() { { "DisabledTreatmentStatus", text } }); text = ""; }
                else
                {
                    var options = new Dictionary<string, string> { { "BeingTreated", _localizer["Option_TreatmentStatus_BeingTreated"] }, { "NeedsTreatment", _localizer["Option_TreatmentStatus_NeedsTreatment"] }, { "Incurable", _localizer["Option_TreatmentStatus_Incurable"] }, { "NeedsTreatmentAbroad", _localizer["Option_TreatmentStatus_NeedsTreatmentAbroad"] } };
                    await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_TreatmentStatus"], options);
                    await _draftRepository.SaveAsync(draft, cancellationToken); return;
                }
            }
            if (draft.DisabledMember.Description == null)
            {
                if (text == "Geç") { draft.ApplyUpdates(new() { { "DisabledDescription", "Belirtilmedi" } }); text = ""; }
                else if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDescription", text } }); text = ""; }

                if (draft.DisabledMember.Description == null)
                {
                    var options = new Dictionary<string, string> { { "Geç", _localizer["Option_SkipDescription"] } };
                    await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_DisabledMemberDescription"], options);
                    await _draftRepository.SaveAsync(draft, cancellationToken); return;
                }
            }
        }

        if (draft.FamilyFinancial.HousingStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<HousingStatus>(text, out var parsedHousingStatus))
            {
                draft.ApplyUpdates(new() { { "HousingStatus", text } });
                string label = _localizer[$"Option_HousingStatus_{parsedHousingStatus}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_HousingStatus"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "OwnHouse", _localizer["Option_HousingStatus_OwnHouse"] }, { "Rent", _localizer["Option_HousingStatus_Rent"] }, { "LivingWithRelatives", _localizer["Option_HousingStatus_LivingWithRelatives"] }, { "RentFree", _localizer["Option_HousingStatus_RentFree"] }, { "Tent", _localizer["Option_HousingStatus_Tent"] }, { "Container", _localizer["Option_HousingStatus_Container"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_HousingStatus"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.FamilyFinancial.SocialSecurity == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<SocialSecurity>(text, out var parsedSocialSecurity))
            {
                draft.ApplyUpdates(new() { { "SocialSecurity", text } });
                string label = _localizer[$"Option_SocialSecurity_{parsedSocialSecurity}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_SocialSecurity"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Sgk", _localizer["Option_SocialSecurity_Sgk"] }, { "BagKur", _localizer["Option_SocialSecurity_BagKur"] }, { "GreenCard", _localizer["Option_SocialSecurity_GreenCard"] }, { "Other", _localizer["Option_SocialSecurity_Other"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_SocialSecurity"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.FamilyFinancial.IncomeStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<IncomeStatus>(text, out var parsedIncomeStatus))
            {
                draft.ApplyUpdates(new() { { "IncomeStatus", text } });
                string label = _localizer[$"Option_IncomeStatus_{parsedIncomeStatus}"].Value;
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, _localizer["Question_IncomeStatus"], label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "FixedSalary", _localizer["Option_IncomeStatus_FixedSalary"] }, { "DailyWage", _localizer["Option_IncomeStatus_DailyWage"] }, { "RentIncome", _localizer["Option_IncomeStatus_RentIncome"] }, { "Other", _localizer["Option_IncomeStatus_Other"] } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, _localizer["Question_IncomeStatus"], options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.FamilyFinancial.Income == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "Income", text } }); text = ""; }
            if (draft.FamilyFinancial.Income == null) { await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_MonthlyIncome"]); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
        }

        if (draft.FamilyFinancial.Expense == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "Expense", text } }); text = ""; }
            if (draft.FamilyFinancial.Expense == null) { await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_MonthlyExpense"]); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
        }

        if (draft.Guardian.Iban == null)
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianIban", text } }); text = ""; }

            if (draft.Guardian.Iban == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, _localizer["Question_GuardianIban"]);
                await _draftRepository.SaveAsync(draft, cancellationToken);
                return;
            }
            else
            {
                // Transitions workflow to confirmation once all questionnaire fields have valid values.
                draft.GenerateAutomatedDetails(_localizer);
                draft.SetStep(RegistrationStep.WaitingForConfirmation);
                await _draftRepository.SaveAsync(draft, cancellationToken);

                await SendSummaryConfirmationAsync(draft);
                return;
            }
        }

        // Ensures users reaching the end of the question list are always guided to registration confirmation.
        if (draft.CurrentStep != RegistrationStep.Completed)
        {
            draft.SetStep(RegistrationStep.WaitingForConfirmation);
            await _draftRepository.SaveAsync(draft, cancellationToken);
            await SendSummaryConfirmationAsync(draft);
        }
    }

    private async Task SendSummaryConfirmationAsync(RegistrationDraftState draft)
    {
        string summaryText = draft.BuildSummaryHtml(_localizer);
        var buttons = new Dictionary<string, string>
        {
            { "confirm_registration", _localizer["Button_ConfirmRegistration"] }
        };

        await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, summaryText, buttons);
    }

    private async Task MarkAsAnsweredIfCallback(long chatId, int? messageId, string question, string answerLabel)
    {
        if (messageId.HasValue)
        {
            await _notifier.UpdateMessageAsAnsweredAsync(chatId, messageId.Value, question, answerLabel);
        }
    }
}
