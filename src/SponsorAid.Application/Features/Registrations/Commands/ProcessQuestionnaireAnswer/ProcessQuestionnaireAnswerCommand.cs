using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;

public record ProcessQuestionnaireAnswerCommand(
    string RegistrationKey,
    string? AnswerText = null, // Telegram'dan gelen anket cevabı (Buton veya Metin)
    int? CallbackMessageId = null // 🚀 YENİ EKLENDİ
) : IRequest, IRegistrationCommand;