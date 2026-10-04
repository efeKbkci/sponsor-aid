using SponsorAid.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace SponsorAid.Domain.Entities;

public class Child
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IdNumber IdentityNumber { get; private set; }
    public DateTime BirthDate { get; private set; }

    // Otomasyon tarafından doldurulacak alanlar
    public string Hobbies { get; private set; } = string.Empty;
    public string FutureDream { get; private set; } = string.Empty;

    public Child(string name, IdNumber identityNumber, DateTime birthDate, FatherDetails fatherDetails)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Çocuk ismi boş olamaz.");

        if (birthDate > DateTime.Now)
            throw new ArgumentException("Doğum tarihi gelecekte olamaz.");

        // 15 yaş kuralı denetimi (Bugünün tarihinden 15 yıl öncesinden daha eski doğmuş olamaz)
        if (birthDate <= DateTime.Now.AddYears(-15))
            throw new ArgumentException("Sistem sadece 15 yaşından küçüklere burs vermeyi vaat ediyor.");

        Id = Guid.NewGuid();
        Name = name;
        IdentityNumber = identityNumber;
        BirthDate = birthDate;
    }

    // Dışarıdan property set edilmesini engelliyoruz, otomasyon bu metodu çağıracak
    public void SetAutomatedDetails(string hobbies, string futureDream)
    {
        Hobbies = hobbies ?? string.Empty;
        FutureDream = futureDream ?? string.Empty;
    }
}
