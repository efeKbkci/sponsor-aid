using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Represents a Turkish Republic Identification Number verified through official checksum rules.
/// </summary>
public record IdNumber
{
    public string Value { get; }

    public IdNumber(string value)
    {
        // Citizen registry rules enforce an 11-digit numeric format without letters.
        if (string.IsNullOrWhiteSpace(value) || value.Length != 11 || !value.All(char.IsDigit))
            throw new BusinessRuleException("Error_TcNumberMustBe11Digits");

        // The national identity system reserves leading zeros for non-citizen formats.
        if (value[0] == '0')
            throw new BusinessRuleException("Error_TcNumberCannotStartWithZero");

        var digits = value.Select(c => c - '0').ToArray();

        int sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int sumEven = digits[1] + digits[3] + digits[5] + digits[7];

        // Validates the 10th check digit using the standard identity algorithm formula.
        int digit10 = ((sumOdd * 7) - sumEven) % 10;
        if (digit10 < 0) digit10 += 10;

        // Validates the 11th check digit based on the sum of the first 10 digits to prevent forgery.
        int sumFirst10 = digits.Take(10).Sum();
        int digit11 = sumFirst10 % 10;

        if (digits[9] != digit10 || digits[10] != digit11)
            throw new BusinessRuleException("Error_InvalidTcNumber");

        Value = value;
    }
}
