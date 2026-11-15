namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Designates commands that target a specific registration draft.
/// </summary>
public interface IRegistrationCommand
{
    /// <summary>
    /// Gets the unique session key that links the command to a registration draft.
    /// </summary>
    string RegistrationKey { get; }
}
