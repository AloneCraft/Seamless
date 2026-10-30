using Microsoft.CodeAnalysis;

using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class ProviderContainsShapeTests {
    [Fact]
    public void HiddenPersistedSrcCountBindsToProviderContains() {
        const string source = """
            using System.Linq;
            namespace Sample;
            public static class Route {
                public static global::Seamless.Collections.Persisted.Set<T> AsX<T>(this global::Seamless.Collections.Persisted.Set<T> source) => source;
                public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
            }
            public class ArrayBase<T> where T : notnull { protected internal T[] srcCount(int count) => []; }
            public class PersistedBase<T> : ArrayBase<T> where T : notnull { protected internal new global::Seamless.Collections.Persisted.Set<T> srcCount(int count) => throw null!; }
            public sealed class Container<T> : PersistedBase<T> where T : notnull {
                private bool Contains() { return this.srcCount(3).AsX<T>().Contains(default!); }
                public bool Run() => this.Contains();
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            !generated.Contains("UnsupportedLinqExpansion(\"Contains\"",StringComparison.Ordinal)
            &&!generated.Contains("public static bool Contains",StringComparison.Ordinal),
            "A hidden provider srcCount must remain a native provider Contains call.",generated,null);
    }

    [Fact]
    public void StaticArrayContainsUsesDirectSpanTerminal() {
        const string source = """
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer { public void Consume(bool value) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] private void Contains(int[] source, int value) { this.consumer.Consume(source.Contains(value)); }
                public void Run(int[] source,int value) => this.Contains(source,value);
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            generated.Contains("ContainsSpan",StringComparison.Ordinal)
            &&!generated.Contains("IEnumerator<int>",StringComparison.Ordinal),
            "Static array Contains must use the direct span terminal.",generated,null);
    }

    [Fact]
    public void StaticIEnumerableContainsUsesDirectInterfaceFallbackLoop() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer { public void Consume(bool value) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] private void Contains(IEnumerable<int> source, int value) { this.consumer.Consume(source.Contains(value)); }
                public void Run(IEnumerable<int> source,int value) => this.Contains(source,value);
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            generated.Contains("IEnumerator<int>",StringComparison.Ordinal)
            &&generated.Contains(".GetEnumerator()",StringComparison.Ordinal)
            &&generated.Contains(".MoveNext()",StringComparison.Ordinal)
            &&!generated.Contains("TryGetNext(out var genericItem",StringComparison.Ordinal),
            "Static IEnumerable<T>.Contains must use one direct IEnumerator<T> fallback loop.",generated,null);
    }

    [Fact]
    public void StaticIEnumerableContainsHoistsValueExpressionOutOfFallbackLoop() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer { public void Consume(bool value) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                private int calls;
                private int NextValue() => ++this.calls;
                [LinqIntercept(true)] private void Contains(IEnumerable<int> source) { this.consumer.Consume(source.Contains(this.NextValue())); }
                public void Run(IEnumerable<int> source) => this.Contains(source);
            }
            """;
        var generated=Generate(source);
        var callCount=generated.Split("__Method_Container_NextValue(@this)",StringSplitOptions.None).Length-1;
        GeneratedSourceAssert.True(
            generated.Contains("var genericValue=__Method_Container_NextValue(@this);",StringComparison.Ordinal)
            &&generated.Contains("comparer.Equals(genericItem,genericValue)",StringComparison.Ordinal)
            &&callCount==1,
            "Contains value expression must be evaluated once before the direct fallback loop.",generated,null);
    }

    [Fact]
    public void StaticIEnumerableElementAtOrDefaultUsesDirectInterfaceLoop() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer { public void Consume(int value) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] private void ElementAtOrDefault(IEnumerable<int> source,int index) { this.consumer.Consume(source.ElementAtOrDefault(index)); }
                public void Run(IEnumerable<int> source,int index) => this.ElementAtOrDefault(source,index);
            }
            """;
        var method=InterceptMethod(Generate(source),"ElementAtOrDefault");
        GeneratedSourceAssert.True(
            method.Contains("=default!;",StringComparison.Ordinal)
            &&method.Contains("ArgumentNullException.ThrowIfNull",StringComparison.Ordinal)
            &&method.Contains(".GetEnumerator()",StringComparison.Ordinal)
            &&method.Contains(".MoveNext()",StringComparison.Ordinal)
            &&method.Contains(".Current;",StringComparison.Ordinal)
            &&!method.Contains(".Compute(",StringComparison.Ordinal)
            &&!method.Contains("TryGetNext",StringComparison.Ordinal),
            "ElementAtOrDefault must use one direct IEnumerator<T> loop.",method,null);
    }

    [Fact]
    public void StaticSystemEnumerableContainsWithComparerPreservesComparerArgument() {
        const string source = """
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer { public void Consume(bool value) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] private void Contains(int[] source,int value) { this.consumer.Consume(global::System.Linq.Enumerable.Contains(source,value,global::System.Collections.Generic.EqualityComparer<int>.Default)); }
                public void Run(int[] source,int value) => this.Contains(source,value);
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            !generated.Contains("UnsupportedLinqExpansion(\"Contains\"",StringComparison.Ordinal)
            &&generated.Contains("EqualityComparer<int>.Default",StringComparison.Ordinal),
            "Static Contains with comparer must retain its comparer expression.",generated,null);
    }

    [Fact]
    public void StaticSystemEnumerableReverseOverListUsesDirectReverseLoop() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Consumer { public void Consume(int value) { } }
            public static class EnumerableExtensions { public static void Consume<T>(this IEnumerable<T> source,Consumer consumer) { } }
            public sealed class Container {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] private void Reverse(List<int> source) { global::System.Linq.Enumerable.Reverse(source).Consume(this.consumer); }
                public void Run(List<int> source) => this.Reverse(source);
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            generated.Contains("reverseSpan",StringComparison.Ordinal)
            &&generated.Contains("Length-1",StringComparison.Ordinal)
            &&!generated.Contains("IEnumerator<int>",StringComparison.Ordinal),
            "Static Reverse over List<T> must remain a direct reverse span loop.",generated,null);
    }

    [Fact]
    public void ProviderAsXContainsUsesNativeLookupTerminal() {
        const string source = """
            using Seamless.FLinq;
            namespace Sample;
            public static class Route {
                public static global::Seamless.Collections.Persisted.Set<T> AsX<T>(this global::Seamless.Collections.Persisted.Set<T> source) => source;
                public static global::System.Collections.Generic.IEnumerable<T> AsX<T>(this global::System.Collections.Generic.IEnumerable<T> source) => source;
            }
            public class Base { protected internal global::Seamless.Collections.Persisted.Set<int> srcCount(int count) => throw null!; }
            public sealed class Consumer { public void Consume(bool value) { } }
            public sealed class Container : Base {
                private readonly Consumer consumer = new();
                [LinqIntercept(true)] public void ContainsValue() { this.consumer.Consume(this.srcCount(3).AsX<int>().Contains(default!)); }
                public void Run() => this.ContainsValue();
            }
            """;
        var generated=Generate(source);
        GeneratedSourceAssert.True(
            !generated.Contains("UnsupportedLinqExpansion(\"ContainsValue\"",StringComparison.Ordinal)
            &&!generated.Contains("OptimizedLinq_ContainsValue",StringComparison.Ordinal),
            "Provider AsX<T>.Contains must remain a native provider lookup and must not create an interceptor forwarder.",generated,null);
    }
    [Fact]
    public void WorkCollectionsUseEnumerableFallbackWithoutLegacyProviderDispatch(){
        foreach(var typeName in new[]{"SortedSet","HashSet","SortedList"}){
            var source=$$"""
                using System.Linq;
                using Seamless.FLinq;
                namespace Sample;
                public sealed class Consumer { public void Consume(bool value) { } }
                public sealed class Container {
                    private readonly Consumer consumer = new();
                    [LinqIntercept(true)] private void ContainsValues(global::Seamless.Collections.Persisted.Work.{{typeName}}<int> source,int value) {
                        this.consumer.Consume(global::System.Linq.Enumerable.Contains(source,value));
                    }
                    public void Run(global::Seamless.Collections.Persisted.Work.{{typeName}}<int> source,int value) => this.ContainsValues(source,value);
                }
                """;
            // Generate は生成後の compilation diagnostics も検証する。
            var generated=Generate(source);
            GeneratedSourceAssert.True(
                generated.Contains("IEnumerator<int>",StringComparison.Ordinal)
                &&generated.Contains(".GetEnumerator()",StringComparison.Ordinal)
                &&generated.Contains(".MoveNext()",StringComparison.Ordinal)
                &&!generated.Contains("global::Seamless.Collections.PersistedList<int>.Enumerator",StringComparison.Ordinal)
                &&!generated.Contains("UnsupportedLinqExpansion",StringComparison.Ordinal),
                $"Work.{typeName}<T> must compile through IEnumerable enumeration instead of a legacy provider or direct span root.",generated,null);
        }
    }
    private static string Generate(string source) {
        var compilation=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var generatorDiagnostics);
        var generated=string.Concat(driver.GetRunResult().Results.SelectMany(static result => result.GeneratedSources).Select(static generatedSource => generatedSource.SourceText.ToString()));
        var errors=generatorDiagnostics.Concat(outputCompilation.GetDiagnostics()).Where(static diagnostic => diagnostic.Severity==DiagnosticSeverity.Error).Where(static diagnostic => diagnostic.Id!="CS9137").ToArray();
        GeneratedSourceAssert.NoErrors(errors,generated,null);
        return generated;
    }

    private static string InterceptMethod(string generated,string methodName) {
        var start=generated.IndexOf($"Intercepts <c>{methodName}",StringComparison.Ordinal);
        var end=generated.IndexOf("private struct",start,StringComparison.Ordinal);
        return generated[start..(end<0 ? generated.Length : end)];
    }
}