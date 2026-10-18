using MediatR;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;
using SponsorAid.Domain.Entities;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;

namespace SponsorAid.Application.Features.Registrations.Commands.FinalizeRegistration;

public class FinalizeRegistrationHandler(
    IDraftStateRepository draftRepository,
    IRegistrationTrackingRepository trackingRepository,
    IFileStorageService fileStorage,
    IUserNotifier notifier,
    ISecureArchiveService archiveService,
    IRegistrationDispatchService dispatchService,
    IStringLocalizer<BotMessages> localizer) : IRequestHandler<FinalizeRegistrationCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IRegistrationTrackingRepository _trackingRepository = trackingRepository;
    private readonly IFileStorageService _fileStorage = fileStorage;
    private readonly IUserNotifier _notifier = notifier;
    private readonly ISecureArchiveService _archiveService = archiveService;
    private readonly IRegistrationDispatchService _dispatchService = dispatchService;
    private readonly IStringLocalizer<BotMessages> _localizer = localizer;

    public async Task Handle(FinalizeRegistrationCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null)
            throw new SponsorAid.Domain.Exceptions.BusinessRuleException("Error_RegistrationNotFound", request.RegistrationKey);

        // 1. ZORUNLU ALAN KONTROLLERİ VE DOMAIN DOĞRULAMASI
        EnsureRequiredFieldsAreFilled(draft);
        ValidateDomainRules(draft);

        // 2. AES-256 ŞİFRELİ ZIP PAKETİ OLUŞTURULMASI
        var (archiveBytes, password) = await _archiveService.CreateEncryptedPackageAsync(draft, cancellationToken);

        // 3. E-POSTA İLE DOSYA GÖNDERİMİ (A KİŞİSİNE)
        await _dispatchService.SendEncryptedPackageAsync(
            draft.RegistrationKey,
            draft.Guardian.Name!,
            archiveBytes,
            cancellationToken);

        // 4. AYRIK KANAL ÜZERİNDEN ŞİFRENİN GÖNDERİLMESİ
        await _dispatchService.SendPasswordAsync(
            draft.RegistrationKey,
            draft.Guardian.Name!,
            password,
            cancellationToken);

        // 5. TAKİP TABLOSUNU "Tamamlandı" OLARAK GÜNCELLE
        await _trackingRepository.MarkAsCompletedAsync(request.RegistrationKey, cancellationToken);

        // 6. GÜVENLİK TEMİZLİĞİ: GEÇİCİ DOSYALARI DİSKTEN KALICI OLARAK SİL (HARD DELETE)
        DeleteUploadedFiles(draft);

        // 7. TASLAĞI (DRAFT) VERİTABANINDAN SİL (HARD DELETE)
        await _draftRepository.DeleteAsync(request.RegistrationKey, cancellationToken);

        // 8. KULLANICIYA BAŞARI BİLDİRİMİ
        await _notifier.SendMessageAsync(draft.ChatId,
            _localizer["Success_RegistrationFinalized", draft.RegistrationKey].Value);
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

        // 🚀 ÇÖZÜM 2: "Yok" kontrolü eklenerek sadece gerçekten hasta/engelli birey tanımlanmışsa eklenir.
        if (draft.DisabledMember != null &&
            !string.IsNullOrWhiteSpace(draft.DisabledMember.Name) &&
            draft.DisabledMember.Name != "Yok" &&
            draft.DisabledMember.TreatmentStatus.HasValue)
        {
            var disabledMember = new DisabledMember(
                name: draft.DisabledMember.Name,
                diagnosis: draft.DisabledMember.Diagnosis ?? "Belirtilmedi",
                status: draft.DisabledMember.DiseaseStatus ?? "Belirtilmedi",
                treatmentStatus: draft.DisabledMember.TreatmentStatus.Value,
                description: draft.DisabledMember.Description ?? string.Empty
            );

            guardian.AddDisabledMember(disabledMember);
        }
    }

    private void EnsureRequiredFieldsAreFilled(RegistrationDraftState draft)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(draft.Guardian.Name)) missingFields.Add(_localizer["Label_Required_GuardianName"].Value);
        if (draft.Guardian.IdNumber == null) missingFields.Add(_localizer["Label_Required_GuardianId"].Value);
        if (draft.Guardian.ApplicantRole == null) missingFields.Add(_localizer["Label_Required_ApplicantRole"].Value);
        if (draft.Guardian.Gender == null) missingFields.Add(_localizer["Label_Required_Gender"].Value);
        if (draft.Guardian.PhoneNumber == null) missingFields.Add(_localizer["Label_Required_PhoneNumber"].Value);
        if (draft.Guardian.Iban == null) missingFields.Add(_localizer["Label_Required_Iban"].Value);

        if (draft.FamilyFinancial.Income == null || draft.FamilyFinancial.Expense == null) missingFields.Add(_localizer["Label_Required_IncomeExpense"].Value);
        if (draft.FamilyFinancial.FamilyMemberCount == null) missingFields.Add(_localizer["Label_Required_FamilyMemberCount"].Value);

        if (string.IsNullOrWhiteSpace(draft.Father.Name)) missingFields.Add(_localizer["Label_Required_FatherName"].Value);
        if (draft.Father.DeathDate == null) missingFields.Add(_localizer["Label_Required_FatherDeathDate"].Value);
        if (draft.Father.Profession == null) missingFields.Add(_localizer["Label_Required_FatherProfession"].Value);
        if (draft.Father.DeathReason == null) missingFields.Add(_localizer["Label_Required_FatherDeathReason"].Value);

        if (string.IsNullOrWhiteSpace(draft.ResidenceAddress.City) || string.IsNullOrWhiteSpace(draft.ResidenceAddress.FullAddress)) missingFields.Add(_localizer["Label_Required_ResidenceAddress"].Value);

        if (draft.SystemDocuments.Petition == null) missingFields.Add(_localizer["Label_Required_Petition"].Value);

        if (!draft.Children.Any()) missingFields.Add(_localizer["Label_Required_Child"].Value);

        if (missingFields.Any())
        {
            throw new SponsorAid.Domain.Exceptions.BusinessRuleException("Error_RequiredFieldsMissing", string.Join("\n- ", missingFields));
        }
    }
}