using MediatR;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Features.Registrations.Commands.ProcessQuestionnaireAnswer;

public class ProcessQuestionnaireAnswerHandler(
    IDraftStateRepository draftRepository,
    IUserNotifier notifier) : IRequestHandler<ProcessQuestionnaireAnswerCommand>
{
    private readonly IDraftStateRepository _draftRepository = draftRepository;
    private readonly IUserNotifier _notifier = notifier;

    public async Task Handle(ProcessQuestionnaireAnswerCommand request, CancellationToken cancellationToken)
    {
        var draft = await _draftRepository.GetAsync(request.RegistrationKey, cancellationToken);
        if (draft == null) throw new KeyNotFoundException($"Draft with key '{request.RegistrationKey}' not found.");

        // 2. TELEGRAM CEVABI İSE ŞELALEYİ ÇALIŞTIR
        string text = request.AnswerText?.Trim() ?? string.Empty;

        // 1. BAŞVURAN ROLÜ
        if (draft.Guardian.ApplicantRole == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<ApplicantRole>(text, out var parsedRole))
            {
                draft.ApplyUpdates(new() { { "GuardianApplicantRole", text } });
                // 🚀 YENİ: Başarılı kayıttan sonra butonları silip yeşil tik koy!
                string label = parsedRole == ApplicantRole.Mother ? "Yetim Annesi" : "Yetim Vasisi";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "📋 Başvuruyu yapan kişi olarak yetimin nesi oluyorsunuz?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Mother", "Yetim Annesi" }, { "Guardian", "Yetim Vasisi" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "📋 Başvuruyu yapan kişi olarak yetimin nesi oluyorsunuz?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 2. CİNSİYET
        if (draft.Guardian.Gender == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<Gender>(text, out var parsedGender))
            {
                draft.ApplyUpdates(new() { { "GuardianGender", text } });
                string label = parsedGender == Gender.Female ? "Kadın" : "Erkek";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "👤 Cinsiyetiniz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Female", "Kadın" }, { "Male", "Erkek" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "👤 Cinsiyetiniz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 3. MEDENİ DURUM
        if (draft.Guardian.MaritalStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<MaritalStatus>(text, out var parsedMaritalStatus))
            {
                draft.ApplyUpdates(new() { { "GuardianMaritalStatus", text } });
                string label = parsedMaritalStatus == MaritalStatus.Single ? "Bekar" :
                               parsedMaritalStatus == MaritalStatus.Married ? "Evli" :
                               parsedMaritalStatus == MaritalStatus.Divorced ? "Boşanmış" : "Dul";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "💍 Medeni durumunuz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Single", "Bekar" }, { "Married", "Evli" }, { "Divorced", "Boşanmış" }, { "Widowed", "Dul" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "💍 Medeni durumunuz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 4. EĞİTİM DÜZEYİ
        if (draft.Guardian.EducationLevel == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<EducationLevel>(text, out var parsedEducation))
            {
                draft.ApplyUpdates(new() { { "GuardianEducationLevel", text } });
                string label = parsedEducation switch
                {
                    EducationLevel.Literate => "Okur Yazar",
                    EducationLevel.PrimarySchool => "İlkokul",
                    EducationLevel.MiddleSchool => "Ortaokul",
                    EducationLevel.HighSchool => "Lise",
                    EducationLevel.AssociatesDegree => "Ön Lisans",
                    EducationLevel.BachelorsDegree => "Lisans",
                    _ => "Diğer"
                };
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "🎓 Eğitim düzeyiniz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> {
                    { "Literate", "Okur Yazar" }, { "PrimarySchool", "İlkokul" }, { "MiddleSchool", "Ortaokul" },
                    { "HighSchool", "Lise" }, { "AssociatesDegree", "Ön Lisans" }, { "BachelorsDegree", "Lisans" }
                };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "🎓 Eğitim düzeyiniz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 5. YETİME YAKINLIK DERECESİ
        if (draft.Guardian.DegreeOfKinship == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<DegreeOfKinship>(text, out var parsedKinship))
            {
                draft.ApplyUpdates(new() { { "GuardianDegreeOfKinship", text } });
                string label = parsedKinship switch
                {
                    DegreeOfKinship.Mother => "Anne",
                    DegreeOfKinship.Grandmother => "Büyükanne",
                    DegreeOfKinship.Grandfather => "Dede",
                    DegreeOfKinship.PaternalUncle => "Amca",
                    DegreeOfKinship.MaternalUncle => "Dayı",
                    DegreeOfKinship.PaternalAunt => "Hala",
                    DegreeOfKinship.MaternalAunt => "Teyze",
                    DegreeOfKinship.Sibling => "Kardeş",
                    _ => "Diğer"
                };
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "👨‍👩‍👧‍👦 Yetime yakınlık dereceniz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> {
                    { "Mother", "Anne" }, { "Grandmother", "Büyükanne" }, { "Grandfather", "Dede" },
                    { "PaternalUncle", "Amca" }, { "MaternalUncle", "Dayı" }, { "PaternalAunt", "Hala" },
                    { "MaternalAunt", "Teyze" }, { "Sibling", "Kardeş" }, { "Other", "Diğer" }
                };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "👨‍👩‍👧‍👦 Yetime yakınlık dereceniz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 6. VASİ MESLEK
        if (string.IsNullOrEmpty(draft.Guardian.Profession))
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianProfession", text } }); text = ""; }

            if (string.IsNullOrEmpty(draft.Guardian.Profession))
            {
                await _notifier.SendMessageAsync(draft.ChatId, "💼 Lütfen şu anki **Mesleğinizi** yazınız. (Örn: Ev Hanımı, Muhasebeci)");
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 7. VASİ TELEFON NUMARASI
        // 🚀 DİKKAT: Try-Catch YOK! Hata olursa ExceptionHandlingBehavior yakalayıp kullanıcıya fırlatacak.
        if (draft.Guardian.PhoneNumber == null)
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianPhoneNumber", text } }); text = ""; }

            if (draft.Guardian.PhoneNumber == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, "📱 Size ulaşabileceğimiz **Telefon Numaranızı** (Örn: 05551234567) rakamla yazınız.");
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 8. AİLEDE YAŞAYAN KİŞİ SAYISI
        if (draft.FamilyFinancial.FamilyMemberCount == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "FamilyMemberCount", text } }); text = ""; }

            if (draft.FamilyFinancial.FamilyMemberCount == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, "🏠 Ailede sizinle birlikte yaşayan toplam **Kişi Sayısını** rakamla yazınız. (Örn: 4)");
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 9. BABA MESLEĞİ
        if (draft.Father.Profession == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<FatherProfession>(text, out var parsedProfession))
            {
                draft.ApplyUpdates(new() { { "FatherProfession", text } });
                string label = parsedProfession == FatherProfession.Worker ? "İşçi" :
                               parsedProfession == FatherProfession.CivilServant ? "Memur" :
                               parsedProfession == FatherProfession.Farmer ? "Çiftçi" :
                               parsedProfession == FatherProfession.Freelancer ? "Serbest Meslek" : "Diğer";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "🛠️ Vefat eden babanın mesleği neydi?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Worker", "İşçi" }, { "CivilServant", "Memur" }, { "Farmer", "Çiftçi" }, { "Freelancer", "Serbest Meslek" }, { "Other", "Diğer" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "🛠️ Vefat eden babanın mesleği neydi?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 10. BABA ÖLÜM NEDENİ
        if (draft.Father.DeathReason == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<FatherDeathReason>(text, out var parsedDeathReason))
            {
                draft.ApplyUpdates(new() { { "FatherDeathReason", text } });
                string label = parsedDeathReason == FatherDeathReason.NaturalDeath ? "Doğal Ölüm" :
                               parsedDeathReason == FatherDeathReason.HeartAttack ? "Kalp Krizi" :
                               parsedDeathReason == FatherDeathReason.Cancer ? "Kanser" :
                               parsedDeathReason == FatherDeathReason.TrafficAccident ? "Trafik Kazası" :
                               parsedDeathReason == FatherDeathReason.WorkAccident ? "İş Kazası" :
                               parsedDeathReason == FatherDeathReason.Earthquake ? "Deprem" : "Diğer";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "🕊️ Babanın ölüm nedeni nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "NaturalDeath", "Doğal Ölüm" }, { "HeartAttack", "Kalp Krizi" }, { "Cancer", "Kanser" }, { "TrafficAccident", "Trafik Kazası" }, { "WorkAccident", "İş Kazası" }, { "Earthquake", "Deprem" }, { "Other", "Diğer" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "🕊️ Babanın ölüm nedeni nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 11. ENGELLİ/HASTA BİREY SİLSİLESİ
        if (draft.DisabledMember == null || draft.DisabledMember.Name == null)
        {
            if (text == "Yok") { draft.ApplyUpdates(new() { { "DisabledName", "Yok" } }); text = ""; }
            else if (text == "Var") { await _notifier.SendMessageAsync(draft.ChatId, "🏥 Lütfen evdeki hasta/engelli bireyin **Adını ve Soyadını** yazınız."); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            else if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledName", text } }); text = ""; }

            if (draft.DisabledMember?.Name == null)
            {
                var options = new Dictionary<string, string> { { "Var", "Evet, var" }, { "Yok", "Hayır, yok" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "♿ Evde yaşayan engelli veya sürekli hastalığı olan bir birey var mı?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        if (draft.DisabledMember != null && draft.DisabledMember.Name != "Yok")
        {
            if (draft.DisabledMember.Diagnosis == null)
            {
                if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDiagnosis", text } }); text = ""; }
                if (draft.DisabledMember.Diagnosis == null) { await _notifier.SendMessageAsync(draft.ChatId, "🩺 Hastalık veya Engellilik **Teşhisini** yazınız. (Örn: Görme Engelli)"); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            }
            if (draft.DisabledMember.DiseaseStatus == null)
            {
                if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDiseaseStatus", text } }); text = ""; }
                if (draft.DisabledMember.DiseaseStatus == null) { await _notifier.SendMessageAsync(draft.ChatId, "📉 Hastalığın/Engelliliğin **Durumunu/Derecesini** yazınız. (Örn: %80 Ağır)"); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
            }
            if (draft.DisabledMember.TreatmentStatus == null)
            {
                if (!string.IsNullOrEmpty(text) && Enum.TryParse<TreatmentStatus>(text, out _)) { draft.ApplyUpdates(new() { { "DisabledTreatmentStatus", text } }); text = ""; }
                else
                {
                    var options = new Dictionary<string, string> { { "BeingTreated", "Tedavi Ediliyor" }, { "NeedsTreatment", "Tedavi Gerekiyor" }, { "Incurable", "Tedavisi Mümkün Değil" }, { "NeedsTreatmentAbroad", "Yurt Dışında Tedavi Gerekiyor" } };
                    await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "💊 Bireyin şu anki tedavi durumu nedir?", options);
                    await _draftRepository.SaveAsync(draft, cancellationToken); return;
                }
            }
            if (draft.DisabledMember.Description == null)
            {
                if (text == "Geç") { draft.ApplyUpdates(new() { { "DisabledDescription", "Belirtilmedi" } }); text = ""; }
                else if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "DisabledDescription", text } }); text = ""; }

                if (draft.DisabledMember.Description == null)
                {
                    var options = new Dictionary<string, string> { { "Geç", "Geç (Açıklama Yok)" } };
                    await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "📝 Hastalıkla ilgili eklemek istediğiniz bir **Açıklama** var mı? (Veya Geç butonuna basabilirsiniz)", options);
                    await _draftRepository.SaveAsync(draft, cancellationToken); return;
                }
            }
        }

        // 12. KONUT DURUMU
        if (draft.FamilyFinancial.HousingStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<HousingStatus>(text, out var parsedHousingStatus))
            {
                draft.ApplyUpdates(new() { { "HousingStatus", text } });
                string label = parsedHousingStatus == HousingStatus.OwnHouse ? "Kendi Evi" :
                               parsedHousingStatus == HousingStatus.Rent ? "Kira" :
                               parsedHousingStatus == HousingStatus.LivingWithRelatives ? "Akraba Yanı" :
                               parsedHousingStatus == HousingStatus.RentFree ? "Bedelsiz Kira" :
                               parsedHousingStatus == HousingStatus.Tent ? "Çadır" : "Konteyner";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "🏡 Şu anki konut durumunuz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "OwnHouse", "Kendi Evi" }, { "Rent", "Kira" }, { "LivingWithRelatives", "Akraba Yanı" }, { "RentFree", "Bedelsiz Kira" }, { "Tent", "Çadır" }, { "Container", "Konteyner" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "🏡 Şu anki konut durumunuz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 13. SOSYAL GÜVENCE
        if (draft.FamilyFinancial.SocialSecurity == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<SocialSecurity>(text, out var parsedSocialSecurity))
            {
                draft.ApplyUpdates(new() { { "SocialSecurity", text } });
                string label = parsedSocialSecurity == SocialSecurity.Sgk ? "SGK" :
                               parsedSocialSecurity == SocialSecurity.BagKur ? "Bağ-Kur" :
                               parsedSocialSecurity == SocialSecurity.GreenCard ? "Yeşil Kart" : "Diğer / Yok";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "🛡️ Sosyal güvenceniz var mı?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "Sgk", "SGK" }, { "BagKur", "Bağ-Kur" }, { "GreenCard", "Yeşil Kart" }, { "Other", "Diğer / Yok" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "🛡️ Sosyal güvenceniz var mı?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 14. GELİR DURUMU
        if (draft.FamilyFinancial.IncomeStatus == null)
        {
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<IncomeStatus>(text, out var parsedIncomeStatus))
            {
                draft.ApplyUpdates(new() { { "IncomeStatus", text } });
                string label = parsedIncomeStatus == IncomeStatus.FixedSalary ? "Sabit Maaş" :
                               parsedIncomeStatus == IncomeStatus.DailyWage ? "Günlük İş" :
                               parsedIncomeStatus == IncomeStatus.RentIncome ? "Kira Geliri" : "Diğer";
                await MarkAsAnsweredIfCallback(draft.ChatId, request.CallbackMessageId, "💰 Gelir tipiniz nedir?", label);
                text = "";
            }
            else
            {
                var options = new Dictionary<string, string> { { "FixedSalary", "Sabit Maaş" }, { "DailyWage", "Günlük İş" }, { "RentIncome", "Kira Geliri" }, { "Other", "Diğer" } };
                await _notifier.SendQuestionWithButtonsAsync(draft.ChatId, "💰 Gelir tipiniz nedir?", options);
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
        }

        // 15. AYLIK GELİR
        if (draft.FamilyFinancial.Income == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "Income", text } }); text = ""; }
            if (draft.FamilyFinancial.Income == null) { await _notifier.SendMessageAsync(draft.ChatId, "💵 Ailenizin toplam **Aylık Gelirini** sadece rakamla yazınız. (Örn: 17000, Geliriniz yoksa 0 yazınız)"); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
        }

        // 16. AYLIK GİDER
        if (draft.FamilyFinancial.Expense == null)
        {
            if (!string.IsNullOrEmpty(text) && int.TryParse(text, out _)) { draft.ApplyUpdates(new() { { "Expense", text } }); text = ""; }
            if (draft.FamilyFinancial.Expense == null) { await _notifier.SendMessageAsync(draft.ChatId, "💸 Ailenizin yaklaşık **Aylık Giderini** rakamla yazınız. (Örn: 12000)"); await _draftRepository.SaveAsync(draft, cancellationToken); return; }
        }

        // 17. IBAN - FİNAL
        if (draft.Guardian.Iban == null)
        {
            if (!string.IsNullOrEmpty(text)) { draft.ApplyUpdates(new() { { "GuardianIban", text } }); text = ""; }

            if (draft.Guardian.Iban == null)
            {
                await _notifier.SendMessageAsync(draft.ChatId, "💳 Son olarak, burs bağlandığı takdirde yatırılacak olan kendinize ait **IBAN Numaranızı** (TR ile başlayarak) yazınız.");
                await _draftRepository.SaveAsync(draft, cancellationToken); return;
            }
            else
            {
                // TÜM SORULAR BİTTİ, FİNALE GEÇ
                draft.GenerateAutomatedDetails(); // 🚀 Dilekçe ve hobileri draft JSON'ına mühürle
                draft.SetStep(RegistrationStep.Completed);
                await _draftRepository.SaveAsync(draft, cancellationToken);
                await _notifier.SendMessageAsync(draft.ChatId, "🎉 Harika! Tüm aile ve gelir bilgileriniz eksiksiz olarak alındı. Başvurunuz uzmanlarımız ve yapay zeka otomasyonumuz tarafından derleniyor. Lütfen bekleyiniz...");
            }
        }
        // 🚀 GÜNCELLEME SONRASI KONTROLÜ:
        // Eğer hiçbir adımda 'return' edilmediyse, tüm sorular başarıyla doludur.
        if (draft.CurrentStep != RegistrationStep.Completed)
        {
            draft.SetStep(RegistrationStep.Completed);
            await _draftRepository.SaveAsync(draft, cancellationToken);
            await _notifier.SendMessageAsync(draft.ChatId, "🎉 Güncellemeniz başarıyla kaydedildi! Başvurunuz onay aşamasına alındı.");
        }
    }

    // Yardımcı Metot: Sadece buton tıklamasıyla geldiyse mesajı günceller
    private async Task MarkAsAnsweredIfCallback(long chatId, int? messageId, string question, string answerLabel)
    {
        if (messageId.HasValue)
        {
            await _notifier.UpdateMessageAsAnsweredAsync(chatId, messageId.Value, question, answerLabel);
        }
    }
}