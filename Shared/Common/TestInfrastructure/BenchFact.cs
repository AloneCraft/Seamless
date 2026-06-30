#if BENCH_AS_TEST
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Common;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchFactAttribute : FactAttribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchTheoryAttribute : TheoryAttribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BenchArgumentsAttribute(params object?[] data) : DataAttribute
{
    public override IEnumerable<object?[]> GetData(MethodInfo testMethod)
    {
        yield return data;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TestPriorityAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
}

#else
using BenchmarkDotNet.Attributes;

namespace Common;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchFactAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class BenchTheoryAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class BenchArgumentsAttribute(params object?[] data) : ArgumentsAttribute(data);

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FactAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TheoryAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InlineDataAttribute(params object?[] data) : Attribute
{
    public object?[] Data { get; } = data;
}

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
