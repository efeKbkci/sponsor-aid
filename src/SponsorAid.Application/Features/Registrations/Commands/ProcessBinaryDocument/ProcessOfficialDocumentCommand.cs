using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;

// Command to process a binary document (e.g., image/PDF). Adjust payload as needed.
public record ProcessOfficialDocumentCommand(
    string RegistrationKey,
    string FileId,      // Telegram'daki dosyanın kimliği
    long FileSize,      // Doğrulama için boyut
    string Extension,
    string DocumentType
) : IRequest, IRegistrationCommand;
