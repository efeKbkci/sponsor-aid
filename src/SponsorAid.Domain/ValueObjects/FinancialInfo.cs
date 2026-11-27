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
        ValidateHouseholdSize(householdSize);
        ValidateAmount(income);
        ValidateAmount(expense);

        HouseholdSize = householdSize;
        Income = income;
        Expense = expense;
    }

    /// <summary>
    /// Validates that household size is at least one person to calculate per-capita thresholds.
    /// </summary>
    public static void ValidateHouseholdSize(int householdSize)
    {
        if (householdSize <= 0)
            throw new BusinessRuleException("Error_HouseholdSizeMustBePositive");
    }

    /// <summary>
    /// Validates that a financial amount (income or expense) is non-negative.
    /// </summary>
    public static void ValidateAmount(decimal amount)
    {
        if (amount < 0)
            throw new BusinessRuleException("Error_IncomeExpenseCannotBeNegative");
    }
}
