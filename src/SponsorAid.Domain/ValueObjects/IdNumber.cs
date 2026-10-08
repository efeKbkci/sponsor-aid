using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record IdNumber
{
    public string Value { get; }

    public IdNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 11 || !value.All(char.IsDigit))
            throw new BusinessRuleException("TC Kimlik Numarası 11 haneli olmalı ve sadece rakamlardan oluşmalıdır.");

        if (value[0] == '0')
            throw new BusinessRuleException("TC Kimlik Numarası 0 ile başlayamaz.");

        var digits = value.Select(c => c - '0').ToArray();

        int sumOdd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        int sumEven = digits[1] + digits[3] + digits[5] + digits[7];

        int digit10 = ((sumOdd * 7) - sumEven) % 10;
        if (digit10 < 0) digit10 += 10;

        int sumFirst10 = digits.Take(10).Sum();
        int digit11 = sumFirst10 % 10;

        if (digits[9] != digit10 || digits[10] != digit11)
            throw new BusinessRuleException("Geçersiz TC Kimlik Numarası (Algoritma doğrulaması başarısız).");

        Value = value;
    }
}
