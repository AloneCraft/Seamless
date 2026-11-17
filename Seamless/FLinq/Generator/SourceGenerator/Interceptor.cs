using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
// #define Tsavorite_ENUMERATOR_MODE  // コメントを外すと常に Enumerator struct 方式になる（フラットループ無効化）
using Microsoft.CodeAnalysis.Text;
namespace Seamless.FLinq.Generator;

[Generator]
public sealed partial class FLinqIncrementalGenerator:IIncrementalGenerator {
    private const string InterceptsLocationAttributeSource = """
        #nullable enable
        namespace System.Runtime.CompilerServices {
            [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]
            internal sealed class InterceptsLocationAttribute : global::System.Attribute {
                public InterceptsLocationAttribute(int version,string data){
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
            [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=false)]
            internal sealed class ExecutionPlanAttribute:global::System.Attribute{
                public ExecutionPlanAttribute(int schemaVersion,string data){this.SchemaVersion=schemaVersion;this.Data=data;}
                public int SchemaVersion{get;}
                public string Data{get;}
            }
            [global::System.Flags]
            internal enum LinqOptimizations {
                None=0,
                Rewrite=1,
                PipelineFusion=2,
                PredicatePushdown=4,
                ProjectionMerge=8,
                All=Rewrite|PipelineFusion|PredicatePushdown|ProjectionMerge,
            }
            internal enum LinqConvergenceMode {
                Legacy=0,
                GlobalFixedPoint=1,
            }
            [global::System.Flags]
            internal enum LinqInstrumentation {
                None = 0,
                Counter = 1,
            }
            [global::System.AttributeUsage(global::System.AttributeTargets.Assembly|global::System.AttributeTargets.Class|global::System.AttributeTargets.Method|global::System.AttributeTargets.Field,AllowMultiple=false)]
            internal sealed class LinqInterceptAttribute : global::System.Attribute {
                public LinqInterceptAttribute(bool optimizeLinq){
                    this.OptimizeLinq=optimizeLinq;
                    this.Intercept=optimizeLinq;
                }
                public LinqInterceptAttribute(bool optimizeLinq,LinqInstrumentation instrumentation){
                    this.OptimizeLinq=optimizeLinq;
                    this.Intercept=optimizeLinq;
                    this.Instrumentation=instrumentation;
                }
                public bool OptimizeLinq { get; }
                public bool Intercept { get; }
                public LinqInstrumentation Instrumentation { get; set; }
                public LinqOptimizations Optimizations { get; set; } = LinqOptimizations.All;
                public LinqConvergenceMode ConvergenceMode { get; set; } = LinqConvergenceMode.Legacy;
                public bool EmitOptimizationTrace { get; set; }
            }
        }
        """;

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var generationEnabled=context.AnalyzerConfigOptionsProvider.Select(static(options,_)=>
            !options.GlobalOptions.TryGetValue("build_property.SeamlessFLinqGeneration",out var enabled)||!string.Equals(enabled,"false",StringComparison.OrdinalIgnoreCase));
        var analysisCompilation=context.CompilationProvider.Select(static (compilation,cancellationToken)=>
            global::Shared.Database.Generation.DatabaseAnalysisCompilation.Create(compilation,cancellationToken));
        var declaredJoinKeys=context.SyntaxProvider.CreateSyntaxProvider(
            static(node,_)=>node is InvocationExpressionSyntax{Parent:ArrowExpressionClauseSyntax{Parent:PropertyDeclarationSyntax}},
            static(syntaxContext,cancellationToken)=>GenerationPipeline.RenderDeclaredJoinKey(syntaxContext.SemanticModel,(InvocationExpressionSyntax)syntaxContext.Node,cancellationToken))
            .Where(static source=>source is not null)
            .Combine(generationEnabled).Where(static input=>input.Right).Select(static(input,_)=>input.Left);
        context.RegisterSourceOutput(declaredJoinKeys,static(productionContext,source)=>
            productionContext.AddSource(source!.Value.Name+".g.cs",SourceText.From(QueryProbeMarkers.Strip(source.Value.Source),Encoding.UTF8)));
        var transactionBegins=context.SyntaxProvider.CreateSyntaxProvider(
            static(node,_)=>node is InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax{Name.Identifier.ValueText:"BeginTransactionAsync"}},
            static(syntaxContext,_)=>(InvocationExpressionSyntax)syntaxContext.Node)
            .Combine(generationEnabled).Where(static input=>input.Right).Select(static(input,_)=>input.Left)
            .Combine(analysisCompilation)
            .Select(static(input,cancellationToken)=>DatabaseTransactionAccessAnalysis.ReadOnlyInterceptor(input.Right.GetSemanticModel(input.Left.SyntaxTree),input.Left,cancellationToken))
            .Where(static source=>source is not null);
        context.RegisterSourceOutput(transactionBegins.Collect(),static(productionContext,sources)=>{
            var index=0;foreach(var source in sources){productionContext.AddSource("DatabaseTransactionBegin"+index+".g.cs",SourceText.From(GeneratedExecutionAnnotations.Apply(QueryProbeMarkers.Strip(source!)),Encoding.UTF8));index++;}
        });
        var discoveryResults = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is InvocationExpressionSyntax,
                static (syntaxContext,_) => (InvocationExpressionSyntax)syntaxContext.Node)
            .Combine(generationEnabled).Where(static input=>input.Right).Select(static(input,_)=>input.Left)
            .Combine(analysisCompilation)
            .Select(static (input,cancellationToken)=>LinqTargetDiscoveryService.Discover(
                input.Right.GetSemanticModel(input.Left.SyntaxTree),input.Left,cancellationToken))
            .Where(static result => result is not null)
            .Select(static (result,_) => result!);
        var interceptTargets = discoveryResults
            .Where(static result => result.Target is not null)
            .Select(static (result,_) => result.Target!);
        var discoveryDiagnostics = discoveryResults
            .Where(static result => result.Diagnostic is not null)
            .Select(static (result,_) => result.Diagnostic!);

        context.RegisterPostInitializationOutput(static productionContext => {
            productionContext.AddSource("InterceptsLocationAttribute.g.cs",SourceText.From("#if !SEAMLESS_FLINQ_GENERATION_DISABLED\n"+InterceptsLocationAttributeSource+"\n#endif",Encoding.UTF8));
            productionContext.AddSource("LinqRewriteAttributes.g.cs",SourceText.From("#if !SEAMLESS_FLINQ_GENERATION_DISABLED\n"+LinqRewriteAttributesSource+"\n#endif",Encoding.UTF8));
        });
        context.RegisterSourceOutput(discoveryDiagnostics,static (productionContext,diagnostic) =>
            productionContext.ReportDiagnostic(diagnostic));
        context.RegisterSourceOutput(interceptTargets.Collect(),static (productionContext,interceptTargets) => {
            foreach(var error in OperatorRegistry.ValidationErrors) {
                productionContext.ReportDiagnostic(Diagnostic.Create(RegistryConsistencyDescriptor,Location.None,error));
            }
        });

        // コード生成: ファイル単位で独立してキャッシュされる
        var perFileGroups = interceptTargets
            .Collect()
            .SelectMany(static (targets,_) => targets
                .Distinct(LinqInterceptTargetComparer.Instance)
                .GroupBy(static t => t.SourcePath,StringComparer.Ordinal)
                .Select(static g => new PerFileInterceptGroup(g.Key,g.ToImmutableArray())));

        context.RegisterSourceOutput(perFileGroups,static (productionContext,group) => {
            var renderResult = GeneratedSourceRenderer.Render(new GeneratedSourceRenderModel(group.Targets));
            var source = renderResult.Source;
            var traceDocuments = renderResult.TraceDocuments;
            var hintName=インターセプターHintName取得(group.SourcePath);
            if(source.Length>0) {
                productionContext.AddSource(hintName,SourceText.From(source,Encoding.UTF8));
            }
            if(traceDocuments.Length>0){
                var traceHintName=hintName.EndsWith(".g.cs",StringComparison.Ordinal)
                    ?hintName.Substring(0,hintName.Length-5)+".OptimizationTrace.g.cs"
                    :hintName+".OptimizationTrace.g.cs";
                productionContext.AddSource(traceHintName,SourceText.From(OptimizationTraceCollector.RenderSource(traceDocuments),Encoding.UTF8));
            }
            foreach(var diagnostic in renderResult.Diagnostics) {
                productionContext.ReportDiagnostic(diagnostic);
            }
        });
    }
}
