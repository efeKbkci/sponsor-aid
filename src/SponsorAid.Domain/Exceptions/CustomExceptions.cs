namespace SponsorAid.Domain.Exceptions;

// Sistemin atası olan soyut hata sınıfı
public abstract class SponsorAidException : Exception
{
    protected SponsorAidException(string message, params object[] arguments) : base(message)
    {
        Arguments = arguments;
    }

    public IReadOnlyList<object> Arguments { get; }
}

// 1. İş Kuralları İhlali (Örn: "Vasi 18 yaşından büyük olmalıdır")
public class BusinessRuleException : SponsorAidException
{
    public BusinessRuleException(string message, params object[] arguments) : base(message, arguments) { }
}

// 2. Belge/Veri Doğrulama İhlali (Örn: "İsimler eşleşmiyor")
public class DocumentValidationException : SponsorAidException
{
    public DocumentValidationException(string message, params object[] arguments) : base(message, arguments) { }
}