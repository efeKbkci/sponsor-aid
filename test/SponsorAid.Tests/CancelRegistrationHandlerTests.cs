using Moq;
using Xunit;
using Microsoft.Extensions.Localization;
using SponsorAid.Application.Features.Registrations.Commands.CancelRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Application.Models;
using SponsorAid.Application.Resources;

namespace SponsorAid.Tests;

/// <summary>
/// Unit tests for <see cref="CancelRegistrationHandler"/>.
/// Verifies draft removal from repository, physical file cleanup from storage,
/// and user notifications when canceling active or missing registrations.
/// </summary>
public class CancelRegistrationHandlerTests
{
    private readonly Mock<IDraftStateRepository> _draftRepositoryMock;
    private readonly Mock<IFileStorageService> _fileStorageMock;
    private readonly Mock<IUserNotifier> _notifierMock;
    private readonly Mock<IStringLocalizer<BotMessages>> _localizerMock;
    private readonly Mock<IRegistrationTrackingRepository> _trackingRepositoryMock;
    private readonly CancelRegistrationHandler _handler;

    public CancelRegistrationHandlerTests()
    {
        _draftRepositoryMock = new Mock<IDraftStateRepository>();
        _fileStorageMock = new Mock<IFileStorageService>();
        _notifierMock = new Mock<IUserNotifier>();
        _localizerMock = new Mock<IStringLocalizer<BotMessages>>();
        _trackingRepositoryMock = new Mock<IRegistrationTrackingRepository>();

        // Mock string localizer response to simulate localized message resource lookup
        _localizerMock.Setup(l => l["Success_RegistrationCancelled"])
            .Returns(new LocalizedString("Success_RegistrationCancelled", "Kayıt başarıyla iptal edildi."));

        _handler = new CancelRegistrationHandler(
            _draftRepositoryMock.Object,
            _trackingRepositoryMock.Object,
            _fileStorageMock.Object,
            _notifierMock.Object,
            _localizerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDraftExists_ShouldDeleteFilesAndDraftAndNotifyUser()
    {
        // Arrange
        var command = new CancelRegistrationCommand("TEST_KEY_123", 987654321);

        // Populate a draft with multiple document paths to test physical cleanup logic
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

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Verify file deletion was requested for each uploaded document path
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/nufus.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/ikametgah.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/on.jpg"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/arka.jpg"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/birlestirilmis.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/vasi_kimlik.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/cocuk1_kimlik.pdf"), Times.Once);
        _fileStorageMock.Verify(fs => fs.DeleteFile("/uploads/cocuk2_kimlik.pdf"), Times.Once);

        // Verify total file deletion count matches all 8 paths
        _fileStorageMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Exactly(8));

        // Verify draft record was removed from repository
        _draftRepositoryMock.Verify(repo => repo.DeleteAsync(command.RegistrationKey, It.IsAny<CancellationToken>()), Times.Once);

        // Verify user received the cancellation confirmation notification
        _notifierMock.Verify(n => n.SendMessageAsync(command.ChatId, "Kayıt başarıyla iptal edildi."), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDraftDoesNotExist_ShouldNotDeleteAnythingButNotifyUser()
    {
        // Arrange
        var command = new CancelRegistrationCommand("NON_EXISTING_KEY", 987654321);

        // Simulate scenario where no active draft exists in repository
        _draftRepositoryMock
            .Setup(repo => repo.GetAsync(command.RegistrationKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegistrationDraftState?)null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        // No storage deletion calls should occur when no draft exists
        _fileStorageMock.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Never);

        // No repository deletion call should occur when draft was not found
        _draftRepositoryMock.Verify(repo => repo.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // User should still receive notification to acknowledge cancellation request
        _notifierMock.Verify(n => n.SendMessageAsync(command.ChatId, "Kayıt başarıyla iptal edildi."), Times.Once);
    }
}
