using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.SourceGenerator;

[Generator]
public sealed partial class SeamlessIncrementalGenerator : IIncrementalGenerator {
    private static readonly SymbolDisplayFormat 完全修飾形式 = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var aggregateTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken) => AggregateFunc傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is TypeDeclarationSyntax,
                static (syntaxContext,_) => 型対象生成(syntaxContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var combined = aggregateTargets.Collect().Combine(typeTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => {
            var aggregateTargets = pair.Left;
            var typeTargets = pair.Right;

            if(!typeTargets.IsDefaultOrEmpty) {
                var databaseSource = データベースソース構築(typeTargets);
                if(databaseSource.Length>0) {
                    productionContext.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(空行除去(databaseSource),Encoding.UTF8));
                }
            }

            if(aggregateTargets.IsDefaultOrEmpty) {
                return;
            }

            var source = インターセプターソース構築(aggregateTargets.Distinct(AggregateFuncInterceptTargetComparer.Instance).ToImmutableArray());
            if(source.Length>0) {
                productionContext.AddSource("Seamless.SourceGenerator.AggregateInterceptors.g.cs",SourceText.From(source,Encoding.UTF8));
            }
        });
    }

    private static AggregateFunc傍受対象型? AggregateFunc傍受対象生成(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }

        var definition = method.ReducedFrom ?? method;
        if(!AggregateFuncメソッド判定(definition)) {
            return null;
        }

        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }

        if(invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containingMethodSyntax) {
            return null;
        }

        var containingMethod = context.SemanticModel.GetDeclaredSymbol(containingMethodSyntax,cancellationToken);
        if(containingMethod is null) {
            return null;
        }

        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }

        var invocationLineSpan = invocation.GetLocation().GetLineSpan();
        var line = invocationLineSpan.StartLinePosition.Line+1;
        var character = invocationLineSpan.StartLinePosition.Character+1;
        var identifier = SanitizeIdentifier($"{System.IO.Path.GetFileNameWithoutExtension(invocationLineSpan.Path)}_{containingMethod.Name}_{line}_{character}");
        var parameterType = method.TypeArguments[0].ToDisplayString(完全修飾形式);
        var funcType = $"global::System.Func<{parameterType},{parameterType},{parameterType}>";
        var sourceIsLinqInvocation = InvocationSourceがLinq呼び出しか(context.SemanticModel,invocation,cancellationToken);
        if(invocation.ArgumentList.Arguments.Count!=1
            ||invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax lambda
            ||!lambda.Modifiers.Any(SyntaxKind.StaticKeyword)
            ||!TryCreateLambdaFuncFragment(lambda,parameterType,out var funcFragment)) {
            return null;
        }

        return new AggregateFunc傍受対象型(
            identifier,
            containingMethod.Name,
            interceptsLocationAttributeText,
            parameterType,
            funcType,
            sourceIsLinqInvocation,
            funcFragment.ParameterNames,
            NormalizeExpressionText(GetLambdaBodyText(lambda)));
    }

    private static bool AggregateFuncメソッド判定(IMethodSymbol method) {
        if(method.Name!="Aggregate"
            ||!method.IsExtensionMethod
            ||method.Arity!=1
            ||method.Parameters.Length!=2
            ||method.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        var typeParameter = method.TypeArguments[0];
        if(!SymbolEqualityComparer.Default.Equals(method.ReturnType,typeParameter)) {
            return false;
        }

        if(method.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1
            ||sourceType.OriginalDefinition.ToDisplayString()!="System.Collections.Generic.IEnumerable<T>") {
            return false;
        }

        if(!SymbolEqualityComparer.Default.Equals(sourceType.TypeArguments[0],typeParameter)) {
            return false;
        }

        if(method.Parameters[1].Type is not INamedTypeSymbol funcType
            ||funcType.TypeArguments.Length!=3
            ||funcType.OriginalDefinition.ToDisplayString()!="System.Func<T1, T2, TResult>") {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(funcType.TypeArguments[0],typeParameter)
            &&SymbolEqualityComparer.Default.Equals(funcType.TypeArguments[1],typeParameter)
            &&SymbolEqualityComparer.Default.Equals(funcType.TypeArguments[2],typeParameter);
    }

    private static string インターセプターソース構築(ImmutableArray<AggregateFunc傍受対象型> targets) {
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace System.Runtime.CompilerServices {");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]");
        builder.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute {");
        builder.AppendLine("        public InterceptsLocationAttribute(int version,string data) {");
        builder.AppendLine("            this.Version=version;");
        builder.AppendLine("            this.Data=data;");
        builder.AppendLine("        }");
        builder.AppendLine("        public int Version { get; }");
        builder.AppendLine("        public string Data { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        builder.AppendLine("    internal static partial class LinqInvocationInterceptors {");

        var index = 0;
        foreach(var target in targets) {
            var funcFragment = new Template.LambdaFuncFragment {
                ReturnTypeName=target.ParameterTypeName,
                ParameterTypeNames=[target.ParameterTypeName,target.ParameterTypeName],
                ParameterNames=target.LambdaParameterNames,
                DefineLambda=$" => {target.LambdaBody};"
            };
            if(!TryCreateAggregateFragment(target.ParameterTypeName,target.SourceIsLinqInvocation,funcFragment,index,out var fragment)) {
                index++;
                continue;
            }

            builder.AppendLine($"        /// <summary>Intercepts Aggregate(func) inside {target.ContainingMethodName}.</summary>");
            if(!string.IsNullOrWhiteSpace(fragment.staticメソッド)) {
                インデント付き追記(builder,fragment.staticメソッド,"        ");
            }
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
            builder.AppendLine($"        public static {target.ParameterTypeName} {target.MethodName}(this global::System.Collections.Generic.IEnumerable<{target.ParameterTypeName}> source,{target.FuncTypeName} func) {{");
            インデント付き追記(builder,RenderFragmentBody(fragment,target.ParameterTypeName),"            ");
            builder.AppendLine("        }");
            builder.AppendLine();
            index++;
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string SanitizeIdentifier(string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return "Aggregate";
        }

        var builder = new StringBuilder(value.Length);
        foreach(var c in value) {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if(builder.Length==0 || !SyntaxFacts.IsIdentifierStartCharacter(builder[0])) {
            builder.Insert(0,'_');
        }

        return builder.ToString();
    }

    private static string 名前空間テキスト取得(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.IsGlobalNamespace ? string.Empty : namespaceSymbol.ToDisplayString();
    }

    private static void インデント付き追記(StringBuilder builder,string value,string indent) {
        foreach(var line in value.Replace("\r\n","\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    private static string 空行除去(string source) {
        var lines = source.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder(source.Length);
        var firstWritten = true;
        foreach(var line in lines) {
            if(string.IsNullOrWhiteSpace(line)) {
                continue;
            }
            if(!firstWritten) {
                builder.Append('\n');
            }
            builder.Append(line);
            firstWritten=false;
        }
        return builder.ToString();
    }

    private static 型対象型? 型対象生成(GeneratorSyntaxContext context) {
        if(context.Node is not TypeDeclarationSyntax declaration) {
            return null;
        }

        var symbol = context.SemanticModel.GetDeclaredSymbol(declaration);
        if(symbol is null) {
            return null;
        }

        var isMarker = symbol.Name=="DatabaseGenerator";
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        var isContainerCandidate = symbol.TypeKind==TypeKind.Class;
        if(!isMarker && !isPartial && !isContainerCandidate) {
            return null;
        }

        return new 型対象型(symbol,isPartial);
    }

    private static bool TryCreateAggregateFragment(string elementTypeName,bool sourceIsLinqInvocation,Template.LambdaFuncFragment funcFragment,int uniqueNumber,out Template.Fragment fragment) {
        fragment=null!;
        var sourceLoopNumber = uniqueNumber*2;
        var aggregateLoopNumber = uniqueNumber*2+1;
        if(!TryCreateSourceFragment(elementTypeName,sourceIsLinqInvocation,sourceLoopNumber,out var sourceFragment)) {
            return false;
        }

        fragment=Template.ResolveAggregateFunc(sourceFragment,funcFragment,aggregateLoopNumber);
        return true;
    }

    private static bool TryCreateSourceFragment(string elementTypeName,bool sourceIsLinqInvocation,int uniqueNumber,out Template.EnumerableFragment fragment) {
        if(sourceIsLinqInvocation) {
            // Chained LINQ source composition will be resolved here as operators are added incrementally.
            fragment=null!;
            return false;
        }

        fragment=CreateRootEnumerableFragment("source",elementTypeName,uniqueNumber);
        return true;
    }

    private static Template.EnumerableFragment CreateRootEnumerableFragment(string result,string elementTypeName,int uniqueNumber) {
        var e = $"e{uniqueNumber}";
        var Start = $"Start{uniqueNumber}";
        var Head = $"Head{uniqueNumber}";
        var Element = $"item{uniqueNumber}";
        var MoveNext = $"{e}.MoveNext()";
        return new Template.EnumerableFragment(
            string.Empty,
            string.Empty,
            $"using var {e}={result}.GetEnumerator();",
            $$"""
                goto {{Start}};
                {{Head}}:
                var {{Element}}={{e}}.Current;
                """,
            string.Empty,
            $$"""
                {{Start}}:;
                if({{MoveNext}})goto {{Head}};
                """,
            string.Empty,
            string.Empty,
            string.Empty,
            result,
            Element,
            MoveNext,
            $"result.Add({Element});",
            elementTypeName);
    }

    private static bool InvocationSourceがLinq呼び出しか(SemanticModel semanticModel,InvocationExpressionSyntax invocation,System.Threading.CancellationToken cancellationToken) {
        if(invocation.Expression is not MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax sourceInvocation }) {
            return false;
        }

        return semanticModel.GetSymbolInfo(sourceInvocation,cancellationToken).Symbol is IMethodSymbol sourceMethod
            && (sourceMethod.ReducedFrom ?? sourceMethod).ContainingType.ToDisplayString()=="System.Linq.Enumerable";
    }

    private static bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,string parameterTypeName,out Template.LambdaFuncFragment fragment) {
        fragment=new Template.LambdaFuncFragment();
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        if(parameters.Count!=2) {
            return false;
        }

        fragment.ReturnTypeName=parameterTypeName;
        fragment.ParameterTypeNames=[parameterTypeName,parameterTypeName];
        fragment.ParameterNames=[parameters[0],parameters[1]];
        fragment.DefineLambda=$" => {NormalizeExpressionText(body.ToString())};";
        return true;
    }

    private static string GetLambdaBodyText(LambdaExpressionSyntax lambda) {
        return TryGetLambdaExpressionBody(lambda,out var body)
            ? NormalizeExpressionText(body.ToString())
            : string.Empty;
    }

    private static bool TryGetLambdaExpressionBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        body=null!;
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        if(lambda.Body is BlockSyntax {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            body=returnExpression;
            return true;
        }
        return false;
    }

    private static List<string> GetLambdaParameters(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simpleLambda) {
            return [simpleLambda.Parameter.Identifier.ValueText];
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda) {
            return parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToList();
        }
        return [];
    }

    private static string NormalizeExpressionText(string value) {
        return value.Replace("\r\n"," ").Replace('\n',' ').Trim();
    }

    private static string RenderFragmentBody(Template.Fragment fragment,string resultTypeName) {
        var builder = new StringBuilder();
        AppendBlock(builder,fragment.BeginGlobal);
        AppendBlock(builder,fragment.BeginLoopOutside);
        AppendBlock(builder,fragment.BeginLoop);
        AppendBlock(builder,fragment.BeginLoopInside);
        AppendBlock(builder,fragment.EndLoopInside);
        AppendBlock(builder,fragment.EndLoop);
        AppendBlock(builder,fragment.EndLoopOutside);
        AppendBlock(builder,fragment.EndGlobal);
        builder.Append("return ");
        builder.Append(fragment.result);
        builder.Append(';');
        return builder
            .ToString()
            .Replace("=default!;",$"=default({resultTypeName});",StringComparison.Ordinal)
            .Replace("throw NoElements();","throw global::Seamless.Linq.Enumerable.NoElements();",StringComparison.Ordinal);
    }

    private static void AppendBlock(StringBuilder builder,string value) {
        if(string.IsNullOrWhiteSpace(value)) {
            return;
        }
        if(builder.Length>0 && builder[^1]!='\n') {
            builder.AppendLine();
        }
        builder.AppendLine(value.Trim());
    }

    private sealed class AggregateFunc傍受対象型 {
        public AggregateFunc傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,string parameterTypeName,string funcTypeName,bool sourceIsLinqInvocation,string[] lambdaParameterNames,string lambdaBody) {
            this.Identifier=identifier;
            this.ContainingMethodName=containingMethodName;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ParameterTypeName=parameterTypeName;
            this.FuncTypeName=funcTypeName;
            this.SourceIsLinqInvocation=sourceIsLinqInvocation;
            this.LambdaParameterNames=lambdaParameterNames;
            this.LambdaBody=lambdaBody;
        }

        public string Identifier { get; }
        public string ContainingMethodName { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ParameterTypeName { get; }
        public string FuncTypeName { get; }
        public bool SourceIsLinqInvocation { get; }
        public string[] LambdaParameterNames { get; }
        public string LambdaBody { get; }
        public string MethodName => $"OptimizedAggregate_{this.Identifier}";
    }

    private sealed class AggregateFuncInterceptTargetComparer : IEqualityComparer<AggregateFunc傍受対象型> {
        public static readonly AggregateFuncInterceptTargetComparer Instance = new();

        public bool Equals(AggregateFunc傍受対象型? x,AggregateFunc傍受対象型? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null || y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }

        public int GetHashCode(AggregateFunc傍受対象型 obj) {
            return obj.InterceptsLocationAttributeText.GetHashCode(StringComparison.Ordinal);
        }
    }

    private sealed class 型対象型 {
        public 型対象型(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }

        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }
}
