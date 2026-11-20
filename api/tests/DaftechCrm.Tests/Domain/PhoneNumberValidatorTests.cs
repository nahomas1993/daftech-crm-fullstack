using DaftechCrm.Application;
using FluentAssertions;
using Xunit;

namespace DaftechCrm.Tests.Domain;

public class PhoneNumberValidatorTests
{
    [Theory]
    [InlineData("+251111136187", "+251111136187")]   // company landline example
    [InlineData("0111136187", "+251111136187")]
    [InlineData("251111136187", "+251111136187")]
    [InlineData("0911223344", "+251911223344")]
    [InlineData("+251 91 122 3344", "+251911223344")]
    [InlineData("091-122-3344", "+251911223344")]
    [InlineData("(011) 113.6187", "+251111136187")]
    [InlineData("0711223344", "+251711223344")]
    public void Valid_numbers_are_normalized_to_plus251(string input, string expected) =>
        PhoneNumberValidator.TryNormalize(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("091122334")]       // one digit short
    [InlineData("09112233445")]     // one digit long
    [InlineData("0611223344")]      // 6 is not a valid leading digit
    [InlineData("+254711223344")]   // Kenyan country code
    public void Invalid_numbers_are_rejected(string input) =>
        PhoneNumberValidator.TryNormalize(input).Should().BeNull();

    [Fact]
    public void Normalize_throws_a_readable_message_for_a_bad_number() =>
        FluentActions.Invoking(() => PhoneNumberValidator.Normalize("abc"))
            .Should().Throw<ValidationException>().WithMessage("*not valid*");
}
