using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

/// <summary>
/// Represents a verified postal address for an applicant or household.
/// </summary>
public record Address
{
    public string Country { get; init; }
    public string City { get; init; }
    public string District { get; init; }
    public string Neighborhood { get; init; }
    public string FullAddress { get; init; }

    public Address(string country, string city, string neighborhood, string district, string fullAddress)
    {
        // Field inspection teams require every address field to locate households for physical aid delivery.
        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(neighborhood) || string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(fullAddress))
            throw new BusinessRuleException("Error_AddressFieldsRequired");

        Country = country;
        City = city;
        Neighborhood = neighborhood;
        District = district;
        FullAddress = fullAddress;
    }
}
