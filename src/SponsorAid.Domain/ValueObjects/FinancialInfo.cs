using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record FinancialInfo
{
    public int HouseholdSize { get; }
    public decimal Income { get; }
    public decimal Expense { get; }

    public FinancialInfo(int householdSize, decimal income, decimal expense)
    {
        // Kişi sayısı sadece sayılardan oluşabilir[cite: 1] ve mantıken 0'dan büyük olmalıdır.
        if (householdSize <= 0)
            throw new BusinessRuleException("Ailede yaşayan kişi sayısı 0 veya negatif olamaz.");

        // Gelir (sayı), gider (sayı)[cite: 1] negatif olamaz.
        if (income < 0 || expense < 0)
            throw new BusinessRuleException("Gelir ve gider bilgisi negatif olamaz.");

        HouseholdSize = householdSize;
        Income = income;
        Expense = expense;
    }
}