using MediatR;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;

public record SaveProcessedDocumentCommand(
    string RegistrationKey,
    string DocumentType, // "PopulationRegistry", "Residence", "Identity"
    bool IsSuccess,      // Worker okuyabildi mi?
    string? ErrorMessage, // Okuyamadıysa neden? (Bulanık, format hatalı vb.)
    string? ExtractedDataJson, // Worker'dan gelen verilerin JSON hali
    string SavedFilePath // Dosyanın sunucudaki konumu
) : IRequest, IRegistrationCommand;
