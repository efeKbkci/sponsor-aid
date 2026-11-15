using Microsoft.Extensions.Localization;
using Microsoft.Win32;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SponsorAid.Application.Models;

/// <summary>
/// Holds the in-memory draft state of an ongoing registration workflow.
/// </summary>
public class RegistrationDraftState
{
    public string RegistrationKey { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public bool IsWaitingForUpdateFieldSelection { get; set; } = false;

    public bool IsWaitingForRegistrationKey { get; set; } = false;

    public GuardianDraft Guardian { get; set; } = new();
    public List<ChildDraft> Children { get; set; } = new();
    public FatherDraft Father { get; set; } = new();
    public FamilyFinancialDraft FamilyFinancial { get; set; } = new();
    public DisabledMemberDraft? DisabledMember { get; set; }
    public SystemDocumentsDraft SystemDocuments { get; set; } = new();
    public AddressDraft ResidenceAddress { get; set; } = new();

    public RegistrationStep CurrentStep { get; set; } = RegistrationStep.WaitingForGuardianId;
    public string? CurrentDocumentTargetName { get; set; } = "Vasi";

    private readonly Dictionary<string, Action<string>> _updateStrategies;

    public RegistrationDraftState()
    {
        _updateStrategies = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "GuardianIdNumber", value => Guardian.IdNumber = new IdNumber(value) },
            { "GuardianApplicantRole", value => Guardian.ApplicantRole = Enum.Parse<ApplicantRole>(value, true) },
            { "GuardianGender", value => Guardian.Gender = Enum.Parse<Gender>(value, true) },
            { "GuardianMaritalStatus", value => Guardian.MaritalStatus = Enum.Parse<MaritalStatus>(value, true) },
            { "GuardianEducationLevel", value => Guardian.EducationLevel = Enum.Parse<EducationLevel>(value, true) },
            { "GuardianEducationStatus", value => Guardian.EducationStatus = Enum.Parse<EducationStatus>(value, true) },
            { "GuardianDegreeOfKinship", value => Guardian.DegreeOfKinship = Enum.Parse<DegreeOfKinship>(value, true) },
            { "GuardianProfession", value => Guardian.Profession = value.Trim() },
            { "GuardianPhoneNumber", value => Guardian.PhoneNumber = new PhoneNumber(value) },
            { "GuardianIban", value => Guardian.Iban = new Iban(value) },

            { "FatherProfession", value => Father.Profession = Enum.Parse<FatherProfession>(value, true) },
            { "FatherDeathReason", value => Father.DeathReason = Enum.Parse<FatherDeathReason>(value, true) },

            { "FamilyMemberCount", value => FamilyFinancial.FamilyMemberCount = int.Parse(value) },
            { "HousingStatus", value => FamilyFinancial.HousingStatus = Enum.Parse<HousingStatus>(value, true) },
            { "SocialSecurity", value => FamilyFinancial.SocialSecurity = Enum.Parse<SocialSecurity>(value, true) },
            { "IncomeStatus", value => FamilyFinancial.IncomeStatus = Enum.Parse<IncomeStatus>(value, true) },
            { "Income", value => FamilyFinancial.Income = int.Parse(value) },
            { "Expense", value => FamilyFinancial.Expense = int.Parse(value) },

            { "DisabledName", value => { EnsureDisabledMemberExists(); DisabledMember!.Name = value; } },
            { "DisabledDiagnosis", value => { EnsureDisabledMemberExists(); DisabledMember!.Diagnosis = value; } },
            { "DisabledDiseaseStatus", value => { EnsureDisabledMemberExists(); DisabledMember!.DiseaseStatus = value; } },
            { "DisabledTreatmentStatus", value => { EnsureDisabledMemberExists(); DisabledMember!.TreatmentStatus = Enum.Parse<TreatmentStatus>(value, true); } },
            { "DisabledDescription", value => { EnsureDisabledMemberExists(); DisabledMember!.Description = value; } },

            { "Petition", value => SystemDocuments.Petition = new Petition(value) }
        };
    }

    public void ApplyUpdates(Dictionary<string, string> updates)
    {
        foreach (var (key, value) in updates)
        {
            if (_updateStrategies.TryGetValue(key, out var action))
            {
                action(value);
            }
        }
    }

    public void ApplyPopulationRegistryResult(PopulationRegistryData data)
    {
        var guardianPerson = data.People.FirstOrDefault(p => p.IdNumber == Guardian.IdNumber?.Value);

        if (guardianPerson == null)
        {
            throw new DocumentValidationException("Error_GuardianIdMismatch", Guardian.IdNumber?.Value ?? string.Empty);
        }

        // The guardian must retrieve the document as the primary applicant to prove legal guardianship.
        if (!IsTurkishMatch(guardianPerson.Kinship, "KENDİSİ"))
        {
            throw new DocumentValidationException("Error_GuardianMustAppearAsSelf");
        }

        Guardian.Name = $"{guardianPerson.FirstName} {guardianPerson.LastName}";
        Guardian.BirthDate = DateOnly.Parse(guardianPerson.BirthDate);

        var fatherPerson = data.People.FirstOrDefault(p =>
            IsTurkishMatch(p.Kinship, "EŞİ") ||
            IsTurkishMatch(p.Kinship, "BABASI"));

        if (fatherPerson != null)
        {
            Father.Name = $"{fatherPerson.FirstName} {fatherPerson.LastName}";
            if (fatherPerson.HasDeathDate && !string.IsNullOrWhiteSpace(fatherPerson.DeathDate))
            {
                Father.DeathDate = DateOnly.Parse(fatherPerson.DeathDate);
            }
            else
            {
                throw new BusinessRuleException("Error_FatherDeathDateMissingFromDocument");
            }
        }

        var children = data.People.Where(p =>
            IsTurkishMatch(p.Kinship, "OĞLU") ||
            IsTurkishMatch(p.Kinship, "KIZI"));

        foreach (var child in children)
        {
            var birthDate = DateOnly.Parse(child.BirthDate);
            // Support programs only accept children under thirteen years of age.
            if (birthDate.Year < DateTime.Now.Year - 13) continue;

            Children.Add(new ChildDraft
            {
                Name = $"{child.FirstName} {child.LastName}",
                IdNumber = new IdNumber(child.IdNumber),
                BirthDate = birthDate
            });
        }

        if (Children.Count == 0)
        {
            throw new BusinessRuleException("Error_NoEligibleChildFound");
        }
    }

    public void ApplyResidenceDocumentResult(ResidenceData data)
    {
        // Verifies that the residence certificate belongs to the registered guardian.
        if (Guardian.IdNumber == null || data.IdNumber != Guardian.IdNumber.Value)
        {
            throw new DocumentValidationException("Error_ResidenceIdMismatch", data.IdNumber);
        }

        ResidenceAddress = new AddressDraft
        {
            Country = "Türkiye",
            City = data.City,
            District = data.District,
            Neighborhood = data.Neighborhood,
            FullAddress = data.FullAddress
        };
    }

    private void EnsureDisabledMemberExists()
    {
        DisabledMember ??= new DisabledMemberDraft();
    }

    public void SetStep(RegistrationStep step)
    {
        CurrentStep = step;
    }

    public void RevertProcessingState()
    {
        if (CurrentStep == RegistrationStep.ProcessingPopulationRegistry)
            CurrentStep = RegistrationStep.WaitingForPopulationRegistry;

        else if (CurrentStep == RegistrationStep.ProcessingResidence)
            CurrentStep = RegistrationStep.WaitingForResidence;

        else if (CurrentStep == RegistrationStep.ProcessingIdentityFront)
            CurrentStep = RegistrationStep.WaitingForIdentityFront;

        else if (CurrentStep == RegistrationStep.ProcessingIdentityBack || CurrentStep == RegistrationStep.MergingDocuments)
            CurrentStep = RegistrationStep.WaitingForIdentityBack;
    }

    public void SetDocumentTarget(string? targetName)
    {
        CurrentDocumentTargetName = targetName;
    }

    public string? GetNextPersonNeedingIdentity()
    {
        if (string.IsNullOrEmpty(Guardian.IdentityDocumentPath))
            return "Vasi";

        var pendingChild = Children.FirstOrDefault(c => string.IsNullOrEmpty(c.IdentityDocumentPath));
        if (pendingChild != null && pendingChild.Name != null)
            return pendingChild.Name;

        return null;
    }

    public string? GetExpectedIdNumberForCurrentTarget()
    {
        if (CurrentDocumentTargetName == "Vasi")
            return Guardian.IdNumber?.Value;

        var child = Children.FirstOrDefault(c => c.Name == CurrentDocumentTargetName);
        return child?.IdNumber?.Value;
    }

    private static bool IsTurkishMatch(string? text1, string? text2)
    {
        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
            return false;

        var trCulture = new CultureInfo("tr-TR");
        return string.Compare(text1.Trim(), text2.Trim(), trCulture, CompareOptions.IgnoreCase) == 0;
    }

    public void GenerateAutomatedDetails(IStringLocalizer<BotMessages> localizer)
    {
        // Generates standard petition content and assigns default hobbies to satisfy registration requirements.
        string deathDateStr = Father.DeathDate?.ToString("dd.MM.yyyy") ?? "belirtilen tarihte";
        int childCount = Children.Count;

        string petitionText = localizer["Document_PetitionContent", Guardian.Name, deathDateStr, childCount].Value;
        
        SystemDocuments.Petition = new Petition(petitionText);

        var hobbiesPool = new[]
        {
            localizer["Generated_ChildHobby_ReadingAndDrawing"].Value,
            localizer["Generated_ChildHobby_FootballAndCycling"].Value,
            localizer["Generated_ChildHobby_ChessAndMusic"].Value,
            localizer["Generated_ChildHobby_LegoAndScience"].Value,
            localizer["Generated_ChildHobby_SwimmingAndHiking"].Value
        };

        var dreamsPool = new[]
        {
            localizer["Generated_ChildDream_Teacher"].Value,
            localizer["Generated_ChildDream_Doctor"].Value,
            localizer["Generated_ChildDream_ComputerEngineer"].Value,
            localizer["Generated_ChildDream_Pilot"].Value,
            localizer["Generated_ChildDream_Architect"].Value,
            localizer["Generated_ChildDream_PoliceOfficer"].Value
        };

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Hobbies ??= hobbiesPool[i % hobbiesPool.Length];
            child.FutureDream ??= dreamsPool[i % dreamsPool.Length];
        }
    }

    public (bool Success, string MessageKey, string QuestionLabelKey) ResetQuestionAnswer(string key)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();

        return normalizedKey switch
        {
            "applicantrole" =>
                ResetIfAnswered(() => Guardian.ApplicantRole != null, () => Guardian.ApplicantRole = null, "Label_Update_ApplicantRole"),

            "gender" =>
                ResetIfAnswered(() => Guardian.Gender != null, () => Guardian.Gender = null, "Label_Update_Gender"),

            "maritalstatus" =>
                ResetIfAnswered(() => Guardian.MaritalStatus != null, () => Guardian.MaritalStatus = null, "Label_Update_MaritalStatus"),

            "educationlevel" =>
                ResetIfAnswered(() => Guardian.EducationLevel != null, () => Guardian.EducationLevel = null, "Label_Update_EducationLevel"),

            "degreeofkinship" =>
                ResetIfAnswered(() => Guardian.DegreeOfKinship != null, () => Guardian.DegreeOfKinship = null, "Label_Update_DegreeOfKinship"),

            "profession" =>
                ResetIfAnswered(() => !string.IsNullOrEmpty(Guardian.Profession), () => Guardian.Profession = null, "Label_Update_Profession"),

            "phonenumber" =>
                ResetIfAnswered(() => Guardian.PhoneNumber != null, () => Guardian.PhoneNumber = null, "Label_Update_PhoneNumber"),

            "familymembercount" =>
                ResetIfAnswered(() => FamilyFinancial.FamilyMemberCount != null, () => FamilyFinancial.FamilyMemberCount = null, "Label_Update_FamilyMemberCount"),

            "fatherprofession" =>
                ResetIfAnswered(() => Father.Profession != null, () => Father.Profession = null, "Label_Update_FatherProfession"),

            "fatherdeathreason" =>
                ResetIfAnswered(() => Father.DeathReason != null, () => Father.DeathReason = null, "Label_Update_FatherDeathReason"),

            "disabledname" =>
                ResetIfAnswered(() => DisabledMember != null && DisabledMember.Name != null, () => DisabledMember = null, "Label_Update_DisabledMember"),

            "housingstatus" =>
                ResetIfAnswered(() => FamilyFinancial.HousingStatus != null, () => FamilyFinancial.HousingStatus = null, "Label_Update_HousingStatus"),

            "socialsecurity" =>
                ResetIfAnswered(() => FamilyFinancial.SocialSecurity != null, () => FamilyFinancial.SocialSecurity = null, "Label_Update_SocialSecurity"),

            "incomestatus" =>
                ResetIfAnswered(() => FamilyFinancial.IncomeStatus != null, () => FamilyFinancial.IncomeStatus = null, "Label_Update_IncomeStatus"),

            "income" =>
                ResetIfAnswered(() => FamilyFinancial.Income != null, () => FamilyFinancial.Income = null, "Label_Update_Income"),

            "expense" =>
                ResetIfAnswered(() => FamilyFinancial.Expense != null, () => FamilyFinancial.Expense = null, "Label_Update_Expense"),

            "iban" =>
                ResetIfAnswered(() => Guardian.Iban != null, () => Guardian.Iban = null, "Label_Update_Iban"),

            _ => (false, "Error_InvalidQuestionKey", string.Empty)
        };
    }

    private static (bool Success, string MessageKey, string QuestionLabelKey) ResetIfAnswered(Func<bool> isAnswered, Action resetAction, string questionLabelKey)
    {
        if (!isAnswered())
        {
            return (false, "Error_QuestionNotAnswered", questionLabelKey);
        }

        resetAction();
        return (true, "Success_QuestionAnswerReset", questionLabelKey);
    }

    public string BuildSummaryHtml(IStringLocalizer<BotMessages> localizer)
    {
        var sb = new StringBuilder();
        sb.AppendLine(localizer["Summary_Title"].Value);
        sb.AppendLine(localizer["Summary_RegistrationKey", RegistrationKey].Value);

        sb.AppendLine(localizer["Summary_GuardianSection"].Value);
        sb.AppendLine(localizer["Summary_GuardianName", Guardian.Name].Value);
        sb.AppendLine(localizer["Summary_GuardianId", Guardian.IdNumber?.Value].Value);
        sb.AppendLine(localizer["Summary_GuardianBirthDate", Guardian.BirthDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)].Value);
        sb.AppendLine(localizer["Summary_GuardianPhone", Guardian.PhoneNumber?.Value].Value);
        sb.AppendLine(localizer["Summary_GuardianIban", Guardian.Iban?.Value].Value);

        sb.AppendLine(localizer["Summary_FatherSection"].Value);
        sb.AppendLine(localizer["Summary_FatherName", Father.Name].Value);
        sb.AppendLine(localizer["Summary_FatherDeathDate", Father.DeathDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)].Value);
        sb.AppendLine(localizer["Summary_FatherDeathReason", GetLocalizedEnumDescription(Father.DeathReason, localizer)].Value);

        sb.AppendLine(localizer["Summary_ResidenceSection"].Value);
        sb.AppendLine(localizer["Summary_Address", ResidenceAddress.FullAddress].Value);
        sb.AppendLine(localizer["Summary_HousingStatus", GetLocalizedEnumDescription(FamilyFinancial.HousingStatus, localizer)].Value);

        sb.AppendLine(localizer["Summary_FinancialSection"].Value);
        sb.AppendLine(localizer["Summary_HouseholdSize", FamilyFinancial.FamilyMemberCount].Value);
        sb.AppendLine(localizer["Summary_SocialSecurity", GetLocalizedEnumDescription(FamilyFinancial.SocialSecurity, localizer)].Value);
        sb.AppendLine(localizer["Summary_MonthlyIncome", FamilyFinancial.Income, GetLocalizedEnumDescription(FamilyFinancial.IncomeStatus, localizer)].Value);
        sb.AppendLine(localizer["Summary_MonthlyExpense", FamilyFinancial.Expense].Value);

        sb.AppendLine(localizer["Summary_ChildrenSection", Children.Count].Value);
        foreach (var child in Children)
        {
            sb.AppendLine(localizer["Summary_Child", child.Name, child.IdNumber?.Value, child.BirthDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)].Value);
        }

        if (DisabledMember != null && DisabledMember.Name != "Yok" && !string.IsNullOrWhiteSpace(DisabledMember.Name))
        {
            sb.AppendLine(localizer["Summary_DisabledMemberSection"].Value);
            sb.AppendLine(localizer["Summary_DisabledMemberName", DisabledMember.Name].Value);
            sb.AppendLine(localizer["Summary_DisabledMemberDiagnosis", DisabledMember.Diagnosis, DisabledMember.DiseaseStatus].Value);
            sb.AppendLine(localizer["Summary_DisabledMemberTreatment", GetLocalizedEnumDescription(DisabledMember.TreatmentStatus, localizer)].Value);
        }

        sb.AppendLine(localizer["Summary_ConfirmationInstructions"].Value);

        return sb.ToString();
    }

    private static string GetLocalizedEnumDescription(Enum? value, IStringLocalizer<BotMessages> localizer)
    {
        if (value == null) return "-";
        return localizer[$"Enum_{value.GetType().Name}_{value}"].Value;
    }

    public (RegistrationMetaDto, RegistrationDto) ExportRegistrationData()
    {
        var meta = new RegistrationMetaDto(
            RegistrationKey,
            ChatId,
            FormatToTexasTime(CreatedAt),
            FormatToTexasTime(UpdatedAt)
        );

        var guardian = new GuardianDto(
            Name: Guardian.Name,
            IdNumber: Guardian.IdNumber?.Value,
            BirthDate: Guardian.BirthDate.ToString("dd.MM.yyyy"),
            ApplicantRole: GetEnumDescription(Guardian.ApplicantRole),
            Gender: GetEnumDescription(Guardian.Gender),
            MaritalStatus: GetEnumDescription(Guardian.MaritalStatus),
            EducationLevel: GetEnumDescription(Guardian.EducationLevel),
            EducationStatus: GetEnumDescription(Guardian.EducationStatus),
            DegreeOfKinship: GetEnumDescription(Guardian.DegreeOfKinship),
            Profession: Guardian.Profession,
            PhoneNumber: Guardian.PhoneNumber?.Value,
            Iban: Guardian.Iban?.Value
        );

        var children = Children.Select(c => new ChildDto(
            Name: c.Name,
            IdNumber: c.IdNumber?.Value,
            BirthDate: c.BirthDate.ToString("dd.MM.yyyy"),
            Hobbies: c.Hobbies,
            FutureDream: c.FutureDream
        )).ToList();

        var father = new FatherDto(
            Name: Father.Name,
            DeathDate: Father.DeathDate?.ToString("dd.MM.yyyy"),
            Profession: GetEnumDescription(Father.Profession),
            DeathReason: GetEnumDescription(Father.DeathReason)
        );

        var financial = new FamilyFinancialDto(
            FamilyMemberCount: FamilyFinancial.FamilyMemberCount,
            HousingStatus: GetEnumDescription(FamilyFinancial.HousingStatus),
            SocialSecurity: GetEnumDescription(FamilyFinancial.SocialSecurity),
            IncomeStatus: GetEnumDescription(FamilyFinancial.IncomeStatus),
            Income: FamilyFinancial.Income,
            Expense: FamilyFinancial.Expense,
            OtherIncome: FamilyFinancial.OtherIncome
        );

        DisabledMemberDto? disabled = DisabledMember == null ? null : new DisabledMemberDto(
            Name: DisabledMember.Name,
            Diagnosis: DisabledMember.Diagnosis,
            DiseaseStatus: DisabledMember.DiseaseStatus,
            TreatmentStatus: GetEnumDescription(DisabledMember.TreatmentStatus),
            Description: DisabledMember.Description
        );

        var data = new RegistrationDto(
            Guardian: guardian,
            Children: children,
            Father: father,
            FamilyFinancial: financial,
            DisabledMember: disabled,
            ResidenceAddress: ResidenceAddress,
            PetitionText: SystemDocuments.Petition?.Content
        );

        return (meta, data);
    }

    private static string? GetEnumDescription(Enum? value)
    {
        if (value == null) return null;

        var fieldInfo = value.GetType().GetField(value.ToString());
        var attribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();

        return attribute != null ? attribute.Description : value.ToString();
    }

    private static string FormatToTexasTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        TimeZoneInfo targetTimeZone;
        try
        {
            targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            // Handles time zone conversion differences between Linux containers and Windows hosts.
            targetTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.ToUniversalTime(), targetTimeZone);
        return localTime.ToString("dd.MM.yyyy HH:mm:ss");
    }
}

/// <summary>
/// Represents temporary guardian details collected during registration.
/// </summary>
public class GuardianDraft
{
    public string? Name { get; set; }
    public IdNumber? IdNumber { get; set; }
    public DateOnly BirthDate { get; set; }
    public ApplicantRole? ApplicantRole { get; set; }
    public Gender? Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }
    public EducationLevel? EducationLevel { get; set; }
    public EducationStatus? EducationStatus { get; set; }
    public DegreeOfKinship? DegreeOfKinship { get; set; }
    public string? Profession { get; set; }
    public PhoneNumber? PhoneNumber { get; set; }
    public Iban? Iban { get; set; }
    public string? IdentityDocumentPath { get; set; }
}

/// <summary>
/// Represents temporary child details collected during registration.
/// </summary>
public class ChildDraft
{
    public string Name { get; set; } = string.Empty;
    public IdNumber? IdNumber { get; set; }
    public DateOnly BirthDate { get; set; }
    public string? Hobbies { get; set; }
    public string? FutureDream { get; set; }
    public string? IdentityDocumentPath { get; set; }
}

/// <summary>
/// Represents temporary deceased father details collected during registration.
/// </summary>
public class FatherDraft
{
    public string? Name { get; set; }
    public DateOnly? DeathDate { get; set; }
    public FatherProfession? Profession { get; set; }
    public FatherDeathReason? DeathReason { get; set; }
}

/// <summary>
/// Represents temporary family and financial information for eligibility assessment.
/// </summary>
public class FamilyFinancialDraft
{
    public int? FamilyMemberCount { get; set; }
    public HousingStatus? HousingStatus { get; set; }
    public SocialSecurity? SocialSecurity { get; set; }
    public IncomeStatus? IncomeStatus { get; set; }
    public int? Income { get; set; }
    public int? Expense { get; set; }
    public int OtherIncome { get; set; } = 0;
}

/// <summary>
/// Represents temporary information about a disabled or ill family member.
/// </summary>
public class DisabledMemberDraft
{
    public string? Name { get; set; }
    public string? Diagnosis { get; set; }
    public string? DiseaseStatus { get; set; }
    public TreatmentStatus? TreatmentStatus { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Represents temporary address information collected during registration.
/// </summary>
public class AddressDraft
{
    public string Country { get; set; } = "Türkiye";
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Neighborhood { get; set; }
    public string? FullAddress { get; set; }
}

/// <summary>
/// Holds file paths and text for uploaded or generated system documents.
/// </summary>
public class SystemDocumentsDraft
{
    public string? PopulationRegistryDocumentPath { get; set; }
    public string? ResidenceDocumentPath { get; set; }
    public string? IdentityFrontPath { get; set; }
    public string? IdentityBackPath { get; set; }
    public string? MergedIdentityPath { get; set; }
    public Petition? Petition { get; set; }
}
