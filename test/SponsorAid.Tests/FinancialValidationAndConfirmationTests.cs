using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Infrastructure.Persistence;
using Xunit;

namespace SponsorAid.Tests;

/// <summary>
/// Black-box regression test suite for fail-fast financial questionnaire validation
/// (FamilyMemberCount, Income, Expense) and confirmation button preservation on finalization failures.
/// </summary>
[Collection("IntegrationTests")]
public class FinancialValidationAndConfirmationTests
{
    private const string ValidIban = "TR330006100519786457841326";
    private const string ValidGuardianTc = "10000000146";
    private const string ValidChildTc = "20000000114";

    private readonly SponsorAidTestFixture _fixture;
    private readonly HttpClient _client;

    public FinancialValidationAndConfirmationTests(SponsorAidTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    #region Helpers

    private async Task<HttpResponseMessage> SendTelegramMessageAsync(long chatId, string text)
    {
        var payload = new
        {
            message = new
            {
                message_id = Random.Shared.Next(1000, 9999),
                chat = new { id = chatId },
                text
            }
        };

        return await _client.PostAsJsonAsync("/api/telegram/webhook", payload);
    }

    private async Task<HttpResponseMessage> SendTelegramCallbackAsync(long chatId, string data, int messageId)
    {
        var payload = new
        {
            callback_query = new
            {
                id = Guid.NewGuid().ToString("N"),
                chat_instance = Guid.NewGuid().ToString("N"),
                message = new
                {
                    message_id = messageId,
                    chat = new { id = chatId }
                },
                data
            }
        };

        return await _client.PostAsJsonAsync("/api/telegram/webhook", payload);
    }

    private async Task SeedCompleteBaseDraftAsync(
        long chatId,
        string registrationKey,
        RegistrationStep step,
        Action<RegistrationDraftState>? customize = null)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();

        var state = new RegistrationDraftState
        {
            RegistrationKey = registrationKey,
            ChatId = chatId,
            CurrentStep = step,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Guardian = new GuardianDraft
            {
                Name = "AYŞE YILMAZ",
                IdNumber = new IdNumber(ValidGuardianTc),
                BirthDate = new DateOnly(1985, 5, 10),
                ApplicantRole = ApplicantRole.Mother,
                Gender = Gender.Female,
                MaritalStatus = MaritalStatus.Widowed,
                EducationLevel = EducationLevel.HighSchool,
                EducationStatus = EducationStatus.Graduated,
                DegreeOfKinship = DegreeOfKinship.Mother,
                Profession = "Ev Hanımı",
                PhoneNumber = new PhoneNumber("05551234567"),
                Iban = new Iban(ValidIban),
                IdentityDocumentPath = "uploads/vasi_merged.pdf"
            },
            Father = new FatherDraft
            {
                Name = "MEHMET YILMAZ",
                DeathDate = new DateOnly(2023, 2, 6),
                Profession = FatherProfession.Worker,
                DeathReason = FatherDeathReason.NaturalDeath
            },
            FamilyFinancial = new FamilyFinancialDraft
            {
                FamilyMemberCount = 3,
                HousingStatus = HousingStatus.Rent,
                SocialSecurity = SocialSecurity.Sgk,
                IncomeStatus = IncomeStatus.FixedSalary,
                Income = 15000,
                Expense = 12000
            },
            DisabledMember = new DisabledMemberDraft
            {
                Name = "Yok"
            },
            ResidenceAddress = new AddressDraft
            {
                Country = "Türkiye",
                City = "İSTANBUL",
                District = "KADIKÖY",
                Neighborhood = "CAFERAĞA MAH.",
                FullAddress = "CAFERAĞA MAH. MODA CAD. NO:1 KADIKÖY / İSTANBUL"
            },
            SystemDocuments = new SystemDocumentsDraft
            {
                PopulationRegistryDocumentPath = "uploads/pop.pdf",
                ResidenceDocumentPath = "uploads/res.pdf",
                BankAccountDocumentPath = "uploads/bank.pdf",
                Petition = new Petition(new string('A', 350))
            },
            Children =
            [
                new ChildDraft
                {
                    Name = "ALİ YILMAZ",
                    IdNumber = new IdNumber(ValidChildTc),
                    BirthDate = new DateOnly(2018, 1, 1),
                    Hobbies = "Kitap okumak",
                    FutureDream = "Öğretmen olmak",
                    IdentityDocumentPath = "uploads/cocuk_merged.pdf"
                }
            ]
        };

        customize?.Invoke(state);

        db.RegistrationTrackings.Add(new RegistrationTrackingRecord
        {
            RegistrationKey = registrationKey,
            ChatId = chatId,
            GuardianName = state.Guardian.Name,
            Status = RegistrationStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Drafts.Add(new DraftRecord
        {
            RegistrationKey = registrationKey,
            ChatId = chatId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            JsonData = JsonSerializer.Serialize(state)
        });

        await db.SaveChangesAsync();
    }

    private async Task<RegistrationDraftState> GetDraftStateAsync(string registrationKey)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
        var record = await db.Drafts.AsNoTracking().SingleAsync(d => d.RegistrationKey == registrationKey);
        return JsonSerializer.Deserialize<RegistrationDraftState>(record.JsonData)!;
    }

    #endregion

    [Fact]
    public async Task WaitingForQuestions_InvalidFinancialValues_ShouldFailFastAndAcceptZeroIncomeAndValidValues()
    {
        // Arrange: Seed draft in WaitingForQuestions with FamilyMemberCount, Income, and Expense unanswered
        _fixture.ResetMocks();
        const long chatId = 300001L;
        const string registrationKey = "FIN001";

        await SeedCompleteBaseDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForQuestions, draft =>
        {
            draft.FamilyFinancial.FamilyMemberCount = null;
            draft.FamilyFinancial.Income = null;
            draft.FamilyFinancial.Expense = null;
            draft.SystemDocuments.Petition = null;
        });

        // --- Stage A: FamilyMemberCount fail-fast validation (0 and -2 rejected; 3 accepted) ---
        await SendTelegramMessageAsync(chatId, "0");
        var afterZeroMembers = await GetDraftStateAsync(registrationKey);
        afterZeroMembers.FamilyFinancial.FamilyMemberCount.Should().BeNull();
        afterZeroMembers.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        await SendTelegramMessageAsync(chatId, "-2");
        var afterNegMembers = await GetDraftStateAsync(registrationKey);
        afterNegMembers.FamilyFinancial.FamilyMemberCount.Should().BeNull();
        afterNegMembers.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("0 veya negatif olamaz"))),
            Times.Exactly(2));

        await SendTelegramMessageAsync(chatId, "3");
        var afterValidMembers = await GetDraftStateAsync(registrationKey);
        afterValidMembers.FamilyFinancial.FamilyMemberCount.Should().Be(3);
        afterValidMembers.FamilyFinancial.Income.Should().BeNull();
        afterValidMembers.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        // --- Stage B: Income fail-fast validation (-1000 rejected; 0 accepted as valid zero income) ---
        _fixture.ResetMocks();

        await SendTelegramMessageAsync(chatId, "-1000");
        var afterNegIncome = await GetDraftStateAsync(registrationKey);
        afterNegIncome.FamilyFinancial.Income.Should().BeNull();
        afterNegIncome.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("negatif olamaz"))),
            Times.Once());

        await SendTelegramMessageAsync(chatId, "0");
        var afterZeroIncome = await GetDraftStateAsync(registrationKey);
        afterZeroIncome.FamilyFinancial.Income.Should().Be(0,
            "Geliri olmayan haneler için 0 TL aylık gelir geçerli kabul edilmelidir.");
        afterZeroIncome.FamilyFinancial.Expense.Should().BeNull();
        afterZeroIncome.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        // --- Stage C: Expense fail-fast validation (-500 rejected; 12000 accepted & transitions to confirmation) ---
        _fixture.ResetMocks();

        await SendTelegramMessageAsync(chatId, "-500");
        var afterNegExpense = await GetDraftStateAsync(registrationKey);
        afterNegExpense.FamilyFinancial.Expense.Should().BeNull();
        afterNegExpense.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("negatif olamaz"))),
            Times.Once());

        await SendTelegramMessageAsync(chatId, "12000");
        var afterValidExpense = await GetDraftStateAsync(registrationKey);
        afterValidExpense.FamilyFinancial.Expense.Should().Be(12000);
        afterValidExpense.SystemDocuments.Petition.Should().NotBeNull();
        afterValidExpense.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);
    }

    [Fact]
    public async Task FinalizeRegistration_WhenDomainValidationFails_ShouldKeepConfirmationButtonsIntact()
    {
        // Arrange: Seed a pre-existing corrupt draft at WaitingForConfirmation with negative Income
        _fixture.ResetMocks();
        const long chatId = 300002L;
        const string registrationKey = "FIN002";
        const int confirmationMsgId = 777;

        await SeedCompleteBaseDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForConfirmation, draft =>
        {
            draft.FamilyFinancial.Income = -100;
        });

        // Act
        var response = await SendTelegramCallbackAsync(chatId, "confirm_registration", confirmationMsgId);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _fixture.RegistrationDispatchMock.Verify(
            x => x.SendEncryptedPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never());

        _fixture.UserNotifierMock.Verify(
            x => x.RemoveButtonsAsync(chatId, It.IsAny<int>()),
            Times.Never(),
            "Doğrulama hatası durumunda onay butonu silinmemeli ve kullanıcı kilitlenmemelidir.");

        var state = await GetDraftStateAsync(registrationKey);
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("negatif olamaz"))),
            Times.Once());
    }

    [Fact]
    public async Task FinalizeRegistration_WhenDispatchFailsTransiently_ShouldKeepButtonsAndSucceedOnRetry()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 300003L;
        const string registrationKey = "FIN003";
        const int confirmationMsgId = 888;

        var tempBankPath = Path.Combine(Path.GetTempPath(), $"sponsoraid_fin003_{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tempBankPath, Encoding.UTF8.GetBytes("%PDF-1.4\n%FIN003\n"));

        try
        {
            await SeedCompleteBaseDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForConfirmation, draft =>
            {
                draft.SystemDocuments.BankAccountDocumentPath = tempBankPath;
            });

            _fixture.RegistrationDispatchMock
                .SetupSequence(x => x.SendEncryptedPackageAsync(
                    registrationKey,
                    "AYŞE YILMAZ",
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("SMTP timeout"))
                .Returns(Task.CompletedTask);

            // --- Act 1: First confirmation click hits transient SMTP failure ---
            var response1 = await SendTelegramCallbackAsync(chatId, "confirm_registration", confirmationMsgId);
            response1.StatusCode.Should().Be(HttpStatusCode.OK);

            // --- Assert 1: Buttons preserved, draft still in WaitingForConfirmation, system error sent ---
            _fixture.UserNotifierMock.Verify(
                x => x.RemoveButtonsAsync(chatId, It.IsAny<int>()),
                Times.Never(),
                "Geçici SMTP hatasında onay butonu kaldırılmamalı, yeniden denemeye açık kalmalıdır.");

            var stateAfterFailure = await GetDraftStateAsync(registrationKey);
            stateAfterFailure.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);

            _fixture.UserNotifierMock.Verify(
                x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("teknik bir sorun"))),
                Times.Once());

            // --- Act 2: User clicks the exact same confirmation button again ---
            var response2 = await SendTelegramCallbackAsync(chatId, "confirm_registration", confirmationMsgId);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            // --- Assert 2: Dispatch succeeds, buttons removed once, draft deleted, tracking completed ---
            _fixture.RegistrationDispatchMock.Verify(
                x => x.SendEncryptedPackageAsync(registrationKey, "AYŞE YILMAZ", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _fixture.RegistrationDispatchMock.Verify(
                x => x.SendPasswordAsync(registrationKey, "AYŞE YILMAZ", It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once());
            _fixture.UserNotifierMock.Verify(
                x => x.RemoveButtonsAsync(chatId, confirmationMsgId),
                Times.Once());

            using (var scope = _fixture.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
                var draftExists = await db.Drafts.AsNoTracking().AnyAsync(d => d.RegistrationKey == registrationKey);
                draftExists.Should().BeFalse();

                var tracking = await db.RegistrationTrackings.AsNoTracking().SingleAsync(t => t.RegistrationKey == registrationKey);
                tracking.Status.Should().Be(RegistrationStatus.Completed);
            }
        }
        finally
        {
            if (File.Exists(tempBankPath))
            {
                File.Delete(tempBankPath);
            }
        }
    }
}
