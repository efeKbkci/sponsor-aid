using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Integration.Contracts;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

public record SaveProcessedDocumentCommand(
    string RegistrationKey,
    string DocumentType, // "PopulationRegistry", "Residence", "IdentityFront", "IdentityBack", "MergedIdentity"
    bool IsSuccess,      // Worker okuyabildi mi?
    string? ErrorMessage, // Okuyamadıysa neden? (Bulanık, format hatalı vb.)
    string? SavedFilePath, // Dosyanın sunucudaki konumu

    PopulationRegistryData? PopulationData = null,
    ResidenceData? ResidenceData = null,
    string? ExtractedTcNumber = null,
    string? ExtractedBarcode = null 
) : IRequest, IRegistrationCommand;
