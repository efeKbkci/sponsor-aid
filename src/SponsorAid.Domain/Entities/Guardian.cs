using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

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

    // Listeleri dışarıya sadece okunabilir (ReadOnly) açıyoruz
    private readonly List<Child> _children = new();
    public IReadOnlyCollection<Child> Children => _children.AsReadOnly();

    private readonly List<DisabledMember> _DisabledMembers = new();
    public IReadOnlyCollection<DisabledMember> DisabledMembers => _DisabledMembers.AsReadOnly();

    public Guardian(
        string name, IdNumber identityNumber, DateTime birthDate,
        ApplicantRole role, Gender gender, MaritalStatus maritalStatus,
        EducationLevel educationLevel, EducationStatus educationStatus,
        DegreeOfKinship kinship, string profession, HousingStatus housingStatus,
        SocialSecurity socialSecurity, FinancialInfo financialInfo,
        Address address, PhoneNumber contactInfo, Iban iban)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Vasi ismi boş olamaz.");

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

    public void AddChild(Child child)
    {
        if (child == null) throw new ArgumentNullException(nameof(child));

        // Aynı TC Kimlik numarasına sahip çocuk eklenmesini engelle
        if (_children.Any(c => c.IdentityNumber.Value == child.IdentityNumber.Value))
            throw new InvalidOperationException("Bu TC Kimlik numarasına sahip çocuk zaten kayıtlı.");

        _children.Add(child);
    }

    public void AddDisabledMember(DisabledMember member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        _DisabledMembers.Add(member);
    }

    // IBAN güncellemesi sadece bu metot üzerinden yapılabilir
    public void UpdateIban(Iban newIban)
    {
        Iban = newIban ?? throw new ArgumentNullException(nameof(newIban));
    }
}

