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

    [Theory]
    [InlineData("welke hulp is er in 2511CV?")]
    [InlineData("Postbus 12345 2500AB")]
    public void Postcode_without_house_number_is_kept(string input)
    {
        var r = PiiFilter.Redact(input);
        Assert.False(r.Redacted);
        Assert.Equal(input, r.Text);
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

    [Fact]
    public void Email_with_bsn_like_local_part_is_redacted_as_email_without_leaking_domain()
    {
        var r = PiiFilter.Redact("mail 111222333@example.org");
        Assert.DoesNotContain("example.org", r.Text);
        Assert.Contains("email", r.Types);
    }

    [Theory]
    [InlineData("ik woon op 2511CV12")]
    [InlineData("ik woon op 2511 cv 12-a")]
    public void Compact_and_hyphenated_addresses_are_fully_redacted(string input)
    {
        var r = PiiFilter.Redact(input);
        Assert.Contains("address", r.Types);
        Assert.Equal("ik woon op [address]", r.Text);
    }

    [Theory]
    [InlineData("bel +31 (0)6 12345678")]
    [InlineData("bel 06 1234 5678")]
    [InlineData("bel 06-12 34 56 78")]
    public void Phone_formats_with_parentheses_and_grouping_are_redacted(string input)
    {
        var r = PiiFilter.Redact(input);
        Assert.Equal("bel [phone]", r.Text);
    }

    [Fact]
    public void Bsn_and_email_in_same_text_both_reported_and_nothing_leaks()
    {
        var r = PiiFilter.Redact("bsn 111222333, mail 111222333@example.org");
        Assert.Equal(["bsn", "email"], r.Types.Order());
        Assert.DoesNotContain("111222333", r.Text);
        Assert.DoesNotContain("example.org", r.Text);
    }

    [Fact]
    public void All_zero_is_not_a_valid_bsn() => Assert.False(PiiFilter.IsValidBsn("000000000"));
}
