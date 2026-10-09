using System.Threading;
using System.Threading.Tasks;

namespace SponsorAid.Application.Interfaces;

public interface IRegistrationDispatchService
{
    // Şifreli arşivi e-posta ile A kişisine gönderir
    Task SendEncryptedPackageAsync(
        string registrationKey, 
        string guardianName, 
        byte[] packageBytes, 
        CancellationToken cancellationToken = default);

    // Arşiv şifresini ayrı bir güvenli kanal üzerinden (örn: Telegram Admin / Yetkili Chat) A kişisine iletir
    Task SendPasswordAsync(
        string registrationKey, 
        string guardianName, 
        string password, 
        CancellationToken cancellationToken = default);
}
