using System.Reflection;

namespace SelfHostLlm.Domain;

/// <summary>Điểm neo để test kiến trúc lấy assembly Domain.</summary>
public static class DomainAssembly
{
    public static readonly Assembly Reference = typeof(DomainAssembly).Assembly;
}
