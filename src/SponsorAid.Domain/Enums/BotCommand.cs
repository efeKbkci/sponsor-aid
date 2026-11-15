using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Supported bot commands for user interaction and registration navigation.
/// </summary>
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
