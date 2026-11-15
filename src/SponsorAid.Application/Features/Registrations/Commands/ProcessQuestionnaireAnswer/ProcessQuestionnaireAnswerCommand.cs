using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;

/// <summary>
/// Represents a command to evaluate and store an answer to an active registration questionnaire question.
/// </summary>
public record ProcessQuestionnaireAnswerCommand(
    string RegistrationKey,
    string? AnswerText = null,
    int? CallbackMessageId = null
) : IRequest, IRegistrationCommand;
