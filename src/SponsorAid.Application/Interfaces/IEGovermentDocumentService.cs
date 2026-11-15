using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Fetches official verification documents from government portals.
/// </summary>
public interface IEGovermentDocumentService
{
    /// <summary>
    /// Downloads an official PDF document and saves it locally for application package assembly.
    /// </summary>
    /// <param name="barcode">Document verification barcode.</param>
    /// <param name="idNumber">Citizen ID number printed on the document.</param>
    /// <param name="cancellationToken">Token to cancel the download task.</param>
    /// <returns>Local file path of the downloaded document.</returns>
    Task<string> DownloadDocumentAsync(string barcode, string idNumber, CancellationToken cancellationToken = default);
}
