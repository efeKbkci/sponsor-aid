using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;

/// <summary>
/// Represents a command to request or apply updates to a previously answered registration questionnaire field.
/// </summary>
public record UpdateQuestionAnswerCommand(
    string RegistrationKey,
    string? UserInput,
    long ChatId 
) : IRequest, IRegistrationCommand;
