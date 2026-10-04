using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> DownloadAndSaveAsync(string fileId, string extension);
}
