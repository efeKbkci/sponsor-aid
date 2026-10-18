using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Models;

// 2. Sadece Saf İş Verisi (Payload) Taşıyan Model
public record RegistrationDto(
    GuardianDto Guardian,
    List<ChildDto> Children,
    FatherDto Father,                   // Draft yerine DTO
    FamilyFinancialDto FamilyFinancial, // Draft yerine DTO
    DisabledMemberDto? DisabledMember,  // Draft yerine DTO
    AddressDraft ResidenceAddress,      // Adreste enum/tarih olmadığı için doğrudan kalabilir
    string? PetitionText
);

public record GuardianDto(string? Name, string? IdNumber, string? BirthDate, string? ApplicantRole, string? Gender, string? MaritalStatus, string? EducationLevel, string? EducationStatus, string? DegreeOfKinship, string? Profession, string? PhoneNumber, string? Iban);

public record ChildDto(string Name, string? IdNumber, string? BirthDate, string? Hobbies, string? FutureDream);

public record FatherDto(string? Name, string? DeathDate, string? Profession, string? DeathReason);

public record FamilyFinancialDto(int? FamilyMemberCount, string? HousingStatus, string? SocialSecurity, string? IncomeStatus, int? Income, int? Expense, int OtherIncome);

public record DisabledMemberDto(string? Name, string? Diagnosis, string? DiseaseStatus, string? TreatmentStatus, string? Description);