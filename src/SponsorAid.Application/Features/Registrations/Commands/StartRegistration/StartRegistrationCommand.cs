using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.StartRegistration;

// Bu komut sonucunda geriye bir RegistrationKey döneceği için IRequest<string> kullanıyoruz.
public record StartRegistrationCommand(long ChatId) : IRequest<string>, IChatCommand;
