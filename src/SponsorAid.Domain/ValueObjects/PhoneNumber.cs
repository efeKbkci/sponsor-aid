using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Represents a normalized Turkish mobile phone number used for SMS updates and verification.
/// </summary>
public record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException("Error_PhoneNumberRequired");

        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());

        // Normalizes Turkish mobile numbers with or without leading zero for SMS gateway delivery.
        if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
            Value = digitsOnly;
        else if (digitsOnly.StartsWith("5") && digitsOnly.Length == 10)
            Value = digitsOnly;
        else
            throw new BusinessRuleException("Error_InvalidPhoneNumber");
    }
}
