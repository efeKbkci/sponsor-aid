using System.Threading;
using System.Threading.Tasks;

namespace SponsorAid.Application.Interfaces;

/// <summary>
/// Dispatches finalized applicant packages and passwords across separate channels to protect privacy.
/// </summary>
public interface IRegistrationDispatchService
{
    /// <summary>
    /// Transmits the encrypted application archive to the designated reviewer over email.
    /// </summary>
    /// <param name="registrationKey">Unique identifier of the registration session.</param>
    /// <param name="guardianName">Full name of the applicant guardian.</param>
    /// <param name="packageBytes">Raw bytes of the encrypted archive file.</param>
    /// <param name="cancellationToken">Token to cancel the transmission task.</param>
    /// <returns>A task representing the dispatch process.</returns>
    Task SendEncryptedPackageAsync(
        string registrationKey, 
        string guardianName, 
        byte[] packageBytes, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the archive decryption password through an independent secure channel to prevent interception.
    /// </summary>
    /// <param name="registrationKey">Unique identifier of the registration session.</param>
    /// <param name="guardianName">Full name of the applicant guardian.</param>
    /// <param name="password">Decryption password for the archive package.</param>
    /// <param name="cancellationToken">Token to cancel the transmission task.</param>
    /// <returns>A task representing the password delivery process.</returns>
    Task SendPasswordAsync(
        string registrationKey, 
        string guardianName, 
        string password, 
        CancellationToken cancellationToken = default);
}
