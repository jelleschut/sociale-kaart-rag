using SocialeKaartRag.Core.Policy;

namespace SocialeKaartRag.Core.Tests;

public class PiiFilterTests
{
    [Theory]
    [InlineData("mijn bsn is 111222333", "bsn")]      // geldig volgens 11-proef
    [InlineData("mail me op jan@example.org", "email")]
    [InlineData("bel 06-12345678 aub", "phone")]
    [InlineData("bel +31 6 12345678 aub", "phone")]
    [InlineData("bel 070-1234567 aub", "phone")]
    [InlineData("ik woon op 2511CV 12", "address")]
    [InlineData("ik woon op 2511 CV 12a", "address")]
    public void Detects_and_redacts(string input, string expectedType)
    {
        var r = PiiFilter.Redact(input);
        Assert.True(r.Redacted);
        Assert.Contains(expectedType, r.Types);
        Assert.DoesNotContain("111222333", r.Text);
        Assert.DoesNotContain("example.org", r.Text);
        Assert.DoesNotContain("12345678", r.Text);
        Assert.DoesNotContain("1234567", r.Text);
        Assert.DoesNotContain("2511", r.Text);
        Assert.Contains($"[{expectedType}]", r.Text);
    }

    [Fact]
    public void Nine_digits_failing_elfproef_is_not_a_bsn()
    {
        var r = PiiFilter.Redact("ordernummer 123456789");
        Assert.DoesNotContain("bsn", r.Types);
        Assert.Contains("123456789", r.Text);
    }

    [Fact]
    public void Postcode_without_house_number_is_kept()
    {
        var r = PiiFilter.Redact("welke hulp is er in 2511CV?");
        Assert.False(r.Redacted);
        Assert.Equal("welke hulp is er in 2511CV?", r.Text);
    }

    [Fact]
    public void Clean_question_is_untouched()
    {
        var r = PiiFilter.Redact("waar kan ik hulp krijgen bij schulden?");
        Assert.False(r.Redacted);
        Assert.Empty(r.Types);
        Assert.Equal("waar kan ik hulp krijgen bij schulden?", r.Text);
    }

    [Fact]
    public void Multiple_types_are_all_reported_once()
    {
        var r = PiiFilter.Redact("bsn 111222333 en 111222333, mail a@b.nl");
        Assert.Equal(["bsn", "email"], r.Types.Order());
    }

    [Theory]
    [InlineData("111222333", true)]
    [InlineData("123456789", false)]
    [InlineData("999999999", false)]
    [InlineData("12345678", false)]
    public void Elfproef(string number, bool valid) => Assert.Equal(valid, PiiFilter.IsValidBsn(number));
}
