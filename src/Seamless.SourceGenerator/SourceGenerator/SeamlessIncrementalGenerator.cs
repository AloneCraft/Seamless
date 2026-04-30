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
// この generator は 2 系統の生成をまとめて担当する。
// 1. List<T>.Where を軽量 enumerable に差し替える interceptor 系コード
// 2. DatabaseGenerator マーカー配下の schema / optimized container コード
public sealed class SeamlessIncrementalGenerator:IIncrementalGenerator {
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

    // SyntaxProvider は候補を粗く拾い、後段で意味解析して本対象だけを残す。
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var whereTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is InvocationExpressionSyntax,static (generatorContext,cancellationToken) => CreateWhereInterceptTarget(generatorContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var containerMethodTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is InvocationExpressionSyntax,static (generatorContext,cancellationToken) => CreateContainerMethodInterceptTarget(generatorContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is TypeDeclarationSyntax,static (generatorContext,_) => CreateTypeTarget(generatorContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var combined = context.CompilationProvider.Combine(whereTargets.Collect()).Combine(typeTargets.Collect()).Combine(containerMethodTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => Execute(
            productionContext,
            pair.Left.Left.Right,
            pair.Left.Right,
            pair.Right));
    }

    // 役割ごとに generated source を分割して、interceptor / optimized helper / schema を読み分けやすくする。
    private static void Execute(SourceProductionContext context,ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<TypeTarget> typeTargets,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets) {
        var optimizedCodeSource = BuildOptimizedCodeSource(whereTargets);
        if(optimizedCodeSource.Length>0) {
            context.AddSource("Seamless.SourceGenerator.OptimizedCode.g.cs",SourceText.From(RemoveBlankLines(optimizedCodeSource),Encoding.UTF8));
        }

        var interceptorSource = BuildInterceptorSource(whereTargets,containerMethodTargets);
        if(interceptorSource.Length>0) {
            context.AddSource("Seamless.SourceGenerator.Interceptors.g.cs",SourceText.From(RemoveBlankLines(interceptorSource),Encoding.UTF8));
        }

        if(!typeTargets.IsDefaultOrEmpty) {
            var databaseSource = BuildDatabaseSource(typeTargets);
            if(databaseSource.Length>0) {
                context.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(RemoveBlankLines(databaseSource),Encoding.UTF8));
            }
        }
    }

    // generated code は人が直接読むので、空行は最後にまとめて取り除く。
    // これで各所の AppendLine() の都合に引きずられず、出力全体の体裁を一定にできる。
    private static string RemoveBlankLines(string source) {
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

    // Enumerable.Where 呼び出しのうち、List<T> 最適化に落とせるものだけを抽出する。
    private static WhereInterceptTarget? CreateWhereInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }
        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }
        if(method.Name!="Where"||!method.IsExtensionMethod) {
            return null;
        }
        var reducedMethod = method.ReducedFrom??method;
        if(reducedMethod.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return null;
        }
        if(method.ReducedFrom is not null) {
            return null;
        }
        if(!TryGetWherePredicateInfo(reducedMethod,out var usesIndex)) {
            return null;
        }
        if(!TryGetWhereCallInfo(invocation,memberAccess,method,out var sourceExpression,out var predicateExpression)) {
            return null;
        }
        if(!TryGetListElementType(context.SemanticModel.GetTypeInfo(sourceExpression,cancellationToken).Type,out var elementType)) {
            return null;
        }
        if(!TryGetPredicateInfo(context.SemanticModel,predicateExpression,cancellationToken,usesIndex,out var elementParameterName,out var indexParameterName,out var predicateText,out var usePredicateDelegate)) {
            return null;
        }
        var span = memberAccess.Name.GetLocation().GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{System.IO.Path.GetFileNameWithoutExtension(span.Path)}_{line}_{character}");
        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }
        return new WhereInterceptTarget(
            id,
            span.Path,
            line,
            character,
            interceptsLocationAttributeText,
            elementType.ToDisplayString(FullyQualifiedFormat),
            usesIndex,
            elementParameterName,
            indexParameterName,
            predicateText,
            usePredicateDelegate);
    }

    // DatabaseGenerator 配下の container method 呼び出しを、継承なしで optimized static interceptor へ差し替える。
    private static ContainerMethodInterceptTarget? CreateContainerMethodInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }
        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }
        var methodDefinition = method.OriginalDefinition;
        if(!IsContainerMethodInterceptCandidate(methodDefinition)) {
            return null;
        }
        var translatedBody = TryTranslateMethod(context.SemanticModel.Compilation,methodDefinition);
        if(translatedBody is null) {
            return null;
        }

        var methodLocation = GetInvocationMethodLocation(invocation);
        var span = methodLocation.GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{System.IO.Path.GetFileNameWithoutExtension(span.Path)}_{method.Name}_{line}_{character}");
        var interceptsLocationAttributeText = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptsLocationAttributeSyntax(interceptableLocation);
        if(string.IsNullOrWhiteSpace(interceptsLocationAttributeText)) {
            return null;
        }

        return new ContainerMethodInterceptTarget(
            id,
            span.Path,
            line,
            character,
            interceptsLocationAttributeText,
            methodDefinition.ContainingType.ToDisplayString(FullyQualifiedFormat),
            methodDefinition.ReturnType.ToDisplayString(FullyQualifiedFormat),
            BuildTypeParameterList(methodDefinition.TypeParameters),
            BuildTypeParameterConstraintList(methodDefinition.TypeParameters),
            BuildParameterDeclarationList(methodDefinition.Parameters),
            RewriteInstanceReceiverReferences(translatedBody.Value.Body),
            translatedBody.Value.IsExpressionBody);
    }

    private static bool IsContainerMethodInterceptCandidate(IMethodSymbol method) {
        if(method.MethodKind!=MethodKind.Ordinary
            ||method.IsStatic
            ||method.IsAbstract
            ||method.ContainingType.TypeKind!=TypeKind.Class
            ||method.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)
            ||method.DeclaringSyntaxReferences.Length==0) {
            return false;
        }
        return HasDatabaseGeneratorMarker(method.ContainingType.ContainingNamespace);
    }

    private static bool HasDatabaseGeneratorMarker(INamespaceSymbol namespaceSymbol) {
        return namespaceSymbol.GetTypeMembers("DatabaseGenerator").Any();
    }

    private static Location GetInvocationMethodLocation(InvocationExpressionSyntax invocation) {
        if(invocation.Expression is MemberAccessExpressionSyntax memberAccess) {
            return memberAccess.Name.GetLocation();
        }
        return invocation.Expression.GetLocation();
    }

    private static string RewriteInstanceReceiverReferences(string body) {
        return body
            .Replace("this.","self.")
            .Replace("EqualityComparer<","global::System.Collections.Generic.EqualityComparer<")
            .Replace(" Comparer<"," global::System.Collections.Generic.Comparer<")
            .Replace("(Comparer<","(global::System.Collections.Generic.Comparer<")
            .Replace(",Comparer<",",global::System.Collections.Generic.Comparer<");
    }

    private static bool TryGetWherePredicateInfo(IMethodSymbol reducedMethod,out bool usesIndex) {
        usesIndex=false;
        if(reducedMethod.Parameters.Length!=2
            ||reducedMethod.Parameters[1].Type is not INamedTypeSymbol predicateType
            ||predicateType.Name!="Func"
            ||predicateType.ContainingNamespace.ToDisplayString()!="System") {
            return false;
        }
        if(predicateType.TypeArguments.Length==2
            &&predicateType.TypeArguments[1].SpecialType==SpecialType.System_Boolean) {
            return true;
        }
        if(predicateType.TypeArguments.Length==3
            &&predicateType.TypeArguments[1].SpecialType==SpecialType.System_Int32
            &&predicateType.TypeArguments[2].SpecialType==SpecialType.System_Boolean) {
            usesIndex=true;
            return true;
        }
        return false;
    }

    private static bool TryGetWhereCallInfo(InvocationExpressionSyntax invocation,MemberAccessExpressionSyntax memberAccess,IMethodSymbol method,out ExpressionSyntax sourceExpression,out ExpressionSyntax predicateExpression) {
        sourceExpression=null!;
        predicateExpression=null!;

        if(method.ReducedFrom is not null) {
            if(invocation.ArgumentList.Arguments.Count!=1) {
                return false;
            }
            sourceExpression=memberAccess.Expression;
            predicateExpression=invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        if(invocation.ArgumentList.Arguments.Count!=2) {
            return false;
        }
        sourceExpression=invocation.ArgumentList.Arguments[0].Expression;
        predicateExpression=invocation.ArgumentList.Arguments[1].Expression;
        return true;
    }

    private static bool TryGetListElementType(ITypeSymbol? type,out ITypeSymbol elementType) {
        for(var current = type; current is not null; current=current.BaseType) {
            if(current is INamedTypeSymbol namedType
                &&namedType.Name=="List"
                &&namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
                &&namedType.TypeArguments.Length==1) {
                elementType=namedType.TypeArguments[0];
                return true;
            }
        }

        elementType=null!;
        return false;
    }

    private static bool TryGetPredicateInfo(SemanticModel semanticModel,ExpressionSyntax predicateExpression,System.Threading.CancellationToken cancellationToken,bool usesIndex,out string parameterName,out string? indexParameterName,out string predicateText,out bool usePredicateDelegate) {
        parameterName="candidate";
        indexParameterName=usesIndex ? "candidateIndex" : null;
        predicateText=predicateExpression.ToString();
        usePredicateDelegate=true;

        if(predicateExpression is not LambdaExpressionSyntax lambda) {
            return true;
        }

        if(lambda switch {
            SimpleLambdaExpressionSyntax => usesIndex,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count!=(usesIndex ? 2 : 1),
            _ => true,
        }) {
            return false;
        }

        parameterName=lambda switch {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => string.Empty,
        };
        if(string.IsNullOrWhiteSpace(parameterName)) {
            return false;
        }
        if(usesIndex&&lambda is ParenthesizedLambdaExpressionSyntax indexedLambda) {
            indexParameterName=indexedLambda.ParameterList.Parameters[1].Identifier.ValueText;
            if(string.IsNullOrWhiteSpace(indexParameterName)) {
                return false;
            }
        }

        predicateText=lambda.Body.ToString();
        if(lambda.Body is not ExpressionSyntax expressionBody) {
            return true;
        }

        predicateText=expressionBody.ToString();
        var lambdaParameters = new HashSet<string>(StringComparer.Ordinal) { parameterName };
        if(indexParameterName is not null) {
            lambdaParameters.Add(indexParameterName);
        }
        if(CanInlinePredicateExpression(semanticModel,expressionBody,cancellationToken,lambdaParameters)) {
            usePredicateDelegate=false;
        }
        return true;
    }

    private static bool CanInlinePredicateExpression(SemanticModel semanticModel,ExpressionSyntax expressionBody,System.Threading.CancellationToken cancellationToken,HashSet<string> lambdaParameters) {
        foreach(var identifier in expressionBody.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            var symbol = semanticModel.GetSymbolInfo(identifier,cancellationToken).Symbol;
            if(symbol is null) {
                continue;
            }
            if(lambdaParameters.Contains(identifier.Identifier.ValueText)) {
                continue;
            }
            if(symbol is ILocalSymbol||symbol is IParameterSymbol) {
                return false;
            }
        }
        return true;
    }

    private static TypeTarget? CreateTypeTarget(GeneratorSyntaxContext context) {
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
        if(!isMarker&&!isPartial&&!isContainerCandidate) {
            return null;
        }
        return new TypeTarget(symbol,isPartial);
    }

    // Where interceptor が使う reusable enumerable / query helper を出力する。
    private static string BuildOptimizedCodeSource(ImmutableArray<WhereInterceptTarget> whereTargets) {
        if(whereTargets.IsDefaultOrEmpty) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("// Generated: query-syntax helper extensions so List<T>.Where can stay on the optimized path.");
        AppendListWhereQueryHelperSource(builder);
        return builder.ToString();
    }

    // InterceptsLocation 付き entry point を出力する。
    private static string BuildInterceptorSource(ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets) {
        if(whereTargets.IsDefaultOrEmpty&&containerMethodTargets.IsDefaultOrEmpty) {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        builder.AppendLine("// Generated: Roslyn interceptors resolve the preview attribute by metadata name.");
        builder.AppendLine("// The target framework does not provide it, so this file-local definition is required by the generated [InterceptsLocation] entry points below.");
        builder.AppendLine("namespace System.Runtime.CompilerServices {");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]");
        builder.AppendLine("    file sealed class InterceptsLocationAttribute:global::System.Attribute {");
        builder.AppendLine("        public InterceptsLocationAttribute(int version,string data) {");
        builder.AppendLine("            this.Version=version;");
        builder.AppendLine("            this.Data=data;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public int Version { get; }");
        builder.AppendLine("        public string Data { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("// Generated: interceptor entry points.");
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        if(!whereTargets.IsDefaultOrEmpty) {
            builder.AppendLine("    internal static class ListWhereInterceptors {");

            foreach(var target in whereTargets.Distinct(WhereInterceptTargetComparer.Instance)) {
                var elementTypeName = target.ElementTypeName;
                var predicateTypeName = target.UsesIndex
                    ? $"global::System.Func<{elementTypeName},int,bool>"
                    : $"global::System.Func<{elementTypeName},bool>";
                builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"        public static global::System.Collections.Generic.IEnumerable<{elementTypeName}> {target.MethodName}(this global::System.Collections.Generic.IEnumerable<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine($"            global::System.Diagnostics.Debug.WriteLine(\"Seamless interceptor: {target.MethodName} ({target.Line}:{target.Character}) predicate=\" + {ToLiteral(target.PredicateText)});");
                builder.AppendLine($"            global::System.Console.WriteLine(\"Seamless interceptor: {target.MethodName} ({target.Line}:{target.Character})\");");
                builder.AppendLine($"            return new {target.EnumerableTypeName}((global::System.Collections.Generic.List<{elementTypeName}>)source,predicate);");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine($"        public readonly struct {target.EnumerableTypeName}:global::System.Collections.Generic.IEnumerable<{elementTypeName}> {{");
                builder.AppendLine($"            private readonly global::System.Collections.Generic.List<{elementTypeName}> source;");
                builder.AppendLine($"            private readonly {predicateTypeName} predicate;");
                builder.AppendLine();
                builder.AppendLine($"            public {target.EnumerableTypeName}(global::System.Collections.Generic.List<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine("                this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
                builder.AppendLine("                this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
                builder.AppendLine("            }");
                builder.AppendLine();
                builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine("            public Enumerator GetEnumerator() => new(this.source,this.predicate);");
                builder.AppendLine();
                builder.AppendLine($"            global::System.Collections.Generic.IEnumerator<{elementTypeName}> global::System.Collections.Generic.IEnumerable<{elementTypeName}>.GetEnumerator() {{");
                builder.AppendLine("                var enumerator = this.GetEnumerator();");
                builder.AppendLine("                while(enumerator.MoveNext()) {");
                builder.AppendLine("                    yield return enumerator.Current;");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine();
                builder.AppendLine($"            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<{elementTypeName}>)this).GetEnumerator();");
                builder.AppendLine();
                builder.AppendLine("            public struct Enumerator {");
                builder.AppendLine($"                private readonly global::System.Collections.Generic.List<{elementTypeName}> source;");
                builder.AppendLine($"                private readonly {predicateTypeName} predicate;");
                builder.AppendLine("                private int index;");
                builder.AppendLine();
                builder.AppendLine($"                public Enumerator(global::System.Collections.Generic.List<{elementTypeName}> source,{predicateTypeName} predicate) {{");
                builder.AppendLine("                    this.source=source;");
                builder.AppendLine("                    this.predicate=predicate;");
                builder.AppendLine("                    this.index=-1;");
                builder.AppendLine("                    this.Current=default!;");
                builder.AppendLine("                }");
                builder.AppendLine();
                builder.AppendLine($"                public {elementTypeName} Current {{ get; private set; }}");
                builder.AppendLine();
                builder.AppendLine("                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine("                public bool MoveNext() {");
                builder.AppendLine("                    while(true) {");
                builder.AppendLine("                        var next = this.index+1;");
                builder.AppendLine("                        if(next>=this.source.Count) {");
                builder.AppendLine("                            return false;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        this.index=next;");
                if(target.UsePredicateDelegate) {
                    builder.AppendLine("                        var candidate = this.source[next];");
                    builder.AppendLine(target.UsesIndex
                        ? "                        if(!this.predicate(candidate,next)) {"
                        : "                        if(!this.predicate(candidate)) {");
                    builder.AppendLine("                            continue;");
                    builder.AppendLine("                        }");
                    builder.AppendLine("                        this.Current=candidate;");
                }
                else {
                    builder.AppendLine($"                        var {target.ParameterName} = this.source[next];");
                    if(target.IndexParameterName is not null) {
                        builder.AppendLine($"                        var {target.IndexParameterName} = next;");
                    }
                    builder.AppendLine($"                        if(!({target.PredicateText})) {{");
                    builder.AppendLine("                            continue;");
                    builder.AppendLine("                        }");
                    builder.AppendLine($"                        this.Current={target.ParameterName};");
                }
                builder.AppendLine("                        return true;");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine();
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        AppendContainerMethodInterceptorSource(builder,containerMethodTargets);
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendContainerMethodInterceptorSource(StringBuilder builder,ImmutableArray<ContainerMethodInterceptTarget> containerMethodTargets) {
        if(containerMethodTargets.IsDefaultOrEmpty) {
            return;
        }

        builder.AppendLine("    internal static class ContainerMethodInterceptors {");
        foreach(var target in containerMethodTargets.Distinct(ContainerMethodInterceptTargetComparer.Instance)) {
            var parameterList = string.IsNullOrWhiteSpace(target.ParameterList)
                ? $"this {target.ReceiverTypeName} self"
                : $"this {target.ReceiverTypeName} self, {target.ParameterList}";
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
            if(target.IsExpressionBody) {
                builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.TypeParameterList}({parameterList}){target.TypeParameterConstraintList} => {target.Body};");
            }
            else {
                builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}{target.TypeParameterList}({parameterList}){target.TypeParameterConstraintList} {target.Body}");
            }
            builder.AppendLine();
        }
        builder.AppendLine("    }");
    }

    private static void AppendListWhereQueryHelperSource(StringBuilder builder) {
        builder.AppendLine("namespace System.Linq {");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Query syntax binds to these helpers before it falls back to Enumerable.Where. See <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Interceptor_Smoke\"/>.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    internal static class ListWhereQueryExtensions {");
        var enumerableBuilder = new StringBuilder();
        AppendReusableListWhereEnumerableSource(enumerableBuilder);
        AppendIndented(builder,enumerableBuilder.ToString(),"    ");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Replaces List&lt;T&gt;.Where(predicate) query binding with the optimized enumerable used by <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Interceptor_Smoke\"/>.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static OptimizedListWhereEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) => new(source,predicate);");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>");
        builder.AppendLine("        /// Replaces List&lt;T&gt;.Where((value, index) =&gt; ...) query binding with the optimized enumerable used by <see cref=\"Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Index_Interceptor_Smoke\"/>.");
        builder.AppendLine("        /// </summary>");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static OptimizedListWhereIndexedEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) => new(source,predicate);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendReusableListWhereEnumerableSource(StringBuilder builder) {
        builder.AppendLine("    // Generated: allocation-light enumerable for List<T>.Where(predicate).");
        builder.AppendLine("    internal readonly struct OptimizedListWhereEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {");
        builder.AppendLine("        private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("        private readonly global::System.Func<TSource,bool> predicate;");
        builder.AppendLine();
        builder.AppendLine("        public OptimizedListWhereEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {");
        builder.AppendLine("            this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public Enumerator GetEnumerator() => new(this.source,this.predicate);");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {");
        builder.AppendLine("            var enumerator = this.GetEnumerator();");
        builder.AppendLine("            while(enumerator.MoveNext()) {");
        builder.AppendLine("                yield return enumerator.Current;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();");
        builder.AppendLine();
        builder.AppendLine("        public struct Enumerator {");
        builder.AppendLine("            private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("            private readonly global::System.Func<TSource,bool> predicate;");
        builder.AppendLine("            private int index;");
        builder.AppendLine();
        builder.AppendLine("            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {");
        builder.AppendLine("                this.source=source;");
        builder.AppendLine("                this.predicate=predicate;");
        builder.AppendLine("                this.index=-1;");
        builder.AppendLine("                this.Current=default!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public TSource Current { get; private set; }");
        builder.AppendLine();
        builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("            public bool MoveNext() {");
        builder.AppendLine("                while(true) {");
        builder.AppendLine("                    var next = this.index+1;");
        builder.AppendLine("                    if(next>=this.source.Count) {");
        builder.AppendLine("                        return false;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.index=next;");
        builder.AppendLine("                    var candidate = this.source[next];");
        builder.AppendLine("                    if(!this.predicate(candidate)) {");
        builder.AppendLine("                        continue;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.Current=candidate;");
        builder.AppendLine("                    return true;");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    // Generated: allocation-light enumerable for List<T>.Where((value, index) => ...).");
        builder.AppendLine("    internal readonly struct OptimizedListWhereIndexedEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {");
        builder.AppendLine("        private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("        private readonly global::System.Func<TSource,int,bool> predicate;");
        builder.AppendLine();
        builder.AppendLine("        public OptimizedListWhereIndexedEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {");
        builder.AppendLine("            this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
        builder.AppendLine("            this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public Enumerator GetEnumerator() => new(this.source,this.predicate);");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {");
        builder.AppendLine("            var enumerator = this.GetEnumerator();");
        builder.AppendLine("            while(enumerator.MoveNext()) {");
        builder.AppendLine("                yield return enumerator.Current;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();");
        builder.AppendLine();
        builder.AppendLine("        public struct Enumerator {");
        builder.AppendLine("            private readonly global::System.Collections.Generic.List<TSource> source;");
        builder.AppendLine("            private readonly global::System.Func<TSource,int,bool> predicate;");
        builder.AppendLine("            private int index;");
        builder.AppendLine();
        builder.AppendLine("            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {");
        builder.AppendLine("                this.source=source;");
        builder.AppendLine("                this.predicate=predicate;");
        builder.AppendLine("                this.index=-1;");
        builder.AppendLine("                this.Current=default!;");
        builder.AppendLine("            }");
        builder.AppendLine();
        builder.AppendLine("            public TSource Current { get; private set; }");
        builder.AppendLine();
        builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("            public bool MoveNext() {");
        builder.AppendLine("                while(true) {");
        builder.AppendLine("                    var next = this.index+1;");
        builder.AppendLine("                    if(next>=this.source.Count) {");
        builder.AppendLine("                        return false;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.index=next;");
        builder.AppendLine("                    var candidate = this.source[next];");
        builder.AppendLine("                    if(!this.predicate(candidate,next)) {");
        builder.AppendLine("                        continue;");
        builder.AppendLine("                    }");
        builder.AppendLine("                    this.Current=candidate;");
        builder.AppendLine("                    return true;");
        builder.AppendLine("                }");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    // DatabaseGenerator を起点にして schema struct を生成する。
    private static string BuildDatabaseSource(ImmutableArray<TypeTarget> typeTargets) {
        var builder = new StringBuilder();
        var rootNamespaces = typeTargets
            .Where(static target => target.Symbol.Name=="DatabaseGenerator")
            .Select(static target => target.Symbol.ContainingNamespace.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if(rootNamespaces.Length==0) {
            return string.Empty;
        }
        builder.AppendLine("#pragma warning disable CS0164, CS8601");

        foreach(var rootNamespace in rootNamespaces) {
            var keys = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&NamespaceEquals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Keys"))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var types = typeTargets
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Struct
                    &&NamespaceEquals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,"Types"))
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();
            var hasRootNs = !string.IsNullOrEmpty(rootNamespace);
            var rootNsIndent = hasRootNs ? "    " : "";
            var innerIndent = hasRootNs ? "        " : "    ";
            if(hasRootNs) {
                builder.AppendLine($"namespace {rootNamespace} {{");
            }

            if(keys.Length>0) {
                builder.AppendLine($"{rootNsIndent}// Generated: schema key records declared under {rootNamespace}.Keys.");
                builder.AppendLine($"{rootNsIndent}namespace Keys {{");
                foreach(var key in keys) {
                    AppendRecordSourceBody(builder,key,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            if(types.Length>0) {
                builder.AppendLine($"{rootNsIndent}// Generated: schema row/value records declared under {rootNamespace}.Types.");
                builder.AppendLine($"{rootNsIndent}namespace Types {{");
                foreach(var type in types) {
                    AppendRecordSourceBody(builder,type,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            if(hasRootNs) {
                builder.AppendLine("}");
            }
        }

        return builder.ToString();
    }

    // Key / Type の partial struct を、MemoryPack で保存できる完全な schema record にする。
    // unmanaged フィールドは _u、managed フィールドは _m に寄せ、backend から扱いやすい形へ整える。
    private static void AppendRecordSourceBody(StringBuilder builder,INamedTypeSymbol symbol,string indent) {
        var properties = GetGeneratableProperties(symbol);
        if(properties.Length==0) {
            return;
        }

        var symbolTypeName = symbol.ToDisplayString(FullyQualifiedFormat);

        var unmanagedProperties = properties.Where(static property => property.Type.IsUnmanagedType).ToImmutableArray();
        var managedProperties = properties.Where(static property => !property.Type.IsUnmanagedType).ToImmutableArray();

        var innerBuilder = new StringBuilder();
        {
            var indentedBuilder = innerBuilder;
            indentedBuilder.AppendLine($"// Generated: completed schema record for {symbolTypeName}. unmanaged data lives in _u and managed data lives in _m.");
            indentedBuilder.AppendLine($"partial struct {symbol.Name}:global::MemoryPack.IMemoryPackable<{symbolTypeName}> {{");
            indentedBuilder.AppendLine($"    public {symbol.Name}({string.Join(", ",properties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {GetParameterName(property)}"))}) {{");
            if(unmanagedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._u=new({string.Join(", ",unmanagedProperties.Select(static property => GetParameterName(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._u=new();");
            }
            if(managedProperties.Length>0) {
                indentedBuilder.AppendLine($"        this._m=new({string.Join(", ",managedProperties.Select(static property => GetParameterName(property)))});");
            }
            else {
                indentedBuilder.AppendLine("        this._m=new();");
            }
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            AppendUnmanagedRecordSource(indentedBuilder,unmanagedProperties);
            indentedBuilder.AppendLine("    private _U _u;");
            indentedBuilder.AppendLine();
            AppendManagedRecordSource(indentedBuilder,managedProperties);
            indentedBuilder.AppendLine("    private _M _m;");
            indentedBuilder.AppendLine();
            foreach(var property in properties) {
                indentedBuilder.AppendLine($"    public partial {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name} => this.{GetStorageName(property)}.{property.Name};");
            }
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static {symbol.Name}() {{");
            indentedBuilder.AppendLine($"        global::MemoryPack.MemoryPackFormatterProvider.Register<{symbolTypeName}>();");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine("    static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.MemoryPackableFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine($"        if(!global::MemoryPack.MemoryPackFormatterProvider.IsRegistered<{symbolTypeName}[]>()) {{");
            indentedBuilder.AppendLine($"            global::MemoryPack.MemoryPackFormatterProvider.Register(new global::MemoryPack.Formatters.ArrayFormatter<{symbolTypeName}>());");
            indentedBuilder.AppendLine("        }");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        writer.WriteUnmanaged(value._u);");
            indentedBuilder.AppendLine("        writer.WritePackable(value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine();
            indentedBuilder.AppendLine($"    static void global::MemoryPack.IMemoryPackable<{symbolTypeName}>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref {symbolTypeName} value) {{");
            indentedBuilder.AppendLine("        reader.ReadUnmanaged(out value._u);");
            indentedBuilder.AppendLine("        reader.ReadPackable(ref value._m);");
            indentedBuilder.AppendLine("    }");
            indentedBuilder.AppendLine("}");
        }
        AppendIndented(builder,innerBuilder.ToString(),indent);
        builder.AppendLine();
    }

    private readonly struct TranslatedMethodBody {
        public TranslatedMethodBody(string body,bool isExpressionBody) {
            Body=body;
            IsExpressionBody=isExpressionBody;
        }

        public string Body { get; }
        public bool IsExpressionBody { get; }
    }

    private static string BuildTypeParameterList(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        if(typeParameters.Length==0) {
            return string.Empty;
        }
        return $"<{string.Join(", ",typeParameters.Select(static typeParameter => typeParameter.Name))}>";
    }

    private static string BuildTypeParameterConstraintList(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        var constraints = new List<string>();
        foreach(var typeParameter in typeParameters) {
            var parts = new List<string>();
            if(typeParameter.HasUnmanagedTypeConstraint) {
                parts.Add("unmanaged");
            }
            else if(typeParameter.HasValueTypeConstraint) {
                parts.Add("struct");
            }
            else if(typeParameter.HasReferenceTypeConstraint) {
                parts.Add("class");
            }
            else if(typeParameter.HasNotNullConstraint) {
                parts.Add("notnull");
            }
            foreach(var constraintType in typeParameter.ConstraintTypes) {
                parts.Add(constraintType.ToDisplayString(FullyQualifiedFormat));
            }
            if(typeParameter.HasConstructorConstraint) {
                parts.Add("new()");
            }
            if(parts.Count>0) {
                constraints.Add($"where {typeParameter.Name} : {string.Join(", ",parts)}");
            }
        }
        return constraints.Count==0 ? string.Empty : " "+string.Join(" ",constraints);
    }

    private static string BuildParameterDeclarationList(ImmutableArray<IParameterSymbol> parameters) {
        return string.Join(", ",parameters.Select(BuildParameterDeclaration));
    }

    private static string BuildParameterDeclaration(IParameterSymbol parameter) {
        var builder = new StringBuilder();
        if(parameter.IsParams) {
            builder.Append("params ");
        }
        switch(parameter.RefKind) {
            case RefKind.Ref:
                builder.Append("ref ");
                break;
            case RefKind.Out:
                builder.Append("out ");
                break;
            case RefKind.In:
                builder.Append("in ");
                break;
        }
        builder.Append(parameter.Type.ToDisplayString(FullyQualifiedFormat));
        builder.Append(' ');
        builder.Append(parameter.Name);
        return builder.ToString();
    }

    // method translation の入口。
    // invocation 系は OptimizeVisitor で Where pushdown を掛けてから文字列化する。
    private static TranslatedMethodBody? TryTranslateMethod(Compilation compilation,IMethodSymbol method) {
        foreach(var syntaxReference in method.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not MethodDeclarationSyntax syntax) {
                continue;
            }
            var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
            if(syntax.ExpressionBody?.Expression is QueryExpressionSyntax expressionQuery) {
                var translatedQuery = TryTranslateQueryExpression(expressionQuery,semanticModel);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.ExpressionBody?.Expression is LambdaExpressionSyntax expressionLambda) {
                var translatedLambda = TryTranslateLambdaExpression(expressionLambda,semanticModel);
                return translatedLambda is null ? null : new TranslatedMethodBody(translatedLambda,true);
            }
            if(syntax.ExpressionBody?.Expression is InvocationExpressionSyntax expressionInvocation) {
                return TryTranslateInvocationBody(method,expressionInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(expressionInvocation),semanticModel);
            }
            if(syntax.Body is null||syntax.Body.Statements.Count!=1) {
                continue;
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax returnStatement&&returnStatement.Expression is QueryExpressionSyntax blockQuery) {
                var translatedQuery = TryTranslateQueryExpression(blockQuery,semanticModel);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax lambdaReturn&&lambdaReturn.Expression is LambdaExpressionSyntax blockLambda) {
                var translatedLambda = TryTranslateLambdaExpression(blockLambda,semanticModel);
                return translatedLambda is null ? null : new TranslatedMethodBody(translatedLambda,true);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax invocationReturn&&invocationReturn.Expression is InvocationExpressionSyntax blockInvocation) {
                return TryTranslateInvocationBody(method,blockInvocation,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(blockInvocation),semanticModel);
            }
        }
        return null;
    }

    // Expression<Func<...>> などの戻り値では、ラムダ本体の query / invocation だけを書き換えて
    // ラムダ自体の形とパラメータはそのまま維持する。
    private static string? TryTranslateLambdaExpression(LambdaExpressionSyntax lambda,SemanticModel semanticModel) {
        if(lambda.Body is not ExpressionSyntax lambdaBody) {
            return null;
        }
        while(lambdaBody is ParenthesizedExpressionSyntax parenthesizedExpression) {
            lambdaBody=parenthesizedExpression.Expression;
        }

        string? translatedBody = lambdaBody switch {
            QueryExpressionSyntax queryExpression => TryTranslateQueryExpression(queryExpression,semanticModel),
            InvocationExpressionSyntax invocationExpression => TryTranslateInvocationExpression((InvocationExpressionSyntax)OptimizeVisitor.Optimize(invocationExpression),semanticModel),
            _ => null,
        };
        if(translatedBody is null) {
            return null;
        }

        var asyncPrefix = lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword) ? "async " : string.Empty;
        return lambda switch {
            SimpleLambdaExpressionSyntax simpleLambda => $"{asyncPrefix}{simpleLambda.Parameter} => {translatedBody}",
            ParenthesizedLambdaExpressionSyntax parenthesizedLambda => $"{asyncPrefix}{parenthesizedLambda.ParameterList} => {translatedBody}",
            _ => null,
        };
    }

    // invocation のうち先行評価・scalar fusion・Aggregate は block body に展開し、他は expression body のまま吐く。
    private static TranslatedMethodBody? TryTranslateInvocationBody(IMethodSymbol method,InvocationExpressionSyntax originalInvocation,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        var 列挙結果Body = TryTranslateSequenceLoopFusionBody(method,invocation,semanticModel);
        if(列挙結果Body is not null) {
            return new TranslatedMethodBody(列挙結果Body,false);
        }
        var スカラー結果Body = TryTranslateScalarLoopFusionBody(method,originalInvocation,invocation,semanticModel);
        if(スカラー結果Body is not null) {
            return new TranslatedMethodBody(スカラー結果Body,false);
        }
        var 集約Body = TryTranslateAggregateBody(method,invocation,semanticModel);
        if(集約Body is not null) {
            return new TranslatedMethodBody(集約Body,false);
        }
        var 式Body = TryTranslateInvocationExpression(invocation,semanticModel);
        return 式Body is null ? null : new TranslatedMethodBody(式Body,true);
    }

    // IEnumerable<T> を返す method-chain の先行評価候補。
    // StorageList を使うかどうかは空間と時間のトレードオフなので、generator は型から自動判断しない。
    // method / invocation / semanticModel は、明示的な Managed/Unmanaged 方針が呼び出し側から渡る形に戻すときの解析入力。
    private static string? TryTranslateSequenceLoopFusionBody(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        _=method;
        _=invocation;
        _=semanticModel;
        return null;
    }

    // Sum / Average 終端の method-chain を、遅延中間操作を挟まず 1 つの列挙ループへ落とす。
    private static string? TryTranslateScalarLoopFusionBody(IMethodSymbol method,InvocationExpressionSyntax originalInvocation,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(!TryCreateScalarFusionPipeline(invocation,semanticModel,out var pipeline)) {
            return null;
        }
        if(semanticModel.GetSymbolInfo(originalInvocation).Symbol is not IMethodSymbol terminalMethodSymbol) {
            return null;
        }
        if(!TryCreateScalarTerminalInfo(method.ReturnType,terminalMethodSymbol,out var terminalInfo)) {
            return null;
        }
        return new ScalarFusionEmitter(pipeline,terminalInfo).Build();
    }

    private static bool TryCreateScalarFusionPipeline(InvocationExpressionSyntax invocation,SemanticModel semanticModel,out ScalarFusionPipeline pipeline) {
        pipeline=null!;
        if(!TryGetScalarFusionCallInfo(invocation,out var terminalCall)) {
            return false;
        }
        if(terminalCall.MethodName!="Sum"&&terminalCall.MethodName!="Average") {
            return false;
        }
        if(terminalCall.Arguments.Length>1) {
            return false;
        }

        var reversedOperations = new List<ScalarFusionOperation>();
        if(terminalCall.Arguments.Length==1) {
            if(!TryCreateLambda(terminalCall.Arguments[0].Expression,out var selector)||selector.Parameters.Length!=1) {
                return false;
            }
            reversedOperations.Add(ScalarFusionOperation.Create("Select",terminalCall.Arguments,terminalCall.TypeArguments,selector,null));
        }

        var source = RemoveParentheses(terminalCall.Source);
        while(source is InvocationExpressionSyntax sourceInvocation&&TryGetScalarFusionCallInfo(sourceInvocation,out var sourceCall)) {
            if(!IsScalarFusionIntermediate(sourceCall.MethodName)) {
                break;
            }
            if(!TryCreateScalarFusionOperation(sourceCall,out var operation)) {
                return false;
            }
            reversedOperations.Add(operation);
            source=RemoveParentheses(sourceCall.Source);
        }

        reversedOperations.Reverse();
        pipeline=new ScalarFusionPipeline(TranslateNestedExpression(source,semanticModel),reversedOperations);
        return true;
    }

    private static bool TryCreateScalarFusionOperation(ScalarFusionCallInfo call,out ScalarFusionOperation operation) {
        operation=null!;
        switch(call.MethodName) {
            case "Where":
            case "Select":
            case "TakeWhile":
            case "SkipWhile":
                if(call.Arguments.Length!=1||!TryCreateLambda(call.Arguments[0].Expression,out var lambda)||(lambda.Parameters.Length!=1&&lambda.Parameters.Length!=2)) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,lambda,null);
                return true;

            case "Cast":
            case "OfType":
                if(call.Arguments.Length!=0||call.TypeArguments.Length!=1) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "Take":
            case "Skip":
                if(call.Arguments.Length!=1) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "Distinct":
                if(call.Arguments.Length!=0) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,null,null);
                return true;

            case "SelectMany":
                if(call.Arguments.Length!=1&&call.Arguments.Length!=2) {
                    return false;
                }
                if(!TryCreateLambda(call.Arguments[0].Expression,out var collectionSelector)||(collectionSelector.Parameters.Length!=1&&collectionSelector.Parameters.Length!=2)) {
                    return false;
                }
                FusionLambda? resultSelector = null;
                if(call.Arguments.Length==2&&(!TryCreateLambda(call.Arguments[1].Expression,out resultSelector)||resultSelector.Parameters.Length!=2)) {
                    return false;
                }
                operation=ScalarFusionOperation.Create(call.MethodName,call.Arguments,call.TypeArguments,collectionSelector,resultSelector);
                return true;

            default:
                return false;
        }
    }

    private static bool IsScalarFusionIntermediate(string methodName) {
        return methodName=="Where"
            ||methodName=="Select"
            ||methodName=="Cast"
            ||methodName=="OfType"
            ||methodName=="Take"
            ||methodName=="TakeWhile"
            ||methodName=="Skip"
            ||methodName=="SkipWhile"
            ||methodName=="Distinct"
            ||methodName=="SelectMany";
    }

    private static bool TryGetScalarFusionCallInfo(InvocationExpressionSyntax invocation,out ScalarFusionCallInfo call) {
        call=default;
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }

        var methodName = memberAccess.Name is GenericNameSyntax genericName
            ? genericName.Identifier.ValueText
            : memberAccess.Name.Identifier.ValueText;
        if(string.IsNullOrWhiteSpace(methodName)) {
            return false;
        }
        var typeArguments = memberAccess.Name is GenericNameSyntax generic
            ? generic.TypeArgumentList.Arguments.Select(static argument => argument.ToString()).ToArray()
            : Array.Empty<string>();
        var isStaticEnumerableCall = IsStaticEnumerableReceiver(NormalizeExpression(RemoveParentheses(memberAccess.Expression)));
        if(isStaticEnumerableCall) {
            if(invocation.ArgumentList.Arguments.Count==0) {
                return false;
            }
            call=new ScalarFusionCallInfo(methodName,RemoveParentheses(invocation.ArgumentList.Arguments[0].Expression),invocation.ArgumentList.Arguments.Skip(1).ToArray(),typeArguments);
            return true;
        }

        call=new ScalarFusionCallInfo(methodName,RemoveParentheses(memberAccess.Expression),invocation.ArgumentList.Arguments.ToArray(),typeArguments);
        return true;
    }

    private static bool TryCreateLambda(ExpressionSyntax expression,out FusionLambda lambda) {
        lambda=null!;
        if(expression is SimpleLambdaExpressionSyntax simpleLambda&&simpleLambda.Body is ExpressionSyntax simpleBody) {
            lambda=new FusionLambda(new[] { simpleLambda.Parameter.Identifier.ValueText },simpleBody);
            return !string.IsNullOrWhiteSpace(lambda.Parameters[0]);
        }
        if(expression is ParenthesizedLambdaExpressionSyntax parenthesizedLambda&&parenthesizedLambda.Body is ExpressionSyntax parenthesizedBody) {
            var parameters = parenthesizedLambda.ParameterList.Parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();
            if(parameters.Length==0||parameters.Any(static parameter => string.IsNullOrWhiteSpace(parameter))) {
                return false;
            }
            lambda=new FusionLambda(parameters,parenthesizedBody);
            return true;
        }
        return false;
    }

    private static bool TryCreateScalarTerminalInfo(ITypeSymbol methodReturnType,IMethodSymbol terminalMethodSymbol,out ScalarTerminalInfo terminalInfo) {
        terminalInfo=default;
        var terminalMethod = terminalMethodSymbol.ReducedFrom??terminalMethodSymbol;
        var methodName = terminalMethod.Name;
        if(methodName!="Sum"&&methodName!="Average") {
            return false;
        }
        if(!TryGetTerminalValueType(terminalMethod,out var terminalValueType)) {
            return false;
        }

        var returnTypeIsNullable = TryGetNullableUnderlyingType(methodReturnType,out var underlyingReturnType);
        var terminalValueIsNullable = TryGetNullableUnderlyingType(terminalValueType,out var underlyingTerminalValueType);
        var effectiveReturnType = returnTypeIsNullable ? underlyingReturnType : methodReturnType;
        var effectiveTerminalValueType = terminalValueIsNullable ? underlyingTerminalValueType : terminalValueType;
        var accumulatorTypeName = methodName=="Average"
            ? GetAverageAccumulatorTypeName(effectiveTerminalValueType,effectiveReturnType)
            : effectiveReturnType.ToDisplayString(FullyQualifiedFormat);
        terminalInfo=new ScalarTerminalInfo(
            methodName,
            methodReturnType.ToDisplayString(FullyQualifiedFormat),
            accumulatorTypeName,
            returnTypeIsNullable,
            terminalValueIsNullable,
            ShouldUseCheckedAccumulator(effectiveTerminalValueType,accumulatorTypeName),
            methodName=="Average"&&string.Equals(accumulatorTypeName,"long",StringComparison.Ordinal));
        return true;
    }

    private static bool TryGetTerminalValueType(IMethodSymbol terminalMethod,out ITypeSymbol valueType) {
        valueType=null!;
        if(terminalMethod.Parameters.Length==0) {
            return false;
        }
        if(terminalMethod.Parameters.Length>=2
            &&terminalMethod.Parameters[terminalMethod.Parameters.Length-1].Type is INamedTypeSymbol selectorType
            &&selectorType.Name=="Func"
            &&selectorType.ContainingNamespace.ToDisplayString()=="System"
            &&selectorType.TypeArguments.Length>=2) {
            valueType=selectorType.TypeArguments[selectorType.TypeArguments.Length-1];
            return true;
        }
        return TryGetEnumerableElementType(terminalMethod.Parameters[0].Type,out valueType);
    }

    private static bool TryGetEnumerableElementType(ITypeSymbol type,out ITypeSymbol elementType) {
        if(type is INamedTypeSymbol namedType
            &&namedType.Name=="IEnumerable"
            &&namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            &&namedType.TypeArguments.Length==1) {
            elementType=namedType.TypeArguments[0];
            return true;
        }
        foreach(var interfaceType in type.AllInterfaces) {
            if(interfaceType.Name=="IEnumerable"
                &&interfaceType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
                &&interfaceType.TypeArguments.Length==1) {
                elementType=interfaceType.TypeArguments[0];
                return true;
            }
        }
        elementType=null!;
        return false;
    }

    private static bool TryGetNullableUnderlyingType(ITypeSymbol type,out ITypeSymbol underlyingType) {
        if(type is INamedTypeSymbol namedType&&namedType.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T&&namedType.TypeArguments.Length==1) {
            underlyingType=namedType.TypeArguments[0];
            return true;
        }
        underlyingType=null!;
        return false;
    }

    private static string GetAverageAccumulatorTypeName(ITypeSymbol terminalValueType,ITypeSymbol returnType) {
        return terminalValueType.SpecialType switch {
            SpecialType.System_Int32 => "long",
            SpecialType.System_Int64 => "decimal",
            SpecialType.System_Single => "double",
            _ => returnType.ToDisplayString(FullyQualifiedFormat),
        };
    }

    private static bool ShouldUseCheckedAccumulator(ITypeSymbol terminalValueType,string accumulatorTypeName) {
        return terminalValueType.SpecialType==SpecialType.System_Int32
            ||terminalValueType.SpecialType==SpecialType.System_Int64
            ||string.Equals(accumulatorTypeName,"decimal",StringComparison.Ordinal)
            ||string.Equals(accumulatorTypeName,"global::System.Decimal",StringComparison.Ordinal);
    }

    private sealed class ScalarFusionEmitter {
        private const string DoneLabel = "__seamlessDone";
        private readonly ScalarFusionPipeline pipeline;
        private readonly ScalarTerminalInfo terminalInfo;
        private readonly List<string> lines = new();
        private int indent;
        private int valueIndex;
        private int loopIndex;

        public ScalarFusionEmitter(ScalarFusionPipeline pipeline,ScalarTerminalInfo terminalInfo) {
            this.pipeline=pipeline;
            this.terminalInfo=terminalInfo;
        }

        public string Build() {
            this.Add("{");
            this.indent++;
            this.Add($"var __seamlessSource = {this.pipeline.SourceExpression};");
            this.Add("global::System.ArgumentNullException.ThrowIfNull(__seamlessSource);");
            this.DeclareOperationState();
            this.DeclareAccumulator();
            this.EmitTakeEmptyShortCircuit();
            var rootContext = this.CreateLoopContext();
            this.Add("foreach(var __seamlessItem in __seamlessSource) {");
            this.indent++;
            this.EmitOperations(0,"__seamlessItem",rootContext);
            this.EmitLoopContinue(rootContext);
            this.indent--;
            this.Add("}");
            this.Add($"{DoneLabel}:;");
            this.EmitReturn();
            this.indent--;
            this.Add("}");
            return string.Join("\n",this.lines);
        }

        private void DeclareOperationState() {
            for(var index = 0; index<this.pipeline.Operations.Count; index++) {
                var operation = this.pipeline.Operations[index];
                switch(operation.MethodName) {
                    case "Where":
                    case "Select":
                    case "TakeWhile":
                    case "SkipWhile":
                        if(operation.Lambda is not null&&operation.Lambda.Parameters.Length==2) {
                            this.Add($"var __seamlessIndex{index} = -1;");
                        }
                        if(operation.MethodName=="SkipWhile") {
                            this.Add($"var __seamlessSkipWhileYielding{index} = false;");
                        }
                        break;

                    case "Skip":
                        this.Add($"var __seamlessSkip{index} = {NormalizeExpression(operation.Arguments[0].Expression)};");
                        break;

                    case "Take":
                        this.Add($"var __seamlessTake{index} = {NormalizeExpression(operation.Arguments[0].Expression)};");
                        break;

                    case "Distinct":
                        this.Add($"var __seamlessDistinct{index} = new global::System.Collections.Generic.HashSet<object>();");
                        break;

                    case "SelectMany":
                        if(operation.Lambda is not null&&operation.Lambda.Parameters.Length==2) {
                            this.Add($"var __seamlessIndex{index} = -1;");
                        }
                        break;
                }
            }
        }

        private void DeclareAccumulator() {
            this.Add($"{this.terminalInfo.AccumulatorTypeName} __seamlessSum = default;");
            if(this.terminalInfo.MethodName=="Average") {
                this.Add("long __seamlessCount = 0;");
            }
        }

        private void EmitTakeEmptyShortCircuit() {
            for(var index = 0; index<this.pipeline.Operations.Count; index++) {
                if(this.pipeline.Operations[index].MethodName!="Take") {
                    continue;
                }
                this.Add($"if(__seamlessTake{index}<=0) {{");
                this.indent++;
                this.Add($"goto {DoneLabel};");
                this.indent--;
                this.Add("}");
            }
        }

        private FusionLoopContext CreateLoopContext() {
            var index = this.loopIndex++;
            var context = new FusionLoopContext($"__seamlessContinue{index}",$"__seamlessStop{index}");
            this.Add($"var {context.StopFlag} = false;");
            return context;
        }

        private void EmitLoopContinue(FusionLoopContext context) {
            this.Add($"{context.ContinueLabel}:;");
            this.Add($"if({context.StopFlag}) {{");
            this.indent++;
            this.Add($"goto {DoneLabel};");
            this.indent--;
            this.Add("}");
        }

        private void EmitOperations(int operationIndex,string valueExpression,FusionLoopContext context) {
            if(operationIndex>=this.pipeline.Operations.Count) {
                this.EmitTerminal(valueExpression,context);
                return;
            }

            var operation = this.pipeline.Operations[operationIndex];
            switch(operation.MethodName) {
                case "Where":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if(!({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))})) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Select":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    var selectedValue = this.NextValueName();
                    this.Add($"var {selectedValue} = {this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))};");
                    this.EmitOperations(operationIndex+1,selectedValue,context);
                    break;

                case "Cast":
                    var castedValue = this.NextValueName();
                    this.Add($"var {castedValue} = ({operation.TypeArguments[0]}){valueExpression}!;");
                    this.EmitOperations(operationIndex+1,castedValue,context);
                    break;

                case "OfType":
                    var typedValue = this.NextValueName();
                    this.Add($"if({valueExpression} is not {operation.TypeArguments[0]} {typedValue}) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,typedValue,context);
                    break;

                case "Skip":
                    this.Add($"if(__seamlessSkip{operationIndex}>0) {{");
                    this.indent++;
                    this.Add($"__seamlessSkip{operationIndex}--;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Take":
                    this.Add($"if(__seamlessTake{operationIndex}<=0) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.Add($"__seamlessTake{operationIndex}--;");
                    this.Add($"if(__seamlessTake{operationIndex}<=0) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "SkipWhile":
                    this.Add($"if(!__seamlessSkipWhileYielding{operationIndex}) {{");
                    this.indent++;
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))}) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.Add($"__seamlessSkipWhileYielding{operationIndex}=true;");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "TakeWhile":
                    this.EmitIndexedLambdaAdvance(operationIndex,operation);
                    this.Add($"if(!({this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))})) {{");
                    this.indent++;
                    this.Add($"{context.StopFlag}=true;");
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "Distinct":
                    this.Add($"if(!__seamlessDistinct{operationIndex}.Add({valueExpression})) {{");
                    this.indent++;
                    this.Add($"goto {context.ContinueLabel};");
                    this.indent--;
                    this.Add("}");
                    this.EmitOperations(operationIndex+1,valueExpression,context);
                    break;

                case "SelectMany":
                    this.EmitSelectMany(operationIndex,valueExpression);
                    break;
            }
        }

        private void EmitSelectMany(int operationIndex,string valueExpression) {
            var operation = this.pipeline.Operations[operationIndex];
            this.EmitIndexedLambdaAdvance(operationIndex,operation);
            var collectionValue = this.NextValueName();
            this.Add($"var {collectionValue} = {this.InlineLambda(operation.Lambda!,valueExpression,this.GetIndexArgument(operationIndex,operation))};");
            var innerItem = this.NextValueName();
            var innerContext = this.CreateLoopContext();
            this.Add($"foreach(var {innerItem} in {collectionValue}) {{");
            this.indent++;
            var downstreamValue = innerItem;
            if(operation.SecondLambda is not null) {
                downstreamValue=this.NextValueName();
                this.Add($"var {downstreamValue} = {this.InlineLambda(operation.SecondLambda,valueExpression,innerItem)};");
            }
            this.EmitOperations(operationIndex+1,downstreamValue,innerContext);
            this.EmitLoopContinue(innerContext);
            this.indent--;
            this.Add("}");
        }

        private void EmitIndexedLambdaAdvance(int operationIndex,ScalarFusionOperation operation) {
            if(operation.Lambda is null||operation.Lambda.Parameters.Length!=2) {
                return;
            }
            this.Add("checked {");
            this.indent++;
            this.Add($"__seamlessIndex{operationIndex}++;");
            this.indent--;
            this.Add("}");
        }

        private string? GetIndexArgument(int operationIndex,ScalarFusionOperation operation) {
            return operation.Lambda is not null&&operation.Lambda.Parameters.Length==2
                ? $"__seamlessIndex{operationIndex}"
                : null;
        }

        private void EmitTerminal(string valueExpression,FusionLoopContext context) {
            var terminalValue = valueExpression;
            if(this.terminalInfo.TerminalValueIsNullable) {
                terminalValue=this.NextValueName();
                this.Add($"var {terminalValue} = {valueExpression};");
                this.Add($"if(!{terminalValue}.HasValue) {{");
                this.indent++;
                this.Add($"goto {context.ContinueLabel};");
                this.indent--;
                this.Add("}");
                terminalValue=$"{terminalValue}.GetValueOrDefault()";
            }
            if(this.terminalInfo.UseCheckedAccumulator) {
                this.Add("checked {");
                this.indent++;
                this.Add($"__seamlessSum+={terminalValue};");
                if(this.terminalInfo.MethodName=="Average") {
                    this.Add("__seamlessCount++;");
                }
                this.indent--;
                this.Add("}");
            }
            else {
                this.Add($"__seamlessSum+={terminalValue};");
                if(this.terminalInfo.MethodName=="Average") {
                    this.Add("checked {");
                    this.indent++;
                    this.Add("__seamlessCount++;");
                    this.indent--;
                    this.Add("}");
                }
            }
        }

        private void EmitReturn() {
            if(this.terminalInfo.MethodName=="Sum") {
                this.Add("return __seamlessSum;");
                return;
            }
            this.Add("if(__seamlessCount==0) {");
            this.indent++;
            if(this.terminalInfo.ReturnTypeIsNullable) {
                this.Add("return default;");
            }
            else {
                this.Add("throw new global::System.InvalidOperationException(\"Sequence contains no elements.\");");
            }
            this.indent--;
            this.Add("}");
            var averageExpression = this.terminalInfo.CastAccumulatorToDoubleBeforeAverageDivide
                ? "(double)__seamlessSum/__seamlessCount"
                : "__seamlessSum/__seamlessCount";
            this.Add($"return ({this.terminalInfo.ReturnTypeName})({averageExpression});");
        }

        private string InlineLambda(FusionLambda lambda,string firstArgument,string? secondArgument) {
            var replacements = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                [lambda.Parameters[0]]=SyntaxFactory.ParseExpression(firstArgument),
            };
            if(secondArgument is not null&&lambda.Parameters.Length>1) {
                replacements[lambda.Parameters[1]]=SyntaxFactory.ParseExpression(secondArgument);
            }
            var rewritten = new LambdaParameterInlineRewriter(replacements).Visit(lambda.Body) as ExpressionSyntax??lambda.Body;
            return NormalizeExpression(rewritten);
        }

        private string NextValueName() {
            return $"__seamlessValue{this.valueIndex++}";
        }

        private void Add(string line) {
            this.lines.Add(new string(' ',this.indent*4)+line);
        }
    }

    // Aggregate は call-through にすると空列時例外や列挙手順の表現が曖昧になるため、明示的な列挙器ループへ展開する。
    private static string? TryTranslateAggregateBody(IMethodSymbol method,InvocationExpressionSyntax invocation,SemanticModel semanticModel) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        if(memberAccess.Name.Identifier.ValueText!="Aggregate") {
            return null;
        }
        var receiver = TranslateNestedExpression(memberAccess.Expression,semanticModel);
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => TranslateNestedExpression(argument.Expression,semanticModel))
            .ToArray();

        string sourceExpression;
        string funcExpression;
        if(IsStaticEnumerableReceiver(receiver)) {
            if(arguments.Length!=2) {
                return null;
            }
            sourceExpression=arguments[0];
            funcExpression=arguments[1];
        }
        else {
            if(arguments.Length!=1) {
                return null;
            }
            sourceExpression=receiver;
            funcExpression=arguments[0];
        }

        var returnType = method.ReturnType.ToDisplayString(FullyQualifiedFormat);
        return string.Join("\n",new[] {
            "{",
            $"    var __aggregateSource = {sourceExpression};",
            "    global::System.ArgumentNullException.ThrowIfNull(__aggregateSource);",
            $"    global::System.Func<{returnType}, {returnType}, {returnType}> __aggregateFunc = {funcExpression};",
            "    global::System.ArgumentNullException.ThrowIfNull(__aggregateFunc);",
            $"    {returnType} __aggregateResult = default!;",
            "    var __aggregateHasValue = false;",
            "    foreach(var __aggregateCurrent in __aggregateSource) {",
            "        if(!__aggregateHasValue) {",
            "            __aggregateResult = __aggregateCurrent;",
            "            __aggregateHasValue = true;",
            "            continue;",
            "        }",
            "        __aggregateResult = __aggregateFunc(__aggregateResult,__aggregateCurrent);",
            "    }",
            "    if(!__aggregateHasValue) {",
            "        throw new global::System.InvalidOperationException(\"Sequence contains no elements.\");",
            "    }",
            "    return __aggregateResult;",
            "}"
        });
    }

    // query syntax はまず extension-style の method chain に下ろし、
    // その後 OptimizeVisitor と既存の invocation translator に流す。
    // これで query continuation (`select ... into x where ...`) も invocation 側と同じ最適化経路に乗る。
    private static string? TryTranslateQueryExpression(QueryExpressionSyntax query,SemanticModel? semanticModel = null) {
        if(TryLowerQueryExpressionToInvocation(query,semanticModel,out var loweredInvocation)) {
            var optimized = OptimizeVisitor.Optimize(loweredInvocation);
            return optimized is InvocationExpressionSyntax optimizedInvocation
                ? TryTranslateInvocationExpression(optimizedInvocation,semanticModel)??NormalizeExpression(optimized)
                : NormalizeExpression(optimized);
        }
        return null;
    }

    private static bool TryLowerQueryExpressionToInvocation(QueryExpressionSyntax query,SemanticModel? semanticModel,out InvocationExpressionSyntax invocation) {
        var temporaryIndex = 0;
        if(TryLowerQueryBody(
                TranslateNestedExpression(query.FromClause.Expression,semanticModel),
                QueryRangeScope.CreateIdentity(query.FromClause.Identifier.ValueText),
                query.Body,
                semanticModel,
                ref temporaryIndex,
                out var loweredText)
            &&SyntaxFactory.ParseExpression(loweredText) is InvocationExpressionSyntax loweredInvocation) {
            invocation=loweredInvocation;
            return true;
        }

        invocation=null!;
        return false;
    }

    private static bool TryLowerQueryBody(string sourceExpression,QueryRangeScope scope,QueryBodySyntax body,SemanticModel? semanticModel,ref int temporaryIndex,out string loweredText) {
        var currentSource = sourceExpression;
        var currentScope = scope;

        foreach(var clause in body.Clauses) {
            switch(clause) {
                case FromClauseSyntax fromClause:
                    var collectionSelector = RewriteQueryRangeVariables(fromClause.Expression,currentScope,semanticModel);
                    var innerRangeVariable = fromClause.Identifier.ValueText;
                    currentSource=$"({currentSource}).SelectMany({currentScope.CurrentParameterName} => {collectionSelector},({currentScope.CurrentParameterName},{innerRangeVariable}) => {BuildCarrierCreationText(currentScope,(innerRangeVariable,innerRangeVariable))})";
                    currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),innerRangeVariable);
                    break;

                case LetClauseSyntax letClause:
                    var letRangeVariable = letClause.Identifier.ValueText;
                    var letExpression = RewriteQueryRangeVariables(letClause.Expression,currentScope,semanticModel);
                    currentSource=$"({currentSource}).Select({currentScope.CurrentParameterName} => {BuildCarrierCreationText(currentScope,(letRangeVariable,letExpression))})";
                    currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),letRangeVariable);
                    break;

                case WhereClauseSyntax whereClause:
                    currentSource=$"({currentSource}).Where({currentScope.CurrentParameterName} => {RewriteQueryRangeVariables(whereClause.Condition,currentScope,semanticModel)})";
                    break;

                case JoinClauseSyntax joinClause:
                    var joinSource = RewriteQueryRangeVariables(joinClause.InExpression,currentScope,semanticModel);
                    var outerParameter = currentScope.CurrentParameterName;
                    var innerParameter = joinClause.Identifier.ValueText;
                    var outerKey = RewriteQueryRangeVariables(joinClause.LeftExpression,currentScope,semanticModel);
                    var innerKey = RewriteQueryRangeVariables(joinClause.RightExpression,QueryRangeScope.CreateIdentity(innerParameter),semanticModel);
                    if(joinClause.Into is null) {
                        currentSource=$"({currentSource}).Join({joinSource},{outerParameter} => {outerKey},{innerParameter} => {innerKey},({outerParameter},{innerParameter}) => {BuildCarrierCreationText(currentScope,(innerParameter,innerParameter))})";
                        currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),innerParameter);
                    }
                    else {
                        var groupedRangeVariable = joinClause.Into.Identifier.ValueText;
                        currentSource=$"({currentSource}).GroupJoin({joinSource},{outerParameter} => {outerKey},{innerParameter} => {innerKey},({outerParameter},{groupedRangeVariable}) => {BuildCarrierCreationText(currentScope,(groupedRangeVariable,groupedRangeVariable))})";
                        currentScope=currentScope.CreateCarrierScope(CreateGeneratedQueryIdentifier(ref temporaryIndex),groupedRangeVariable);
                    }
                    break;

                case OrderByClauseSyntax orderByClause:
                    for(var index = 0; index<orderByClause.Orderings.Count; index++) {
                        var ordering = orderByClause.Orderings[index];
                        var keySelector = RewriteQueryRangeVariables(ordering.Expression,currentScope,semanticModel);
                        var isDescending = ordering.AscendingOrDescendingKeyword.IsKind(SyntaxKind.DescendingKeyword);
                        var methodName = index==0
                            ? (isDescending ? "OrderByDescending" : "OrderBy")
                            : (isDescending ? "ThenByDescending" : "ThenBy");
                        currentSource=$"({currentSource}).{methodName}({currentScope.CurrentParameterName} => {keySelector})";
                    }
                    break;

                default:
                    loweredText=string.Empty;
                    return false;
            }
        }

        switch(body.SelectOrGroup) {
            case SelectClauseSyntax selectClause:
                var selection = RewriteQueryRangeVariables(selectClause.Expression,currentScope,semanticModel);
                currentSource=$"({currentSource}).Select({currentScope.CurrentParameterName} => {selection})";
                if(body.Continuation is null) {
                    loweredText=currentSource;
                    return true;
                }
                return TryLowerQueryBody(currentSource,QueryRangeScope.CreateIdentity(body.Continuation.Identifier.ValueText),body.Continuation.Body,semanticModel,ref temporaryIndex,out loweredText);

            case GroupClauseSyntax groupClause:
                var groupedElement = RewriteQueryRangeVariables(groupClause.GroupExpression,currentScope,semanticModel);
                var groupedKey = RewriteQueryRangeVariables(groupClause.ByExpression,currentScope,semanticModel);
                currentSource=$"({currentSource}).GroupBy({currentScope.CurrentParameterName} => {groupedKey},{currentScope.CurrentParameterName} => {groupedElement})";
                if(body.Continuation is null) {
                    loweredText=currentSource;
                    return true;
                }
                return TryLowerQueryBody(currentSource,QueryRangeScope.CreateIdentity(body.Continuation.Identifier.ValueText),body.Continuation.Body,semanticModel,ref temporaryIndex,out loweredText);

            default:
                loweredText=string.Empty;
                return false;
        }
    }

    private static string RewriteQueryRangeVariables(ExpressionSyntax expression,QueryRangeScope scope,SemanticModel? semanticModel) {
        var rewritten = new QueryRangeVariableRewriter(scope.RangeExpressions).Visit(expression);
        return TranslateNestedExpression(rewritten as ExpressionSyntax??expression,semanticModel);
    }

    private static string BuildCarrierCreationText(QueryRangeScope scope,params (string Name,string ExpressionText)[] additionalFields) {
        var assignments = new List<string>(scope.VisibleVariables.Count+additionalFields.Length);
        foreach(var visibleVariable in scope.VisibleVariables) {
            assignments.Add($"{visibleVariable} = {scope.RangeExpressions[visibleVariable]}");
        }
        foreach(var additionalField in additionalFields) {
            assignments.Add($"{additionalField.Name} = {additionalField.ExpressionText}");
        }
        return $"new {{ {string.Join(", ",assignments)} }}";
    }

    private static string CreateGeneratedQueryIdentifier(ref int temporaryIndex) {
        return $"__q{temporaryIndex++}";
    }

    // method chain を再帰的に辿って Seamless LINQ 呼び出しへそろえる。
    // receiver だけでなく argument 側の nested invocation も変換するのが重要。
    private static readonly HashSet<string> EnumerableMethodNames = new(StringComparer.Ordinal) {
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "AsEnumerable",
        "Average",
        "Cast",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Empty",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "InfiniteSequence",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LeftJoin",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "OfType",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Range",
        "Repeat",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "Sequence",
        "SequenceEqual",
        "Shuffle",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ThenBy",
        "ThenByDescending",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "TryGetNonEnumeratedCount",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    };

    private static readonly HashSet<string> HashSetMethodNames = new(StringComparer.Ordinal) {
        "Aggregate",
        "AggregateBy",
        "All",
        "Any",
        "Append",
        "AsEnumerable",
        "Average",
        "Chunk",
        "Concat",
        "Contains",
        "Count",
        "CountBy",
        "DefaultIfEmpty",
        "Distinct",
        "DistinctBy",
        "ElementAt",
        "ElementAtOrDefault",
        "Except",
        "ExceptBy",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "GroupJoin",
        "Index",
        "Intersect",
        "IntersectBy",
        "Join",
        "Last",
        "LastOrDefault",
        "LeftJoin",
        "LongCount",
        "Max",
        "MaxBy",
        "Min",
        "MinBy",
        "Order",
        "OrderBy",
        "OrderByDescending",
        "OrderDescending",
        "Prepend",
        "Reverse",
        "RightJoin",
        "Select",
        "SelectMany",
        "SequenceEqual",
        "Shuffle",
        "Single",
        "SingleOrDefault",
        "Skip",
        "SkipLast",
        "SkipWhile",
        "Sum",
        "Take",
        "TakeLast",
        "TakeWhile",
        "ToArray",
        "ToDictionary",
        "ToHashSet",
        "ToList",
        "ToLookup",
        "TryGetNonEnumeratedCount",
        "Union",
        "UnionBy",
        "Where",
        "Zip"
    };

    // method chain を再帰的に辿って Seamless LINQ 呼び出しへそろえる。
    // semanticModel がある場合は呼び出し先の型で Enumerable / HashSet を正確に振り分ける。
    private static string? TryTranslateInvocationExpression(InvocationExpressionSyntax invocation,SemanticModel? semanticModel = null) {
        if(invocation.Expression is SimpleNameSyntax simpleName) {
            return TryTranslateUnqualifiedMemberInvocation(invocation,simpleName,semanticModel);
        }
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var methodName = memberAccess.Name.Identifier.ValueText;
        var typeArgumentListText = GetTypeArgumentListText(memberAccess.Name);
        var receiver = TranslateNestedExpression(memberAccess.Expression,semanticModel);
        var semanticTargetType = TryGetTranslatedLinqTarget(invocation,semanticModel,out var symbolResolved);
        var targetType = semanticTargetType
            ??(IsStaticHashSetReceiver(receiver) ? "HashSet" : null)
            ??(IsStaticEnumerableReceiver(receiver) ? "Enumerable" : null);
        if(targetType is null&&symbolResolved) {
            return null;
        }
        targetType??="Enumerable";
        var supportedNames = targetType=="HashSet" ? HashSetMethodNames : EnumerableMethodNames;
        if(!supportedNames.Contains(methodName)) {
            return null;
        }
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => TranslateNestedArgument(argument,semanticModel))
            .ToArray();
        if(IsStaticEnumerableReceiver(receiver)||IsStaticHashSetReceiver(receiver)) {
            return arguments.Length==0
                ? $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}()"
                : $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({string.Join(",",arguments)})";
        }
        return arguments.Length==0
            ? $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({receiver})"
            : $"global::Seamless.Linq.{targetType}.{methodName}{typeArgumentListText}({receiver},{string.Join(",",arguments)})";
    }

    private static string? TryTranslateUnqualifiedMemberInvocation(InvocationExpressionSyntax invocation,SimpleNameSyntax simpleName,SemanticModel? semanticModel) {
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
            return null;
        }
        var definition = methodSymbol.ReducedFrom??methodSymbol;
        if(definition.MethodKind!=MethodKind.Ordinary||definition.ContainingType is null) {
            return null;
        }
        var arguments = invocation.ArgumentList.Arguments
            .Select(argument => TranslateNestedArgument(argument,semanticModel))
            .ToArray();
        var typeArgumentListText = GetTypeArgumentListText(simpleName);
        if(definition.IsStatic) {
            return $"{definition.ContainingType.ToDisplayString(FullyQualifiedFormat)}.{definition.Name}{typeArgumentListText}({string.Join(",",arguments)})";
        }
        return $"self.{definition.Name}{typeArgumentListText}({string.Join(",",arguments)})";
    }

    private static string GetTypeArgumentListText(SimpleNameSyntax name) {
        return name is GenericNameSyntax genericName ? genericName.TypeArgumentList.ToString() : string.Empty;
    }

    private static string? TryGetTranslatedLinqTarget(InvocationExpressionSyntax invocation,SemanticModel? semanticModel,out bool symbolResolved) {
        symbolResolved=false;
        if(!TryGetInvocationMethodSymbol(invocation,semanticModel,out var methodSymbol)) {
            return null;
        }
        symbolResolved=true;
        var definition = methodSymbol.ReducedFrom??methodSymbol;
        var containingType = definition.ContainingType.ToDisplayString();
        return containingType switch {
            "System.Linq.Enumerable" => "Enumerable",
            "Seamless.Linq.Enumerable" => "Enumerable",
            "Seamless.Linq.HashSet" => "HashSet",
            _ => null
        };
    }

    private static bool TryGetInvocationMethodSymbol(InvocationExpressionSyntax invocation,SemanticModel? semanticModel,out IMethodSymbol methodSymbol) {
        methodSymbol=null!;
        if(semanticModel is null) {
            return false;
        }
        try {
            if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol) {
                return false;
            }
            methodSymbol=symbol;
        }
        catch(ArgumentException) {
            return false;
        }
        return true;
    }

    private static string TranslateNestedArgument(ArgumentSyntax argument,SemanticModel? semanticModel) {
        var expression = TranslateNestedExpression(argument.Expression,semanticModel);
        return argument.RefKindKeyword.IsKind(SyntaxKind.None)
            ? expression
            : $"{argument.RefKindKeyword.Text} {expression}";
    }

    private static string TranslateNestedExpression(ExpressionSyntax expression,SemanticModel? semanticModel = null) {
        while(expression is ParenthesizedExpressionSyntax parenthesizedExpression) {
            expression=parenthesizedExpression.Expression;
        }
        if(expression is InvocationExpressionSyntax invocationExpression) {
            return TryTranslateInvocationExpression(invocationExpression,semanticModel)??NormalizeExpression(invocationExpression);
        }
        var 入れ子Invocation変換後 = new NestedInvocationTranslator(semanticModel).Visit(expression) as ExpressionSyntax??expression;
        return NormalizeExpression(入れ子Invocation変換後);
    }

    // lambda body や匿名型 initializer の中に残った LINQ invocation を同じ翻訳規則でそろえる。
    // これを挟まないと、generated source 側に using System.Linq が無い場所で `.Where(...)` が残る。
    private sealed class NestedInvocationTranslator:CSharpSyntaxRewriter {
        private readonly SemanticModel? semanticModel;

        public NestedInvocationTranslator(SemanticModel? semanticModel) {
            this.semanticModel=semanticModel;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node) {
            var 変換済み = TryTranslateInvocationExpression(node,this.semanticModel);
            if(変換済み is not null) {
                return SyntaxFactory.ParseExpression(変換済み);
            }
            return base.VisitInvocationExpression(node);
        }
    }

    private static bool IsStaticEnumerableReceiver(string receiver) {
        return string.Equals(receiver,"Enumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SystemEnumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SeamlessEnumerable",StringComparison.Ordinal)
            ||receiver.EndsWith(".Enumerable",StringComparison.Ordinal);
    }

    private static bool IsStaticHashSetReceiver(string receiver) {
        return string.Equals(receiver,"HashSet",StringComparison.Ordinal)
            ||string.Equals(receiver,"SeamlessHashSet",StringComparison.Ordinal)
            ||receiver.EndsWith(".HashSet",StringComparison.Ordinal);
    }

    private static ImmutableArray<IPropertySymbol> GetGeneratableProperties(INamedTypeSymbol symbol) {
        var builder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        foreach(var property in symbol.GetMembers().OfType<IPropertySymbol>()) {
            if(ShouldGenerateProperty(property)) {
                builder.Add(property);
            }
        }
        return builder.ToImmutable();
    }

    private static bool ShouldGenerateProperty(IPropertySymbol property) {
        foreach(var syntaxReference in property.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not PropertyDeclarationSyntax syntax) {
                continue;
            }
            if(!syntax.Modifiers.Any(SyntaxKind.PartialKeyword)) {
                continue;
            }
            if(syntax.ExpressionBody is not null) {
                continue;
            }
            if(syntax.AccessorList is null||syntax.AccessorList.Accessors.Count!=1) {
                continue;
            }
            var accessor = syntax.AccessorList.Accessors[0];
            if(accessor.Kind()!=SyntaxKind.GetAccessorDeclaration) {
                continue;
            }
            if(accessor.Body is not null||accessor.ExpressionBody is not null) {
                continue;
            }
            return true;
        }
        return false;
    }

    private static string GetParameterName(IPropertySymbol property) {
        var name = property.Name;
        if(string.IsNullOrEmpty(name)) {
            return "value";
        }
        if(name.Length==1) {
            return char.ToLowerInvariant(name[0]).ToString();
        }
        return char.ToLowerInvariant(name[0])+name.Substring(1);
    }

    private static string NormalizeExpression(ExpressionSyntax expression) {
        return expression.ToString();
    }

    private static ExpressionSyntax RemoveParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    private static bool NamespaceEquals(string candidateNamespace,string rootNamespace,string childNamespace) {
        return string.Equals(candidateNamespace,$"{rootNamespace}.{childNamespace}",StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeIdentifier(string value) {
        var builder = new StringBuilder(value.Length);
        foreach(var character in value) {
            if(char.IsLetterOrDigit(character)||character=='_') {
                builder.Append(character);
            }
            else {
                builder.Append('_');
            }
        }
        if(builder.Length==0||!char.IsLetter(builder[0])&&builder[0]!='_') {
            builder.Insert(0,'_');
        }
        return builder.ToString();
    }

    private static string ToLiteral(string value) {
        return SymbolDisplay.FormatLiteral(value,quote: true);
    }

    // unmanaged フィールドは record struct にまとめて一括シリアライズしやすくする。
    private static void AppendUnmanagedRecordSource(StringBuilder builder,ImmutableArray<IPropertySymbol> unmanagedProperties) {
        if(unmanagedProperties.Length>0) {
            builder.AppendLine($"    private readonly record struct _U({string.Join(", ",unmanagedProperties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name}"))});");
            return;
        }

        builder.AppendLine("    private struct _U {");
        builder.AppendLine("    }");
    }

    // managed フィールドは MemoryPackable な _M 側へ寄せ、型別の serialize / deserialize 文をここで組み立てる。
    private static void AppendManagedRecordSource(StringBuilder builder,ImmutableArray<IPropertySymbol> managedProperties) {
        builder.AppendLine("    private struct _M:global::MemoryPack.IMemoryPackable<_M> {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"        public {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name};");
        }
        if(managedProperties.Length>0) {
            builder.AppendLine();
            builder.AppendLine($"        public _M({string.Join(", ",managedProperties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {GetParameterName(property)}"))}) {{");
            foreach(var property in managedProperties) {
                builder.AppendLine($"            this.{property.Name}={GetParameterName(property)};");
            }
            builder.AppendLine("        }");
            builder.AppendLine();
        }
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter() {");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Serialize<TBufferWriter>(ref global::MemoryPack.MemoryPackWriter<TBufferWriter> writer,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildManagedSerializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        static void global::MemoryPack.IMemoryPackable<_M>.Deserialize(ref global::MemoryPack.MemoryPackReader reader,scoped ref _M value) {");
        foreach(var property in managedProperties) {
            builder.AppendLine($"            {BuildManagedDeserializeStatement(property)}");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static string BuildManagedSerializeStatement(IPropertySymbol property) {
        var propertyAccess = $"value.{property.Name}";
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"writer.WriteString({propertyAccess});";
        }
        return $"writer.WritePackable({propertyAccess});";
    }

    private static string BuildManagedDeserializeStatement(IPropertySymbol property) {
        if(property.Type.SpecialType==SpecialType.System_String) {
            return $"value.{property.Name}=reader.ReadString();";
        }
        return $"reader.ReadPackable(ref value.{property.Name});";
    }

    private static string GetStorageName(IPropertySymbol property) {
        return property.Type.IsUnmanagedType ? "_u" : "_m";
    }

    private static void AppendIndented(StringBuilder builder,string value,string indent) {
        var lines = value.Replace("\r\n","\n").Split('\n');
        foreach(var line in lines) {
            if(line.Length==0) {
                builder.AppendLine();
                continue;
            }

            builder.Append(indent);
            builder.AppendLine(line);
        }
    }

    private sealed class QueryRangeScope {
        public QueryRangeScope(string currentParameterName,IReadOnlyList<string> visibleVariables,IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions) {
            this.CurrentParameterName=currentParameterName;
            this.VisibleVariables=visibleVariables;
            this.RangeExpressions=rangeExpressions;
        }

        public string CurrentParameterName { get; }

        public IReadOnlyList<string> VisibleVariables { get; }

        public IReadOnlyDictionary<string,ExpressionSyntax> RangeExpressions { get; }

        public static QueryRangeScope CreateIdentity(string rangeVariable) {
            return new QueryRangeScope(
                rangeVariable,
                new[] { rangeVariable },
                new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal) {
                    [rangeVariable]=SyntaxFactory.IdentifierName(rangeVariable)
                });
        }

        public QueryRangeScope CreateCarrierScope(string currentParameterName,params string[] additionalVariables) {
            var visibleVariables = new List<string>(this.VisibleVariables.Count+additionalVariables.Length);
            visibleVariables.AddRange(this.VisibleVariables);
            visibleVariables.AddRange(additionalVariables);

            var rangeExpressions = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal);
            foreach(var visibleVariable in visibleVariables) {
                rangeExpressions[visibleVariable]=SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(currentParameterName),
                    SyntaxFactory.IdentifierName(visibleVariable));
            }

            return new QueryRangeScope(currentParameterName,visibleVariables,rangeExpressions);
        }
    }

    private sealed class QueryRangeVariableRewriter:CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions;
        private readonly Dictionary<string,int> shadowedNames = new(StringComparer.Ordinal);

        public QueryRangeVariableRewriter(IReadOnlyDictionary<string,ExpressionSyntax> rangeExpressions) {
            this.rangeExpressions=rangeExpressions;
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            var parameterName = node.Parameter.Identifier.ValueText;
            this.AddShadowedName(parameterName);
            var result = base.VisitSimpleLambdaExpression(node);
            this.RemoveShadowedName(parameterName);
            return result;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            this.AddShadowedNames(node.ParameterList.Parameters);
            var result = base.VisitParenthesizedLambdaExpression(node);
            this.RemoveShadowedNames(node.ParameterList.Parameters);
            return result;
        }

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) {
            if(node.ParameterList is not null) {
                this.AddShadowedNames(node.ParameterList.Parameters);
            }
            var result = base.VisitAnonymousMethodExpression(node);
            if(node.ParameterList is not null) {
                this.RemoveShadowedNames(node.ParameterList.Parameters);
            }
            return result;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var identifier = node.Identifier.ValueText;
            if(this.shadowedNames.ContainsKey(identifier)||!this.rangeExpressions.TryGetValue(identifier,out var replacement)) {
                return base.VisitIdentifierName(node);
            }

            return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(node);
        }

        private void AddShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.AddShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void RemoveShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.RemoveShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void AddShadowedName(string name) {
            if(this.shadowedNames.TryGetValue(name,out var count)) {
                this.shadowedNames[name]=count+1;
            }
            else {
                this.shadowedNames[name]=1;
            }
        }

        private void RemoveShadowedName(string name) {
            if(!this.shadowedNames.TryGetValue(name,out var count)) {
                return;
            }
            if(count==1) {
                this.shadowedNames.Remove(name);
            }
            else {
                this.shadowedNames[name]=count-1;
            }
        }
    }

    private readonly struct ScalarTerminalInfo {
        public ScalarTerminalInfo(string methodName,string returnTypeName,string accumulatorTypeName,bool returnTypeIsNullable,bool terminalValueIsNullable,bool useCheckedAccumulator,bool castAccumulatorToDoubleBeforeAverageDivide) {
            MethodName=methodName;
            ReturnTypeName=returnTypeName;
            AccumulatorTypeName=accumulatorTypeName;
            ReturnTypeIsNullable=returnTypeIsNullable;
            TerminalValueIsNullable=terminalValueIsNullable;
            UseCheckedAccumulator=useCheckedAccumulator;
            CastAccumulatorToDoubleBeforeAverageDivide=castAccumulatorToDoubleBeforeAverageDivide;
        }

        public string MethodName { get; }
        public string ReturnTypeName { get; }
        public string AccumulatorTypeName { get; }
        public bool ReturnTypeIsNullable { get; }
        public bool TerminalValueIsNullable { get; }
        public bool UseCheckedAccumulator { get; }
        public bool CastAccumulatorToDoubleBeforeAverageDivide { get; }
    }

    private readonly struct ScalarFusionCallInfo {
        public ScalarFusionCallInfo(string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments,string[] typeArguments) {
            MethodName=methodName;
            Source=source;
            Arguments=arguments;
            TypeArguments=typeArguments;
        }

        public string MethodName { get; }
        public ExpressionSyntax Source { get; }
        public ArgumentSyntax[] Arguments { get; }
        public string[] TypeArguments { get; }
    }

    private sealed class ScalarFusionPipeline {
        public ScalarFusionPipeline(string sourceExpression,IReadOnlyList<ScalarFusionOperation> operations) {
            SourceExpression=sourceExpression;
            Operations=operations;
        }

        public string SourceExpression { get; }
        public IReadOnlyList<ScalarFusionOperation> Operations { get; }
    }

    private sealed class ScalarFusionOperation {
        private ScalarFusionOperation(string methodName,ArgumentSyntax[] arguments,string[] typeArguments,FusionLambda? lambda,FusionLambda? secondLambda) {
            MethodName=methodName;
            Arguments=arguments;
            TypeArguments=typeArguments;
            Lambda=lambda;
            SecondLambda=secondLambda;
        }

        public string MethodName { get; }
        public ArgumentSyntax[] Arguments { get; }
        public string[] TypeArguments { get; }
        public FusionLambda? Lambda { get; }
        public FusionLambda? SecondLambda { get; }

        public static ScalarFusionOperation Create(string methodName,ArgumentSyntax[] arguments,string[] typeArguments,FusionLambda? lambda,FusionLambda? secondLambda) {
            return new ScalarFusionOperation(methodName,arguments,typeArguments,lambda,secondLambda);
        }
    }

    private sealed class FusionLambda {
        public FusionLambda(string[] parameters,ExpressionSyntax body) {
            Parameters=parameters;
            Body=body;
        }

        public string[] Parameters { get; }
        public ExpressionSyntax Body { get; }
    }

    private readonly struct FusionLoopContext {
        public FusionLoopContext(string continueLabel,string stopFlag) {
            ContinueLabel=continueLabel;
            StopFlag=stopFlag;
        }

        public string ContinueLabel { get; }
        public string StopFlag { get; }
    }

    private sealed class LambdaParameterInlineRewriter:CSharpSyntaxRewriter {
        private readonly IReadOnlyDictionary<string,ExpressionSyntax> replacements;
        private readonly Dictionary<string,int> shadowedNames = new(StringComparer.Ordinal);

        public LambdaParameterInlineRewriter(IReadOnlyDictionary<string,ExpressionSyntax> replacements) {
            this.replacements=replacements;
        }

        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) {
            this.AddShadowedName(node.Parameter.Identifier.ValueText);
            var result = base.VisitSimpleLambdaExpression(node);
            this.RemoveShadowedName(node.Parameter.Identifier.ValueText);
            return result;
        }

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) {
            this.AddShadowedNames(node.ParameterList.Parameters);
            var result = base.VisitParenthesizedLambdaExpression(node);
            this.RemoveShadowedNames(node.ParameterList.Parameters);
            return result;
        }

        public override SyntaxNode? VisitArgument(ArgumentSyntax node) {
            if(node.Parent is TupleExpressionSyntax
                &&node.NameColon is null
                &&node.Expression is IdentifierNameSyntax identifierName
                &&!this.shadowedNames.ContainsKey(identifierName.Identifier.ValueText)
                &&this.replacements.ContainsKey(identifierName.Identifier.ValueText)) {
                var expression = (ExpressionSyntax)this.Visit(node.Expression);
                return node
                    .WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(identifierName.Identifier.ValueText)))
                    .WithExpression(expression);
            }
            return base.VisitArgument(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            var identifier = node.Identifier.ValueText;
            if(node.Parent is MemberAccessExpressionSyntax memberAccess&&memberAccess.Name==node) {
                return base.VisitIdentifierName(node);
            }
            if(this.shadowedNames.ContainsKey(identifier)||!this.replacements.TryGetValue(identifier,out var replacement)) {
                return base.VisitIdentifierName(node);
            }
            return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(node);
        }

        private void AddShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.AddShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void RemoveShadowedNames(SeparatedSyntaxList<ParameterSyntax> parameters) {
            foreach(var parameter in parameters) {
                this.RemoveShadowedName(parameter.Identifier.ValueText);
            }
        }

        private void AddShadowedName(string name) {
            if(this.shadowedNames.TryGetValue(name,out var count)) {
                this.shadowedNames[name]=count+1;
            }
            else {
                this.shadowedNames[name]=1;
            }
        }

        private void RemoveShadowedName(string name) {
            if(!this.shadowedNames.TryGetValue(name,out var count)) {
                return;
            }
            if(count==1) {
                this.shadowedNames.Remove(name);
            }
            else {
                this.shadowedNames[name]=count-1;
            }
        }
    }

    // 同一呼び出し位置に対する interceptor 二重生成を防ぐ comparer。
    private sealed class ContainerMethodInterceptTargetComparer:IEqualityComparer<ContainerMethodInterceptTarget> {
        public static ContainerMethodInterceptTargetComparer Instance { get; } = new();

        public bool Equals(ContainerMethodInterceptTarget? x,ContainerMethodInterceptTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return x.FilePath==y.FilePath&&x.Line==y.Line&&x.Character==y.Character;
        }

        public int GetHashCode(ContainerMethodInterceptTarget obj) {
            unchecked {
                var hash = 17;
                hash=(hash*31)+obj.FilePath.GetHashCode();
                hash=(hash*31)+obj.Line;
                hash=(hash*31)+obj.Character;
                return hash;
            }
        }
    }

    private sealed class WhereInterceptTargetComparer:IEqualityComparer<WhereInterceptTarget> {
        public static WhereInterceptTargetComparer Instance { get; } = new();

        public bool Equals(WhereInterceptTarget? x,WhereInterceptTarget? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null||y is null) {
                return false;
            }
            return x.FilePath==y.FilePath&&x.Line==y.Line&&x.Character==y.Character;
        }

        public int GetHashCode(WhereInterceptTarget obj) {
            unchecked {
                var hash = 17;
                hash=(hash*31)+obj.FilePath.GetHashCode();
                hash=(hash*31)+obj.Line;
                hash=(hash*31)+obj.Character;
                return hash;
            }
        }
    }

    // InterceptsLocation と generated interceptor の本文生成に必要な情報を集約した DTO。
    private sealed class WhereInterceptTarget {
        public WhereInterceptTarget(string identifier,string filePath,int line,int character,string interceptsLocationAttributeText,string elementTypeName,bool usesIndex,string parameterName,string? indexParameterName,string predicateText,bool usePredicateDelegate) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ElementTypeName=elementTypeName;
            this.UsesIndex=usesIndex;
            this.ParameterName=parameterName;
            this.IndexParameterName=indexParameterName;
            this.PredicateText=predicateText;
            this.UsePredicateDelegate=usePredicateDelegate;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ElementTypeName { get; }
        public bool UsesIndex { get; }
        public string ParameterName { get; }
        public string? IndexParameterName { get; }
        public string PredicateText { get; }
        public bool UsePredicateDelegate { get; }
        public string MethodName => $"OptimizedWhere_{this.Identifier}";
        public string EnumerableTypeName => $"OptimizedWhereEnumerable_{this.Identifier}";
    }

    private sealed class ContainerMethodInterceptTarget {
        public ContainerMethodInterceptTarget(string identifier,string filePath,int line,int character,string interceptsLocationAttributeText,string receiverTypeName,string returnTypeName,string typeParameterList,string typeParameterConstraintList,string parameterList,string body,bool isExpressionBody) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.ReceiverTypeName=receiverTypeName;
            this.ReturnTypeName=returnTypeName;
            this.TypeParameterList=typeParameterList;
            this.TypeParameterConstraintList=typeParameterConstraintList;
            this.ParameterList=parameterList;
            this.Body=body;
            this.IsExpressionBody=isExpressionBody;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
        public string InterceptsLocationAttributeText { get; }
        public string ReceiverTypeName { get; }
        public string ReturnTypeName { get; }
        public string TypeParameterList { get; }
        public string TypeParameterConstraintList { get; }
        public string ParameterList { get; }
        public string Body { get; }
        public bool IsExpressionBody { get; }
        public string MethodName => $"OptimizedContainerMethod_{this.Identifier}";
    }

    // DatabaseGenerator マーカー配下で見つけた型宣言の最小情報。
    private sealed class TypeTarget {
        public TypeTarget(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }

        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }

}
