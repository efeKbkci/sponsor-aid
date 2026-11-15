using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Stores household economic data to determine family need and financial aid eligibility.
/// </summary>
public record FinancialInfo
{
    public int HouseholdSize { get; }
    public decimal Income { get; }
    public decimal Expense { get; }

    public FinancialInfo(int householdSize, decimal income, decimal expense)
    {
        // Household size must be at least one person to calculate per capita income for poverty thresholds.
        if (householdSize <= 0)
            throw new BusinessRuleException("Error_HouseholdSizeMustBePositive");

        // Financial figures cannot be negative when computing net deficit balance.
        if (income < 0 || expense < 0)
            throw new BusinessRuleException("Error_IncomeExpenseCannotBeNegative");

        HouseholdSize = householdSize;
        Income = income;
        Expense = expense;
    }
}
