using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Models;

/// <summary>
/// Data transfer object containing tracking metadata and timestamps for a registration.
/// </summary>
public record RegistrationMetaDto(
    string RegistrationKey,
    long ChatId,
    string CreatedAt,
    string UpdatedAt
);
