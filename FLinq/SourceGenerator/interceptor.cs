// #define Tsavorite_ENUMERATOR_MODE  // コメントを外すと常に Enumerator struct 方式になる（フラットループ無効化）

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static Seamless.FLinq.GeneratorHelpers;

using Seamless.FLinq.Linq.Fragments;
using Enumerable = Seamless.FLinq.Linq.Fragments.Enumerable;

namespace Seamless.FLinq;

[Generator]
public sealed partial class TsavoriteIncrementalGenerator : IIncrementalGenerator {
    private const string InterceptsLocationAttributeSource = """
        #nullable enable
        namespace System.Runtime.CompilerServices {
            [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
            internal sealed class InterceptsLocationAttribute : global::System.Attribute {
                public InterceptsLocationAttribute(int version,string data) {
                    this.Version=version;
                    this.Data=data;
                }
                public int Version { get; }
                public string Data { get; }
            }
        }
        """;

    private const string LinqRewriteAttributesSource = """
        #nullable enable
        namespace Seamless.FLinq {
            [global::System.Flags]
            internal enum LinqInstrumentation {
                None = 0,
                Counter = 1,
            }
            [global::System.AttributeUsage(global::System.AttributeTargets.Assembly|global::System.AttributeTargets.Class|global::System.AttributeTargets.Method,AllowMultiple=false)]
            internal sealed class LinqInterceptAttribute : global::System.Attribute {
                public LinqInterceptAttribute(bool optimizeLinq) {
                    this.OptimizeLinq=optimizeLinq;
                    this.Intercept=optimizeLinq;
                }
                public LinqInterceptAttribute(bool optimizeLinq,LinqInstrumentation instrumentation) {
                    this.OptimizeLinq=optimizeLinq;
                    this.Intercept=optimizeLinq;
                    this.Instrumentation=instrumentation;
                }
                public bool OptimizeLinq { get; }
                public bool Intercept { get; }
                public LinqInstrumentation Instrumentation { get; set; }
            }
        }
        """;

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var interceptTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_)=>node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken)=>Linq傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target=>target is not null)
            .Select(static (target,_)=>target!);

        context.RegisterPostInitializationOutput(static productionContext=>{
            productionContext.AddSource("InterceptsLocationAttribute.g.cs",SourceText.From(InterceptsLocationAttributeSource,Encoding.UTF8));
            productionContext.AddSource("LinqRewriteAttributes.g.cs",SourceText.From(LinqRewriteAttributesSource,Encoding.UTF8));
        });

        // Tsavorite001 警告: グローバルな重複除去が必要なため Collect で一括処理（軽量）
        context.RegisterSourceOutput(interceptTargets.Collect(),static (productionContext,interceptTargets)=>{
            if(interceptTargets.IsDefaultOrEmpty) {
                return;
            }
            var reportedMethods = new HashSet<string>(StringComparer.Ordinal);
            foreach(var target in interceptTargets.Distinct(LinqInterceptTargetComparer.Instance)) {
                if(!target.LinqAnalysis.HasAnonymousTypeLinq) {
                    continue;
                }
                var methodKey = target.Method.ToDisplayString();
                if(!reportedMethods.Add(methodKey)) {
                    continue;
                }
                var location = target.Method.Locations.FirstOrDefault() ?? target.Invocation.GetLocation();
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    匿名型警告Descriptor,
                    location,
                    target.Method.Name));
            }
        });

        // コード生成: ファイル単位で独立してキャッシュされる
        var perFileGroups = interceptTargets
            .Collect()
            .SelectMany(static (targets,_)=>targets
                .Distinct(LinqInterceptTargetComparer.Instance)
                .GroupBy(static t=>t.SourcePath,StringComparer.Ordinal)
                .Select(static g=>new PerFileInterceptGroup(g.Key,[..g])));

        context.RegisterSourceOutput(perFileGroups,static (productionContext,group)=>{
            var diagnostics = new List<Diagnostic>();
            var source = インターセプターソース構築(group.Targets,diagnostics);
            if(source.Length>0) {
                productionContext.AddSource(インターセプターHintName取得(group.SourcePath),SourceText.From(source,Encoding.UTF8));
            }
            foreach(var diagnostic in diagnostics) {
                productionContext.ReportDiagnostic(diagnostic);
            }
        });
    }
}
