using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Models;

public record RegistrationMetaDto(
    string RegistrationKey,
    long ChatId,
    string CreatedAt, // Artık string ("17.10.2026 23:59:00" formatında olacak)
    string UpdatedAt  // Artık string
);