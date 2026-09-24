using SponsorAid.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.ValueObjects;

public record Address
{
    public string Country { get; init; }
    public TurkishCity City { get; init; }
    public string Town { get; init; }
    public string District { get; init; }
    public string FullAddress { get; init; }

    public Address(string country, TurkishCity city, string town, string district, string fullAddress)
    {
        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(town) ||
            string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(fullAddress))
            throw new ArgumentException("Adres bilgileri eksik bırakılamaz.");

        Country = country;
        City = city;
        Town = town;
        District = district;
        FullAddress = fullAddress;
    }
}