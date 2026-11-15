using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Medical treatment status for disabled household members.
/// </summary>
public enum TreatmentStatus
{
    [Description("Tedavi Ediliyor")] BeingTreated = 1,
    [Description("Tedavi Edilmesi Gerekiyor")] NeedsTreatment = 2,
    [Description("Tedavisi Mümkün Değil")] Incurable = 3,
    [Description("Yurt Dışında Tedavisi Gerekiyor")] NeedsTreatmentAbroad = 4
}
