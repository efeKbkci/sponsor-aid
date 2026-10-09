using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum RegistrationStatus
{
    [Description("Devam Ediyor")]
    InProgress = 1,

    [Description("Tamamlandı")]
    Completed = 2
}
