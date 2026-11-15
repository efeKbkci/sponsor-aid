using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Educational levels used to assess household background and social profile.
/// </summary>
public enum EducationLevel
{
    [Description("Diploma")] Diploma = 1,
    [Description("Doktora")] Doctorate = 2,
    [Description("İlkokul")] PrimarySchool = 3,
    [Description("Lisans")] BachelorsDegree = 4,
    [Description("Lise")] HighSchool = 5,
    [Description("Okur Yazar")] Literate = 6,
    [Description("Ortaokul")] MiddleSchool = 7,
    [Description("Ön Lisans")] AssociatesDegree = 8,
    [Description("Yüksek Lisans")] MastersDegree = 9
}
