using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new BusinessRuleException("İletişim bilgisi boş olamaz.");

        // Sadece rakamları al
        var digitsOnly = new string(value.Where(char.IsDigit).ToArray());

        // Desene uygunluk kontrolü[cite: 1]
        if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
            Value = digitsOnly;
        else if (digitsOnly.StartsWith("5") && digitsOnly.Length == 10)
            Value = digitsOnly;
        else
            throw new BusinessRuleException("Telefon numarası 0 ile başlıyorsa 11 hane, 5 ile başlıyorsa 10 hane olmalıdır.");
    }
}