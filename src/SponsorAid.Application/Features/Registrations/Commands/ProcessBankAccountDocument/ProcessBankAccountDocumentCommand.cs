using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessBankAccountDocument;

/// <summary>
/// Represents a command to download, store, and enqueue a bank account document (PDF or image) for IBAN extraction.
/// </summary>
public record ProcessBankAccountDocumentCommand(
    string RegistrationKey,
    string FileId,
    long FileSize,
    string Extension
) : IRequest, IRegistrationCommand;
