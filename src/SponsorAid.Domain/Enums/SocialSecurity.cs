using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Social insurance or security coverage available to the household.
/// </summary>
public enum SocialSecurity
{
    [Description("SGK")] Sgk = 1,
    [Description("Bağ-Kur")] BagKur = 2,
    [Description("Yeşil Kart")] GreenCard = 3,
    [Description("Diğer")] Other = 4
}
