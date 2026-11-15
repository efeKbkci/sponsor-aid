using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Stores background information about the deceased father for orphan aid evaluation.
/// </summary>
public record FatherDetails
{
    public string Name { get; init; }
    public DateTime DeathDate { get; init; }
    public FatherProfession Profession { get; init; }
    public FatherDeathReason DeathReason { get; init; }

    public FatherDetails(string name, DateTime deathDate, FatherProfession profession, FatherDeathReason deathReason)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Error_FatherNameRequired");

        // The death date cannot be in the future because official death registration documents must already exist.
        if (deathDate > DateTime.Now)
            throw new BusinessRuleException("Error_FatherDeathDateCannotBeFuture");

        Name = name;
        DeathDate = deathDate;
        Profession = profession;
        DeathReason = deathReason;
    }
}
