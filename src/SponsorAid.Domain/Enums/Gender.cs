using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Biological sex classification used for official records and identity matching.
/// </summary>
public enum Gender
{
    [Description("Erkek")] Male = 1,
    [Description("Kadın")] Female = 2
}
