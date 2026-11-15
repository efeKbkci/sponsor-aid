using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

/// <summary>
/// Current status of educational involvement.
/// </summary>
public enum EducationStatus
{
    [Description("Hiç Okula Gitmedi")] NeverAttended = 1,
    [Description("Mezun")] Graduated = 2,
    [Description("Okulu Bıraktı")] DroppedOut = 3,
    [Description("Öğrenci")] Student = 4
}
