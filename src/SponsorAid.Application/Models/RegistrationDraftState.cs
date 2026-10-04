using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument.Dtos;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;

namespace SponsorAid.Application.Models;

public class RegistrationDraftState
{
    public string RegistrationKey { get; set; } = string.Empty;
    public long ChatId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // --- FOLDERS (Composition) ---
    public GuardianDraft Guardian { get; private set; } = new();
    public List<ChildDraft> Children { get; private set; } = new();
    public FatherDraft Father { get; private set; } = new();
    public FamilyFinancialDraft FamilyFinancial { get; private set; } = new();
    public DisabledMemberDraft? DisabledMember { get; private set; }
    public SystemDocumentsDraft SystemDocuments { get; private set; } = new();
    public AddressDraft ResidenceAddress { get; private set; } = new();

    private readonly Dictionary<string, Action<string>> _updateStrategies;

    public RegistrationDraftState()
    {
        _updateStrategies = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // --- GUARDIAN UPDATES ---
            { "GuardianName", value => Guardian.Name = value.Trim() },
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

    public void ApplyPopulationRegistryResult(PopulationRegistryDto data)
    {
        Guardian.IdNumber = new IdNumber(data.Mother.IdNumber); // ValueObject
        Guardian.BirthDate = data.Mother.BirthDate;

        Father.Name = data.Father.Name;
        Father.DeathDate = data.Father.DeathDate;

        foreach (var child in data.Children)
        {
            Children.Add(new ChildDraft
            {
                Name = child.Name,
                IdNumber = new IdNumber(child.IdNumber),
                BirthDate = child.BirthDate,
            });
        }
    }

    public void ApplyResidenceDocumentResult(ResidenceDto data)
    {
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
    public string? IdentityDocumentPath { get; set; }
    public Petition? Petition { get; set; } // ValueObject
}