using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessTextInput;

/// <summary>
/// Represents a user command carrying incoming text or button callback data from the chat interface.
/// </summary>
public record ProcessTextInputCommand(
    long ChatId,
    string Text,
    int? CallbackMessageId = null
) : IRequest, IChatCommand;
