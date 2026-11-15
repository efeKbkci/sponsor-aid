using System.Threading;
using System.Threading.Tasks;
using SponsorAid.Application.Models;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Bundles draft details and documents into an encrypted archive to protect sensitive personal data.
/// </summary>
public interface ISecureArchiveService
{
    /// <summary>
    /// Compiles registration details and stored files into an AES-256 encrypted ZIP package.
    /// </summary>
    /// <param name="draft">Draft state containing personal details and document references.</param>
    /// <param name="cancellationToken">Token to cancel the archive creation process.</param>
    /// <returns>A tuple containing the archive byte array and the generated encryption password.</returns>
    Task<(byte[] ArchiveBytes, string Password)> CreateEncryptedPackageAsync(
        RegistrationDraftState draft, 
        CancellationToken cancellationToken = default);
}
