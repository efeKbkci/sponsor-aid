using MediatR;
using SponsorAid.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration;

public record ContinueRegistrationCommand(
    string? RegistrationKey,
    long ChatId) : IRequest, IChatCommand
{
    public const string SelectionCallbackPrefix = "continue:";
}
