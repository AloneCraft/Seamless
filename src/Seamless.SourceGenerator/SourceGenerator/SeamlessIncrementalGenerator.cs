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

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(static (node,_) => node is TypeDeclarationSyntax,static (generatorContext,_) => CreateTypeTarget(generatorContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var combined = context.CompilationProvider.Combine(whereTargets.Collect()).Combine(typeTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => Execute(
            productionContext,
            pair.Left.Right,
            pair.Right));
    }

    // 各サブ生成器の結果を 1 つの Template.g.cs に束ねる最終出力点。
    private static void Execute(SourceProductionContext context,ImmutableArray<WhereInterceptTarget> whereTargets,ImmutableArray<TypeTarget> typeTargets) {
        var builder = new StringBuilder();
        var generatedAny = false;

        if(!whereTargets.IsDefaultOrEmpty) {
            AppendInterceptorSource(builder,whereTargets);
            generatedAny=true;
        }

        if(!typeTargets.IsDefaultOrEmpty) {
            var databaseSource = BuildDatabaseSource(typeTargets);
            if(databaseSource.Length>0) {
                builder.AppendLine();
                builder.Append(databaseSource);
                generatedAny=true;
            }
        }

        if(!generatedAny) {
            return;
        }

        context.AddSource("Seamless.SourceGenerator.Template.g.cs",SourceText.From(RemoveBlankLines(builder.ToString()),Encoding.UTF8));
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

    // Where interceptor 系の generated source 一式を出力する。
    private static void AppendInterceptorSource(StringBuilder builder,ImmutableArray<WhereInterceptTarget> whereTargets) {
        builder.AppendLine("#nullable enable");
        builder.AppendLine("// Generated: Roslyn interceptor compatibility attribute used by the Where optimization path.");
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
        builder.AppendLine("// Generated: query-syntax helper extensions so List<T>.Where can stay on the optimized path.");
        AppendListWhereQueryHelperSource(builder);
        builder.AppendLine();
        builder.AppendLine("// Generated: reusable enumerable implementations and interceptor entry points.");
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        AppendReusableListWhereEnumerableSource(builder);
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
        builder.AppendLine("}");
    }

    private static void AppendListWhereQueryHelperSource(StringBuilder builder) {
        builder.AppendLine("namespace System.Linq {");
        builder.AppendLine("    // Generated: query syntax binds to these helpers before it falls back to Enumerable.Where.");
        builder.AppendLine("    internal static class ListWhereQueryExtensions {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static global::Seamless.SourceGenerator.Generated.OptimizedListWhereEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) => new(source,predicate);");
        builder.AppendLine();
        builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine("        public static global::Seamless.SourceGenerator.Generated.OptimizedListWhereIndexedEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) => new(source,predicate);");
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

    // DatabaseGenerator を起点にして schema struct と optimized container を生成する。
    // 利用されていなかった container metadata 配列は出さない。
    private static string BuildDatabaseSource(ImmutableArray<TypeTarget> typeTargets) {
        var builder = new StringBuilder();
        var rootNamespaces = typeTargets
            .Where(static target => target.Symbol.Name=="DatabaseGenerator")
            .Select(static target => target.Symbol.ContainingNamespace.ToDisplayString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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
            var containers = typeTargets
                .Where(target => target.Symbol.TypeKind==TypeKind.Class
                    &&string.Equals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,StringComparison.Ordinal)
                    &&target.Symbol.Name!="DatabaseGenerator")
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

            foreach(var container in containers) {
                var optimizedMembers = BuildOptimizedOverrideMemberLines(container);
                if(optimizedMembers.Count>0) {
                    var forwardingConstructors = BuildForwardingConstructorLines(container,$"Optimized{container.Name}");
                    builder.AppendLine();
                    builder.AppendLine($"{rootNsIndent}// Generated: optimized subclass that overrides translatable members with Seamless LINQ calls.");
                    builder.AppendLine($"{rootNsIndent}{GetGeneratedTypeAccessibilityText(container)} class Optimized{container.Name}:{container.ToDisplayString(FullyQualifiedFormat)} {{");
                    foreach(var constructorLine in forwardingConstructors) {
                        AppendIndented(builder,constructorLine,innerIndent);
                    }
                    if(forwardingConstructors.Count>0) {
                        builder.AppendLine();
                    }
                    foreach(var memberLine in optimizedMembers) {
                        AppendIndented(builder,memberLine,innerIndent);
                    }
                    builder.AppendLine($"{rootNsIndent}}}");
                }
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

    // translatable な virtual member を抽出して、Optimized{Container} の override 宣言へ変換する。
    // ここは「どの member を最適化対象にするか」のゲートであり、translation 自体は TryTranslate* 系に委譲する。
    private static List<string> BuildOptimizedOverrideMemberLines(INamedTypeSymbol container) {
        var members = new List<string>();
        foreach(var member in container.GetMembers().Where(static member => member.IsVirtual)) {
            if(member is IPropertySymbol property) {
                var translatedBody = TryTranslateProperty(property);
                if(translatedBody is null) {
                    continue;
                }
                members.Add($"{GetMemberAccessibilityText(property.DeclaredAccessibility)} override {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name} => {translatedBody};");
                continue;
            }
            if(member is IMethodSymbol method&&method.MethodKind==MethodKind.Ordinary) {
                var translatedBody = TryTranslateMethod(method);
                if(translatedBody is null) {
                    continue;
                }
                var parameterList = BuildParameterDeclarationList(method.Parameters);
                var typeParameterList = BuildTypeParameterList(method.TypeParameters);
                members.Add(translatedBody.Value.IsExpressionBody
                    ? $"{GetMemberAccessibilityText(method.DeclaredAccessibility)} override {method.ReturnType.ToDisplayString(FullyQualifiedFormat)} {method.Name}{typeParameterList}({parameterList}) => {translatedBody.Value.Body};"
                    : $"{GetMemberAccessibilityText(method.DeclaredAccessibility)} override {method.ReturnType.ToDisplayString(FullyQualifiedFormat)} {method.Name}{typeParameterList}({parameterList}) {translatedBody.Value.Body}");
            }
        }
        return members;
    }

    // base container が引数付き constructor しか持たないケースでも、generated subclass を new できるようにする。
    private static List<string> BuildForwardingConstructorLines(INamedTypeSymbol container,string optimizedTypeName) {
        var constructors = new List<string>();
        foreach(var constructor in container.InstanceConstructors.Where(static constructor => !constructor.IsImplicitlyDeclared&&constructor.DeclaredAccessibility!=Accessibility.Private)) {
            var parameterList = BuildParameterDeclarationList(constructor.Parameters);
            var argumentList = BuildArgumentExpressionList(constructor.Parameters);
            constructors.Add($"{GetMemberAccessibilityText(constructor.DeclaredAccessibility)} {optimizedTypeName}({parameterList}) : base({argumentList}) {{ }}");
        }
        return constructors;
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

    private static string BuildTypeParameterConstraints(ImmutableArray<ITypeParameterSymbol> typeParameters) {
        var clauses = new List<string>();
        foreach(var typeParameter in typeParameters) {
            var constraints = new List<string>();
            if(typeParameter.HasUnmanagedTypeConstraint) {
                constraints.Add("unmanaged");
            }
            else if(typeParameter.HasValueTypeConstraint) {
                constraints.Add("struct");
            }
            else if(typeParameter.HasReferenceTypeConstraint) {
                constraints.Add(typeParameter.ReferenceTypeConstraintNullableAnnotation==NullableAnnotation.Annotated ? "class?" : "class");
            }
            else if(typeParameter.HasNotNullConstraint) {
                constraints.Add("notnull");
            }
            constraints.AddRange(typeParameter.ConstraintTypes.Select(static type => type.ToDisplayString(FullyQualifiedFormat)));
            if(typeParameter.HasConstructorConstraint) {
                constraints.Add("new()");
            }
            if(constraints.Count==0) {
                continue;
            }
            clauses.Add($" where {typeParameter.Name}:{string.Join(",",constraints)}");
        }
        return string.Concat(clauses);
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

    private static string BuildArgumentExpressionList(ImmutableArray<IParameterSymbol> parameters) {
        return string.Join(", ",parameters.Select(BuildArgumentExpression));
    }

    private static string BuildArgumentExpression(IParameterSymbol parameter) {
        return parameter.RefKind switch {
            RefKind.Ref => $"ref {parameter.Name}",
            RefKind.Out => $"out {parameter.Name}",
            RefKind.In => $"in {parameter.Name}",
            _ => parameter.Name,
        };
    }

    private static string GetGeneratedTypeAccessibilityText(INamedTypeSymbol symbol) {
        return symbol.DeclaredAccessibility==Accessibility.Public ? "public" : "internal";
    }

    private static string GetMemberAccessibilityText(Accessibility accessibility) {
        return accessibility switch {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };
    }

    // property getter の query syntax を Seamless LINQ へ落とせる場合だけ override を生成する。
    private static string? TryTranslateProperty(IPropertySymbol property) {
        foreach(var syntaxReference in property.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not PropertyDeclarationSyntax syntax) {
                continue;
            }
            if(syntax.ExpressionBody?.Expression is QueryExpressionSyntax expressionQuery) {
                return TryTranslateQueryExpression(expressionQuery);
            }
            if(syntax.AccessorList is null) {
                continue;
            }
            foreach(var accessor in syntax.AccessorList.Accessors) {
                if(accessor.Kind()!=SyntaxKind.GetAccessorDeclaration||accessor.Body is null) {
                    continue;
                }
                if(accessor.Body.Statements.Count==1&&accessor.Body.Statements[0] is ReturnStatementSyntax returnStatement&&returnStatement.Expression is QueryExpressionSyntax blockQuery) {
                    return TryTranslateQueryExpression(blockQuery);
                }
            }
        }
        return null;
    }

    // method translation の入口。
    // invocation 系は OptimizeVisitor で Where pushdown を掛けてから文字列化する。
    private static TranslatedMethodBody? TryTranslateMethod(IMethodSymbol method) {
        foreach(var syntaxReference in method.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not MethodDeclarationSyntax syntax) {
                continue;
            }
            if(syntax.ExpressionBody?.Expression is QueryExpressionSyntax expressionQuery) {
                var translatedQuery = TryTranslateQueryExpression(expressionQuery);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.ExpressionBody?.Expression is InvocationExpressionSyntax expressionInvocation) {
                return TryTranslateInvocationBody(method,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(expressionInvocation));
            }
            if(syntax.Body is null||syntax.Body.Statements.Count!=1) {
                continue;
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax returnStatement&&returnStatement.Expression is QueryExpressionSyntax blockQuery) {
                var translatedQuery = TryTranslateQueryExpression(blockQuery);
                return translatedQuery is null ? null : new TranslatedMethodBody(translatedQuery,true);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax invocationReturn&&invocationReturn.Expression is InvocationExpressionSyntax blockInvocation) {
                return TryTranslateInvocationBody(method,(InvocationExpressionSyntax)OptimizeVisitor.Optimize(blockInvocation));
            }
        }
        return null;
    }

    // invocation のうち Aggregate だけは block body に展開し、他は expression body のまま吐く。
    private static TranslatedMethodBody? TryTranslateInvocationBody(IMethodSymbol method,InvocationExpressionSyntax invocation) {
        var aggregateBody = TryTranslateAggregateBody(method,invocation);
        if(aggregateBody is not null) {
            return new TranslatedMethodBody(aggregateBody,false);
        }
        var expressionBody = TryTranslateInvocationExpression(invocation);
        return expressionBody is null ? null : new TranslatedMethodBody(expressionBody,true);
    }

    // Aggregate は call-through にすると空列時例外や列挙手順の表現が曖昧になるため、明示的な列挙器ループへ展開する。
    private static string? TryTranslateAggregateBody(IMethodSymbol method,InvocationExpressionSyntax invocation) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        if(memberAccess.Name.Identifier.ValueText!="Aggregate") {
            return null;
        }
        var receiver = memberAccess.Expression is InvocationExpressionSyntax receiverInvocation
            ? TryTranslateInvocationExpression(receiverInvocation)??NormalizeExpression(receiverInvocation)
            : NormalizeExpression(memberAccess.Expression);
        var arguments = invocation.ArgumentList.Arguments
            .Select(static argument => argument.Expression is InvocationExpressionSyntax invocationArgument
                ? TryTranslateInvocationExpression(invocationArgument)??NormalizeExpression(invocationArgument)
                : NormalizeExpression(argument.Expression))
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
            "    using var __aggregateEnumerator = __aggregateSource.GetEnumerator();",
            "    if(!__aggregateEnumerator.MoveNext()) {",
            "        throw new global::System.InvalidOperationException(\"Sequence contains no elements.\");",
            "    }",
            "    var __aggregateResult = __aggregateEnumerator.Current;",
            "    while(__aggregateEnumerator.MoveNext()) {",
            "        __aggregateResult = __aggregateFunc(__aggregateResult,__aggregateEnumerator.Current);",
            "    }",
            "    return __aggregateResult;",
            "}"
        });
    }

    // 単純な query syntax を global::Seamless.Linq.Enumerable の method chain に変換する。
    private static string? TryTranslateQueryExpression(QueryExpressionSyntax query) {
        if(query.Body.Continuation is not null) {
            return null;
        }
        if(query.Body.Clauses.Count==1
            &&query.Body.Clauses[0] is FromClauseSyntax selectManyClause
            &&query.Body.SelectOrGroup is SelectClauseSyntax selectManySelectClause) {
            var outerRangeVariable = query.FromClause.Identifier.ValueText;
            var innerRangeVariable = selectManyClause.Identifier.ValueText;
            return $"global::Seamless.Linq.Enumerable.SelectMany({NormalizeExpression(query.FromClause.Expression)},{outerRangeVariable} => {NormalizeExpression(selectManyClause.Expression)},({outerRangeVariable},{innerRangeVariable}) => {NormalizeExpression(selectManySelectClause.Expression)})";
        }
        var rangeVariable = query.FromClause.Identifier.ValueText;
        var current = NormalizeExpression(query.FromClause.Expression);
        foreach(var clause in query.Body.Clauses) {
            if(clause is WhereClauseSyntax whereClause) {
                current=$"global::Seamless.Linq.Enumerable.Where({current},{rangeVariable} => {NormalizeExpression(whereClause.Condition)})";
                continue;
            }
            return null;
        }
        if(query.Body.SelectOrGroup is not SelectClauseSyntax selectClause) {
            return null;
        }
        var selection = NormalizeExpression(selectClause.Expression);
        if(!string.Equals(selection,rangeVariable,StringComparison.Ordinal)) {
            current=$"global::Seamless.Linq.Enumerable.Select({current},{rangeVariable} => {selection})";
        }
        return current;
    }

    // method chain を再帰的に辿って Seamless LINQ 呼び出しへそろえる。
    // receiver だけでなく argument 側の nested invocation も変換するのが重要。
    private static string? TryTranslateInvocationExpression(InvocationExpressionSyntax invocation) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var methodName = memberAccess.Name.Identifier.ValueText;
        if(methodName!="Where"
            &&methodName!="Select"
            &&methodName!="SelectMany"
            &&methodName!="Aggregate"
            &&methodName!="Join"
            &&methodName!="LeftJoin"
            &&methodName!="Except"
            &&methodName!="ExceptBy"
            &&methodName!="Intersect"
            &&methodName!="IntersectBy"
            &&methodName!="Union"
            &&methodName!="Concat"
            &&methodName!="Distinct"
            &&methodName!="DistinctBy"
            &&methodName!="Order"
            &&methodName!="OrderBy"
            &&methodName!="OrderDescending"
            &&methodName!="OrderByDescending"
            &&methodName!="Reverse") {
            return null;
        }
        var receiver = memberAccess.Expression is InvocationExpressionSyntax receiverInvocation
            ? TryTranslateInvocationExpression(receiverInvocation)??NormalizeExpression(receiverInvocation)
            : NormalizeExpression(memberAccess.Expression);
        var arguments = invocation.ArgumentList.Arguments
            .Select(static argument => argument.Expression is InvocationExpressionSyntax invocationArgument
                ? TryTranslateInvocationExpression(invocationArgument)??NormalizeExpression(invocationArgument)
                : NormalizeExpression(argument.Expression))
            .ToArray();
        if(IsStaticEnumerableReceiver(receiver)) {
            return arguments.Length==0
                ? $"global::Seamless.Linq.Enumerable.{methodName}()"
                : $"global::Seamless.Linq.Enumerable.{methodName}({string.Join(",",arguments)})";
        }
        return arguments.Length==0
            ? $"global::Seamless.Linq.Enumerable.{methodName}({receiver})"
            : $"global::Seamless.Linq.Enumerable.{methodName}({receiver},{string.Join(",",arguments)})";
    }

    private static bool IsStaticEnumerableReceiver(string receiver) {
        return string.Equals(receiver,"Enumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SystemEnumerable",StringComparison.Ordinal)
            ||string.Equals(receiver,"SeamlessEnumerable",StringComparison.Ordinal)
            ||receiver.EndsWith(".Enumerable",StringComparison.Ordinal);
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

    // 元ソースの namespace 宣言形状を保って generated body を差し込む helper。
    private static void AppendInDeclaredNamespace(StringBuilder builder,INamedTypeSymbol symbol,Action<StringBuilder> appendBody) {
        var namespaceShape = TryCreateNamespaceShape(symbol);
        if(namespaceShape is null) {
            appendBody(builder);
            return;
        }

        foreach(var line in namespaceShape.OpenLines) {
            builder.AppendLine(line);
        }

        var bodyBuilder = new StringBuilder();
        appendBody(bodyBuilder);
        AppendIndented(builder,bodyBuilder.ToString(),namespaceShape.IndentLevel);

        foreach(var line in namespaceShape.CloseLines) {
            builder.AppendLine(line);
        }
    }

    // file-scoped namespace と block namespace の両方を復元できる形で open / close 行を保存する。
    private static NamespaceShape? TryCreateNamespaceShape(INamedTypeSymbol symbol) {
        foreach(var syntaxReference in symbol.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration) {
                continue;
            }

            var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().ToArray();
            if(namespaces.Length==0) {
                return NamespaceShape.Empty;
            }

            var openLines = new List<string>();
            var closeLines = new List<string>();
            var indentLevel = 0;

            foreach(var namespaceSyntax in namespaces) {
                if(namespaceSyntax is FileScopedNamespaceDeclarationSyntax fileScopedNamespace) {
                    openLines.Add($"namespace {fileScopedNamespace.Name} ;".Replace(" ;",";"));
                    indentLevel=0;
                    continue;
                }

                openLines.Add(new string(' ',indentLevel*4)+$"namespace {namespaceSyntax.Name} {{");
                indentLevel++;
                closeLines.Insert(0,new string(' ',(indentLevel-1)*4)+"}");
            }

            return new NamespaceShape(openLines,closeLines,indentLevel);
        }

        return null;
    }

    private static void AppendIndented(StringBuilder builder,string value,int indentLevel) {
        AppendIndented(builder,value,new string(' ',indentLevel*4));
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

    // 同一呼び出し位置に対する interceptor 二重生成を防ぐ comparer。
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

    // DatabaseGenerator マーカー配下で見つけた型宣言の最小情報。
    private sealed class TypeTarget {
        public TypeTarget(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }

        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }

    // namespace 復元のための open / close 行セット。
    private sealed class NamespaceShape {
        public static NamespaceShape Empty { get; } = new([],[],0);

        public NamespaceShape(IReadOnlyList<string> openLines,IReadOnlyList<string> closeLines,int indentLevel) {
            this.OpenLines=openLines;
            this.CloseLines=closeLines;
            this.IndentLevel=indentLevel;
        }

        public IReadOnlyList<string> OpenLines { get; }
        public IReadOnlyList<string> CloseLines { get; }
        public int IndentLevel { get; }
    }

}
