using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBinaryDocument;

/// <summary>
/// Represents a command to process an official PDF or scanned image document submitted during registration.
/// </summary>
public record ProcessOfficialDocumentCommand(
    string RegistrationKey,
    string FileId,
    long FileSize,
    string Extension,
    string DocumentType
) : IRequest, IRegistrationCommand;
