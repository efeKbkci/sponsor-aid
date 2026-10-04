namespace SponsorAid.Application.Interfaces;

// Bu arayüzü uygulayan her komut, içinde bir RegistrationKey taşımak zorundadır.
public interface IRegistrationCommand
{
    string RegistrationKey { get; }
}