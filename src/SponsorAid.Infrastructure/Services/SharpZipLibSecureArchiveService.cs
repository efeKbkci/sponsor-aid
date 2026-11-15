using ICSharpCode.SharpZipLib.Zip;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Models;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Creates AES-256 encrypted zip archive packages containing application documents and metadata.
/// </summary>
public class SharpZipLibSecureArchiveService : ISecureArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<(byte[] ArchiveBytes, string Password)> CreateEncryptedPackageAsync(
        RegistrationDraftState draft,
        CancellationToken cancellationToken = default)
    {
        string password = GenerateSecurePassword(16);

        using var memoryStream = new MemoryStream();
        using (var zipStream = new ZipOutputStream(memoryStream))
        {
            // Keeps the underlying memory stream open when the zip stream finishes writing.
            zipStream.IsStreamOwner = false;

            zipStream.SetLevel(6);
            zipStream.Password = password;

            // Extracts metadata and application payload representations from the draft.
            var (metaModel, dataModel) = draft.ExportRegistrationData();

            var metaJson = JsonSerializer.SerializeToUtf8Bytes(metaModel, JsonOptions);
            var dataJson = JsonSerializer.SerializeToUtf8Bytes(dataModel, JsonOptions);

            await AddEntryToZipAsync(zipStream, $"meta_{draft.RegistrationKey}.json", metaJson, cancellationToken);
            await AddEntryToZipAsync(zipStream, $"data_{draft.RegistrationKey}.json", dataJson, cancellationToken);

            var documentPaths = CollectDocumentPaths(draft);
            foreach (var (archiveEntryName, diskPath) in documentPaths)
            {
                if (!string.IsNullOrWhiteSpace(diskPath) && File.Exists(diskPath))
                {
                    var fileBytes = await File.ReadAllBytesAsync(diskPath, cancellationToken);
                    await AddEntryToZipAsync(zipStream, archiveEntryName, fileBytes, cancellationToken);
                }
            }

            // Finalizes central directory records before reading memory buffer.
            zipStream.Finish();
        }

        return (memoryStream.ToArray(), password);
    }

    private static async Task AddEntryToZipAsync(
        ZipOutputStream zipStream,
        string entryName,
        byte[] data,
        CancellationToken cancellationToken)
    {
        var entry = new ZipEntry(ZipEntry.CleanName(entryName))
        {
            DateTime = DateTime.UtcNow,
            Size = data.Length,
            // Applies AES-256 bit encryption to satisfy data protection standards.
            AESKeySize = 256
        };

        zipStream.PutNextEntry(entry);
        await zipStream.WriteAsync(data, cancellationToken);
        zipStream.CloseEntry();
    }

    private static Dictionary<string, string?> CollectDocumentPaths(RegistrationDraftState draft)
    {
        var paths = new Dictionary<string, string?>
        {
            { "documents/NufusKayitOrnegi.pdf", draft.SystemDocuments.PopulationRegistryDocumentPath },
            { "documents/IkametgahBelgesi.pdf", draft.SystemDocuments.ResidenceDocumentPath },
            { "documents/Vasi_Kimlik.pdf", draft.Guardian.IdentityDocumentPath }
        };

        int childIndex = 1;
        foreach (var child in draft.Children)
        {
            // Provides fallback name and sanitizes illegal characters for file path safety.
            string safeNameBase = child.Name ?? "Isimsiz";
            string childSafeName = string.Join("_", safeNameBase.Split(Path.GetInvalidFileNameChars()));

            paths.Add($"documents/Cocuk_{childIndex}_{childSafeName}_Kimlik.pdf", child.IdentityDocumentPath);
            childIndex++;
        }

        return paths;
    }

    private static string GenerateSecurePassword(int length)
    {
        const string validChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        return RandomNumberGenerator.GetString(validChars, length);
    }
}
