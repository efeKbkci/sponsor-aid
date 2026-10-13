using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;

public record ProcessBinaryInputCommand(
    long ChatId,
    string FileId,
    long FileSize,
    string Extension
) : IRequest, IChatCommand;
