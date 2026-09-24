using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum ApplicantRole
{
    [Description("Yetim Annesi")] Mother = 1,
    [Description("Yetim Vasisi")] Guardian = 2
}
