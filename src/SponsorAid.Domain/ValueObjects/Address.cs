using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record Address
{
    public string Country { get; init; }
    public string City { get; init; }
    public string District { get; init; }
    public string Neighborhood { get; init; }
    public string FullAddress { get; init; }

    public Address(string country, string city, string neighborhood, string district, string fullAddress)
    {
        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(neighborhood) || string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(fullAddress))
            throw new ArgumentException("Adres bilgileri eksik bırakılamaz.");

        Country = country;
        City = city;
        Neighborhood = neighborhood;
        District = district;
        FullAddress = fullAddress;
    }
}