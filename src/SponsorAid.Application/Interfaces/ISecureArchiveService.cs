using System.Threading;
using System.Threading.Tasks;
using SponsorAid.Application.Models;

namespace SponsorAid.Application.Interfaces;

public interface ISecureArchiveService
{
    // Verileri ve diskteki belgeleri AES-256 ile şifrelenmiş ZIP byte dizisine çevirir ve şifreyi döner.
    Task<(byte[] ArchiveBytes, string Password)> CreateEncryptedPackageAsync(
        RegistrationDraftState draft, 
        CancellationToken cancellationToken = default);
}
