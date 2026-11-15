using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryInput;

/// <summary>
/// Represents a user request carrying an uploaded binary file from the chat interface.
/// </summary>
public record ProcessBinaryInputCommand(
    long ChatId,
    string FileId,
    long FileSize,
    string Extension
) : IRequest, IChatCommand;
