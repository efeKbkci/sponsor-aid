using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

/// <summary>
/// Represents a dependent orphan child eligible for financial and social aid.
/// </summary>
public class Child
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IdNumber IdentityNumber { get; private set; }
    public DateTime BirthDate { get; private set; }

    public string Hobbies { get; private set; } = string.Empty;
    public string FutureDream { get; private set; } = string.Empty;

    /// <summary>
    /// Initializes a new child entity with identification and birth date validation.
    /// </summary>
    /// <param name="name">Full name of the child.</param>
    /// <param name="identityNumber">National identity number.</param>
    /// <param name="birthDate">Birth date used to verify age eligibility.</param>
    /// <param name="fatherDetails">Information regarding the deceased father.</param>
    public Child(string name, IdNumber identityNumber, DateTime birthDate, FatherDetails fatherDetails)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Error_ChildNameRequired");

        if (birthDate > DateTime.Now)
            throw new BusinessRuleException("Error_ChildBirthDateCannotBeFuture");

        // Sponsorship eligibility requires the child to be strictly younger than 13 years old.
        if (birthDate <= DateTime.Now.AddYears(-13))
            throw new BusinessRuleException("Error_ChildMustBeUnder13");

        Id = Guid.NewGuid();
        Name = name;
        IdentityNumber = identityNumber;
        BirthDate = birthDate;
    }

    /// <summary>
    /// Updates enrichment fields gathered by automated background processing.
    /// </summary>
    /// <param name="hobbies">Interests or hobbies of the child.</param>
    /// <param name="futureDream">Personal goals or future dreams of the child.</param>
    public void SetAutomatedDetails(string hobbies, string futureDream)
    {
        Hobbies = hobbies ?? string.Empty;
        FutureDream = futureDream ?? string.Empty;
    }
}
