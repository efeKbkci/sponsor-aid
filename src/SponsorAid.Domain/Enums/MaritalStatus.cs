using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Marital status of the applicant or guardian.
/// </summary>
public enum MaritalStatus
{
    [Description("Bekar")] Single = 1,
    [Description("Boşanmış")] Divorced = 2,
    [Description("Evli")] Married = 3,
    [Description("Dul")] Widowed = 4
}
