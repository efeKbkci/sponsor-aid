using System.Text.Json;
using FluentAssertions;
using SponsorAid.Application.Models;
using SponsorAid.Domain.Enums;
using SponsorAid.Domain.Exceptions;
using SponsorAid.Domain.ValueObjects;
using Xunit;

namespace SponsorAid.Tests;

/// <summary>
/// Unit test suite for the <see cref="Iban"/> domain value object.
/// Verifies sanitization, Turkish 26-character digit format rules, ISO 7064 MOD-97 checksum validation,
/// exception parity between constructor and <see cref="Iban.TryCreate"/>, and System.Text.Json serialization.
/// </summary>
public class IbanTests
{
    private const string ValidIban1 = "TR330006100519786457841326";
    private const string ValidIban2 = "TR730006200001234567890123";
    private const string InvalidMod97Iban = "TR330006100519786457841327";

    [Theory]
    [InlineData("TR330006100519786457841326", ValidIban1)]
    [InlineData("TR730006200001234567890123", ValidIban2)]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", ValidIban1)]
    [InlineData("TR73-0006-2000-0123-4567-8901-23", ValidIban2)]
    [InlineData("  tr33 0006 1005 1978 6457 8413 26  ", ValidIban1)]
    [InlineData("tr730006200001234567890123", ValidIban2)]
    public void ConstructorAndTryCreate_WithValidTurkishIbanVariants_ShouldNormalizeAndSucceed(
        string rawInput,
        string expectedNormalized)
    {
        // Act
        var constructed = new Iban(rawInput);
        var tryResult = Iban.TryCreate(rawInput, out var parsed);

        // Assert
        constructed.Value.Should().Be(expectedNormalized);
        tryResult.Should().BeTrue($"'{rawInput}' geçerli bir Türk IBAN numarasıdır.");
        parsed.Should().NotBeNull();
        parsed!.Value.Should().Be(expectedNormalized);
        parsed.Should().Be(constructed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void ConstructorAndTryCreate_WithNullOrWhitespace_ShouldRejectAsRequired(string? input)
    {
        // Act
        Action act = () => _ = new Iban(input!);
        var tryResult = Iban.TryCreate(input, out var parsed);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_IbanRequired");
        tryResult.Should().BeFalse();
        parsed.Should().BeNull();
    }

    [Theory]
    [InlineData("DE89370400440532013000")]          // Valid German IBAN, non-TR prefix
    [InlineData("US330006100519786457841326")]      // 26 chars, non-TR prefix
    [InlineData("TR33000610051978645784132")]       // Too short (25 chars)
    [InlineData("TR3300061005197864578413260")]     // Too long (27 chars)
    [InlineData("TR33000610051978645784132A")]      // Non-digit character in BBAN
    [InlineData("TR80000610051978645784132A")]      // Non-digit BBAN whose ISO-7064 expansion is 1 mod 97
    [InlineData("TRAB0006100519786457841326")]      // Non-digit check characters
    [InlineData("TR33000610051978645784132@")]      // Symbol character in BBAN
    public void ConstructorAndTryCreate_WithInvalidFormatOrNonDigitBban_ShouldRejectAsInvalidFormat(string input)
    {
        // Act
        Action act = () => _ = new Iban(input);
        var tryResult = Iban.TryCreate(input, out var parsed);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_InvalidIbanFormat",
                $"'{input}' format kuralını (TR + 24 rakam) ihlal ettiği için Error_InvalidIbanFormat fırlatmalıdır.");
        tryResult.Should().BeFalse($"'{input}' için TryCreate false dönmelidir.");
        parsed.Should().BeNull();
    }

    [Fact]
    public void ConstructorAndTryCreate_WithInvalidMod97Checksum_ShouldRejectAsInvalidChecksum()
    {
        // Act
        Action act = () => _ = new Iban(InvalidMod97Iban);
        var tryResult = Iban.TryCreate(InvalidMod97Iban, out var parsed);

        // Assert
        act.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.Message == "Error_InvalidIbanChecksum");
        tryResult.Should().BeFalse();
        parsed.Should().BeNull();
    }

    [Fact]
    public void JsonSerializer_RoundTripOfRegistrationDraftStateWithIban_ShouldPreserveNormalizedValue()
    {
        // Arrange
        var draft = new RegistrationDraftState
        {
            RegistrationKey = "JSON01",
            ChatId = 777001L,
            CurrentStep = RegistrationStep.WaitingForQuestions,
            Guardian = new GuardianDraft
            {
                Name = "AYŞE YILMAZ",
                Iban = new Iban("TR33 0006 1005 1978 6457 8413 26")
            },
            SystemDocuments = new SystemDocumentsDraft
            {
                BankAccountDocumentPath = "uploads/bank.pdf"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(draft);
        var deserialized = JsonSerializer.Deserialize<RegistrationDraftState>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.CurrentStep.Should().Be(RegistrationStep.WaitingForQuestions);
        deserialized.SystemDocuments.BankAccountDocumentPath.Should().Be("uploads/bank.pdf");
        deserialized.Guardian.Iban.Should().NotBeNull();
        deserialized.Guardian.Iban!.Value.Should().Be(ValidIban1);
    }
}
