using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument.Dtos;

public record class ResidenceDto(string City, string District, string Neighborhood, string FullAddress);
