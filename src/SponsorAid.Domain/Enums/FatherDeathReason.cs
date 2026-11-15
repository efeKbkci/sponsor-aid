using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Documented cause of death of the father for orphan registration criteria.
/// </summary>
public enum FatherDeathReason
{
    [Description("Ateş (Humma)")] Fever = 1,
    [Description("Beyin Kanaması")] BrainHemorrhage = 2,
    [Description("Boğulma")] Drowning = 3,
    [Description("Böbrek Yetmezliği")] KidneyFailure = 4,
    [Description("Deprem")] Earthquake = 5,
    [Description("Doğal Afet (Diğer)")] NaturalDisasterOther = 6,
    [Description("Doğal Ölüm")] NaturalDeath = 7,
    [Description("Ebola")] Ebola = 8,
    [Description("Elektrik Çarpması")] Electrocution = 9,
    [Description("Ev Kazası")] HomeAccident = 10,
    [Description("Felç")] Paralysis = 11,
    [Description("Hastalık (Diğer)")] IllnessOther = 12,
    [Description("Hepatit")] Hepatitis = 13,
    [Description("HIV / AIDS")] HivAids = 14,
    [Description("İdam")] Execution = 15,
    [Description("İntihar")] Suicide = 16,
    [Description("İş Kazası")] WorkAccident = 17,
    [Description("Kalp Krizi")] HeartAttack = 18,
    [Description("Kanser")] Cancer = 19,
    [Description("Kayıp")] Missing = 20,
    [Description("Öldürülme")] Murdered = 21,
    [Description("Savaş")] War = 22,
    [Description("Sel")] Flood = 23,
    [Description("Sıtma")] Malaria = 24,
    [Description("Siroz")] Cirrhosis = 25,
    [Description("Şeker Hastalığı")] Diabetes = 26,
    [Description("Tansiyon")] BloodPressure = 27,
    [Description("Terör Olayları")] Terrorism = 28,
    [Description("Trafik Kazası")] TrafficAccident = 29,
    [Description("Diğer")] Other = 30
}
