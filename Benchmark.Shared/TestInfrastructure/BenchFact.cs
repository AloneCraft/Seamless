#if BENCH_AS_TEST
using Xunit;

namespace Benchmark.Shared;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchFactAttribute : FactAttribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

#else
namespace Benchmark.Shared;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchFactAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FactAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraitAttribute(string name, string value) : Attribute
{
    public string Name { get; } = name;
    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}
#endif
