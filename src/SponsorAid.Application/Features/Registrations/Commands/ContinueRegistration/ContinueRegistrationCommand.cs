using MediatR;
using SponsorAid.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;

/// <summary>
/// Command to resume an existing or pending registration workflow.
/// </summary>
public record ContinueRegistrationCommand(
    string? RegistrationKey,
    long ChatId) : IRequest, IChatCommand
{
    public const string SelectionCallbackPrefix = "continue:";
}
