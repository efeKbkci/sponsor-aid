using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.UpdateQuestionAnswer;

public record UpdateQuestionAnswerCommand(
    string RegistrationKey,
    string QuestionKey,
    long ChatId 
) : IRequest, IRegistrationCommand;
