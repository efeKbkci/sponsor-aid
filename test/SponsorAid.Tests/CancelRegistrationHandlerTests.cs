using Moq;
using Xunit;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;

namespace SponsorAid.Tests;

public class CancelRegistrationHandlerTests
{
    private readonly Mock<IDraftStateRepository> _draftRepositoryMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IUserNotifier> _notifierMock;
    private readonly Mock<IStringLocalizer<BotMessages>> _localizerMock;
    private readonly CancelRegistrationHandler _handler;

    public CancelRegistrationHandlerTests()
    {
        _draftRepositoryMock = new Mock<IDraftStateRepository>();
        _fileStorageMock = new Mock<IFileStorageService>();
        _notifierMock = new Mock<IUserNotifier>();
        _localizerMock = new Mock<IStringLocalizer<BotMessages>>();

        // Localizer'ın sahte dönüş değerini ayarlıyoruz (Resource dosyası kullanıyormuş gibi)
        _localizerMock.Setup(l => l["Success_RegistrationCancelled"])
            .Returns(new LocalizedString("Success_RegistrationCancelled", "Kayıt başarıyla iptal edildi."));

        _handler = new CancelRegistrationHandler(
            _draftRepositoryMock.Object,
            _fileStorageMock.Object,
            _notifierMock.Object,
            _localizerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDraftExists_ShouldDeleteFilesAndDraftAndNotifyUser()
    {
        // Arrange (Hazırlık)
        var command = new CancelRegistrationCommand("TEST_KEY_123", 987654321);

        // Test için içi dosya yollarıyla doldurulmuş sahte bir taslak oluşturuyoruz
        // Projendeki constructor yapılarına göre nesne başlatma (init) kısımlarını kendi domain'ine uydurabilirsin.
        var fakeDraft = new RegistrationDraftState
        {
            RegistrationKey = "TEST_KEY_123",
            ChatId = 987654321,
            SystemDocuments = new SystemDocumentsDraft
            {
                PopulationRegistryDocumentPath = "/uploads/nufus.pdf",
                ResidenceDocumentPath = "/uploads/ikametgah.pdf",
                IdentityFrontPath = "/uploads/on.jpg",
                IdentityBackPath = "/uploads/arka.jpg",
                MergedIdentityPath = "/uploads/birlestirilmis.pdf"
            },
            Guardian = new GuardianDraft
            {
                IdentityDocumentPath = "/uploads/vasi_kimlik.pdf"
            },
            Children = new List<ChildDraft>
            {
                new() { IdentityDocumentPath = "/uploads/cocuk1_kimlik.pdf" },
                new() { IdentityDocumentPath = "/uploads/cocuk2_kimlik.pdf" }
            }
        };

        _draftRepositoryMock
            .Setup(repo => repo.GetAsync(command.RegistrationKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeDraft);

        // Act (Eylem)
        await _handler.Handle(command, CancellationToken.None);

        // Assert (Doğrulama)

        // 1. Fiziksel dosyaların hepsi için DeleteFile metodunun tetiklendiğini doğrula
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/nufus.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/ikametgah.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/on.jpg"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/arka.jpg"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/birlestirilmis.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/vasi_kimlik.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/cocuk1_kimlik.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/cocuk2_kimlik.pdf"), Times.Once);

        // Toplamda tam olarak 8 dosya silme isteği atılmış olmalı
        _fileStorageMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Exactly(8));

        // 2. Veritabanından taslağın tamamen silindiğini doğrula
        _draftRepositoryMock.Verify(repo => repo.DeleteAsync(command.RegistrationKey, It.IsAny<CancellationToken>()), Times.Once);

        // 3. Kullanıcıya başarı mesajının gönderildiğini doğrula
        _notifierMock.Verify(n => n.SendMessageAsync(command.ChatId, "Kayıt başarıyla iptal edildi."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDraftDoesNotExist_ShouldNotDeleteAnythingButNotifyUser()
    {
        // Arrange (Hazırlık)
        var command = new CancelRegistrationCommand("NON_EXISTING_KEY", 987654321);

        // Taslak veritabanında bulunamadığı için Null dönecek
        _draftRepositoryMock
            .Setup(repo => repo.GetAsync(command.RegistrationKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegistrationDraftState?)null);

        // Act (Eylem)
        await _handler.Handle(command, CancellationToken.None);

        // Assert (Doğrulama)

        // 1. Taslak olmadığı için fiziksel dosya silme operasyonu HİÇ çalışmamalı
        _fileStorageMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);

        // 2. Veritabanından silme işlemi (DeleteAsync) HİÇ çalışmamalı
        _draftRepositoryMock.Verify(repo => repo.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // 3. Sistemde zaten taslak yoksa bile, kullanıcının komutu askıda kalmasın diye yine de "İptal edildi" mesajı atılmalı
        _notifierMock.Verify(n => n.SendMessageAsync(command.ChatId, "Kayıt başarıyla iptal edildi."), Times.Once);
    }
}