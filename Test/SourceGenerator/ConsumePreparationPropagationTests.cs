using Microsoft.CodeAnalysis;

using Seamless.FLinq;

namespace Test.SourceGenerator;

[LinqIntercept(false)]
public sealed class ConsumePreparationPropagationTests(ITestOutputHelper output) {
    [Fact]
    public void DelegatingWrappersForwardPreparedChildStatements() {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Consumer<T> {
                public void Consume(T item) {
                }
            }
            public static class ConsumeSupport {
                public static void Consume<T>(this IEnumerable<T> source,Consumer<T> consumer) {
                    foreach(var item in source) {
                        consumer.Consume(item);
                    }
                }
            }
            public sealed class Container {
                private static void CastPipeline<T>(int start,int count,Func<int,T> selector,Consumer<T> consumer) =>
                    Enumerable.Range(start,count).Select(selector).Cast<T>().Consume(consumer);
                private static void OfTypePipeline<T>(int start,int count,Func<int,T> selector,Consumer<T> consumer) =>
                    Enumerable.Range(start,count).Select(selector).OfType<T>().Consume(consumer);
                private static void ReversePipeline(int start,int count,Consumer<int> consumer) =>
                    Enumerable.Range(start,count).Select(static value=>value+1).Reverse().Reverse().Consume(consumer);
                private static void CastConcat<T>(IEnumerable<T> first,IEnumerable<T> second,Consumer<T> consumer) =>
                    first.Concat(second).Cast<T>().Consume(consumer);
                private static void OfTypeConcat<T>(IEnumerable<T> first,IEnumerable<T> second,Consumer<T> consumer) =>
                    first.Concat(second).OfType<T>().Consume(consumer);
                private static void ReverseConcat(int start,int count,Consumer<int> consumer) =>
                    Enumerable.Range(start,count).Concat(Enumerable.Range(start+10,count)).Reverse().Reverse().Consume(consumer);
                [LinqIntercept(true)]
                public static void Run<T>(int start,int count,IEnumerable<T> first,IEnumerable<T> second,Func<int,T> selector,Consumer<T> consumer,Consumer<int> intConsumer) {
                    CastPipeline(start,count,selector,consumer);
                    OfTypePipeline(start,count,selector,consumer);
                    ReversePipeline(start,count,intConsumer);
                    CastConcat(first,second,consumer);
                    OfTypeConcat(first,second,consumer);
                    ReverseConcat(start,count,intConsumer);
                }
            }
            """;

        var generated = GeneratedSource(source);

        var reverseConcat=GeneratedMethod(generated,"ReverseConcat");
        GeneratedSourceAssert.True(
            reverseConcat.Contains("ArgumentOutOfRangeException(\"count\")",StringComparison.Ordinal)
            &&reverseConcat.Contains("concatLeftCount",StringComparison.Ordinal)
            &&reverseConcat.Contains("concatRightCount",StringComparison.Ordinal),
            "Double Reverse must retain each prepared child's validation and direct consume locals.",
            generated,
            output);
    }

    private static string GeneratedMethod(string generated,string methodName) {
        var summaryMarker=$"<c>{methodName}()</c>";
        var summaryIndex=generated.IndexOf(summaryMarker,StringComparison.Ordinal);
        GeneratedSourceAssert.True(summaryIndex>=0,$"Generated method {methodName} was not found.",generated,null);
        var start=generated.IndexOf("public static",summaryIndex,StringComparison.Ordinal);
        var openBrace=generated.IndexOf('{',start);
        GeneratedSourceAssert.True(start>=0&&openBrace>=0,$"Generated method {methodName} has no body.",generated,null);
        var depth=0;
        for(var index=openBrace;index<generated.Length;index++) {
            if(generated[index]=='{') {
                depth++;
            } else if(generated[index]=='}') {
                depth--;
                if(depth==0) {
                    return generated[start..(index+1)];
                }
            }
        }
        return generated[start..];
    }

    private static string GeneratedSource(string source) {
        var comp=GeneratorTestHelpers.CreateCompilation([("Container.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(comp,out var outputCompilation,out var generatorDiagnostics);
        var generated=string.Concat(driver.GetRunResult().Results
            .SelectMany(static result=>result.GeneratedSources)
            .Select(static result=>result.SourceText.ToString()));
        var diagnostics=generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error)
            .Where(static diagnostic=>diagnostic.Id!="CS9137")
            .ToArray();
        GeneratedSourceAssert.NoErrors(diagnostics,generated,null);
        return generated;
    }
}
