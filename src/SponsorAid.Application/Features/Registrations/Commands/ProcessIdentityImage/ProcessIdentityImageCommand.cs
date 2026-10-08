using MediatR;
using SponsorAid.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessIdentityImage;

public record ProcessIdentityImageCommand(
    string RegistrationKey,
    string FileId,
    long FileSize,
    string Extension,
    string ImageSide // "front" veya "back" gelecek
) : IRequest, IRegistrationCommand;