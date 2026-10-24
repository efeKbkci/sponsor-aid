using SponsorAid.Application.Interfaces;
using SponsorAid.Domain.Enums;

namespace SponsorAid.Application.Features.Registrations.Common;

public static class RegistrationSemanticCatalog
{
    public static readonly IReadOnlyList<MatchCandidate> UpdateTargets = new List<MatchCandidate>
    {
        new("applicantrole", "Başvuran Rolü",
            ["başvuran rolümü güncelle", "başvuru yapan kişi bilgisini değiştir"]),
        new("gender", "Cinsiyet",
            ["cinsiyetimi güncelle", "cinsiyet bilgisini değiştir"]),
        new("maritalstatus", "Medeni Durum",
            ["medeni durumumu güncelle", "evlilik durumumu değiştir"]),
        new("educationlevel", "Eğitim Düzeyi",
            ["eğitim düzeyimi güncelle", "öğrenim durumumu değiştir"]),
        new("degreeofkinship", "Yetime Yakınlık Derecesi",
            ["yakınlık derecemi güncelle", "yetimle akrabalık durumumu değiştir"]),
        new("profession", "Vasi Mesleği",
            ["mesleğimi güncelle", "işimi değiştir", "meslek bilgimi düzelt"]),
        new("phonenumber", "Telefon Numarası",
            ["telefon numaramı güncelle", "telefonumu değiştirmek istiyorum", "iletişim numaramı düzelt"]),
        new("familymembercount", "Aile Birey Sayısı",
            ["ailedeki kişi sayısını güncelle", "hane nüfusunu değiştir"]),
        new("fatherprofession", "Baba Mesleği",
            ["babamın mesleğini güncelle", "vefat eden babanın mesleğini değiştir"]),
        new("fatherdeathreason", "Baba Ölüm Nedeni",
            ["babamın ölüm nedenini güncelle", "babanın vefat sebebini değiştir"]),
        new("disabledname", "Engelli / Hasta Birey Durumu",
            ["engelli birey bilgisini güncelle", "hasta birey bilgisini değiştir"]),
        new("housingstatus", "Konut Durumu",
            ["konut durumumu güncelle", "ev durumunu değiştir"]),
        new("socialsecurity", "Sosyal Güvence",
            ["sosyal güvence durumumu güncelle", "sigorta bilgimi değiştir"]),
        new("incomestatus", "Gelir Tipi",
            ["gelir tipimi güncelle", "gelir türümü değiştir"]),
        new("income", "Aylık Gelir",
            ["aylık gelirimi güncelle", "gelir miktarımı değiştir"]),
        new("expense", "Aylık Gider",
            ["aylık giderimi güncelle", "gider miktarımı değiştir"]),
        new("iban", "IBAN Numarası",
            ["iban numaramı güncelle", "ibanımı değiştirmek istiyorum", "banka hesap numaramı düzelt"])
    };

    // =========================================================================
    // 2. BABA ÖLÜM NEDENLERİ (FatherDeathReason)
    // =========================================================================
    public static readonly IReadOnlyList<MatchCandidate> FatherDeathReasons = new List<MatchCandidate>
    {
        new(nameof(FatherDeathReason.Fever), "Ateş (Humma)",
            ["yüksek ateşi vardı", "ateş içinde idi", "lezyon vardı", "enfeksiyondan öldü"]),

        new(nameof(FatherDeathReason.BrainHemorrhage), "Beyin Kanaması",
            ["beynine pıhtı attı", "tansiyondan beyin kanaması geçirdi", "felç sonrası kanama"]),

        new(nameof(FatherDeathReason.Drowning), "Boğulma",
            ["denizde boğuldu", "suda boğuldu", "gölde kayboldu"]),

        new(nameof(FatherDeathReason.KidneyFailure), "Böbrek Yetmezliği",
            ["diyalize giriyordu", "böbrekleri iflas etti", "organ yetmezliği"]),

        new(nameof(FatherDeathReason.Earthquake), "Deprem",
            ["enkaz altında kaldı", "depremde vefat etti", "bina çöktü"]),

        new(nameof(FatherDeathReason.NaturalDisasterOther), "Doğal Afet (Diğer)",
            ["fırtına sonucu öldü", "erozyon altında kaldı", "çığ altında kaldı"]),

        new(nameof(FatherDeathReason.NaturalDeath), "Doğal Ölüm",
            ["yaşlılıktan öldü", "uykusunda vefat etti", "eceliyle öldü"]),

        new(nameof(FatherDeathReason.Ebola), "Ebola",
            ["ebola virüsü vardı", "kanamaya başladı", "bulaşıcı hastalıktan öldü"]),

        new(nameof(FatherDeathReason.Electrocution), "Elektrik Çarpması",
            ["elektrik akımına maruz kaldı", "akaryakıt tesisinde elektrik çarpması", "yüksek gerilim çarpması"]),

        new(nameof(FatherDeathReason.HomeAccident), "Ev Kazası",
            ["evde düştü", "merdivenlerden düştü", "yanmış evi"]),

        new(nameof(FatherDeathReason.Paralysis), "Felç",
            ["felç geçirdi", "lifleri tutuldu", "hareket edemedi"]),

        new(nameof(FatherDeathReason.IllnessOther), "Hastalık (Diğer)",
            ["bilinmeyen bir hastalığa yakalandı", "tuhaf semptomlar gösterdi", "doktor tespit edemedi"]),

        new(nameof(FatherDeathReason.Hepatitis), "Hepatit",
            ["hepatit vardı", "karacığında problem oldu", "sarılık hastalığı"]),

        new(nameof(FatherDeathReason.HivAids), "HIV / AIDS",
            ["hiv pozitif idi", "aids semptomları gösterdi", "bağışıklık sistemi bozuldu"]),

        new(nameof(FatherDeathReason.Execution), "İdam",
            ["idam edildı", "mahkeme kararıyla infaz edildi", "yürütülen ceza"]),

        new(nameof(FatherDeathReason.Suicide), "İntihar",
            ["kendini astı", "canına kıydı", "intihar etti"]),

        new(nameof(FatherDeathReason.WorkAccident), "İş Kazası",
            ["inşaattan düştü", "fabrikada kaza oldu", "madende göçük altında kaldı", "iş yerinde kaza geçirdi"]),

        new(nameof(FatherDeathReason.HeartAttack), "Kalp Krizi",
            ["kalbi durdu", "kalp spazmı geçirdi", "enfarktüs", "ani kalp durması"]),

        new(nameof(FatherDeathReason.Cancer), "Kanser",
            ["tümör vardı", "lösemi", "akciğer kanseri", "amansız hastalık", "kemoterapi görüyordu"]),

        new(nameof(FatherDeathReason.Missing), "Kayıp",
            ["kayıp bir kişi", "ortadan kayboldu", "bulunmadı"]),

        new(nameof(FatherDeathReason.Murdered), "Öldürülme, Cinayet",
            ["bıçaklandı", "silahla vuruldu", "kavgada öldü", "cinayete kurban gitti", "vurularak öldürüldü", "saldırıya uğradı"]),

        new(nameof(FatherDeathReason.War), "Savaş",
            ["savaşta şehit oldu", "cephe hattında vefat etti", "savaş sırasında öldürüldü"]),

        new(nameof(FatherDeathReason.Flood), "Sel",
            ["sel sularında boğuldu", "taşkın sırasında vefat etti", "su baskınında kaldı"]),

        new(nameof(FatherDeathReason.Malaria), "Sıtma",
            ["sıtma hastalığı vardı", "sıtma ateşi geçirdi", "tropikal hastalık bulaştı"]),

        new(nameof(FatherDeathReason.Cirrhosis), "Siroz",
            ["karaciğer sirozu vardı", "cirroz hastalığı", "karaciğer yetmezliği"]),

        new(nameof(FatherDeathReason.Diabetes), "Şeker Hastalığı",
            ["şeker hastalığı vardı", "diyabet kontrolsüz idi", "komplikasyonlar gelişti"]),

        new(nameof(FatherDeathReason.BloodPressure), "Tansiyon",
            ["tansiyon krizi yaşadı", "yüksek tansiyon vardı", "hipertansiyon öldürdü"]),

        new(nameof(FatherDeathReason.Terrorism), "Terör Olayları",
            ["bomba patladı", "şehit oldu", "terör saldırısında vefat etti"]),

        new(nameof(FatherDeathReason.TrafficAccident), "Trafik Kazası",
            ["araba çarptı", "araç takla attı", "motor kazası yaptı", "otobüs kazasında vefat etti"]),

        new(nameof(FatherDeathReason.Other), "Diğer",
            ["başka bir nedenle öldü", "belirtilmeyen neden", "tanı konulamadı"])
    };

    // =========================================================================
    // 3. BABA MESLEKLERİ (FatherProfession)
    // =========================================================================
    public static readonly IReadOnlyList<MatchCandidate> FatherProfessions = new List<MatchCandidate>
    {
        new(nameof(FatherProfession.MilitaryOrPolice), "Asker / Polis",
            ["uzman çavuştu", "astsubaydı", "emniyette polisti", "bekçiydi"]),

        new(nameof(FatherProfession.Chef), "Aşçı",
            ["restorand çalışıyordu", "aşçılık yapıyordu", "mutfakta çalışana", "großküche şefi"]),

        new(nameof(FatherProfession.Lawyer), "Avukat",
            ["avukatlık yapıyordu", "hukuk bürosunun sahibiydi", "adli meslek ilgili"]),

        new(nameof(FatherProfession.BarberOrHairdresser), "Berber / Kuaför",
            ["berber dükkanı vardı", "kuaför olarak çalışıyordu", "traş makinesine sahipti"]),

        new(nameof(FatherProfession.Farmer), "Çiftçi",
            ["tarla ekiyordu", "bağ bahçe işi yapıyordu", "tarımla uğraşıyordu", "köylü"]),

        new(nameof(FatherProfession.Doctor), "Doktor",
            ["hekimlik yapıyordu", "sağlık ocağında çalışıyordu", "ameliyathane çalışanıydı"]),

        new(nameof(FatherProfession.Pharmacist), "Eczacı",
            ["eczane açısından sahibiydi", "ilaç satıyordu", "Pharmacist olarak çalışıyordu"]),

        new(nameof(FatherProfession.Waiter), "Garson",
            ["lokantada çalışıyordu", "kafe servisi yapıyordu", "tabakları dağıtıyordu"]),

        new(nameof(FatherProfession.DayLaborer), "Gündelik İşçi",
            ["yevmiyeli çalışıyordu", "günlük işlere gidiyordu", "hammallık yapıyordu"]),

        new(nameof(FatherProfession.AnimalHusbandry), "Hayvancılık",
            ["çobanydı", "hayvan bakıyordu", "besicilik yapıyordu"]),

        new(nameof(FatherProfession.Nurse), "Hemşire",
            ["hemşirelik yapıyordu", "hastanede çalışıyordu", "hasta bakıcıydı"]),

        new(nameof(FatherProfession.ReligiousOfficial), "İmam / Din Görevlisi",
            ["imama camide çalışıyordu", "dini hizmetlerde görev alıyordu", "müezzin görevi yapıyordu"]),

        new(nameof(FatherProfession.Worker), "İşçi",
            ["fabrikada çalışıyordu", "asgari ücretli işçiydi", "tekstilde çalışıyordu", "ameleydi", "inşaat işçisi"]),

        new(nameof(FatherProfession.Miner), "Madenci",
            ["madenin çalışıyordu", "madene gidiyordu", "antrasit çıkartıyordu"]),

        new(nameof(FatherProfession.CivilServant), "Memur",
            ["devlet dairesindeydi", "kamuda çalışıyordu", "belediyede memurdu"]),

        new(nameof(FatherProfession.Accountant), "Muhasebeci",
            ["muhasebe işleriyle uğraşıyordu", "defteri kebirleri tutuyordu", "finansal işleri takip ediyordu"]),

        new(nameof(FatherProfession.Engineer), "Mühendis",
            ["mühendislik yapıyordu", "projealde çalışıyordu", "inşaat mühendisiydi"]),

        new(nameof(FatherProfession.AutoMechanic), "Oto Tamircisi",
            ["sanayide ustaydı", "araba tamir ediyordu", "kaportacıydı", "motor ustası"]),

        new(nameof(FatherProfession.TeacherOrEducator), "Öğretmen / Eğitimci",
            ["okulda öğretmen çalışıyordu", "eğitim sektöründe çalışıyordu", "dersler veriyordu"]),

        new(nameof(FatherProfession.HealthcareWorker), "Sağlık Görevlisi",
            ["sağlık alanında çalışıyordu", "hastane personeli idi", "tıbbi yardımcıydı"]),

        new(nameof(FatherProfession.Freelancer), "Serbest Meslek",
            ["kendi işini yapıyordu", "pazarcıydı", "seyyar satıcıydı"]),

        new(nameof(FatherProfession.Driver), "Şoför",
            ["taksiciydi", "kamyon sürüyordu", "tır şoförüydü", "servis çekiyordu", "dolmuşçu"]),

        new(nameof(FatherProfession.Tailor), "Terzi",
            ["dükkanında terzilik yapıyordu", "elbise dikhiyordu", "kumaş işleriyle uğraşıyordu"]),

        new(nameof(FatherProfession.Plumber), "Tesisatçı",
            ["su tesisatçısı", "doğalgazcı", "borucu"]),

        new(nameof(FatherProfession.Artisan), "Zanaatkar / Esnaf",
            ["dükkanı vardı", "marangozdu", "kaynakçıydı", "demirciydi"]),

        new(nameof(FatherProfession.Other), "Diğer",
            ["başka bir meslek dalında çalışıyordu", "belirtilmeyen meslekteydi", "farklı işler yapıyordu"])
    };
}