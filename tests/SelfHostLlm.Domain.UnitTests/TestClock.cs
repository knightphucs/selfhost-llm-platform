namespace SelfHostLlm.Domain.UnitTests;

internal static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
}
