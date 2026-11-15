using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

/// <summary>
/// Represents a family member with a disability or long-term illness requiring care.
/// </summary>
public class DisabledMember
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Diagnosis { get; private set; }
    public string Status { get; private set; }
    public TreatmentStatus TreatmentStatus { get; private set; }
    public string Description { get; private set; }

    /// <summary>
    /// Initializes a new disabled family member record with health and treatment details.
    /// </summary>
    /// <param name="name">Full name of the disabled member.</param>
    /// <param name="diagnosis">Medical diagnosis of the disability or illness.</param>
    /// <param name="status">General health or mobility condition.</param>
    /// <param name="treatmentStatus">Current medical care and treatment status.</param>
    /// <param name="description">Additional context or special care requirements.</param>
    public DisabledMember(string name, string diagnosis, string status, TreatmentStatus treatmentStatus, string description)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(diagnosis) || string.IsNullOrWhiteSpace(status))
            throw new BusinessRuleException("Error_DisabledMemberFieldsRequired");

        Id = Guid.NewGuid();
        Name = name;
        Diagnosis = diagnosis;
        Status = status;
        TreatmentStatus = treatmentStatus;
        Description = description ?? string.Empty;
    }
}
