using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

/// <summary>
/// Represents a command conveying document processing results received from background workers.
/// </summary>
public record SaveProcessedDocumentCommand(
    string RegistrationKey,
    string DocumentType,
    bool IsSuccess,
    string? ErrorMessage,
    string? SavedFilePath,

    PopulationRegistryData? PopulationData = null,
    ResidenceData? ResidenceData = null,
    string? ExtractedTcNumber = null,
    string? ExtractedBarcode = null,
    string? ExtractedIban = null
) : IRequest, IRegistrationCommand;
