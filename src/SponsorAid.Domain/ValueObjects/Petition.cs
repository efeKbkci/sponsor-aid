using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Represents the formal aid petition letter submitted by the applicant.
/// </summary>
public record Petition
{
    public string Content { get; }

    public Petition(string content)
    {
        // Evaluation committees require at least 300 characters to assess family circumstances and aid urgency.
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 300)
            throw new BusinessRuleException("Error_PetitionMinimumLength");

        Content = content.Trim();
    }
}
