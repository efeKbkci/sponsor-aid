using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;
using SponsorAid.Application.Features.Registrations.Common;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Infrastructure.Persistence;
using SponsorAid.Integration.Contracts;
using Xunit;

namespace SponsorAid.Tests;

/// <summary>
/// Black-box integration test suite covering all edge cases, failure rollbacks, ambiguity fallbacks,
/// manual IBAN validation, resume/update guards, button-only question enforcement, and finalization rules
/// for the Bank Account Document Upload &amp; IBAN Extraction workflow.
/// </summary>
[Collection("IntegrationTests")]
public class BankAccountStepJourneyTests
{
    private const string ValidIban1 = "TR330006100519786457841326";
    private const string ValidIban2 = "TR730006200001234567890123";
    private const string InvalidMod97Iban = "TR330006100519786457841327";
    private const string ValidGuardianTc = "10000000146";
    private const string ValidChildTc = "20000000114";

    private readonly SponsorAidTestFixture _fixture;
    private readonly HttpClient _client;

    public BankAccountStepJourneyTests(SponsorAidTestFixture fixture)
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

    private async Task<HttpResponseMessage> SendTelegramCallbackAsync(long chatId, string data, int messageId = 10)
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

    private async Task<HttpResponseMessage> SendTelegramDocumentAsync(long chatId, string fileId, long fileSize, string fileName)
    {
        var payload = new
        {
            message = new
            {
                message_id = Random.Shared.Next(1000, 9999),
                chat = new { id = chatId },
                document = new
                {
                    file_id = fileId,
                    file_size = fileSize,
                    file_name = fileName
                }
            }
        };

        return await _client.PostAsJsonAsync("/api/telegram/webhook", payload);
    }

    private async Task SeedDraftAsync(
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
                IdentityDocumentPath = "uploads/vasi_merged.pdf"
            },
            Father = new FatherDraft
            {
                Name = "MEHMET YILMAZ",
                DeathDate = new DateOnly(2023, 2, 6)
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
                ResidenceDocumentPath = "uploads/res.pdf"
            },
            Children =
            [
                new ChildDraft
                {
                    Name = "ALİ YILMAZ",
                    IdNumber = new IdNumber(ValidChildTc),
                    BirthDate = new DateOnly(2018, 1, 1),
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
    public async Task WaitingForBankAccountDocument_WhenInvalidBinaryOrPlainTextSent_ShouldRejectBeforeMutatingStepOrCallingStorageAndQueue()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200001L;
        const string registrationKey = "BNK001";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForBankAccountDocument);

        // Act 1: Oversized PDF (> 5 MB)
        var resLarge = await SendTelegramDocumentAsync(chatId, "oversized_bank", 6 * 1024 * 1024, "dekont.pdf");
        // Act 2: Unsupported extension (.exe)
        var resExt = await SendTelegramDocumentAsync(chatId, "bad_ext_bank", 10240, "iban.exe");
        // Act 3: Plain text instead of document
        var resText = await SendTelegramMessageAsync(chatId, "IBANım yok");

        // Assert
        resLarge.StatusCode.Should().Be(HttpStatusCode.OK);
        resExt.StatusCode.Should().Be(HttpStatusCode.OK);
        resText.StatusCode.Should().Be(HttpStatusCode.OK);

        var state = await GetDraftStateAsync(registrationKey);
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForBankAccountDocument,
            "Geçersiz boyut, uzantı veya metin girişinde adım ProcessingBankAccountDocument durumuna geçmemelidir.");
        state.SystemDocuments.BankAccountDocumentPath.Should().BeNull();

        _fixture.FileStorageMock.Verify(
            x => x.DownloadAndSaveAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
        _fixture.EventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<string>(), It.IsAny<WorkerTaskEnvelope>()),
            Times.Never());

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("5 MB"))),
            Times.Once());
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("Desteklenmeyen dosya türü"))),
            Times.Once());
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("belge yükleme aşamasındasınız"))),
            Times.Once());
    }

    [Fact]
    public async Task ProcessingBankAccountDocument_WhenDuplicateUploadArrives_ShouldKeepProcessingStepAndSkipStorageAndQueue()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200002L;
        const string registrationKey = "BNK002";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.ProcessingBankAccountDocument);

        // Act
        var response = await SendTelegramDocumentAsync(chatId, "dup_bank_doc", 50000, "dekont2.pdf");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var state = await GetDraftStateAsync(registrationKey);
        state.CurrentStep.Should().Be(RegistrationStep.ProcessingBankAccountDocument);

        _fixture.FileStorageMock.Verify(
            x => x.DownloadAndSaveAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
        _fixture.EventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<string>(), It.IsAny<WorkerTaskEnvelope>()),
            Times.Never());
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("önceki belgeniz üzerinde çalışıyor"))),
            Times.Once());
    }

    [Fact]
    public async Task ProcessingBankAccountDocument_WhenWorkerReportsTechnicalFailure_ShouldRevertToWaitingAndKeepPathsNull()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200003L;
        const string registrationKey = "BNK003";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.ProcessingBankAccountDocument);

        // Act
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "BankAccount",
                IsSuccess: false,
                ErrorMessage: "Bozuk dosya",
                SavedFilePath: "uploads/bad.pdf"));
        }

        // Assert
        var state = await GetDraftStateAsync(registrationKey);
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForBankAccountDocument);
        state.SystemDocuments.BankAccountDocumentPath.Should().BeNull();
        state.Guardian.Iban.Should().BeNull();

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("Bozuk dosya"))),
            Times.Once());
    }

    [Fact]
    public async Task ProcessingBankAccountDocument_WhenWorkerSucceedsWithoutExtractedIban_ShouldSaveFileAndTransitionToWaitingForIban()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200004L;
        const string registrationKey = "BNK004";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.ProcessingBankAccountDocument);

        // Act
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "BankAccount",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/bank_no_iban.jpg",
                ExtractedIban: null));
        }

        // Assert
        var state = await GetDraftStateAsync(registrationKey);
        state.SystemDocuments.BankAccountDocumentPath.Should().Be("uploads/bank_no_iban.jpg");
        state.Guardian.Iban.Should().BeNull();
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("kaydedildi") && m.Contains("IBAN"))),
            Times.Once());
    }

    [Fact]
    public async Task ProcessingBankAccountDocument_WhenWorkerReturnsInvalidMod97Iban_ShouldSaveFileWithoutRollbackAndTransitionToWaitingForIban()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200005L;
        const string registrationKey = "BNK005";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.ProcessingBankAccountDocument);

        // Act
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "BankAccount",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/bank_invalid_crc.pdf",
                ExtractedIban: InvalidMod97Iban));
        }

        // Assert
        var state = await GetDraftStateAsync(registrationKey);
        state.SystemDocuments.BankAccountDocumentPath.Should().Be("uploads/bank_invalid_crc.pdf",
            "MOD-97 hatası exception fırlatıp dosyayı silmemeli, belge korunmalıdır.");
        state.Guardian.Iban.Should().BeNull();
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForIban,
            "Geçersiz MOD-97 IBAN durumunda WaitingForBankAccountDocument adımına geri dönülmemeli, WaitingForIban adımına geçilmelidir.");
    }

    [Fact]
    public async Task WaitingForIban_ManualEntryFlow_ShouldRejectInvalidInputsAndBinaryThenAcceptValidSpacedIban()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200006L;
        const string registrationKey = "BNK006";
        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForIban, draft =>
        {
            draft.SystemDocuments.BankAccountDocumentPath = "uploads/bank_saved.pdf";
        });

        // Act 1: Short invalid IBAN ("TR0000")
        await SendTelegramMessageAsync(chatId, "TR0000");
        var afterShort6 = await GetDraftStateAsync(registrationKey);
        afterShort6.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        afterShort6.Guardian.Iban.Should().BeNull();

        // Act 2: Invalid length IBAN ("TR12345") triggering direct Iban format exception
        await SendTelegramMessageAsync(chatId, "TR12345");
        var afterInvalidFormat = await GetDraftStateAsync(registrationKey);
        afterInvalidFormat.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        afterInvalidFormat.Guardian.Iban.Should().BeNull();
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("26 karakter"))),
            Times.Once());

        // Act 3: Valid format but invalid MOD-97 checksum
        await SendTelegramMessageAsync(chatId, InvalidMod97Iban);
        var afterInvalidChecksum = await GetDraftStateAsync(registrationKey);
        afterInvalidChecksum.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        afterInvalidChecksum.Guardian.Iban.Should().BeNull();
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("Mod 97"))),
            Times.Once());

        // Act 4: Binary file upload during WaitingForIban
        await SendTelegramDocumentAsync(chatId, "unexpected_doc", 20480, "dekont.pdf");
        var afterBinary = await GetDraftStateAsync(registrationKey);
        afterBinary.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        _fixture.FileStorageMock.Verify(
            x => x.DownloadAndSaveAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never());
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("belge yükleme aşamasında değilsiniz"))),
            Times.Once());

        // Act 5: Valid Turkish IBAN #2 with spaces
        var responseValid = await SendTelegramMessageAsync(chatId, "TR73 0006 2000 0123 4567 8901 23");
        responseValid.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert 5
        var finalState = await GetDraftStateAsync(registrationKey);
        finalState.Guardian.Iban.Should().NotBeNull();
        finalState.Guardian.Iban!.Value.Should().Be(ValidIban2);
        finalState.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains(ValidIban2))),
            Times.Once());
        _fixture.UserNotifierMock.Verify(
            x => x.SendQuestionWithButtonsAsync(
                chatId,
                It.Is<string>(q => q.Contains("yetimin nesi oluyorsunuz")),
                It.Is<Dictionary<string, string>>(opts => opts.ContainsKey("Mother") && opts.ContainsKey("Guardian"))),
            Times.Once());
    }

    [Fact]
    public async Task BankAccountSteps_ResumeAndPrematureUpdateGuards_ShouldPreserveStepAndSendDedicatedPrompts()
    {
        // Arrange — Case A: WaitingForBankAccountDocument
        _fixture.ResetMocks();
        const long chatIdDoc = 200007L;
        const string keyDoc = "BNK007";
        await SeedDraftAsync(chatIdDoc, keyDoc, RegistrationStep.WaitingForBankAccountDocument);

        // Act & Assert A1: Premature /update
        await SendTelegramMessageAsync(chatIdDoc, "/update");
        var stateDocAfterUpdate = await GetDraftStateAsync(keyDoc);
        stateDocAfterUpdate.IsWaitingForUpdateFieldSelection.Should().BeFalse();
        stateDocAfterUpdate.CurrentStep.Should().Be(RegistrationStep.WaitingForBankAccountDocument);
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdDoc, It.Is<string>(m => m.Contains("soru-cevap aşamasına gelmediniz"))),
            Times.Once());

        // Act & Assert A2: Resume via /continue callback and 6-char key
        await SendTelegramCallbackAsync(
            chatIdDoc,
            SponsorAid.Application.Features.Registrations.Commands.ContinueRegistration.ContinueRegistrationCommand.SelectionCallbackPrefix + keyDoc,
            messageId: 41);
        var stateDocAfterContinue = await GetDraftStateAsync(keyDoc);
        stateDocAfterContinue.CurrentStep.Should().Be(RegistrationStep.WaitingForBankAccountDocument);
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdDoc, It.Is<string>(m => m.Contains(keyDoc) && m.Contains("Banka Hesap Bilgilerini"))),
            Times.Once());

        // Arrange — Case B: WaitingForIban
        _fixture.ResetMocks();
        const long chatIdIban = 200008L;
        const string keyIban = "BNK008";
        await SeedDraftAsync(chatIdIban, keyIban, RegistrationStep.WaitingForIban, draft =>
        {
            draft.SystemDocuments.BankAccountDocumentPath = "uploads/bank.pdf";
        });

        // Act & Assert B1: Premature /update
        await SendTelegramMessageAsync(chatIdIban, "/update");
        var stateIbanAfterUpdate = await GetDraftStateAsync(keyIban);
        stateIbanAfterUpdate.IsWaitingForUpdateFieldSelection.Should().BeFalse();
        stateIbanAfterUpdate.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdIban, It.Is<string>(m => m.Contains("soru-cevap aşamasına gelmediniz"))),
            Times.Once());

        // Act & Assert B2: Resume via 6-character registration key
        await SendTelegramMessageAsync(chatIdIban, keyIban);
        var stateIbanAfterContinue = await GetDraftStateAsync(keyIban);
        stateIbanAfterContinue.CurrentStep.Should().Be(RegistrationStep.WaitingForIban);
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdIban, It.Is<string>(m => m.Contains(keyIban) && m.Contains("IBAN Numaranızı"))),
            Times.Once());
    }

    [Fact]
    public async Task FinalizeRegistration_WhenBankAccountDocumentPathIsMissing_ShouldBlockDispatchAndPreserveDraft()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200009L;
        const string registrationKey = "BNK009";

        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForConfirmation, draft =>
        {
            draft.Guardian.ApplicantRole = ApplicantRole.Mother;
            draft.Guardian.Gender = Gender.Female;
            draft.Guardian.MaritalStatus = MaritalStatus.Widowed;
            draft.Guardian.EducationLevel = EducationLevel.HighSchool;
            draft.Guardian.EducationStatus = EducationStatus.Graduated;
            draft.Guardian.DegreeOfKinship = DegreeOfKinship.Mother;
            draft.Guardian.Profession = "Ev Hanımı";
            draft.Guardian.PhoneNumber = new PhoneNumber("05551234567");
            draft.Guardian.Iban = new Iban(ValidIban1);

            draft.Father.Profession = FatherProfession.Worker;
            draft.Father.DeathReason = FatherDeathReason.NaturalDeath;

            draft.FamilyFinancial.FamilyMemberCount = 2;
            draft.FamilyFinancial.HousingStatus = HousingStatus.Rent;
            draft.FamilyFinancial.SocialSecurity = SocialSecurity.Sgk;
            draft.FamilyFinancial.IncomeStatus = IncomeStatus.FixedSalary;
            draft.FamilyFinancial.Income = 15000;
            draft.FamilyFinancial.Expense = 12000;

            draft.DisabledMember = new DisabledMemberDraft { Name = "Yok" };
            draft.Children[0].Hobbies = "Kitap okumak";
            draft.Children[0].FutureDream = "Doktor olmak";
            draft.SystemDocuments.Petition = new Petition(new string('A', 350));

            // Intentionally leave BankAccountDocumentPath null
            draft.SystemDocuments.BankAccountDocumentPath = null;
        });

        // Act
        var response = await SendTelegramCallbackAsync(chatId, "confirm_registration", messageId: 88);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _fixture.RegistrationDispatchMock.Verify(
            x => x.SendEncryptedPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never());

        _fixture.UserNotifierMock.Verify(
            x => x.RemoveButtonsAsync(chatId, It.IsAny<int>()),
            Times.Never(),
            "Zorunlu alan hatasında onay butonu kaldırılmamalıdır.");

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftExists = await db.Drafts.AsNoTracking().AnyAsync(d => d.RegistrationKey == registrationKey);
            draftExists.Should().BeTrue("Zorunlu belge eksik olduğunda taslak silinmemelidir.");

            var tracking = await db.RegistrationTrackings.AsNoTracking().SingleAsync(t => t.RegistrationKey == registrationKey);
            tracking.Status.Should().Be(RegistrationStatus.InProgress);
        }

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatId, It.Is<string>(m => m.Contains("Banka Hesap Belgesi"))),
            Times.Once());
    }

    [Fact]
    public async Task WaitingForQuestions_WhenDegreeOfKinshipAnsweredWithPlainText_ShouldKeepNullAndReSendInlineButtons()
    {
        // Arrange
        _fixture.ResetMocks();
        const long chatId = 200010L;
        const string registrationKey = "BNK010";

        await SeedDraftAsync(chatId, registrationKey, RegistrationStep.WaitingForQuestions, draft =>
        {
            draft.SystemDocuments.BankAccountDocumentPath = "uploads/bank.pdf";
            draft.Guardian.Iban = new Iban(ValidIban1);
            draft.Guardian.ApplicantRole = ApplicantRole.Guardian;
            draft.Guardian.Gender = Gender.Female;
            draft.Guardian.MaritalStatus = MaritalStatus.Single;
            draft.Guardian.EducationLevel = EducationLevel.HighSchool;
            draft.Guardian.EducationStatus = EducationStatus.Graduated;
            // DegreeOfKinship intentionally left null as the active button-only question
            draft.Guardian.DegreeOfKinship = null;
        });

        // Act
        var response = await SendTelegramMessageAsync(chatId, "Teyzesi");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var state = await GetDraftStateAsync(registrationKey);
        state.Guardian.DegreeOfKinship.Should().BeNull(
            "Butonla seçilmesi gereken yakınlık derecesi sorusuna düz metin yazıldığında alan atanmamalıdır.");
        state.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendQuestionWithButtonsAsync(
                chatId,
                It.Is<string>(q => q.Contains("Yetime yakınlık dereceniz nedir")),
                It.Is<Dictionary<string, string>>(opts =>
                    opts.ContainsKey("Mother") &&
                    opts.ContainsKey("MaternalAunt") &&
                    opts.ContainsKey("Other"))),
            Times.Once());
    }

    [Fact]
    public async Task UpdateIban_MidQuestionnaireAndFromConfirmation_ShouldPromptImmediatelySaveNewIbanAndResumeProperly()
    {
        // Verify semantic catalog includes bare "iban" example for >= 0.8 threshold matching
        RegistrationSemanticCatalog.UpdateTargets
            .Single(c => c.Key == "iban")
            .Examples.Should().Contain("iban");

        // ====================================================================
        // PART A: Mid-Questionnaire IBAN Update (after BankAccount extraction)
        // ====================================================================
        _fixture.ResetMocks();
        const long chatIdMid = 200011L;
        const string keyMid = "BNK011";

        _fixture.SemanticMatcherMock
            .Setup(x => x.FindBestMatchAsync(
                "iban",
                It.IsAny<IEnumerable<MatchCandidate>>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemanticMatchResult("iban", 1.0f, false));

        await SeedDraftAsync(chatIdMid, keyMid, RegistrationStep.ProcessingBankAccountDocument);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: keyMid,
                DocumentType: "BankAccount",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/bank_mid.pdf",
                ExtractedIban: ValidIban1));
        }

        var initialMidState = await GetDraftStateAsync(keyMid);
        initialMidState.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);
        initialMidState.Guardian.Iban!.Value.Should().Be(ValidIban1);
        initialMidState.Guardian.ApplicantRole.Should().BeNull();

        _fixture.ResetMocks();

        // Act A1: Initiate /update mid-questionnaire
        await SendTelegramMessageAsync(chatIdMid, "/update");
        var midAfterUpdateCmd = await GetDraftStateAsync(keyMid);
        midAfterUpdateCmd.IsWaitingForUpdateFieldSelection.Should().BeTrue();
        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdMid, It.Is<string>(m => m.Contains("Hangi bilginizi güncellemek istiyorsunuz"))),
            Times.Once());

        _fixture.ResetMocks();

        // Act A2: Select "iban" -> must immediately prompt Question_GuardianIban (NOT ApplicantRole)
        await SendTelegramMessageAsync(chatIdMid, "iban");
        var midAfterFieldSelect = await GetDraftStateAsync(keyMid);
        midAfterFieldSelect.IsWaitingForUpdateFieldSelection.Should().BeFalse();
        midAfterFieldSelect.Guardian.Iban.Should().BeNull();
        midAfterFieldSelect.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendMessageAsync(chatIdMid, It.Is<string>(m => m.Contains("IBAN Numaranızı"))),
            Times.Once());
        _fixture.UserNotifierMock.Verify(
            x => x.SendQuestionWithButtonsAsync(chatIdMid, It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
            Times.Never(),
            "IBAN sıfırlandığında diğer boş anket soruları değil, doğrudan IBAN sorusu sorulmalıdır.");

        _fixture.ResetMocks();

        // Act A3: Provide new valid IBAN #2 -> saves new IBAN and resumes next unanswered survey question (ApplicantRole)
        await SendTelegramMessageAsync(chatIdMid, ValidIban2);
        var midAfterNewIban = await GetDraftStateAsync(keyMid);
        midAfterNewIban.Guardian.Iban.Should().NotBeNull();
        midAfterNewIban.Guardian.Iban!.Value.Should().Be(ValidIban2);
        midAfterNewIban.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        _fixture.UserNotifierMock.Verify(
            x => x.SendQuestionWithButtonsAsync(
                chatIdMid,
                It.Is<string>(q => q.Contains("yetimin nesi oluyorsunuz")),
                It.Is<Dictionary<string, string>>(opts => opts.ContainsKey("Mother") && opts.ContainsKey("Guardian"))),
            Times.Once());

        // ====================================================================
        // PART B: Confirmation-Stage IBAN Update with Distinct Valid IBAN
        // ====================================================================
        _fixture.ResetMocks();
        const long chatIdConf = 200012L;
        const string keyConf = "BNK012";

        await SeedDraftAsync(chatIdConf, keyConf, RegistrationStep.WaitingForConfirmation, draft =>
        {
            draft.SystemDocuments.BankAccountDocumentPath = "uploads/bank_conf.pdf";
            draft.Guardian.ApplicantRole = ApplicantRole.Mother;
            draft.Guardian.Gender = Gender.Female;
            draft.Guardian.MaritalStatus = MaritalStatus.Widowed;
            draft.Guardian.EducationLevel = EducationLevel.HighSchool;
            draft.Guardian.EducationStatus = EducationStatus.Graduated;
            draft.Guardian.DegreeOfKinship = DegreeOfKinship.Mother;
            draft.Guardian.Profession = "Ev Hanımı";
            draft.Guardian.PhoneNumber = new PhoneNumber("05551234567");
            draft.Guardian.Iban = new Iban(ValidIban1);

            draft.Father.Profession = FatherProfession.Worker;
            draft.Father.DeathReason = FatherDeathReason.Earthquake;

            draft.FamilyFinancial.FamilyMemberCount = 2;
            draft.FamilyFinancial.HousingStatus = HousingStatus.Rent;
            draft.FamilyFinancial.SocialSecurity = SocialSecurity.Sgk;
            draft.FamilyFinancial.IncomeStatus = IncomeStatus.FixedSalary;
            draft.FamilyFinancial.Income = 15000;
            draft.FamilyFinancial.Expense = 12000;
            draft.DisabledMember = new DisabledMemberDraft { Name = "Yok" };
        });

        await SendTelegramMessageAsync(chatIdConf, "/update");
        await SendTelegramMessageAsync(chatIdConf, "iban");

        var confAfterReset = await GetDraftStateAsync(keyConf);
        confAfterReset.Guardian.Iban.Should().BeNull();
        confAfterReset.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);

        await SendTelegramMessageAsync(chatIdConf, ValidIban2);

        var confAfterNewIban = await GetDraftStateAsync(keyConf);
        confAfterNewIban.Guardian.Iban.Should().NotBeNull();
        confAfterNewIban.Guardian.Iban!.Value.Should().Be(ValidIban2);
        confAfterNewIban.SystemDocuments.Petition.Should().NotBeNull();
        confAfterNewIban.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);
    }
}
