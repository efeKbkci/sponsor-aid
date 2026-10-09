using Microsoft.Win32;
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

public class RegistrationDraftState
{
    public string RegistrationKey { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;  
    public bool IsActive { get; set; } = true; // 🚀 YENİ

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
            throw new DocumentValidationException($"Girdiğiniz T.C. Kimlik Numarası ({Guardian.IdNumber?.Value}) ile Nüfus belgesindeki kayıtlar eşleşmiyor. Lütfen kendinize ait belgeyi yüklediğinize emin olun.");
        }

        // Kural: Nüfus belgesini Vasi KENDİSİ almış olmalıdır.
        if (!IsTurkishMatch(guardianPerson.Kinship, "KENDİSİ"))
        {
            throw new DocumentValidationException("Nüfus belgesinde sizin (Vasi'nin) 'KENDİSİ' olarak görünmeniz gerekmektedir. Belgeyi e-Devlet'ten kendi hesabınızla aldığınıza emin olun.");
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
                throw new BusinessRuleException("Baba vefat tarihinin nüfus belgesinde yazdığından emin olun.");
            }
        }

        // --- ÇOCUK TESPİTİ ---
        var children = data.People.Where(p =>
            IsTurkishMatch(p.Kinship, "OĞLU") ||
            IsTurkishMatch(p.Kinship, "KIZI"));

        foreach (var child in children)
        {
            var birthDate = DateOnly.Parse(child.BirthDate);
            if (birthDate.Year <= DateTime.Now.Year - 15) continue;

            Children.Add(new ChildDraft
            {
                Name = $"{child.FirstName} {child.LastName}",
                IdNumber = new IdNumber(child.IdNumber),
                BirthDate = birthDate
            });
        }

        if (Children.Count == 0)
        {
            throw new BusinessRuleException("Nüfus belgenizde 15 yaşından küçük yetim/ihtiyaç sahibi bir çocuk bulunamadı. Sistem sadece bu yaş sınırındaki çocukları işleme almaktadır.");
        }
    }

    public void ApplyResidenceDocumentResult(ResidenceData data)
    {
        // 🚀 KURAL 4: İkametgahtan gelen Kimlik Numarası, Vasiye ait olmalıdır.
        if (Guardian.IdNumber == null || data.IdNumber != Guardian.IdNumber.Value)
        {
            throw new DocumentValidationException($"İkametgah belgesindeki TC Kimlik Numarası ({data.IdNumber}) ile Vasi'nin TC Kimlik Numarası eşleşmiyor. Lütfen size ait olan ikametgah belgesini yükleyin.");
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
    public void GenerateAutomatedDetails()
    {
        // 1. DİLEKÇE ŞABLONU (300+ Karakter Kuralını Karşılar)
        string deathDateStr = Father.DeathDate?.ToString("dd.MM.yyyy") ?? "belirtilen tarihte";
        int childCount = Children.Count;

        string petitionText =
            $"İlgili Makama,\n\n" +
            $"Ben {Guardian.Name}, {deathDateStr} tarihinde vefat eden eşimin vefatı neticesinde yetim kalan {childCount} çocuğum ile birlikte ailemizin geçimini ve idamesini sağlamaya çalışmaktayım. " +
            $"Hanemizin gelir düzeyinin yetersiz olması sebebiyle çocuklarımın temel gıda, barınma, sağlık ve özellikle eğitim ihtiyaçlarını karşılamakta ciddi maddi zorluklar yaşamaktayım. " +
            $"Yetim çocuklarımızın eğitim hayatlarını aksatmadan sürdürebilmeleri ve geleceğe güvenle hazırlanabilmeleri adına vakfınızın / derneğinizin yetim burs ve aile destek programından istifade etmek istiyorum. " +
            $"Yetim sponsorluk ve burs talebimin değerlendirilerek kabul edilmesini saygılarımla arz ederim.";
        
        SystemDocuments.Petition = new Petition(petitionText);

        // 2. ÇOCUKLARIN HOBİ VE GELECEK HAYALLERİ LİSTESİ
        var hobbiesPool = new[]
        {
            "Kitap okumak ve resim çizmek",
            "Futbol oynamak ve bisiklete binmek",
            "Satranç oynamak ve müzik dinlemek",
            "Lego yapmak ve bilim belgeselleri izlemek",
            "Yüzmek ve doğa yürüyüşü yapmak"
        };

        var dreamsPool = new[]
        {
            "Öğretmen olmak",
            "Doktor olmak",
            "Bilgisayar Mühendisi olmak",
            "Pilot olmak",
            "Mimar olmak",
            "Polis olmak"
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
    public (bool Success, string Message) ResetQuestionAnswer(string key)
    {
        var normalizedKey = key.Trim().ToLowerInvariant();

        return normalizedKey switch
        {
            "applicantrole" =>
                ResetIfAnswered(() => Guardian.ApplicantRole != null, () => Guardian.ApplicantRole = null, "Başvuran Rolü"),

            "gender" =>
                ResetIfAnswered(() => Guardian.Gender != null, () => Guardian.Gender = null, "Cinsiyet"),

            "maritalstatus" =>
                ResetIfAnswered(() => Guardian.MaritalStatus != null, () => Guardian.MaritalStatus = null, "Medeni Durum"),

            "educationlevel" =>
                ResetIfAnswered(() => Guardian.EducationLevel != null, () => Guardian.EducationLevel = null, "Eğitim Düzeyi"),

            "degreeofkinship" =>
                ResetIfAnswered(() => Guardian.DegreeOfKinship != null, () => Guardian.DegreeOfKinship = null, "Yetime Yakınlık Derecesi"),

            "profession" =>
                ResetIfAnswered(() => !string.IsNullOrEmpty(Guardian.Profession), () => Guardian.Profession = null, "Vasi Mesleği"),

            "phonenumber" =>
                ResetIfAnswered(() => Guardian.PhoneNumber != null, () => Guardian.PhoneNumber = null, "Telefon Numarası"),

            "familymembercount" =>
                ResetIfAnswered(() => FamilyFinancial.FamilyMemberCount != null, () => FamilyFinancial.FamilyMemberCount = null, "Aile Birey Sayısı"),

            "fatherprofession" =>
                ResetIfAnswered(() => Father.Profession != null, () => Father.Profession = null, "Baba Mesleği"),

            "fatherdeathreason" =>
                ResetIfAnswered(() => Father.DeathReason != null, () => Father.DeathReason = null, "Baba Ölüm Nedeni"),

            "disabledname" =>
                ResetIfAnswered(() => DisabledMember != null && DisabledMember.Name != null, () => DisabledMember = null, "Engelli / Hasta Birey Durumu"),

            "housingstatus" =>
                ResetIfAnswered(() => FamilyFinancial.HousingStatus != null, () => FamilyFinancial.HousingStatus = null, "Konut Durumu"),

            "socialsecurity" =>
                ResetIfAnswered(() => FamilyFinancial.SocialSecurity != null, () => FamilyFinancial.SocialSecurity = null, "Sosyal Güvence"),

            "incomestatus" =>
                ResetIfAnswered(() => FamilyFinancial.IncomeStatus != null, () => FamilyFinancial.IncomeStatus = null, "Gelir Tipi"),

            "income" =>
                ResetIfAnswered(() => FamilyFinancial.Income != null, () => FamilyFinancial.Income = null, "Aylık Gelir"),

            "expense" =>
                ResetIfAnswered(() => FamilyFinancial.Expense != null, () => FamilyFinancial.Expense = null, "Aylık Gider"),

            "iban" =>
                ResetIfAnswered(() => Guardian.Iban != null, () => Guardian.Iban = null, "IBAN Numarası"),

            _ => (false, "Geçersiz soru anahtarı. Güncellenebilir alanlar: applicantrole, gender, maritalstatus, educationlevel, degreeofkinship, profession, phonenumber, familymembercount, fatherprofession, fatherdeathreason, disabledname, housingstatus, socialsecurity, incomestatus, income, expense, iban")
        };
    }

    private static (bool Success, string Message) ResetIfAnswered(Func<bool> isAnswered, Action resetAction, string questionLabel)
    {
        if (!isAnswered())
        {
            return (false, $"⚠️ <b>{questionLabel}</b> sorusu henüz cevaplanmamış. Sadece daha önce cevap verdiğiniz soruları güncelleyebilirsiniz.");
        }

        resetAction();
        return (true, $"🔄 <b>{questionLabel}</b> bilgisi sıfırlandı. Lütfen yeni cevabı giriniz:");
    }

    public string BuildSummaryHtml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 <b>BAŞVURU BİLGİLERİ ÖZETİ</b>\n");
        sb.AppendLine($"🔑 <b>Takip Kodu:</b> <code>{RegistrationKey}</code>\n");

        sb.AppendLine("👤 <b>Vasi Bilgileri:</b>");
        sb.AppendLine($"• <b>Ad Soyad:</b> {Guardian.Name}");
        sb.AppendLine($"• <b>T.C. Kimlik No:</b> {Guardian.IdNumber?.Value}");
        sb.AppendLine($"• <b>Doğum Tarihi:</b> {Guardian.BirthDate:dd.MM.yyyy}");
        sb.AppendLine($"• <b>Telefon:</b> {Guardian.PhoneNumber?.Value}");
        sb.AppendLine($"• <b>IBAN:</b> {Guardian.Iban?.Value}\n");

        sb.AppendLine("🕊️ <b>Vefat Eden Baba:</b>");
        sb.AppendLine($"• <b>Ad Soyad:</b> {Father.Name}");
        sb.AppendLine($"• <b>Ölüm Tarihi:</b> {Father.DeathDate?.ToString("dd.MM.yyyy")}");
        sb.AppendLine($"• <b>Ölüm Nedeni:</b> {GetEnumDescription(Father.DeathReason)}\n");

        sb.AppendLine("🏠 <b>İkametgâh:</b>");
        sb.AppendLine($"• <b>Açık Adres:</b> {ResidenceAddress.FullAddress}");
        sb.AppendLine($"• <b>Konut Durumu:</b> {GetEnumDescription(FamilyFinancial.HousingStatus)}\n");

        sb.AppendLine("<b>Maddi Durum:</b>");
        sb.AppendLine($"• <b>Aile Kişi Sayısı:</b> {FamilyFinancial.FamilyMemberCount}");
        sb.AppendLine($"• <b>Sosyal Güvence:</b> {GetEnumDescription(FamilyFinancial.SocialSecurity)}");
        sb.AppendLine($"• <b>Aylık Gelir:</b> {FamilyFinancial.Income} TL ({GetEnumDescription(FamilyFinancial.IncomeStatus)})");
        sb.AppendLine($"• <b>Aylık Gider:</b> {FamilyFinancial.Expense} TL\n");

        sb.AppendLine($"<b>Çocuklar ({Children.Count} Kişi):</b>");
        foreach (var child in Children)
        {
            sb.AppendLine($"• <b>{child.Name}</b> (T.C: {child.IdNumber?.Value}, D.Tarihi: {child.BirthDate:dd.MM.yyyy})");
        }

        if (DisabledMember != null && DisabledMember.Name != "Yok" && !string.IsNullOrWhiteSpace(DisabledMember.Name))
        {
            sb.AppendLine("\n🏥 <b>Engelli / Hasta Birey:</b>");
            sb.AppendLine($"• <b>Ad Soyad:</b> {DisabledMember.Name}");
            sb.AppendLine($"• <b>Teşhis / Durum:</b> {DisabledMember.Diagnosis} ({DisabledMember.DiseaseStatus})");
            sb.AppendLine($"• <b>Tedavi Durumu:</b> {GetEnumDescription(DisabledMember.TreatmentStatus)}");
        }

        sb.AppendLine("\n<i>Lütfen yukarıdaki bilgileri kontrol ediniz. Bilgiler doğruysa aşağıdaki butona basarak başvurunuzu tamamlayabilirsiniz. Hatalı bir bilgi varsa <code>/update &lt;alan&gt;</code> komutu ile düzeltebilirsiniz.</i>");

        return sb.ToString();
    }

    private static string GetEnumDescription(Enum? value)
    {
        if (value == null) return "-";
        var field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        var attribute = field.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
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