using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using SponsorAid.Infrastructure.Persistence;
using SponsorAid.Integration.Contracts;
using Xunit;

namespace SponsorAid.Tests;

[Collection("IntegrationTests")]
public class RegistrationJourneyTests
{
    private readonly SponsorAidTestFixture _fixture;
    private readonly HttpClient _client;

    public RegistrationJourneyTests(SponsorAidTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _fixture.ResetMocks();
    }

    #region Telegram Webhook Helper Methods

    private async Task<HttpResponseMessage> SendTelegramMessageAsync(long chatId, string text)
    {
        var payload = new
        {
            message = new
            {
                message_id = Random.Shared.Next(1000, 9999),
                chat = new { id = chatId },
                text = text
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
                data = data
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

    private async Task<HttpResponseMessage> SendTelegramPhotoAsync(long chatId, string fileId, long fileSize)
    {
        var payload = new
        {
            message = new
            {
                message_id = Random.Shared.Next(1000, 9999),
                chat = new { id = chatId },
                photo = new[]
                {
                    new
                    {
                        file_id = fileId,
                        file_size = fileSize,
                        width = 1280,
                        height = 720
                    }
                }
            }
        };

        return await _client.PostAsJsonAsync("/api/telegram/webhook", payload);
    }

    #endregion

    [Fact]
    public async Task Registration_EndToEndJourney_ShouldCompleteSuccessfully_FromStartToFinish()
    {
        // ====================================================================
        // GENEL ARRANGE (Hazırlık)
        // ====================================================================
        const long chatId = 123456789L;
        const string validGuardianTc = "10000000146"; // Mod10/11 algoritmik olarak geçerli Vasi TC
        const string validChildTc = "20000000114";    // Mod10/11 algoritmik olarak geçerli Çocuk TC
        const string validIban = "TR330006100519786457841326";

        _fixture.FileStorageMock
            .Setup(x => x.DownloadAndSaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("fake/path/file.pdf");

        // ====================================================================
        // AŞAMA 1: Kayıt Başlatma (/new)
        // ====================================================================

        // --- Act 1: Kayıt yokken metin gönderme ---
        var responseAct1 = await SendTelegramMessageAsync(chatId, "Merhaba");

        // --- Assert 1 ---
        responseAct1.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.ChatId == chatId);
            draftRecord.Should().BeNull("Aktif kayıt yokken gönderilen mesaj taslak oluşturmamalıdır.");
        }

        // --- Act 2: Yeni kayıt başlatma ---
        var responseAct2 = await SendTelegramMessageAsync(chatId, "/new");

        // --- Assert 2 ---
        responseAct2.StatusCode.Should().Be(HttpStatusCode.OK);

        string registrationKey;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();

            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.ChatId == chatId);
            draftRecord.Should().NotBeNull("'/new' sonrasında taslak kaydı oluşmalıdır.");

            var trackingRecord = await db.RegistrationTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.ChatId == chatId);
            trackingRecord.Should().NotBeNull("'/new' sonrasında takip kaydı oluşmalıdır.");

            draftRecord!.RegistrationKey.Should().Be(trackingRecord!.RegistrationKey);
            registrationKey = draftRecord.RegistrationKey;

            // RegistrationKey format kontrolü
            registrationKey.Length.Should().Be(6);
            Regex.IsMatch(registrationKey, @"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z0-9]{6}$").Should().BeTrue();

            trackingRecord.Status.Should().Be(RegistrationStatus.InProgress);

            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord.JsonData);
            state.Should().NotBeNull();
            state!.IsActive.Should().BeTrue();
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForGuardianId);
            state.Guardian.IdNumber.Should().BeNull();
        }

        // ====================================================================
        // AŞAMA 2: Vasi TC Kimlik Numarası Girişi
        // ====================================================================

        // --- Act 3: Hatalı TC Girişleri ---
        var responseAct3Part1 = await SendTelegramMessageAsync(chatId, "12345");
        var responseAct3Part2 = await SendTelegramMessageAsync(chatId, "11111111111");

        // --- Assert 3 ---
        responseAct3Part1.StatusCode.Should().Be(HttpStatusCode.OK);
        responseAct3Part2.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.IdNumber.Should().BeNull("Geçersiz TC girişlerinde IdNumber set edilmemelidir.");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForGuardianId);
        }

        _fixture.UserNotifierMock.Verify(x => x.SendMessageAsync(chatId, It.IsAny<string>()), Times.AtLeast(2));

        // --- Act 4: Geçerli Vasi TC Girişi ---
        var responseAct4 = await SendTelegramMessageAsync(chatId, validGuardianTc);

        // --- Assert 4 ---
        responseAct4.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.IdNumber.Should().NotBeNull();
            state.Guardian.IdNumber!.Value.Should().Be(validGuardianTc);
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForPopulationRegistry);
        }

        // ====================================================================
        // AŞAMA 3: Nüfus Kayıt Örneği Yükleme ve İşleme
        // ====================================================================

        // --- Act 5: Hatalı Girdi - Metin Gönderme ---
        var responseAct5 = await SendTelegramMessageAsync(chatId, "belgem yok");

        // --- Assert 5 ---
        responseAct5.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForPopulationRegistry);
        }

        // --- Act 6: Hatalı Girdi - Boyut ve Uzantı İhlali ---
        var responseAct6Large = await SendTelegramDocumentAsync(chatId, "large_doc", 6 * 1024 * 1024, "nufus.pdf");
        var responseAct6Ext = await SendTelegramDocumentAsync(chatId, "exe_doc", 1024 * 100, "nufus.exe");

        // --- Assert 6 ---
        responseAct6Large.StatusCode.Should().Be(HttpStatusCode.OK);
        responseAct6Ext.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForPopulationRegistry);
        }

        // --- Act 7: Geçerli PDF Yükleme ---
        var responseAct7 = await SendTelegramDocumentAsync(chatId, "pop_1", 102400, "nufus.pdf");

        // --- Assert 7 ---
        responseAct7.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.ProcessingPopulationRegistry);
        }

        _fixture.EventPublisherMock.Verify(x => x.PublishAsync(
            "document-processing-queue",
            It.Is<WorkerTaskEnvelope>(e => e.CommandType == CommandType.ProcessOfficialDoc &&
                                           e.OfficialDocTask.DocumentType == DocumentType.PopulationRegistry)),
            Times.AtLeastOnce());

        // --- Act 8: Mükerrer Belge Gönderimi Engeli ---
        var responseAct8 = await SendTelegramDocumentAsync(chatId, "pop_duplicate", 102400, "nufus2.pdf");

        // --- Assert 8 ---
        responseAct8.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.ProcessingPopulationRegistry);
        }

        // --- Act 9: Hatalı Worker Sonucu (Babanın Ölüm Tarihi Yok) ---
        var invalidPopulationData = new PopulationRegistryData();
        invalidPopulationData.People.Add(new Person { Kinship = "KENDİSİ", IdNumber = validGuardianTc, FirstName = "AYŞE", LastName = "YILMAZ", BirthDate = "1985-05-10" });
        invalidPopulationData.People.Add(new Person { Kinship = "EŞİ", IdNumber = "30000000010", FirstName = "MEHMET", LastName = "YILMAZ", BirthDate = "1982-04-12" });

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "PopulationRegistry",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/pop_invalid.pdf",
                PopulationData: invalidPopulationData
            ));
        }

        // --- Assert 9 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForPopulationRegistry);
        }

        // --- Act 10: Başarılı Belge ve Çocuk Yaş Filtreleme ---
        await SendTelegramDocumentAsync(chatId, "pop_valid_file", 102400, "nufus_gecerli.pdf");

        var validPopulationData = new PopulationRegistryData();
        validPopulationData.People.Add(new Person { Kinship = "KENDİSİ", IdNumber = validGuardianTc, FirstName = "AYŞE", LastName = "YILMAZ", BirthDate = "1985-05-10" });
        validPopulationData.People.Add(new Person { Kinship = "EŞİ", IdNumber = "40000000018", FirstName = "MEHMET", LastName = "YILMAZ", BirthDate = "1982-04-12", DeathDate = "2023-02-06" });
        validPopulationData.People.Add(new Person { Kinship = "OĞLU", IdNumber = validChildTc, FirstName = "ALİ", LastName = "YILMAZ", BirthDate = "2018-01-01" });
        validPopulationData.People.Add(new Person { Kinship = "KIZI", IdNumber = "50000000016", FirstName = "ZEYNEP", LastName = "YILMAZ", BirthDate = "2005-01-01" }); // 13 yaş üstü

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "PopulationRegistry",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/pop_valid.pdf",
                PopulationData: validPopulationData
            ));
        }

        // --- Assert 10 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();

            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            var trackingRecord = await db.RegistrationTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.RegistrationKey == registrationKey);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForResidence);
            state.Guardian.Name.Should().Be("AYŞE YILMAZ");
            trackingRecord!.GuardianName.Should().Be("AYŞE YILMAZ");
            state.Father.Name.Should().Be("MEHMET YILMAZ");
            state.Father.DeathDate.Should().Be(new DateOnly(2023, 2, 6));

            state.Children.Should().HaveCount(1, "13 yaş üstü çocuk kayda dahil edilmemelidir.");
            state.Children[0].Name.Should().Be("ALİ YILMAZ");
            state.Children[0].IdNumber!.Value.Should().Be(validChildTc);
            state.SystemDocuments.PopulationRegistryDocumentPath.Should().Be("uploads/pop_valid.pdf");
        }

        // ====================================================================
        // AŞAMA 4: İkametgah Belgesi (Fotoğraf -> Barkod -> e-Devlet Akışı)
        // ====================================================================

        // --- Act 11: Fotoğraf Olarak İkametgah Yükleme ---
        var responseAct11 = await SendTelegramPhotoAsync(chatId, "residence_photo_1", 200000);

        // --- Assert 11 ---
        responseAct11.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.ProcessingResidence);
        }

        _fixture.EventPublisherMock.Verify(x => x.PublishAsync(
            "document-processing-queue",
            It.Is<WorkerTaskEnvelope>(e => e.CommandType == CommandType.ExtractBarcode &&
                                           e.OfficialDocTask.DocumentType == DocumentType.Residence)),
            Times.AtLeastOnce());

        // --- Act 12: Worker Barkod Çıkarma Sonucu & e-Devlet İndirme ---
        const string extractedBarcode = "ARKD-1122-3344-5566";
        const string downloadedPdfPath = "uploads/edevlet_residence.pdf";

        _fixture.EGovernmentServiceMock
            .Setup(x => x.DownloadDocumentAsync(extractedBarcode, validGuardianTc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadedPdfPath);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "Residence",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/residence_photo.jpg",
                ExtractedBarcode: extractedBarcode
            ));
        }

        // --- Assert 12 ---
        _fixture.EGovernmentServiceMock.Verify(x => x.DownloadDocumentAsync(extractedBarcode, validGuardianTc, It.IsAny<CancellationToken>()), Times.Once());
        _fixture.EventPublisherMock.Verify(x => x.PublishAsync(
            "document-processing-queue",
            It.Is<WorkerTaskEnvelope>(e => e.CommandType == CommandType.ProcessOfficialDoc &&
                                           e.OfficialDocTask.FilePath == downloadedPdfPath)),
            Times.AtLeastOnce());

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.ProcessingResidence);
        }

        // --- Act 13: Hatalı Worker Sonucu (TC Uyuşmazlığı) ---
        var invalidResidenceData = new ResidenceData
        {
            IdNumber = "99999999990",
            City = "ANKARA",
            District = "ÇANKAYA",
            Neighborhood = "KIZILAY MAH.",
            FullAddress = "KIZILAY MAH. ATATÜRK BULV. NO:10 ÇANKAYA / ANKARA"
        };

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "Residence",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: downloadedPdfPath,
                ResidenceData: invalidResidenceData
            ));
        }

        // --- Assert 13 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForResidence);
        }

        // --- Act 14: Doğru İkametgah Sonucu ---
        await SendTelegramPhotoAsync(chatId, "residence_photo_correct", 200000);

        var validResidenceData = new ResidenceData
        {
            IdNumber = validGuardianTc,
            City = "İSTANBUL",
            District = "KADIKÖY",
            Neighborhood = "CAFERAĞA MAH.",
            FullAddress = "CAFERAĞA MAH. MODA CAD. NO:1 KADIKÖY / İSTANBUL"
        };

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "Residence",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/residence_final.pdf",
                ResidenceData: validResidenceData
            ));
        }

        // --- Assert 14 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state.Should().NotBeNull();
            state!.ResidenceAddress.City.Should().Be("İSTANBUL");
            state.ResidenceAddress.District.Should().Be("KADIKÖY");
            state.ResidenceAddress.Neighborhood.Should().Be("CAFERAĞA MAH.");
            state.ResidenceAddress.FullAddress.Should().Be("CAFERAĞA MAH. MODA CAD. NO:1 KADIKÖY / İSTANBUL");

            state.SystemDocuments.ResidenceDocumentPath.Should().Be("uploads/residence_final.pdf");
            state.CurrentDocumentTargetName.Should().Be("Vasi");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForIdentityFront);
        }

        // ====================================================================
        // AŞAMA 5: Vasi ve Çocuk Kimlik Fotoğrafları (Ön, Arka ve Birleştirme)
        // ====================================================================

        // --- Act 15: Hatalı Girdi - Kimlik İçin PDF Gönderme ---
        var responseAct15 = await SendTelegramDocumentAsync(chatId, "id_pdf", 10240, "vasi_kimlik.pdf");

        // --- Assert 15 ---
        responseAct15.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForIdentityFront);
        }

        // --- Act 16: Vasi Kimlik Ön Yüz - Yanlış TC Okuma Hatası ---
        await SendTelegramPhotoAsync(chatId, "vasi_front_err", 150000);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "IdentityFront",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/vasi_front_err.jpg",
                ExtractedTcNumber: "99999999990"
            ));
        }

        // --- Assert 16 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForIdentityFront);
        }

        // --- Act 17: Vasi Kimlik Ön Yüz - Doğru TC ---
        await SendTelegramPhotoAsync(chatId, "vasi_front_ok", 150000);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "IdentityFront",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/vasi_front.jpg",
                ExtractedTcNumber: validGuardianTc
            ));
        }

        // --- Assert 17 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForIdentityBack);
            state.SystemDocuments.IdentityFrontPath.Should().Be("uploads/vasi_front.jpg");
        }

        // --- Act 18: Vasi Kimlik Arka Yüz ---
        await SendTelegramPhotoAsync(chatId, "vasi_back_ok", 150000);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "IdentityBack",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/vasi_back.jpg",
                ExtractedTcNumber: validGuardianTc
            ));
        }

        // --- Assert 18 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.CurrentStep.Should().Be(RegistrationStep.MergingDocuments);
            state.SystemDocuments.IdentityBackPath.Should().Be("uploads/vasi_back.jpg");
        }

        _fixture.EventPublisherMock.Verify(x => x.PublishAsync(
            "document-processing-queue",
            It.Is<WorkerTaskEnvelope>(e => e.CommandType == CommandType.MergePdf &&
                                           e.MergeTask.RegistrationKey == registrationKey &&
                                           e.MergeTask.FrontFilePath == "uploads/vasi_front.jpg" &&
                                           e.MergeTask.BackFilePath == "uploads/vasi_back.jpg")),
            Times.AtLeastOnce());

        // --- Act 19: Vasi Kimlik PDF Birleştirme Sonucu ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "MergedIdentity",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/vasi_merged.pdf"
            ));
        }

        // --- Assert 19 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.IdentityDocumentPath.Should().Be("uploads/vasi_merged.pdf");
            state.CurrentDocumentTargetName.Should().Be("ALİ YILMAZ");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForIdentityFront);
        }

        // --- Act 20: Çocuk Kimlik Akışı (Ön, Arka ve Birleştirme) ---
        // 1. Ön Yüz
        await SendTelegramPhotoAsync(chatId, "child_front_ok", 150000);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "IdentityFront",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/cocuk_front.jpg",
                ExtractedTcNumber: validChildTc
            ));
        }

        // 2. Arka Yüz
        await SendTelegramPhotoAsync(chatId, "child_back_ok", 150000);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "IdentityBack",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/cocuk_back.jpg",
                ExtractedTcNumber: validChildTc
            ));
        }

        // 3. Birleştirme
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
            await mediator.Send(new SaveProcessedDocumentCommand(
                RegistrationKey: registrationKey,
                DocumentType: "MergedIdentity",
                IsSuccess: true,
                ErrorMessage: null,
                SavedFilePath: "uploads/cocuk_merged.pdf"
            ));
        }

        // --- Assert 20 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Children.Should().HaveCount(1);
            state.Children[0].IdentityDocumentPath.Should().Be("uploads/cocuk_merged.pdf");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);
        }

        // ====================================================================
        // AŞAMA 6: Soru-Cevap (Anket) Akışı
        // ====================================================================

        // --- Act 21: Başvuru Rolü ---
        await SendTelegramMessageAsync(chatId, "Komşusu");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Guardian.ApplicantRole.Should().BeNull();
        }

        await SendTelegramCallbackAsync(chatId, "Mother", messageId: 101);

        // --- Assert 21 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Guardian.ApplicantRole.Should().Be(ApplicantRole.Mother);
        }

        // --- Act 22: Cinsiyet, Medeni Hal, Eğitim Seviyesi, Eğitim Durumu, Yakınlık Derecesi ve Meslek ---
        await SendTelegramCallbackAsync(chatId, "Female", messageId: 102);
        await SendTelegramCallbackAsync(chatId, "Widowed", messageId: 103);
        await SendTelegramCallbackAsync(chatId, "HighSchool", messageId: 104);
        await SendTelegramCallbackAsync(chatId, "Graduated", messageId: 105);
        await SendTelegramCallbackAsync(chatId, "Mother", messageId: 106);
        await SendTelegramMessageAsync(chatId, "Ev Hanımı");

        // --- Assert 22 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.Gender.Should().Be(Gender.Female);
            state.Guardian.MaritalStatus.Should().Be(MaritalStatus.Widowed);
            state.Guardian.EducationLevel.Should().Be(EducationLevel.HighSchool);
            state.Guardian.EducationStatus.Should().Be(EducationStatus.Graduated);
            state.Guardian.DegreeOfKinship.Should().Be(DegreeOfKinship.Mother);
            state.Guardian.Profession.Should().Be("Ev Hanımı");
        }

        // --- Act 23: Telefon Numarası ---
        await SendTelegramMessageAsync(chatId, "12345");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Guardian.PhoneNumber.Should().BeNull();
        }

        await SendTelegramMessageAsync(chatId, "05551234567");

        // --- Assert 23 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.PhoneNumber.Should().NotBeNull();
            state.Guardian.PhoneNumber!.Value.Should().Be("05551234567");
        }

        // --- Act 24: Hane Kişi Sayısı ---
        await SendTelegramMessageAsync(chatId, "üç");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.FamilyFinancial.FamilyMemberCount.Should().BeNull();
        }

        await SendTelegramMessageAsync(chatId, "3");

        // --- Assert 24 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.FamilyFinancial.FamilyMemberCount.Should().Be(3);
        }

        // --- Act 25: Baba Mesleği (Semantic Matcher) ---
        _fixture.SemanticMatcherMock
            .Setup(x => x.FindBestMatchAsync("anlamsız metin", It.IsAny<IEnumerable<MatchCandidate>>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SemanticMatchResult?)null);

        _fixture.SemanticMatcherMock
            .Setup(x => x.FindBestMatchAsync("inşaatta çalışıyordu", It.IsAny<IEnumerable<MatchCandidate>>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemanticMatchResult("Worker", 0.85f, false));

        await SendTelegramMessageAsync(chatId, "anlamsız metin");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Father.Profession.Should().BeNull();
        }

        await SendTelegramMessageAsync(chatId, "inşaatta çalışıyordu");

        // --- Assert 25 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Father.Profession.Should().Be(FatherProfession.Worker);
        }

        // --- Act 26: Baba Vefat Nedeni ---
        await SendTelegramCallbackAsync(chatId, "Earthquake", messageId: 107);

        // --- Assert 26 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Father.DeathReason.Should().Be(FatherDeathReason.Earthquake);
        }

        // --- Act 27: Engelli Birey Alt Akışı ---
        await SendTelegramCallbackAsync(chatId, "Var", messageId: 108);
        await SendTelegramMessageAsync(chatId, "Mehmet Yılmaz");
        await SendTelegramMessageAsync(chatId, "Astım");
        await SendTelegramMessageAsync(chatId, "Kronik");
        await SendTelegramCallbackAsync(chatId, "BeingTreated", messageId: 109);
        await SendTelegramMessageAsync(chatId, "Geç");

        // --- Assert 27 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.DisabledMember.Should().NotBeNull();
            state.DisabledMember!.Name.Should().Be("Mehmet Yılmaz");
            state.DisabledMember.Diagnosis.Should().Be("Astım");
            state.DisabledMember.DiseaseStatus.Should().Be("Kronik");
            state.DisabledMember.TreatmentStatus.Should().Be(TreatmentStatus.BeingTreated);
            state.DisabledMember.Description.Should().Be("Belirtilmedi");
        }

        // --- Act 28: Finansal Bilgiler ---
        await SendTelegramCallbackAsync(chatId, "Rent", messageId: 110);
        await SendTelegramCallbackAsync(chatId, "Sgk", messageId: 111);
        await SendTelegramCallbackAsync(chatId, "FixedSalary", messageId: 112);
        await SendTelegramMessageAsync(chatId, "15000");
        await SendTelegramMessageAsync(chatId, "12000");

        // --- Assert 28 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.FamilyFinancial.HousingStatus.Should().Be(HousingStatus.Rent);
            state.FamilyFinancial.SocialSecurity.Should().Be(SocialSecurity.Sgk);
            state.FamilyFinancial.IncomeStatus.Should().Be(IncomeStatus.FixedSalary);
            state.FamilyFinancial.Income.Should().Be(15000);
            state.FamilyFinancial.Expense.Should().Be(12000);
        }

        // --- Act 29: IBAN Girişi ---
        await SendTelegramMessageAsync(chatId, "TR0000");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.Guardian.Iban.Should().BeNull();
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);
        }

        await SendTelegramMessageAsync(chatId, validIban);

        // --- Assert 29 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state!.Guardian.Iban.Should().NotBeNull();
            state.Guardian.Iban!.Value.Should().Be(validIban);

            state.SystemDocuments.Petition.Should().NotBeNull();
            state.SystemDocuments.Petition!.Content.Length.Should().BeGreaterThanOrEqualTo(300);

            state.Children[0].Hobbies.Should().NotBeNullOrWhiteSpace();
            state.Children[0].FutureDream.Should().NotBeNullOrWhiteSpace();

            state.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);
        }

        // ====================================================================
        // AŞAMA 7: Onaylama ve Kaydı Tamamlama (FinalizeRegistration)
        // ====================================================================

        // --- Act 30: Onay Butonu Yerine Metin Yazma ---
        await SendTelegramMessageAsync(chatId, "onaylıyorum");

        // --- Assert 30 ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);

            draftRecord.Should().NotBeNull();
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation);
        }

        // --- Act 31: Kaydı Onaylama (confirm_registration Callback) ---
        var responseAct31 = await SendTelegramCallbackAsync(chatId, "confirm_registration", messageId: 99);
        responseAct31.StatusCode.Should().Be(HttpStatusCode.OK);

        // --- Assert 31 ---
        // 1. Şifreli ZIP Paketi ve Parola Gönderimi Doğrulaması
        _fixture.RegistrationDispatchMock.Verify(x => x.SendEncryptedPackageAsync(
            registrationKey,
            "AYŞE YILMAZ",
            It.Is<byte[]>(bytes => bytes != null && bytes.Length > 0),
            It.IsAny<CancellationToken>()),
            Times.Once());

        _fixture.RegistrationDispatchMock.Verify(x => x.SendPasswordAsync(
            registrationKey,
            "AYŞE YILMAZ",
            It.Is<string>(pwd => !string.IsNullOrEmpty(pwd) && pwd.Length == 16),
            It.IsAny<CancellationToken>()),
            Times.Once());

        // 2. Yüklenen Belgelerin Temizlenme Doğrulaması
        _fixture.FileStorageMock.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.AtLeastOnce());

        // 3. Veritabanı Nihai Durum Doğrulaması
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();

            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            draftRecord.Should().BeNull("Başarıyla finalize edilen kayıt Drafts tablosundan temizlenmelidir.");

            var trackingRecord = await db.RegistrationTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.RegistrationKey == registrationKey);
            trackingRecord.Should().NotBeNull();
            trackingRecord!.Status.Should().Be(RegistrationStatus.Completed);
        }
    }
}