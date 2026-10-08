using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

public interface IEGovermentDocumentService
{
    // PDF'i indirir, diske kaydeder ve fiziksel dosya yolunu geri döner.
    Task<string> DownloadDocumentAsync(string barcode, string idNumber, CancellationToken cancellationToken = default);
}
