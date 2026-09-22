using System.Reflection;

namespace SelfHostLlm.Contracts;

/// <summary>Điểm neo để test kiến trúc lấy assembly Contracts.</summary>
public static class ContractsAssembly
{
    public static readonly Assembly Reference = typeof(ContractsAssembly).Assembly;
}
