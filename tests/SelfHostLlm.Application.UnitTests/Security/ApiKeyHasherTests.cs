using SelfHostLlm.Application.Security;
using SelfHostLlm.Domain.Access;

namespace SelfHostLlm.Application.UnitTests.Security;

public sealed class ApiKeyHasherTests
{
    private readonly ApiKeyHasher _hasher = new();

    [Fact]
    public void Generate_ProducesWellFormedKeyWithMatchingPrefixAndHash()
    {
        var key = _hasher.Generate();

        key.PlainText.Should().StartWith("sk-").And.HaveLength(ApiKeyHasher.KeyLength);
        ApiKeyHasher.IsWellFormed(key.PlainText).Should().BeTrue();
        key.PlainText.Should().StartWith(key.Prefix);
        key.Prefix.Should().HaveLength(ApiKeyHasher.DisplayPrefixLength);
        key.Hash.Should().Be(_hasher.ComputeHash(key.PlainText));
    }

    [Fact]
    public void Generate_TwoCalls_ProduceDifferentKeys()
    {
        _hasher.Generate().PlainText.Should().NotBe(_hasher.Generate().PlainText);
    }

    [Fact]
    public void ComputeHash_IsDeterministicLowerHexAcceptedByDomain()
    {
        var key = _hasher.Generate();

        var hash = _hasher.ComputeHash(key.PlainText);

        hash.Should().Be(_hasher.ComputeHash(key.PlainText)).And.MatchRegex("^[0-9a-f]{64}$");
        ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), hash, key.Prefix, null, DateTimeOffset.UtcNow)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ComputeHash_KnownVector_MatchesSha256()
    {
        // SHA-256("abc") — vector chuẩn của FIPS 180-2.
        _hasher.ComputeHash("abc").Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Verify_WithCorrectKey_ReturnsTrue()
    {
        var key = _hasher.Generate();

        _hasher.Verify(key.PlainText, key.Hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithDifferentKey_ReturnsFalse()
    {
        var key = _hasher.Generate();

        _hasher.Verify(_hasher.Generate().PlainText, key.Hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zz7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void Verify_WithMalformedStoredHash_ReturnsFalse(string storedHash)
    {
        _hasher.Verify("sk-anything", storedHash).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer sk-abc")]
    [InlineData("pk-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sk-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("sk-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+A")]
    public void IsWellFormed_WithMalformedKey_ReturnsFalse(string? key)
    {
        ApiKeyHasher.IsWellFormed(key).Should().BeFalse();
    }

    [Fact]
    public void GeneratedApiKey_ToString_DoesNotLeakPlainText()
    {
        var key = _hasher.Generate();

        key.ToString().Should().NotContain(key.PlainText).And.Contain(key.Prefix);
    }
}
