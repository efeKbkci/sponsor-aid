using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.ValueObjects;
using SponsorAid.Infrastructure.Persistence;
using Xunit;

namespace SponsorAid.Tests;

[Collection("IntegrationTests")]
public class RegistrationUpdateJourneyTests
{
    private readonly SponsorAidTestFixture _fixture;
    private readonly HttpClient _client;

    public RegistrationUpdateJourneyTests(SponsorAidTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
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

    #endregion

    [Fact]
    public async Task UpdateRegistration_Journey_ShouldManageStateCorrectly()
    {
        // ====================================================================
        // ARRANGE (Hazırlık)
        // ====================================================================
        _fixture.ResetMocks();

        const long chatId = 987654321L;
        const string registrationKey = "UPD123";

        //Act 4 adımında sistem zaten mevcut IBAN'ı hedef alan olduğu için veritabanında null yapıyor.
        //Act 5 adımında ise yeni değeri atıyor.
        //Bu yüzden iki değişkenin aynı olması testin state (durum) değişim mantığını bozmaz
        const string initialIban = "TR330006100519786457841326";
        const string updatedIban = "TR330006100519786457841326";

        // 1. Semantic Matcher Mock Kurulumu
        _fixture.SemanticMatcherMock
            .Setup(x => x.FindBestMatchAsync(
                "IBAN'ımı yanlış girdim",
                It.IsAny<IEnumerable<MatchCandidate>>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SemanticMatchResult("iban", 0.9f, false));

        // 2. Veritabanına Başlangıç Durumunun Manuel Eklenmesi
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();

            var tracking = new RegistrationTrackingRecord
            {
                RegistrationKey = registrationKey,
                ChatId = chatId,
                Status = RegistrationStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var initialDraftState = new RegistrationDraftState
            {
                RegistrationKey = registrationKey,
                ChatId = chatId,
                CurrentStep = RegistrationStep.WaitingForIdentityFront,
                IsWaitingForUpdateFieldSelection = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                // Sistemin diğer sorularda takılmaması için tüm alanları dolduruyoruz
                Guardian = new GuardianDraft
                {
                    ApplicantRole = ApplicantRole.Mother,
                    Gender = Gender.Female,
                    MaritalStatus = MaritalStatus.Widowed,
                    EducationLevel = EducationLevel.HighSchool,
                    EducationStatus = EducationStatus.Graduated,
                    DegreeOfKinship = DegreeOfKinship.Mother,
                    Profession = "Ev Hanımı",
                    PhoneNumber = new PhoneNumber("05551234567"),
                    Iban = new Iban(initialIban)
                },
                Father = new FatherDraft
                {
                    Profession = FatherProfession.Worker,
                    DeathReason = FatherDeathReason.Earthquake
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
                    Name = "Yok" // Engelli sorusuna takılmamak için
                },
                Children = new List<ChildDraft>
                {
                    new ChildDraft { Name = "Test Çocuk" }
                }
            };

            var draft = new DraftRecord
            {
                RegistrationKey = registrationKey,
                ChatId = chatId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                JsonData = JsonSerializer.Serialize(initialDraftState)
            };

            db.RegistrationTrackings.Add(tracking);
            db.Drafts.Add(draft);
            await db.SaveChangesAsync();
        }

        // ====================================================================
        // ACT & ASSERT ADIMLARI
        // ====================================================================

        // --- Act 1: Hatalı Durum - Erken Aşamada Güncelleme İsteği ---
        var responseAct1 = await SendTelegramMessageAsync(chatId, "/update");

        // --- Assert 1 ---
        responseAct1.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            draftRecord.Should().NotBeNull();

            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state.Should().NotBeNull();
            state!.IsWaitingForUpdateFieldSelection.Should().BeFalse(
                "Belge yükleme aşamasında güncelleme komutu geldiğinde alan seçim bayrağı false kalmalıdır.");
        }

        _fixture.UserNotifierMock.Verify(x => x.SendMessageAsync(
            chatId,
            It.Is<string>(msg => msg.Contains("soru-cevap aşamasına gelmediniz"))),
            Times.AtLeastOnce(),
            "Kullanıcıya henüz anket aşamasında olunmadığına dair uyarı mesajı gitmelidir.");

        // --- Act 2: Geçerli Adıma Alma (WaitingForConfirmation) ve Güncelleme Başlatma ---
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            draftRecord.Should().NotBeNull();

            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);
            state!.SetStep(RegistrationStep.WaitingForConfirmation);

            draftRecord.JsonData = JsonSerializer.Serialize(state);
            await db.SaveChangesAsync();
        }

        var responseAct2 = await SendTelegramMessageAsync(chatId, "/update");

        // --- Assert 2 ---
        responseAct2.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state.Should().NotBeNull();
            state!.IsWaitingForUpdateFieldSelection.Should().BeTrue(
                "Uygun adımda /update komutu verildiğinde sistem alan seçimi beklemeye (IsWaitingForUpdateFieldSelection = true) geçmelidir.");
        }

        // --- Act 3: Güncellemeyi İptal Etme ---
        var responseAct3 = await SendTelegramMessageAsync(chatId, "iptal");

        // --- Assert 3 ---
        responseAct3.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state.Should().NotBeNull();
            state!.IsWaitingForUpdateFieldSelection.Should().BeFalse(
                "'iptal' yazıldığında güncelleme seçimi sonlandırılmalıdır.");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation,
                "İptal sonrası adım bozulmamalı, WaitingForConfirmation olarak kalmalıdır.");
        }

        // --- Act 4: IBAN Güncellemesini Tetikleme ---
        var responseAct4Update = await SendTelegramMessageAsync(chatId, "/update");
        responseAct4Update.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseAct4Target = await SendTelegramMessageAsync(chatId, "IBAN'ımı yanlış girdim");

        // --- Assert 4 ---
        responseAct4Target.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state.Should().NotBeNull();
            state!.IsWaitingForUpdateFieldSelection.Should().BeFalse(
                "Alan başarıyla eşleştikten sonra seçim bekleme modu kapatılmalıdır.");
            state.Guardian.Iban.Should().BeNull(
                "Güncellenecek alan (IBAN) sıfırlanarak yeniden girişe hazır hale getirilmelidir.");
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions,
                "Eksik kalan alanı sormak üzere durum makinesi WaitingForQuestions adımına geri dönmelidir.");
        }

        // --- Act 5: Yeni IBAN'ı Gönderme ve Süreci Tamamlama ---
        var responseAct5 = await SendTelegramMessageAsync(chatId, updatedIban);

        // --- Assert 5 ---
        responseAct5.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
            var draftRecord = await db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.RegistrationKey == registrationKey);
            var state = JsonSerializer.Deserialize<RegistrationDraftState>(draftRecord!.JsonData);

            state.Should().NotBeNull();
            state!.Guardian.Iban.Should().NotBeNull("Yeni IBAN başarıyla parse edilip atanmalıdır.");
            state.Guardian.Iban!.Value.Should().Be(updatedIban);
            state.CurrentStep.Should().Be(RegistrationStep.WaitingForConfirmation,
                "Tüm anket soruları tamamlandığında sistem tekrar onay adımına (WaitingForConfirmation) geçmelidir.");
        }
    }
}