using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PgVector = Pgvector.Vector;
using SelfHostLlm.Domain.Deployments;

namespace SelfHostLlm.Persistence.Conversions;

/// <summary>Converter + comparer cho các kiểu Domain không map thẳng sang cột Postgres.</summary>
internal static class ValueConversions
{
    public static readonly ValueConverter<Address, string> AddressToString =
        new(a => a.ToString(), s => Address.Create(s).Value);

    /// <summary><c>ReadOnlyMemory&lt;float&gt;</c> ↔ <c>vector(n)</c> của pgvector.</summary>
    public static readonly ValueConverter<ReadOnlyMemory<float>, PgVector> MemoryToVector =
        new(m => new PgVector(m), v => v.Memory);

    public static readonly ValueComparer<ReadOnlyMemory<float>> MemoryComparer =
        new(
            (a, b) => a.ToArray().SequenceEqual(b.ToArray()),
            m => m.Length,
            m => new ReadOnlyMemory<float>(m.ToArray()));

    public static ValueConverter<List<TEnum>, string[]> EnumListToStringArray<TEnum>()
        where TEnum : struct, Enum =>
        new(
            list => list.Select(e => e.ToString()).ToArray(),
            array => array.Select(s => Enum.Parse<TEnum>(s)).ToList());

    public static ValueComparer<List<T>> ListComparer<T>() =>
        new(
            (a, b) => a!.SequenceEqual(b!),
            l => l.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            l => l.ToList());

    public static ValueConverter<Dictionary<string, TValue>, string> DictionaryToJson<TValue>() =>
        new(
            d => JsonSerializer.Serialize(d, (JsonSerializerOptions?)null),
            s => JsonSerializer.Deserialize<Dictionary<string, TValue>>(s, (JsonSerializerOptions?)null) ?? new());

    public static ValueComparer<Dictionary<string, TValue>> DictionaryComparer<TValue>() =>
        new(
            (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
            d => d.Count,
            d => new Dictionary<string, TValue>(d));
}
