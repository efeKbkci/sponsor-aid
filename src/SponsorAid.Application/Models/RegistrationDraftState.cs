using Microsoft.Win32;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Integration.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SponsorAid.Application.Models;

public class RegistrationDraftState
{
    public string RegistrationKey { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Aynı chatid altında birden fazla kayıt varsa, sadece 1 tanesi aktif olabilir.
    public bool IsActive { get; set; } = true;

    // Kullanıcı "Bilgi Güncelle" butonuna bastığında 1 olur
    // Flag 1 ise, kullanıcıdan gelen metin tabanlı girdiler NLI modeline iletilir. 
    public bool IsWaitingForUpdateFieldSelection { get; set; } = false;

    // Kullanıcı "Kayıt Tamamlama" butonuna bastığında 1 olur
    // Flag 1 ise, kullanıcıdan gelen eski kaydın anahtarları ContinueReg özelliğine iletilir.
    public bool IsWaitingForRegistrationKey { get; set; } = false;

    // --- FOLDERS (Composition) ---
    public GuardianDraft Guardian { get; set; } = new();
    public List<ChildDraft> Children { get; set; } = new();
    public FatherDraft Father { get; set; } = new();
    public FamilyFinancialDraft FamilyFinancial { get; set; } = new();
    public DisabledMemberDraft? DisabledMember { get; set; }
    public SystemDocumentsDraft SystemDocuments { get; set; } = new();
    public AddressDraft ResidenceAddress { get; set; } = new();

    // --- REGISTRATION STEP TRACKING ---
    public RegistrationStep CurrentStep { get; set; } = RegistrationStep.WaitingForGuardianId;
    // Şu an kimin kimlik belgesini istiyoruz? (Varsayılan olarak Vasi'den başlıyoruz)
    public string? CurrentDocumentTargetName { get; set; } = "Vasi";

    private readonly Dictionary<string, Action<string>> _updateStrategies;

    public RegistrationDraftState()
    {
        _updateStrategies = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // --- GUARDIAN UPDATES ---
            { "GuardianIdNumber", value => Guardian.IdNumber = new IdNumber(value) }, // ValueObject otomatik doğrulayacak
            { "GuardianApplicantRole", value => Guardian.ApplicantRole = Enum.Parse<ApplicantRole>(value, true) },
            { "GuardianGender", value => Guardian.Gender = Enum.Parse<Gender>(value, true) },
            { "GuardianMaritalStatus", value => Guardian.MaritalStatus = Enum.Parse<MaritalStatus>(value, true) },
            { "GuardianEducationLevel", value => Guardian.EducationLevel = Enum.Parse<EducationLevel>(value, true) },
            { "GuardianEducationStatus", value => Guardian.EducationStatus = Enum.Parse<EducationStatus>(value, true) },
            { "GuardianDegreeOfKinship", value => Guardian.DegreeOfKinship = Enum.Parse<DegreeOfKinship>(value, true) },
            { "GuardianProfession", value => Guardian.Profession = value.Trim() },
            { "GuardianPhoneNumber", value => Guardian.PhoneNumber = new PhoneNumber(value) }, // ValueObject validation
            { "GuardianIban", value => Guardian.Iban = new Iban(value) }, // ValueObject validation

            // --- FATHER UPDATES ---
            { "FatherProfession", value => Father.Profession = Enum.Parse<FatherProfession>(value, true) },
            { "FatherDeathReason", value => Father.DeathReason = Enum.Parse<FatherDeathReason>(value, true) },

            // --- FAMILY & FINANCIAL UPDATES ---
            { "FamilyMemberCount", value => FamilyFinancial.FamilyMemberCount = int.Parse(value) },
            { "HousingStatus", value => FamilyFinancial.HousingStatus = Enum.Parse<HousingStatus>(value, true) },
            { "SocialSecurity", value => FamilyFinancial.SocialSecurity = Enum.Parse<SocialSecurity>(value, true) },
            { "IncomeStatus", value => FamilyFinancial.IncomeStatus = Enum.Parse<IncomeStatus>(value, true) },
            { "Income", value => FamilyFinancial.Income = int.Parse(value) },
            { "Expense", value => FamilyFinancial.Expense = int.Parse(value) },

            // --- DISABLED MEMBER UPDATES ---
            { "DisabledName", value => { EnsureDisabledMemberExists(); DisabledMember!.Name = value; } },
            { "DisabledDiagnosis", value => { EnsureDisabledMemberExists(); DisabledMember!.Diagnosis = value; } },
            { "DisabledDiseaseStatus", value => { EnsureDisabledMemberExists(); DisabledMember!.DiseaseStatus = value; } },
            { "DisabledTreatmentStatus", value => { EnsureDisabledMemberExists(); DisabledMember!.TreatmentStatus = Enum.Parse<TreatmentStatus>(value, true); } },
            { "DisabledDescription", value => { EnsureDisabledMemberExists(); DisabledMember!.Description = value; } },

            // --- AUTOMATION UPDATES ---
            { "Petition", value => SystemDocuments.Petition = new Petition(value) } // ValueObject validation (min 300 chars)
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
    // Nüfus belgesi verilerini işleyip, Vasi, Baba ve Çocukları eşleştirir ve draft state'i günceller.
    public void ApplyPopulationRegistryResult(PopulationRegistryData data)
    {
        // 1. GUARDIAN (VASİ) TESPİTİ
        // 🚀 ARTIK KUSURSUZ EŞLEŞTİRME: Elimizdeki kesin TC No ile tablodaki TC'yi arıyoruz
        var guardianPerson = data.People.FirstOrDefault(p => p.IdNumber == Guardian.IdNumber?.Value);

        if (guardianPerson == null)
        {
            throw new DocumentValidationException("Error_GuardianIdMismatch", Guardian.IdNumber?.Value ?? string.Empty);
        }

        // Kural: Nüfus belgesini Vasi KENDİSİ almış olmalıdır.
        if (!IsTurkishMatch(guardianPerson.Kinship, "KENDİSİ"))
        {
            throw new DocumentValidationException("Error_GuardianMustAppearAsSelf");
        }

        // 🚀 İSİM ARTIK %100 RESMİ OLARAK BELGEDEN ALINIYOR (Kullanıcının harf hatası yapma ihtimali bitti)
        Guardian.Name = $"{guardianPerson.FirstName} {guardianPerson.LastName}";
        Guardian.BirthDate = DateOnly.Parse(guardianPerson.BirthDate);

        // --- BABA TESPİTİ ---
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

        // --- ÇOCUK TESPİTİ ---
        var children = data.People.Where(p =>
            IsTurkishMatch(p.Kinship, "OĞLU") ||
            IsTurkishMatch(p.Kinship, "KIZI"));

        foreach (var child in children)
        {
            var birthDate = DateOnly.Parse(child.BirthDate);
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
        // 🚀 KURAL 4: İkametgahtan gelen Kimlik Numarası, Vasiye ait olmalıdır.
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

    // Durumu değiştirmek için dışarıya açık kontrollü bir metot
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

    // Kimlik bilgisi istenen kişinin ismini döner.
    public string? GetNextPersonNeedingIdentity()
    {
        // 1. Vasi kimliği eksikse, hedef Vasi'dir.
        if (string.IsNullOrEmpty(Guardian.IdentityDocumentPath))
            return "Vasi";

        // 2. Çocuklardan kimliği eksik olan ilk çocuğu bul. 
        // (Çocuklar Nüfus belgesi işlendiğinde eklendiği için bu liste dolu olacaktır)
        var pendingChild = Children.FirstOrDefault(c => string.IsNullOrEmpty(c.IdentityDocumentPath));
        if (pendingChild != null && pendingChild.Name != null)
            return pendingChild.Name;

        // Herkesin kimliği tamamsa null döner (Döngü biter)
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
    /// <summary>
    /// Vasi dilekçesini ve çocukların hobi/hayal bilgilerini otomatik oluşturup atar.
    // </summary>
    public void GenerateAutomatedDetails(IStringLocalizer<BotMessages> localizer)
    {
        // 1. DİLEKÇE ŞABLONU (300+ Karakter Kuralını Karşılar)
        string deathDateStr = Father.DeathDate?.ToString("dd.MM.yyyy") ?? "belirtilen tarihte";
        int childCount = Children.Count;

        string petitionText = localizer["Document_PetitionContent", Guardian.Name, deathDateStr, childCount].Value;
        
        SystemDocuments.Petition = new Petition(petitionText);

        // 2. ÇOCUKLARIN HOBİ VE GELECEK HAYALLERİ LİSTESİ
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
            // Eğer daha önce atanmadıysa havuzdan sırayla ata
            child.Hobbies ??= hobbiesPool[i % hobbiesPool.Length];
            child.FutureDream ??= dreamsPool[i % dreamsPool.Length];
        }
    }

    // Belirtilen soru anahtarının daha önce cevaplanıp cevaplanmadığını denetler ve cevabı sıfırlar.
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
}

// ====================================================================
// NESTED DRAFT CLASSES
// ====================================================================

public class GuardianDraft
{
    public string? Name { get; set; }
    public IdNumber? IdNumber { get; set; } // ValueObject
    public DateOnly BirthDate { get; set; }
    public ApplicantRole? ApplicantRole { get; set; } // Enum
    public Gender? Gender { get; set; } // Enum
    public MaritalStatus? MaritalStatus { get; set; } // Enum
    public EducationLevel? EducationLevel { get; set; } // Enum
    public EducationStatus? EducationStatus { get; set; } // Enum
    public DegreeOfKinship? DegreeOfKinship { get; set; } // Enum
    public string? Profession { get; set; }
    public PhoneNumber? PhoneNumber { get; set; } // ValueObject
    public Iban? Iban { get; set; } // ValueObject
    public string? IdentityDocumentPath { get; set; }
}

public class ChildDraft
{
    public string Name { get; set; } = string.Empty;
    public IdNumber? IdNumber { get; set; } // ValueObject
    public DateOnly BirthDate { get; set; }
    public string? Hobbies { get; set; }
    public string? FutureDream { get; set; }
    public string? IdentityDocumentPath { get; set; }
}

public class FatherDraft
{
    public string? Name { get; set; }
    public DateOnly? DeathDate { get; set; }
    public FatherProfession? Profession { get; set; } // Enum
    public FatherDeathReason? DeathReason { get; set; } // Enum
}

public class FamilyFinancialDraft
{
    public int? FamilyMemberCount { get; set; }
    public HousingStatus? HousingStatus { get; set; } // Enum
    public SocialSecurity? SocialSecurity { get; set; } // Enum
    public IncomeStatus? IncomeStatus { get; set; } // Enum
    public int? Income { get; set; }
    public int? Expense { get; set; }
    public int OtherIncome { get; set; } = 0;
}

public class DisabledMemberDraft
{
    public string? Name { get; set; }
    public string? Diagnosis { get; set; }
    public string? DiseaseStatus { get; set; }
    public TreatmentStatus? TreatmentStatus { get; set; } // Enum
    public string? Description { get; set; }
}

public class AddressDraft
{
    public string Country { get; set; } = "Türkiye";
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Neighborhood { get; set; }
    public string? FullAddress { get; set; }
}

public class SystemDocumentsDraft
{
    public string? PopulationRegistryDocumentPath { get; set; }
    public string? ResidenceDocumentPath { get; set; }
    public string? IdentityFrontPath { get; set; }
    public string? IdentityBackPath { get; set; }
    public string? MergedIdentityPath { get; set; }
    public Petition? Petition { get; set; } // ValueObject
}