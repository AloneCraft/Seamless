using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Seamless.FLinq;
using Seamless.FLinq.Generator;

namespace Test.SourceGenerator;

/// <summary>
/// Order().….Consume(consumer) の「ソースが既に昇順なら並べ替えを省く」直接経路の意味を、生成コードを実行して確かめる。
/// 2026-10-02 まで、この経路は Order の後ろの Where などを適用せずに全件を流し、
/// 昇順でないときの代替経路ではソース式をもう1回評価していた。
/// </summary>
[LinqIntercept(false)]
public sealed class SortedOrderDirectConsumeTests {
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.FLinq;
        namespace Sample;
        public sealed class Consumer<T> {
            public readonly List<T> Items = new();
            public void Consume(T item) => this.Items.Add(item);
        }
        public static class ConsumeSupport {
            public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                foreach(var item in source) {
                    consumer.Consume(item);
                }
            }
        }
        public sealed class Box(int value):IComparable<Box> {
            public int Value { get; } = value;
            public int CompareTo(Box? other) => this.Value.CompareTo(other!.Value);
            public override string ToString() => this.Value.ToString();
        }
        public sealed class Container {
            public static int SourceCalls;
            // FLinq は [LinqIntercept(true)] のメソッドの呼び出し箇所を、本体を展開した版へ差し替える。反射で直接呼ぶと差し替わらない。
            public static void RunArrayWhere(int[] values,Consumer<int> consumer) => ArrayWhere(values,consumer);
            public static void RunArrayDescendingSelect(int[] values,Consumer<int> consumer) => ArrayDescendingSelect(values,consumer);
            public static void RunListWhere(int[] values,Consumer<int> consumer) => ListWhere(values,consumer);
            public static void RunBoxWhere(int[] values,Consumer<Box> consumer) => BoxWhere(values,consumer);
            public static void RunArrayOnly(int[] values,Consumer<int> consumer) => ArrayOnly(values,consumer);
            [LinqIntercept(false)]
            private static int[] SourceArray(int[] values) {
                SourceCalls++;
                return values;
            }
            [LinqIntercept(false)]
            private static List<int> SourceList(int[] values) {
                SourceCalls++;
                return new List<int>(values);
            }
            [LinqIntercept(false)]
            private static Box[] SourceBoxes(int[] values) {
                SourceCalls++;
                return values.Select(static value => new Box(value)).ToArray();
            }
            [LinqIntercept(true)]
            private static void ArrayWhere(int[] values,Consumer<int> consumer) =>
                SourceArray(values).Order().Where(static value => (value&1)==0).Consume(consumer);
            [LinqIntercept(true)]
            private static void ArrayDescendingSelect(int[] values,Consumer<int> consumer) =>
                SourceArray(values).OrderDescending().Select(static value => value*10).Consume(consumer);
            [LinqIntercept(true)]
            private static void ListWhere(int[] values,Consumer<int> consumer) =>
                SourceList(values).Order().Where(static value => (value&1)==0).Consume(consumer);
            [LinqIntercept(true)]
            private static void BoxWhere(int[] values,Consumer<Box> consumer) =>
                SourceBoxes(values).Order().Where(static value => (value.Value&1)==0).Consume(consumer);
            [LinqIntercept(true)]
            private static void ArrayOnly(int[] values,Consumer<int> consumer) =>
                SourceArray(values).Order().Consume(consumer);
        }
        """;

    [Theory]
    [InlineData("ArrayWhere",new[] { 1,2,3,4,5,6 },"2,4,6")]
    [InlineData("ArrayWhere",new[] { 6,5,4,3,2,1 },"2,4,6")]
    [InlineData("ArrayDescendingSelect",new[] { 1,2,3 },"30,20,10")]
    [InlineData("ArrayDescendingSelect",new[] { 2,3,1 },"30,20,10")]
    [InlineData("ListWhere",new[] { 1,2,3,4,5,6 },"2,4,6")]
    [InlineData("ListWhere",new[] { 6,5,4,3,2,1 },"2,4,6")]
    [InlineData("BoxWhere",new[] { 1,2,3,4 },"2,4")]
    [InlineData("BoxWhere",new[] { 4,3,2,1 },"2,4")]
    [InlineData("ArrayOnly",new[] { 1,2,3 },"1,2,3")]
    [InlineData("ArrayOnly",new[] { 3,1,2 },"1,2,3")]
    public void 後続の段を適用しソース式を1回だけ評価する(string method,int[] values,string expected) {
        var (actual,sourceCalls) = Run(method,values);
        {
            //昇順でも昇順でなくても、Order の後ろの段が適用される
            Assert.Equal(expected,actual);
        }
        {
            //ソース式(副作用のあるメソッド呼び出し)は1回だけ評価する
            Assert.Equal(1,sourceCalls);
        }
    }

    private static (string Result,int SourceCalls) Run(string method,int[] values) {
        var compilation = GenerateExecutable(Source);
        using var pe = new MemoryStream();
        var emit = compilation.Emit(pe);
        Assert.True(emit.Success,string.Join(Environment.NewLine,emit.Diagnostics));
        pe.Position=0;
        var loadContext = new global::System.Runtime.Loader.AssemblyLoadContext("SortedOrderDirectConsume",isCollectible: true);
        try {
            var container = loadContext.LoadFromStream(pe).GetType("Sample.Container")!;
            var probe = container.GetMethod("Run"+method)!;
            var consumerType = probe.GetParameters()[1].ParameterType;
            var consumer = Activator.CreateInstance(consumerType)!;
            probe.Invoke(null,[values,consumer]);
            var items = (global::System.Collections.IEnumerable)consumerType.GetField("Items")!.GetValue(consumer)!;
            var result = string.Join(",",items.Cast<object>());
            var calls = (int)container.GetField("SourceCalls")!.GetValue(null)!;
            return (result,calls);
        } finally {
            loadContext.Unload();
        }
    }

    private static CSharpCompilation GenerateExecutable(string source) {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview)
            .WithFeatures([
                new("InterceptorsPreview","true"),
                new("InterceptorsNamespaces","Intercept"),
                new("InterceptorsPreviewNamespaces","Intercept"),
            ]);
        var compilation = GeneratorTestHelpers.CreateCompilation([("Container.cs",source)])
            .WithAssemblyName("SortedOrderDirectConsumeProbe");
        var originalTree = compilation.SyntaxTrees.Single();
        var executableTree = CSharpSyntaxTree.ParseText(source,parseOptions,path: "Container.cs");
        compilation=compilation.ReplaceSyntaxTree(originalTree,executableTree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new FLinqIncrementalGenerator().AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,trackIncrementalGeneratorSteps: false));
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var generatorDiagnostics);
        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic => diagnostic.Severity==DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
        // static class の中や反射で直接呼んだメソッドは差し替わらず、テストが空振りする。差し替えが生成されたことを確かめる。
        var generated = string.Concat(outputCompilation.SyntaxTrees.Select(static tree => tree.ToString()));
        Assert.True(generated.Split("InterceptsLocationAttribute(1,").Length-1>=5,"Order 系の呼び出しが傍受されていません。");
        return (CSharpCompilation)outputCompilation;
    }
}
