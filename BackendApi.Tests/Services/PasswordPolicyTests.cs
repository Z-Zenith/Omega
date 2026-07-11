using BackendApi.Services;

namespace BackendApi.Tests.Services;

public class PasswordPolicyTests
{
    // #140 — no server-side check existed at all; a 1-character password used to be accepted.
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("short1")]
    public void IsValid_RejectsTooShortPasswords(string password)
    {
        Assert.False(PasswordPolicy.IsValid(password, out _));
    }

    [Fact]
    public void IsValid_RejectsPasswordWithOnlyLetters()
    {
        Assert.False(PasswordPolicy.IsValid("onlyletters", out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void IsValid_AcceptsPasswordMeetingLengthAndComplexity()
    {
        Assert.True(PasswordPolicy.IsValid("Str0ngPass!", out _));
    }

    [Fact]
    public void IsValid_RejectsNull()
    {
        Assert.False(PasswordPolicy.IsValid(null, out _));
    }
}
