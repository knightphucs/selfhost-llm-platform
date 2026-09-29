using SelfHostLlm.Domain.Access;
using SelfHostLlm.Domain.Common;

namespace SelfHostLlm.Domain.UnitTests.Access;

public sealed class ApiKeyTests
{
    private static readonly string ValidHash = new('a', ApiKey.KeyHashLength);

    private static ApiKey NewKey(DateTimeOffset? expiresAt = null) =>
        ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), ValidHash, "sk-a1b2", expiresAt, TestClock.Now).Value;

    [Fact]
    public void IsActiveAt_NewKeyWithoutExpiry_ReturnsTrue()
    {
        NewKey().IsActiveAt(TestClock.Now.AddYears(5)).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_AfterExpiry_ReturnsFalse()
    {
        var key = NewKey(expiresAt: TestClock.Now.AddDays(30));

        key.IsActiveAt(TestClock.Now.AddDays(29)).Should().BeTrue();
        key.IsActiveAt(TestClock.Now.AddDays(30)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_ActiveKey_MakesKeyInactive()
    {
        var key = NewKey();

        var result = key.Revoke(TestClock.Now);

        result.IsSuccess.Should().BeTrue();
        key.RevokedAt.Should().Be(TestClock.Now);
        key.IsActiveAt(TestClock.Now).Should().BeFalse();
    }

    [Fact]
    public void Revoke_AlreadyRevokedKey_ReturnsConflict()
    {
        var key = NewKey();
        key.Revoke(TestClock.Now);

        var result = key.Revoke(TestClock.Now.AddMinutes(1));

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        key.RevokedAt.Should().Be(TestClock.Now);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sk-plaintext-should-never-be-stored-here-0000000000000000000000")]
    public void Create_WithNonSha256HexHash_ReturnsValidationError(string hash)
    {
        ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), hash, "sk-a1b2", null, TestClock.Now)
            .Error!.Code.Should().Be("api_key.key_hash.invalid");
    }

    [Fact]
    public void Create_WithPastExpiry_ReturnsValidationError()
    {
        ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), ValidHash, "sk-a1b2", TestClock.Now.AddSeconds(-1), TestClock.Now)
            .Error!.Code.Should().Be("api_key.expires_at.past");
    }
}
