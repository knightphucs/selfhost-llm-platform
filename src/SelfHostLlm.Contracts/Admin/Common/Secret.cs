namespace SelfHostLlm.Contracts.Admin.Common;

/// <summary>Che giá trị bí mật khi DTO bị in ra (ToString, log).</summary>
internal static class Secret
{
    public const string Masked = "***";

    public static string? Mask(string? value) => value is null ? null : Masked;
}
