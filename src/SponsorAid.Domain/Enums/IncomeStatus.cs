using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Income regularities and sources used to evaluate financial aid priority.
/// </summary>
public enum IncomeStatus
{
    [Description("Sabit Maaş")] FixedSalary = 1,
    [Description("Kira")] RentIncome = 2,
    [Description("Günlük İş")] DailyWage = 3,
    [Description("Diğer")] Other = 4
}
