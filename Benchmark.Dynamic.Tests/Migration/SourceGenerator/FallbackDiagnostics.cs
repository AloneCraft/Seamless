using InlineDataAttribute=Benchmark.Dynamic.Worker.RegressionDataAttribute;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using FactAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
using TheoryAttribute=Benchmark.Dynamic.Worker.RegressionContractAttribute;
namespace Test.SourceGenerator {
    // カウンター自体と、このプロジェクト全体の生成物を検査する。
    // チェーンの実行時フォールバック検証は Benchmark.Dynamic.Tests で行う。
    public sealed class FallbackDiagnostics {
        [Fact, Trait("Category","Unit")]
        public void EnumerableFallbackCounter() {
            {
                //加算と詳細集計
                var counter = new LinqCounter();
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.IncrementEnumerableFallback("Other.Method");
                Assert.Equal(3,counter.EnumerableFallback);
                Assert.Equal(2,counter.EnumerableFallbackDetails["Sample.Method"]);
            }
            {
                //Reset で全消去
                var counter = new LinqCounter();
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.Reset();
                Assert.Equal(0,counter.EnumerableFallback);
                Assert.Empty(counter.EnumerableFallbackDetails);
            }
        }

        [Fact, Trait("Category","Unit")]
        public void GeneratedInterceptors_ContainNoFallbackFailure() {
            var generatedRoot = FindGeneratedRoot();
            Assert.True(Directory.Exists(generatedRoot),$"生成コード出力先が見つかりません: {generatedRoot}");

            var failures = new List<string>();
            var inspectedFileCount = 0;
            var compiledTypes=typeof(FallbackDiagnostics).Assembly.GetTypes()
                .Where(type=>type.Namespace?.StartsWith("Intercept.",StringComparison.Ordinal)==true)
                .Select(type=>global::System.Text.RegularExpressions.Regex.Replace(type.FullName!.Replace('+','.'),@"`\d+",string.Empty))
                .ToHashSet(StringComparer.Ordinal);
            foreach(var file in Directory.EnumerateFiles(generatedRoot,"*.cs",SearchOption.AllDirectories)) {
                if(!file.Contains("FLinq",StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                var text = File.ReadAllText(file);
                // EmitCompilerGeneratedFilesは前回だけの生成物を消さないため、現在のアセンブリにある型だけを検査する。
                if(!CurrentAssemblyOwnsSource(text,compiledTypes)){continue;}
                if(!text.Contains("InterceptsLocation",StringComparison.Ordinal)) {
                    continue;
                }
                inspectedFileCount++;
                var index = 0;
                foreach(var marker in new[] { "LinqInterceptorFailure(","UnsupportedLinqExpansion(" }) {
                    index=0;
                    while((index=text.IndexOf(marker,index,StringComparison.Ordinal))>=0) {
                        var lineNumber = CountLines(text,index);
                        failures.Add($"{file}:{lineNumber}:{marker}");
                        index+=1;
                    }
                }
            }

            Assert.True(inspectedFileCount>0,$"FLinqの生成インターセプターが見つかりません: {generatedRoot}");
            Assert.True(failures.Count==0,
                "生成インターセプターに未展開フォールバックが残っています:\n"+string.Join("\n",failures));
        }

        private static bool CurrentAssemblyOwnsSource(string text,global::System.Collections.Generic.IReadOnlySet<string> compiledTypes){
            var root=global::Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach(var type in root.DescendantNodes().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()){
                var ns=string.Join(".",type.Ancestors().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>().Reverse().Select(item=>item.Name.ToString()));
                var names=type.Ancestors().OfType<global::Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Reverse().Select(item=>item.Identifier.ValueText).Append(type.Identifier.ValueText);
                if(compiledTypes.Contains(ns+"."+string.Join(".",names))){return true;}
            }
            return false;
        }

        private static string FindGeneratedRoot() {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            var targetFramework = directory.Name;
            var configuration = directory.Parent?.Name??string.Empty;
            var projectDirectory = directory.Parent?.Parent?.Parent;
            return Path.Combine(projectDirectory?.FullName??string.Empty,"obj",configuration,targetFramework,"generated");
        }

        private static int CountLines(string text,int position) {
            var lines = 1;
            for(var index = 0;index<position;index++) {
                if(text[index]=='\n') {
                    lines++;
                }
            }
            return lines;
        }
    }
}
