using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum BotCommand
{
    [Description("/start")]
    StartBot = 1,

    [Description("/new")]
    NewRegistration = 2,

    [Description("/continue")]
    ContinueRegistration = 3,

    [Description("/cancel")]
    CancelRegistration = 4,

    [Description("/update")]
    UpdateRegistration = 5
}
