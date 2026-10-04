using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateRegistrationData;

public record UpdateRegistrationDataCommand(
    string RegistrationKey,
    Dictionary<string, string> Updates
) : IRequest, IRegistrationCommand;
