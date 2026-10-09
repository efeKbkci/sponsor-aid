using ICSharpCode.SharpZipLib.Zip;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SponsorAid.Infrastructure.Services;

public class SharpZipLibSecureArchiveService : ISecureArchiveService
{
    // 🚀 GÜNCELLENEN AYARLAR
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // 1. Türkçe karakterlerin \u015F yerine doğrudan 'ş, ğ, ı, ö, ü, ç' yazılmasını sağlar:
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 2. Enum değerlerinin 1, 2 yerine "Mother", "Single", "Completed" gibi string yazılmasını sağlar:
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public async Task<(byte[] ArchiveBytes, string Password)> CreateEncryptedPackageAsync(
        RegistrationDraftState draft,
        CancellationToken cancellationToken = default)
    {
        string password = GenerateSecurePassword(16);

        using var memoryStream = new MemoryStream();
        using (var zipStream = new ZipOutputStream(memoryStream))
        {
            zipStream.SetLevel(6);
            zipStream.Password = password;

            // JSON Serileştirme (Artık UTF-8 ve metin enum'lar ile yazılır)
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(draft, JsonOptions);
            await AddEntryToZipAsync(zipStream, $"basvuru_{draft.RegistrationKey}.json", jsonBytes, cancellationToken);

            var documentPaths = CollectDocumentPaths(draft);
            foreach (var (archiveEntryName, diskPath) in documentPaths)
            {
                if (!string.IsNullOrWhiteSpace(diskPath) && File.Exists(diskPath))
                {
                    var fileBytes = await File.ReadAllBytesAsync(diskPath, cancellationToken);
                    await AddEntryToZipAsync(zipStream, archiveEntryName, fileBytes, cancellationToken);
                }
            }

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
            { "belgeler/NufusKayitOrnegi.pdf", draft.SystemDocuments.PopulationRegistryDocumentPath },
            { "belgeler/IkametgahBelgesi.pdf", draft.SystemDocuments.ResidenceDocumentPath },
            { "belgeler/Vasi_Kimlik.pdf", draft.Guardian.IdentityDocumentPath }
        };

        int childIndex = 1;
        foreach (var child in draft.Children)
        {
            string childSafeName = string.Join("_", child.Name.Split(Path.GetInvalidFileNameChars()));
            paths.Add($"belgeler/Cocuk_{childIndex}_{childSafeName}_Kimlik.pdf", child.IdentityDocumentPath);
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