using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record Iban
{
    public string Value { get; }

    public Iban(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException("Error_IbanRequired");

        // Boşlukları temizle ve büyük harfe çevir
        var sanitizedIban = value.Replace(" ", "").ToUpperInvariant();

        if (!sanitizedIban.StartsWith("TR") || sanitizedIban.Length != 26)
            throw new BusinessRuleException("Error_InvalidIbanFormat");

        if (!ValidateMod97(sanitizedIban))
            throw new BusinessRuleException("Error_InvalidIbanChecksum");

        Value = sanitizedIban;
    }

    private static bool ValidateMod97(string iban)
    {
        // İlk 4 karakteri sona taşı
        string rearranged = iban.Substring(4) + iban.Substring(0, 4);

        // Harfleri sayılara dönüştür (A=10, B=11 ... Z=35)
        var numericString = string.Concat(rearranged.Select(c => char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        return BigInteger.TryParse(numericString, out var number) && number % 97 == 1;
    }
}
