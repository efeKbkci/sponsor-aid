using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum MaritalStatus
{
    [Description("Bekar")] Single = 1,
    [Description("Boşanmış")] Divorced = 2,
    [Description("Evli")] Married = 3,
    [Description("Dul")] Widowed = 4
}
