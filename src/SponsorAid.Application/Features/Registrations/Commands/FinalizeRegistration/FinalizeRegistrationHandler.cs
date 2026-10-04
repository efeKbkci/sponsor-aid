using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Entities;
using SponsorAid.Domain.ValueObjects;

namespace SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;

public class FinalizeRegistrationHandler : IRequestHandler<FinalizeRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository;
    private readonly IUserNotifier _notifier;

    public FinalizeRegistrationHandler(IDraftStateRepository draftRepository, IUserNotifier notifier)
    {
        _draftRepository = draftRepository;
        _notifier = notifier;
    }

    public async Task Handle(FinalizeRegistrationCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);

        if (draft == null)
        {
            throw new KeyNotFoundException($"'{request.RegistrationKey}' anahtarına sahip kayıt bulunamadı.");
        }

        // 1. ZORUNLU ALAN KONTROLÜ
        EnsureRequiredFieldsAreFilled(draft);

        // 2. VALUE OBJECT (DEĞER NESNESİ) DÖNÜŞÜMLERİ
        // Sepetteki ilkel veriler Domain katmanındaki Value Object'lere dönüştürülüyor.
        var financialInfo = new FinancialInfo(
            householdSize: draft.FamilyFinancial.FamilyMemberCount!.Value,
            income: draft.FamilyFinancial.Income!.Value,
            expense: draft.FamilyFinancial.Expense!.Value
        );

        var address = new Address(
            country: draft.ResidenceAddress.Country,
            city: draft.ResidenceAddress.City!,
            neighborhood: draft.ResidenceAddress.Neighborhood!,
            district: draft.ResidenceAddress.District!,
            fullAddress: draft.ResidenceAddress.FullAddress!
        );

        var fatherDetails = new FatherDetails(
            name: draft.Father.Name!,
            deathDate: draft.Father.DeathDate!.Value.ToDateTime(TimeOnly.MinValue), // DateOnly'den DateTime'a çevrim
            profession: draft.Father.Profession!.Value,
            deathReason: draft.Father.DeathReason!.Value
        );

        // 3. ANA VARLIK (GUARDIAN) YARATIMI
        // Tüm iş kuralları (örn: 18 yaş kontrolü) burada çalışacak.
        var guardian = new Guardian(
            name: draft.Guardian.Name!,
            identityNumber: draft.Guardian.IdNumber!,
            birthDate: draft.Guardian.BirthDate.ToDateTime(TimeOnly.MinValue),
            role: draft.Guardian.ApplicantRole!.Value,
            gender: draft.Guardian.Gender!.Value,
            maritalStatus: draft.Guardian.MaritalStatus!.Value,
            educationLevel: draft.Guardian.EducationLevel!.Value,
            educationStatus: draft.Guardian.EducationStatus!.Value,
            kinship: draft.Guardian.DegreeOfKinship!.Value,
            profession: draft.Guardian.Profession!,
            housingStatus: draft.FamilyFinancial.HousingStatus!.Value,
            socialSecurity: draft.FamilyFinancial.SocialSecurity!.Value,
            financialInfo: financialInfo,
            address: address,
            contactInfo: draft.Guardian.PhoneNumber!,
            iban: draft.Guardian.Iban!
        );

        // 4. ALT VARLIKLARIN (CHILDREN) YARATIMI VE EKLENMESİ
        foreach (var childDraft in draft.Children)
        {
            var child = new Child(
                name: childDraft.Name,
                identityNumber: childDraft.IdNumber!,
                birthDate: childDraft.BirthDate.ToDateTime(TimeOnly.MinValue),
                fatherDetails: fatherDetails
            );

            // Otomasyon verilerini ekle (Dilekçe, Hobiler vb.)[cite: 2]
            child.SetAutomatedDetails(
                hobbies: childDraft.Hobbies!,
                futureDream: childDraft.FutureDream!
            );

            guardian.AddChild(child);
        }

        // 5. ENGELLİ/HASTA BİREY KONTROLÜ
        if (draft.DisabledMember != null && !string.IsNullOrWhiteSpace(draft.DisabledMember.Name))
        {
            var disabledMember = new DisabledMember(
                name: draft.DisabledMember.Name,
                diagnosis: draft.DisabledMember.Diagnosis!,
                status: draft.DisabledMember.DiseaseStatus!,
                treatmentStatus: draft.DisabledMember.TreatmentStatus!.Value,
                description: draft.DisabledMember.Description!
            );

            guardian.AddDisabledMember(disabledMember);
        }

        // 6. BAŞARI DURUMU: KAYIT MÜHÜRLENDİ
        // Eğer buraya kadar geldiysek, sistem IHH'nın tüm Domain kurallarını başarıyla geçmiştir.
        await _notifier.SendMessageAsync(draft.ChatId,
            "✅ Kayıt başarıyla doğrulandı ve tamamlandı!\n" +
            "Tüm iş kuralları (yaş sınırları, gelir durumları) kontrol edildi ve onaylandı.");

        // Geçici taslağı hafızadan siliyoruz[cite: 2].
        await _draftRepository.DeleteAsync(request.RegistrationKey, cancellationToken);
    }

    private void EnsureRequiredFieldsAreFilled(RegistrationDraftState draft)
    {
        // Temel verilerin (Null) kontrolleri yapılarak, Entity oluştururken alınabilecek NullReferenceException'ların önüne geçilir.
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Guardian.Name)) missingFields.Add("Vasi İsmi");
        if (draft.Guardian.IdNumber == null) missingFields.Add("Vasi TC Kimlik No");
        if (draft.Guardian.ApplicantRole == null) missingFields.Add("Başvuru Yapan Kişi (Rol)");
        if (draft.Guardian.Gender == null) missingFields.Add("Vasi Cinsiyeti");
        if (draft.Guardian.PhoneNumber == null) missingFields.Add("Vasi İletişim Numarası");
        if (draft.Guardian.Iban == null) missingFields.Add("IBAN");

        if (draft.FamilyFinancial.Income == null || draft.FamilyFinancial.Expense == null) missingFields.Add("Gelir/Gider Bilgisi");
        if (draft.FamilyFinancial.FamilyMemberCount == null) missingFields.Add("Aile Kişi Sayısı");

        if (string.IsNullOrWhiteSpace(draft.Father.Name)) missingFields.Add("Baba Adı");
        if (draft.Father.DeathDate == null) missingFields.Add("Baba Ölüm Tarihi");
        if (draft.Father.Profession == null) missingFields.Add("Baba Mesleği");
        if (draft.Father.DeathReason == null) missingFields.Add("Baba Ölüm Nedeni");

        if (string.IsNullOrWhiteSpace(draft.ResidenceAddress.City) || string.IsNullOrWhiteSpace(draft.ResidenceAddress.FullAddress)) missingFields.Add("İkametgah Adresi");

        if (draft.SystemDocuments.Petition == null) missingFields.Add("Sistem Dilekçesi");

        if (!draft.Children.Any()) missingFields.Add("Çocuk Bilgisi (En az 1 çocuk gereklidir)");

        if (missingFields.Any())
        {
            throw new InvalidOperationException("Aşağıdaki zorunlu alanlar eksik, lütfen tamamlayın:\n- " + string.Join("\n- ", missingFields));
        }
    }
}