using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Represents a Turkish bank account number validated with the MOD-97 algorithm.
/// </summary>
public record Iban
{
    public string Value { get; }

    public Iban(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException("Error_IbanRequired");

        var sanitizedIban = Sanitize(value);

        // Turkish banking regulations require 26 alphanumeric characters starting with the TR country code.
        if (!IsValidFormat(sanitizedIban))
            throw new BusinessRuleException("Error_InvalidIbanFormat");

        // Verifies the checksum to stop payment errors before dispatching funds to banks.
        if (!ValidateMod97(sanitizedIban))
            throw new BusinessRuleException("Error_InvalidIbanChecksum");

        Value = sanitizedIban;
    }

    private Iban(string sanitizedValue, bool _)
    {
        Value = sanitizedValue;
    }

    /// <summary>
    /// Attempts to parse and validate a Turkish IBAN using MOD-97 without throwing exceptions.
    /// </summary>
    public static bool TryCreate(string? value, out Iban? iban)
    {
        iban = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var sanitizedIban = Sanitize(value);

        if (!IsValidFormat(sanitizedIban) || !ValidateMod97(sanitizedIban))
            return false;

        iban = new Iban(sanitizedIban, true);
        return true;
    }

    private static string Sanitize(string value) =>
        value.Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();

    private static bool IsValidFormat(string sanitizedIban) =>
        sanitizedIban.StartsWith("TR", StringComparison.Ordinal) &&
        sanitizedIban.Length == 26 &&
        sanitizedIban.Skip(2).All(char.IsAsciiDigit);

    private static bool ValidateMod97(string iban)
    {
        // Rearranges country code and check digits to the end per ISO 7064 specifications.
        string rearranged = iban.Substring(4) + iban.Substring(0, 4);

        // Converts letters to digits where A equals 10 and Z equals 35 for large number division.
        var numericString = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        return BigInteger.TryParse(numericString, out var number) && number % 97 == 1;
    }
}
