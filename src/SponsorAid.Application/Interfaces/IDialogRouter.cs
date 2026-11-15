using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Routes raw incoming chat messages to the corresponding application handlers.
/// </summary>
public interface IDialogRouter
{
    /// <summary>
    /// Routes a raw message string to preserve Clean Architecture and avoid binding to platform-specific JSON libraries.
    /// </summary>
    /// <param name="payload">Raw message content received from the chat platform.</param>
    /// <param name="cancellationToken">Token to cancel the routing task.</param>
    /// <returns>A task representing the message routing operation.</returns>
    Task RouteIncomingMessageAsync(string payload, CancellationToken cancellationToken = default);
}
