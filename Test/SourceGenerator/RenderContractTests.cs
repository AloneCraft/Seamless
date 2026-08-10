using System.Reflection;

using Seamless.FLinq;

namespace Test.SourceGenerator;

public sealed class RenderContractTests {
    private static readonly Assembly GeneratorAssembly=typeof(FLinqIncrementalGenerator).Assembly;

    [Fact, Trait("Category","Unit")]
    public void GeneratedCodeBlock_ComposesAndIndentsLeafBlocksInOrder() {
        var blockType=GetGeneratorType("GeneratedCodeBlock");
        var head=InvokeStatic(blockType,"Leaf","head");
        var tail=InvokeStatic(blockType,"Leaf","tail");
        var sequence=InvokeStatic(blockType,"Sequence",ArrayOf(blockType,head,tail));
        var indented=InvokeInstance(sequence,"Indent","  ");

        Assert.Equal("  head\n  tail",InvokeRender(indented));
    }

    [Fact, Trait("Category","Unit")]
    public void ClosureRenderPlan_DeduplicatesDelegateFieldsAndPreservesFirstArgument() {
        var funcType=GetGeneratorType("DelegateFuncFragment");
        var first=CreateInternal(funcType,"global::System.Func<int,int>","selector","selector0");
        var duplicate=CreateInternal(funcType,"global::System.Func<int,int>","selector","selector1");
        var funcs=Array.CreateInstance(funcType,2);
        funcs.SetValue(first,0);
        funcs.SetValue(duplicate,1);

        var plan=InvokeStatic(GetGeneratorType("ClosureRenderPlan"),"Create",funcs);

        var delegates=GetProperty(plan,"Delegates") as global::System.Collections.ICollection;
        Assert.NotNull(delegates);
        Assert.Single(delegates!.Cast<object>());
        Assert.Equal(", selector0",InvokeInstance(plan,"ArgumentsSuffix"));
        Assert.Equal(", selector",InvokeInstance(plan,"FactoryArguments"));
    }

    [Fact, Trait("Category","Unit")]
    public void EnumeratorRenderPlan_RendersTryGetNextBeforeDispose() {
        var planType=GetGeneratorType("EnumeratorRenderPlan");
        var plan=InvokeStatic(
            planType,
            "Create",
            "private struct S : global::Seamless.Linq.IValueEnumerator<int> {",
            "S",
            "",
            "",
            "",
            "public int Current { get; private set; }",
            "",
            "",
            "",
            "current = 1;",
            "source.Dispose();",
            "int");

        var rendered=InvokeRender(plan);
        Assert.Contains("current = 1;",rendered,StringComparison.Ordinal);
        Assert.Contains("source.Dispose();",rendered,StringComparison.Ordinal);
        Assert.True(rendered.IndexOf("TryGetNext",StringComparison.Ordinal)<rendered.IndexOf("Dispose",StringComparison.Ordinal));
    }

    [Fact, Trait("Category","Unit")]
    public void DirectConsumeRenderPlan_PreservesSpanAndEnumeratorFallbackOrder() {
        var planType=GetGeneratorType("DirectConsumeRenderPlan");
        var direct=CreateInternal(
            planType,
            3,
            "source",
            "int",
            "consumer",
            true,
            false,
            "source.AsSpan()");
        var directText=InvokeRender(direct);
        Assert.Contains("IncrementDirectSpanUsed",directText,StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetNext",directText,StringComparison.Ordinal);

        var fallback=CreateInternal(
            planType,
            4,
            "source",
            "int",
            "consumer",
            true,
            false,
            null);
        var fallbackText=InvokeRender(fallback);
        var tryGetSpanIndex=fallbackText.IndexOf("TryGetSpan",StringComparison.Ordinal);
        var tryGetNextIndex=fallbackText.IndexOf("TryGetNext",StringComparison.Ordinal);
        Assert.True(tryGetSpanIndex>=0);
        Assert.True(tryGetNextIndex>tryGetSpanIndex);
    }

    [Fact, Trait("Category","Unit")]
    public void MethodBodyRenderPlan_PreservesScalarUsingBeforeTryGetNext() {
        var plan=InvokeStatic(
            GetGeneratorType("MethodBodyRenderPlan"),
            "ScalarRead",
            "int",
            "result",
            "enumerator",
            "source.GetEnumerator()");
        var rendered=InvokeRender(plan);
        var usingIndex=rendered.IndexOf("using var enumerator",StringComparison.Ordinal);
        var tryGetNextIndex=rendered.IndexOf("enumerator.TryGetNext",StringComparison.Ordinal);
        Assert.True(usingIndex>=0);
        Assert.True(tryGetNextIndex>usingIndex);
    }
    private Type GetGeneratorType(string name) =>
        GeneratorAssembly.GetType("Seamless.FLinq."+name,throwOnError:true)!;

    private static object CreateInternal(Type type,params object?[] arguments) =>
        Activator.CreateInstance(
            type,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
            binder:null,
            args:arguments,
            culture:null)!;

    private static object InvokeStatic(Type type,string name,params object?[] arguments) =>
        type.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,arguments)!;

    private static object InvokeInstance(object instance,string name,params object?[] arguments) =>
        instance.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(instance,arguments)!;

    private static string InvokeRender(object instance) => (string)InvokeInstance(instance,"Render");

    private static Array ArrayOf(Type elementType,params object[] values) {
        var result=Array.CreateInstance(elementType,values.Length);
        for(var i=0;i<values.Length;i++) {
            result.SetValue(values[i],i);
        }
        return result;
    }

    private static object? GetProperty(object instance,string name) =>
        instance.GetType().GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(instance);
}
