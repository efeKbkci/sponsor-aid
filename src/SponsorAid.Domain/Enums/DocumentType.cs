using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SponsorAid.Domain.Enums;

public enum DocumentType
{
    [Description("Nüfus Kayıt Belgesi")] PopulationRegistry = 1,
    [Description("İkametgah Belgesi")] Residence = 2,
    [Description("Kimlik Belgesi")] Identity = 3    
}
