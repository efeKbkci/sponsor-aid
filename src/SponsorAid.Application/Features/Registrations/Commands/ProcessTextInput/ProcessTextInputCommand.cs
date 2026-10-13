using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessTextInput;

public record ProcessTextInputCommand(
    long ChatId,
    string Text,
    int? CallbackMessageId = null
) : IRequest, IChatCommand;
