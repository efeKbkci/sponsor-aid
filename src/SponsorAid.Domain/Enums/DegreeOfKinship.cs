using System.ComponentModel;

namespace SponsorAid.Domain.Enums;

public enum DegreeOfKinship
{
    [Description("Anne")] Mother = 1,
    [Description("Amca")] PaternalUncle = 2,
    [Description("Akraba")] Relative = 3,
    [Description("Büyükanne")] Grandmother = 4,
    [Description("Dayı")] MaternalUncle = 5,
    [Description("Dede")] Grandfather = 6,
    [Description("Hala")] PaternalAunt = 7,
    [Description("Kardeş")] Sibling = 8,
    [Description("Teyze")] MaternalAunt = 9,
    [Description("Diğer")] Other = 10
}
