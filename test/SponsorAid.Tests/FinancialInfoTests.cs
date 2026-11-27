using FluentAssertions;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using Xunit;

namespace SponsorAid.Tests;

/// <summary>
/// Unit test suite for <see cref="FinancialInfo"/> domain invariants.
/// Verifies boundary values for household size, income, and expense across both the constructor
/// and static fail-fast validation methods.
/// </summary>
public class FinancialInfoTests
{
    [Theory]
    [InlineData(-5)]
    [InlineData(-1)]
    [InlineData(0)]
    public void ValidateHouseholdSizeAndConstructor_WhenNonPositive_ShouldThrowBusinessRuleException(int invalidSize)
    {
        // Act
        Action staticAct = () => FinancialInfo.ValidateHouseholdSize(invalidSize);
        Action ctorAct = () => _ = new FinancialInfo(invalidSize, 10000m, 8000m);

        // Assert
        staticAct.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_HouseholdSizeMustBePositive");
        ctorAct.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_HouseholdSizeMustBePositive");
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 0, 12000)]
    [InlineData(4, 15000, 12000)]
    [InlineData(8, 25000.50, 25000.50)]
    public void ValidateAndConstructor_WithPositiveHouseholdSizeAndNonNegativeAmounts_ShouldSucceed(
        int householdSize,
        decimal income,
        decimal expense)
    {
        // Act
        FinancialInfo.ValidateHouseholdSize(householdSize);
        FinancialInfo.ValidateAmount(income);
        FinancialInfo.ValidateAmount(expense);
        var info = new FinancialInfo(householdSize, income, expense);

        // Assert
        info.HouseholdSize.Should().Be(householdSize);
        info.Income.Should().Be(income);
        info.Expense.Should().Be(expense);
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void ValidateAmountAndConstructor_WhenIncomeOrExpenseIsNegative_ShouldThrowBusinessRuleException(
        decimal negativeAmount)
    {
        // Act
        Action staticAct = () => FinancialInfo.ValidateAmount(negativeAmount);
        Action negativeIncomeAct = () => _ = new FinancialInfo(3, negativeAmount, 5000m);
        Action negativeExpenseAct = () => _ = new FinancialInfo(3, 5000m, negativeAmount);

        // Assert
        staticAct.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_IncomeExpenseCannotBeNegative");
        negativeIncomeAct.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_IncomeExpenseCannotBeNegative");
        negativeExpenseAct.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_IncomeExpenseCannotBeNegative");
    }
}
