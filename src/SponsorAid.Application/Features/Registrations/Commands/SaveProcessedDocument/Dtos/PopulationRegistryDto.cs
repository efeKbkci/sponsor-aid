namespace SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument.Dtos;

// Üst Sınıf (Temel Özellikler)
public abstract record PersonDto(string Name, string IdNumber, DateOnly BirthDate);

// Kalıtım Alan Alt Sınıflar
public record MotherDto(string Name, string IdNumber, DateOnly BirthDate)
    : PersonDto(Name, IdNumber, BirthDate);

public record FatherDto(string Name, string IdNumber, DateOnly BirthDate, DateOnly DeathDate)
    : PersonDto(Name, IdNumber, BirthDate);

public record ChildDto(string Name, string IdNumber, DateOnly BirthDate)
    : PersonDto(Name, IdNumber, BirthDate);

// Worker'ın bize göndereceği asıl paket (Ana DTO)
public record PopulationRegistryDto(
    MotherDto Mother,
    FatherDto Father,
    List<ChildDto> Children
);