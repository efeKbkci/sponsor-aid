using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Entities;
using SponsorAid.Domain.ValueObjects;

namespace SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;

public class FinalizeRegistrationHandler(
    IDraftStateRepository draftRepository,
    IRegistrationTrackingRepository trackingRepository,
    IFileStorageService fileStorage,
    IUserNotifier notifier) : IRequestHandler<FinalizeRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IFileStorageService _fileStorage = fileStorage;
    private readonly IUserNotifier _notifier = notifier;

    public async Task Handle(FinalizeRegistrationCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null)
        {
            throw new KeyNotFoundException($"'{request.RegistrationKey}' anahtarına sahip kayıt bulunamadı.");
        }

        // 1. ZORUNLU ALAN KONTROLLERİ
        EnsureRequiredFieldsAreFilled(draft);

        // 2. DOMAIN KURALLARI DOĞRULAMASI (Yaş, IBAN, T.C. vb.)
        ValidateDomainRules(draft);

        // 3. TAKİP TABLOSUNDA DURUMU "Tamamlandı" OLARAK İŞARETLE
        await _trackingRepository.MarkAsCompletedAsync(request.RegistrationKey, cancellationToken);

        // 4. BELGELERİ HARD DELETE İLE DİSKTEN SİL
        DeleteUploadedFiles(draft);

        // 5. TASLAĞI (DRAFT) VERİTABANINDAN HARD DELETE İLE SİL
        await _draftRepository.DeleteAsync(request.RegistrationKey, cancellationToken);

        // 6. KULLANICIYA BAŞARI BİLDİRİMİ GÖNDER
        await _notifier.SendMessageAsync(draft.ChatId,
            "✅ <b>Başvurunuz Başarıyla Tamamlandı!</b>\n\n" +
            $"Tüm aile, ikamet ve gelir bilgileriniz doğrulandı. Takip kodunuz: <code>{draft.RegistrationKey}</code>\n" +
            "Geçici belgeleriniz ve kayıt taslağınız gizlilik gereği sunucularımızdan güvenli bir şekilde silinmiştir.");
    }

    private void DeleteUploadedFiles(RegistrationDraftState draft)
    {
        var filesToDelete = new HashSet<string?>();

        // Resmi Belgeler
        filesToDelete.Add(draft.SystemDocuments.PopulationRegistryDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.ResidenceDocumentPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityFrontPath);
        filesToDelete.Add(draft.SystemDocuments.IdentityBackPath);
        filesToDelete.Add(draft.SystemDocuments.MergedIdentityPath);

        // Vasi Kimliği
        filesToDelete.Add(draft.Guardian.IdentityDocumentPath);

        // Çocukların Kimlikleri
        foreach (var child in draft.Children)
        {
            filesToDelete.Add(child.IdentityDocumentPath);
        }

        foreach (var filePath in filesToDelete)
        {
            _fileStorage.DeleteFile(filePath);
        }
    }

    private void ValidateDomainRules(RegistrationDraftState draft)
    {
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
            deathDate: draft.Father.DeathDate!.Value.ToDateTime(TimeOnly.MinValue),
            profession: draft.Father.Profession!.Value,
            deathReason: draft.Father.DeathReason!.Value
        );

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

        foreach (var childDraft in draft.Children)
        {
            var child = new Child(
                name: childDraft.Name,
                identityNumber: childDraft.IdNumber!,
                birthDate: childDraft.BirthDate.ToDateTime(TimeOnly.MinValue),
                fatherDetails: fatherDetails
            );

            child.SetAutomatedDetails(childDraft.Hobbies!, childDraft.FutureDream!);
            guardian.AddChild(child);
        }
    }

    private void EnsureRequiredFieldsAreFilled(RegistrationDraftState draft)
    {
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
            throw new InvalidOperationException("Aşağıdaki zorunlu alanlar eksik:\n- " + string.Join("\n- ", missingFields));
        }
    }
}