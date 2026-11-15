using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Profession of the deceased father used for social background records.
/// </summary>
public enum FatherProfession
{
    [Description("Asker / Polis")] MilitaryOrPolice = 1,
    [Description("Aşçı")] Chef = 2,
    [Description("Avukat")] Lawyer = 3,
    [Description("Berber/ Kuaför")] BarberOrHairdresser = 4,
    [Description("Çiftçi")] Farmer = 5,
    [Description("Doktor")] Doctor = 6,
    [Description("Eczacı")] Pharmacist = 7,
    [Description("Garson")] Waiter = 8,
    [Description("Gündelik İşçi")] DayLaborer = 9,
    [Description("Hayvancılık")] AnimalHusbandry = 10,
    [Description("Hemşire")] Nurse = 11,
    [Description("İmam / Din Görevlisi")] ReligiousOfficial = 12,
    [Description("İşçi")] Worker = 13,
    [Description("Madenci")] Miner = 14,
    [Description("Memur")] CivilServant = 15,
    [Description("Muhasebeci")] Accountant = 16,
    [Description("Mühendis")] Engineer = 17,
    [Description("Oto Tamircisi")] AutoMechanic = 18,
    [Description("Öğretmen / Eğitimci")] TeacherOrEducator = 19,
    [Description("Sağlık Görevlisi")] HealthcareWorker = 20,
    [Description("Serbest Meslek")] Freelancer = 21,
    [Description("Şoför")] Driver = 22,
    [Description("Terzi")] Tailor = 23,
    [Description("Tesisatçı")] Plumber = 24,
    [Description("Zanaatkar")] Artisan = 25,
    [Description("Diğer")] Other = 26
}
