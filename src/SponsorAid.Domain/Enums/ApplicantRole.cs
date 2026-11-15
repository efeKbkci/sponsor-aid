using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Defines the role of the applicant in relation to the orphaned child.
/// </summary>
public enum ApplicantRole
{
    [Description("Yetim Annesi")] Mother = 1,
    [Description("Yetim Vasisi")] Guardian = 2
}
