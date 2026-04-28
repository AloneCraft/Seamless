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
public sealed class SeamlessIncrementalGenerator:IIncrementalGenerator {
    private static readonly SymbolDisplayFormat FullyQualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
    );

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

        context.AddSource("Seamless.SourceGenerator.Template.g.cs",SourceText.From(builder.ToString(),Encoding.UTF8));
    }

    private static WhereInterceptTarget? CreateWhereInterceptTarget(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
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
        if(context.SemanticModel.GetTypeInfo(memberAccess.Expression,cancellationToken).Type is not INamedTypeSymbol receiverType) {
            return null;
        }
        if(receiverType.Name!="List"
            ||receiverType.ContainingNamespace.ToDisplayString()!="System.Collections.Generic"
            ||receiverType.TypeArguments.Length!=1
            ||receiverType.TypeArguments[0].SpecialType!=SpecialType.System_Int32) {
            return null;
        }
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return null;
        }
        if(invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax lambda) {
            return null;
        }
        if(!TryGetLambdaInfo(context.SemanticModel,lambda,cancellationToken,out var parameterName,out var predicateBody,out var hasCaptures)) {
            return null;
        }
        var span = memberAccess.Name.GetLocation().GetLineSpan();
        var line = span.StartLinePosition.Line+1;
        var character = span.StartLinePosition.Character+1;
        var id = SanitizeIdentifier($"{System.IO.Path.GetFileNameWithoutExtension(span.Path)}_{line}_{character}");
        return new WhereInterceptTarget(
            id,
            span.Path,
            line,
            character,
            parameterName,
            predicateBody,
            hasCaptures);
    }

    private static bool TryGetLambdaInfo(SemanticModel semanticModel,LambdaExpressionSyntax lambda,System.Threading.CancellationToken cancellationToken,out string parameterName,out string predicateBody,out bool hasCaptures) {
        parameterName=string.Empty;
        predicateBody=string.Empty;
        hasCaptures=false;

        if(lambda switch {
            SimpleLambdaExpressionSyntax => false,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.Count!=1,
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
        if(lambda.Body is not ExpressionSyntax expressionBody) {
            return false;
        }

        predicateBody=expressionBody.ToString();
        var lambdaParameters = new HashSet<string>(StringComparer.Ordinal) { parameterName };
        foreach(var identifier in expressionBody.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            var symbol = semanticModel.GetSymbolInfo(identifier,cancellationToken).Symbol;
            if(symbol is null) {
                continue;
            }
            if(lambdaParameters.Contains(identifier.Identifier.ValueText)) {
                continue;
            }
            if(symbol is ILocalSymbol||symbol is IParameterSymbol) {
                hasCaptures=true;
                break;
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
        if(!isMarker&&!isPartial) {
            return null;
        }
        return new TypeTarget(symbol,isPartial);
    }

    private static void AppendInterceptorSource(StringBuilder builder,ImmutableArray<WhereInterceptTarget> whereTargets) {
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace System.Runtime.CompilerServices {");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method,AllowMultiple=true)]");
        builder.AppendLine("    file sealed class InterceptsLocationAttribute:global::System.Attribute {");
        builder.AppendLine("        public InterceptsLocationAttribute(string filePath,int line,int character) {");
        builder.AppendLine("            this.FilePath=filePath;");
        builder.AppendLine("            this.Line=line;");
        builder.AppendLine("            this.Character=character;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        public string FilePath { get; }");
        builder.AppendLine("        public int Line { get; }");
        builder.AppendLine("        public int Character { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("namespace Seamless.SourceGenerator.Generated {");
        builder.AppendLine("    internal static class ListWhereInterceptors {");

        foreach(var target in whereTargets.Distinct(WhereInterceptTargetComparer.Instance)) {
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute({ToLiteral(target.FilePath)},{target.Line},{target.Character})]");
            builder.AppendLine($"        public static global::System.Collections.Generic.IEnumerable<int> {target.MethodName}(this global::System.Collections.Generic.IEnumerable<int> source,global::System.Func<int,bool> predicate) {{");
            builder.AppendLine($"            global::System.Console.WriteLine(\"Seamless interceptor: {target.MethodName} ({target.Line}:{target.Character})\");");
            builder.AppendLine($"            return new {target.EnumerableTypeName}((global::System.Collections.Generic.List<int>)source,predicate);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public readonly struct {target.EnumerableTypeName}:global::System.Collections.Generic.IEnumerable<int> {{");
            builder.AppendLine("            private readonly global::System.Collections.Generic.List<int> source;");
            builder.AppendLine("            private readonly global::System.Func<int,bool> predicate;");
            builder.AppendLine();
            builder.AppendLine($"            public {target.EnumerableTypeName}(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {{");
            builder.AppendLine("                this.source=source ?? throw new global::System.ArgumentNullException(nameof(source));");
            builder.AppendLine("                this.predicate=predicate ?? throw new global::System.ArgumentNullException(nameof(predicate));");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine("            public Enumerator GetEnumerator() => new(this.source,this.predicate);");
            builder.AppendLine();
            builder.AppendLine("            global::System.Collections.Generic.IEnumerator<int> global::System.Collections.Generic.IEnumerable<int>.GetEnumerator() {");
            builder.AppendLine("                var enumerator = this.GetEnumerator();");
            builder.AppendLine("                while(enumerator.MoveNext()) {");
            builder.AppendLine("                    yield return enumerator.Current;");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<int>)this).GetEnumerator();");
            builder.AppendLine();
            builder.AppendLine("            public struct Enumerator {");
            builder.AppendLine("                private readonly global::System.Collections.Generic.List<int> source;");
            builder.AppendLine("                private readonly global::System.Func<int,bool> predicate;");
            builder.AppendLine("                private int index;");
            builder.AppendLine();
            builder.AppendLine("                public Enumerator(global::System.Collections.Generic.List<int> source,global::System.Func<int,bool> predicate) {");
            builder.AppendLine("                    this.source=source;");
            builder.AppendLine("                    this.predicate=predicate;");
            builder.AppendLine("                    this.index=-1;");
            builder.AppendLine("                    this.Current=default;");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                public int Current { get; private set; }");
            builder.AppendLine();
            builder.AppendLine("                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine("                public bool MoveNext() {");
            builder.AppendLine("                    while(true) {");
            builder.AppendLine("                        var next = this.index+1;");
            builder.AppendLine("                        if(next>=this.source.Count) {");
            builder.AppendLine("                            return false;");
            builder.AppendLine("                        }");
            builder.AppendLine("                        this.index=next;");
            if(target.HasCaptures) {
                builder.AppendLine("                        var candidate = this.source[next];");
                builder.AppendLine("                        if(!this.predicate(candidate)) {");
                builder.AppendLine("                            continue;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        this.Current=candidate;");
            }
            else {
                builder.AppendLine($"                        var {target.ParameterName} = this.source[next];");
                builder.AppendLine($"                        if(!({target.PredicateBody})) {{");
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
                .Where(target => target.IsPartial
                    &&target.Symbol.TypeKind==TypeKind.Class
                    &&string.Equals(target.Symbol.ContainingNamespace.ToDisplayString(),rootNamespace,StringComparison.Ordinal)
                    &&target.Symbol.Name!="DatabaseGenerator")
                .Select(target => target.Symbol)
                .GroupBy(static symbol => symbol.ToDisplayString(FullyQualifiedFormat),StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static symbol => symbol.Name,StringComparer.Ordinal)
                .ToArray();

            var rootNsIndent = "";
            var innerIndent = "    ";
            var hasRootNs = !string.IsNullOrEmpty(rootNamespace);
            if(hasRootNs) {
                builder.AppendLine($"namespace {rootNamespace} {{");
            }

            if(keys.Length>0) {
                builder.AppendLine($"{rootNsIndent}namespace Keys {{");
                foreach(var key in keys) {
                    AppendRecordSourceBody(builder,key,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            if(types.Length>0) {
                builder.AppendLine($"{rootNsIndent}namespace Types {{");
                foreach(var type in types) {
                    AppendRecordSourceBody(builder,type,innerIndent);
                }
                builder.AppendLine($"{rootNsIndent}}}");
                builder.AppendLine();
            }

            foreach(var container in containers) {
                builder.AppendLine($"{rootNsIndent}partial class {container.Name} {{");
                builder.AppendLine($"{innerIndent}public static global::System.Type[] __GeneratedKeyTypes {{ get; }} = new global::System.Type[] {{ {string.Join(", ",keys.Select(static symbol => $"typeof({symbol.ToDisplayString(FullyQualifiedFormat)})"))} }};");
                builder.AppendLine($"{innerIndent}public static global::System.Type[] __GeneratedTypes {{ get; }} = new global::System.Type[] {{ {string.Join(", ",types.Select(static symbol => $"typeof({symbol.ToDisplayString(FullyQualifiedFormat)})"))} }};");
                builder.AppendLine($"{rootNsIndent}}}");

                var translatedMembers = BuildTranslatedMemberLines(container);
                if(translatedMembers.Count>0) {
                    builder.AppendLine();
                    builder.AppendLine($"{rootNsIndent}public static class {container.Name}GeneratedExtensions {{");
                    foreach(var memberLine in translatedMembers) {
                        builder.AppendLine($"{innerIndent}{memberLine}");
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

    private static List<string> BuildTranslatedMemberLines(INamedTypeSymbol container) {
        var members = new List<string>();
        foreach(var member in container.GetMembers().Where(static member => member.IsVirtual)) {
            if(member is IPropertySymbol property) {
                var translatedBody = TryTranslateProperty(property);
                if(translatedBody is null) {
                    continue;
                }
                members.Add($"public static {property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name}AsExtension(this {container.ToDisplayString(FullyQualifiedFormat)} self) => {translatedBody};");
                continue;
            }
            if(member is IMethodSymbol method&&method.MethodKind==MethodKind.Ordinary) {
                var translatedBody = TryTranslateMethod(method);
                if(translatedBody is null) {
                    continue;
                }
                var parameterList = string.Join(", ",method.Parameters.Select(static parameter => $"{parameter.Type.ToDisplayString(FullyQualifiedFormat)} {parameter.Name}"));
                var typeParameterList = BuildTypeParameterList(method.TypeParameters);
                var typeParameterConstraints = BuildTypeParameterConstraints(method.TypeParameters);
                var signature = string.IsNullOrEmpty(parameterList)
                    ? $"this {container.ToDisplayString(FullyQualifiedFormat)} self"
                    : $"this {container.ToDisplayString(FullyQualifiedFormat)} self, {parameterList}";
                members.Add($"public static {method.ReturnType.ToDisplayString(FullyQualifiedFormat)} {method.Name}AsExtension{typeParameterList}({signature}){typeParameterConstraints} => {translatedBody};");
            }
        }
        return members;
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

    private static string? TryTranslateMethod(IMethodSymbol method) {
        foreach(var syntaxReference in method.DeclaringSyntaxReferences) {
            if(syntaxReference.GetSyntax() is not MethodDeclarationSyntax syntax) {
                continue;
            }
            if(syntax.ExpressionBody?.Expression is QueryExpressionSyntax expressionQuery) {
                return TryTranslateQueryExpression(expressionQuery);
            }
            if(syntax.ExpressionBody?.Expression is InvocationExpressionSyntax expressionInvocation) {
                return TryTranslateInvocationExpression(expressionInvocation);
            }
            if(syntax.Body is null||syntax.Body.Statements.Count!=1) {
                continue;
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax returnStatement&&returnStatement.Expression is QueryExpressionSyntax blockQuery) {
                return TryTranslateQueryExpression(blockQuery);
            }
            if(syntax.Body.Statements[0] is ReturnStatementSyntax invocationReturn&&invocationReturn.Expression is InvocationExpressionSyntax blockInvocation) {
                return TryTranslateInvocationExpression(blockInvocation);
            }
        }
        return null;
    }

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

    private static string? TryTranslateInvocationExpression(InvocationExpressionSyntax invocation) {
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return null;
        }
        var methodName = memberAccess.Name.Identifier.ValueText;
        if(methodName!="Where"&&methodName!="Select"&&methodName!="SelectMany") {
            return null;
        }
        var receiver = memberAccess.Expression is InvocationExpressionSyntax receiverInvocation
            ? TryTranslateInvocationExpression(receiverInvocation)??NormalizeExpression(receiverInvocation)
            : NormalizeExpression(memberAccess.Expression);
        var arguments = invocation.ArgumentList.Arguments
            .Select(static argument => NormalizeExpression(argument.Expression))
            .ToArray();
        return arguments.Length==0
            ? $"global::Seamless.Linq.Enumerable.{methodName}({receiver})"
            : $"global::Seamless.Linq.Enumerable.{methodName}({receiver},{string.Join(",",arguments)})";
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
        return RewriteThisToSelf(expression).ToString();
    }

    private static ExpressionSyntax RewriteThisToSelf(ExpressionSyntax expression) {
        return (ExpressionSyntax)new SelfReceiverRewriter().Visit(expression);
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

    private static void AppendUnmanagedRecordSource(StringBuilder builder,ImmutableArray<IPropertySymbol> unmanagedProperties) {
        if(unmanagedProperties.Length>0) {
            builder.AppendLine($"    private readonly record struct _U({string.Join(", ",unmanagedProperties.Select(static property => $"{property.Type.ToDisplayString(FullyQualifiedFormat)} {property.Name}"))});");
            return;
        }

        builder.AppendLine("    private struct _U {");
        builder.AppendLine("    }");
    }

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

    private sealed class WhereInterceptTarget {
        public WhereInterceptTarget(string identifier,string filePath,int line,int character,string parameterName,string predicateBody,bool hasCaptures) {
            this.Identifier=identifier;
            this.FilePath=filePath;
            this.Line=line;
            this.Character=character;
            this.ParameterName=parameterName;
            this.PredicateBody=predicateBody;
            this.HasCaptures=hasCaptures;
        }

        public string Identifier { get; }
        public string FilePath { get; }
        public int Line { get; }
        public int Character { get; }
        public string ParameterName { get; }
        public string PredicateBody { get; }
        public bool HasCaptures { get; }
        public string MethodName => $"OptimizedWhere_{this.Identifier}";
        public string EnumerableTypeName => $"OptimizedWhereEnumerable_{this.Identifier}";
    }

    private sealed class TypeTarget {
        public TypeTarget(INamedTypeSymbol symbol,bool isPartial) {
            this.Symbol=symbol;
            this.IsPartial=isPartial;
        }

        public INamedTypeSymbol Symbol { get; }
        public bool IsPartial { get; }
    }

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

    private sealed class SelfReceiverRewriter:CSharpSyntaxRewriter {
        public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node) {
            return SyntaxFactory.IdentifierName("self").WithTriviaFrom(node);
        }
    }
}
