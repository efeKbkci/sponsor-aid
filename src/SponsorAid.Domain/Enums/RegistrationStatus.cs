using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Overall lifecycle status of an aid registration session.
/// </summary>
public enum RegistrationStatus
{
    [Description("Devam Ediyor")]
    InProgress = 1,

    [Description("Tamamlandı")]
    Completed = 2
}
