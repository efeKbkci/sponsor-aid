using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Models;

/// <summary>
/// Represents the complete registration data payload exported for downstream processing.
/// </summary>
public record RegistrationDto(
    GuardianDto Guardian,
    List<ChildDto> Children,
    FatherDto Father,
    FamilyFinancialDto FamilyFinancial,
    DisabledMemberDto? DisabledMember,
    AddressDraft ResidenceAddress,
    string? PetitionText
);

/// <summary>
/// Data transfer object containing formatted guardian information.
/// </summary>
public record GuardianDto(string? Name, string? IdNumber, string? BirthDate, string? ApplicantRole, string? Gender, string? MaritalStatus, string? EducationLevel, string? EducationStatus, string? DegreeOfKinship, string? Profession, string? PhoneNumber, string? Iban);

/// <summary>
/// Data transfer object containing child profile details.
/// </summary>
public record ChildDto(string Name, string? IdNumber, string? BirthDate, string? Hobbies, string? FutureDream);

/// <summary>
/// Data transfer object containing details of the deceased father.
/// </summary>
public record FatherDto(string? Name, string? DeathDate, string? Profession, string? DeathReason);

/// <summary>
/// Data transfer object containing household financial indicators.
/// </summary>
public record FamilyFinancialDto(int? FamilyMemberCount, string? HousingStatus, string? SocialSecurity, string? IncomeStatus, int? Income, int? Expense, int OtherIncome);

/// <summary>
/// Data transfer object containing medical information for a dependent member.
/// </summary>
public record DisabledMemberDto(string? Name, string? Diagnosis, string? DiseaseStatus, string? TreatmentStatus, string? Description);
