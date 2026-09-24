using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum HousingStatus
{
    [Description("Kendi Evi")] OwnHouse = 1,
    [Description("Kira")] Rent = 2,
    [Description("Akraba Yanı")] LivingWithRelatives = 3,
    [Description("Bedelsiz Kira")] RentFree = 4,
    [Description("Çadır")] Tent = 5,
    [Description("Konteyner")] Container = 6,
    [Description("Miras Evi")] InheritedHouse = 7
}
