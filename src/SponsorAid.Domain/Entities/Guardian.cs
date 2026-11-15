using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

/// <summary>
/// Represents the primary applicant or legal guardian who manages dependent children and family records.
/// </summary>
public class Guardian
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IdNumber IdentityNumber { get; private set; }
    public DateTime BirthDate { get; private set; }
    public ApplicantRole Role { get; private set; }
    public Gender Gender { get; private set; }
    public MaritalStatus MaritalStatus { get; private set; }
    public EducationLevel EducationLevel { get; private set; }
    public EducationStatus EducationStatus { get; private set; }
    public DegreeOfKinship Kinship { get; private set; }
    public string Profession { get; private set; }

    public HousingStatus HousingStatus { get; private set; }
    public SocialSecurity SocialSecurity { get; private set; }
    public FinancialInfo FinancialInfo { get; private set; }

    public Address Address { get; private set; }
    public PhoneNumber ContactInfo { get; private set; }
    public Iban Iban { get; private set; }

    private readonly List<Child> _children = new();
    public IReadOnlyCollection<Child> Children => _children.AsReadOnly();

    private readonly List<DisabledMember> _DisabledMembers = new();
    public IReadOnlyCollection<DisabledMember> DisabledMembers => _DisabledMembers.AsReadOnly();

    /// <summary>
    /// Initializes a new guardian entity with personal, social, and economic information.
    /// </summary>
    /// <param name="name">Full name of the guardian.</param>
    /// <param name="identityNumber">National identity number.</param>
    /// <param name="birthDate">Birth date of the guardian.</param>
    /// <param name="role">Role of the applicant in the household.</param>
    /// <param name="gender">Biological gender.</param>
    /// <param name="maritalStatus">Marital status.</param>
    /// <param name="educationLevel">Completed level of education.</param>
    /// <param name="educationStatus">Active status of schooling or graduation.</param>
    /// <param name="kinship">Degree of kinship to the dependent children.</param>
    /// <param name="profession">Current employment or trade.</param>
    /// <param name="housingStatus">Living and residential condition.</param>
    /// <param name="socialSecurity">Social security coverage type.</param>
    /// <param name="financialInfo">Income and debt evaluation details.</param>
    /// <param name="address">Residential physical address.</param>
    /// <param name="contactInfo">Primary contact phone number.</param>
    /// <param name="iban">Bank account number for aid disbursements.</param>
    public Guardian(
        string name, IdNumber identityNumber, DateTime birthDate,
        ApplicantRole role, Gender gender, MaritalStatus maritalStatus,
        EducationLevel educationLevel, EducationStatus educationStatus,
        DegreeOfKinship kinship, string profession, HousingStatus housingStatus,
        SocialSecurity socialSecurity, FinancialInfo financialInfo,
        Address address, PhoneNumber contactInfo, Iban iban)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new BusinessRuleException("Error_GuardianNameRequired");

        Id = Guid.NewGuid();
        Name = name;
        IdentityNumber = identityNumber;
        BirthDate = birthDate;
        Role = role;
        Gender = gender;
        MaritalStatus = maritalStatus;
        EducationLevel = educationLevel;
        EducationStatus = educationStatus;
        Kinship = kinship;
        Profession = profession;
        HousingStatus = housingStatus;
        SocialSecurity = socialSecurity;
        FinancialInfo = financialInfo;
        Address = address;
        ContactInfo = contactInfo;
        Iban = iban;
    }

    /// <summary>
    /// Associates a dependent child with this guardian.
    /// </summary>
    /// <param name="child">Child entity to register.</param>
    public void AddChild(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Prevent duplicate child registration under the same guardian by identity number.
        if (_children.Any(c => c.IdentityNumber.Value == child.IdentityNumber.Value))
            throw new BusinessRuleException("Error_ChildAlreadyRegistered");

        _children.Add(child);
    }

    /// <summary>
    /// Associates a family member with special care needs with this guardian.
    /// </summary>
    /// <param name="member">Disabled member entity to register.</param>
    public void AddDisabledMember(DisabledMember member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        _DisabledMembers.Add(member);
    }

    /// <summary>
    /// Updates the bank account number used for aid transfers.
    /// </summary>
    /// <param name="newIban">Validated new IBAN value object.</param>
    public void UpdateIban(Iban newIban)
    {
        Iban = newIban ?? throw new ArgumentNullException(nameof(newIban));
    }
}
