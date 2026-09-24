using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

public class DisabledMember
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Diagnosis { get; private set; }
    public string Status { get; private set; }
    public TreatmentStatus TreatmentStatus { get; private set; }
    public string Description { get; private set; }

    public DisabledMember(string name, string diagnosis, string status, TreatmentStatus treatmentStatus, string description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(diagnosis) || string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Hasta adı, teşhis ve durum bilgileri boş olamaz.");

        Id = Guid.NewGuid();
        Name = name;
        Diagnosis = diagnosis;
        Status = status;
        TreatmentStatus = treatmentStatus;
        Description = description ?? string.Empty;
    }
}
