using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Manages temporary file downloads and storage cleanup on disk during the intake workflow.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Downloads a remote attachment and saves it to local temporary disk storage.
    /// </summary>
    /// <param name="fileId">External file identifier from the chat provider.</param>
    /// <param name="extension">Target file extension for disk storage.</param>
    /// <returns>The local file system path where the file was saved.</returns>
    Task<string> DownloadAndSaveAsync(string fileId, string extension);

    /// <summary>
    /// Deletes a temporary file from disk storage to prevent accumulating sensitive data.
    /// </summary>
    /// <param name="filePath">Local physical file path to delete.</param>
    void DeleteFile(string? filePath);
}
