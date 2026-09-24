using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum TreatmentStatus
{
    [Description("Tedavi Ediliyor")] BeingTreated = 1,
    [Description("Tedavi Edilmesi Gerekiyor")] NeedsTreatment = 2,
    [Description("Tedavisi Mümkün Değil")] Incurable = 3,
    [Description("Yurt Dışında Tedavisi Gerekiyor")] NeedsTreatmentAbroad = 4
}
