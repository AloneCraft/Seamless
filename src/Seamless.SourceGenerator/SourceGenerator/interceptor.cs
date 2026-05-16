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
        var interceptTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is InvocationExpressionSyntax,
                static (syntaxContext,cancellationToken) => Linq傍受対象生成(syntaxContext,cancellationToken))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var typeTargets = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,_) => node is TypeDeclarationSyntax,
                static (syntaxContext,_) => 型対象生成(syntaxContext))
            .Where(static target => target is not null)
            .Select(static (target,_) => target!);

        var combined = interceptTargets.Collect().Combine(typeTargets.Collect());
        context.RegisterSourceOutput(combined,static (productionContext,pair) => {
            var interceptTargets = pair.Left;
            var typeTargets = pair.Right;

            if(!typeTargets.IsDefaultOrEmpty) {
                var databaseSource = データベースソース構築(typeTargets);
                if(databaseSource.Length>0) {
                    productionContext.AddSource("Seamless.SourceGenerator.Schema.g.cs",SourceText.From(空行除去(databaseSource),Encoding.UTF8));
                }
            }

            if(interceptTargets.IsDefaultOrEmpty) {
                return;
            }

            var source = インターセプターソース構築(interceptTargets.Distinct(LinqInterceptTargetComparer.Instance).ToImmutableArray());
            if(source.Length>0) {
                productionContext.AddSource("Seamless.SourceGenerator.LinqInterceptors.g.cs",SourceText.From(source,Encoding.UTF8));
            }
        });
    }

    private static Linq傍受対象型? Linq傍受対象生成(GeneratorSyntaxContext context,System.Threading.CancellationToken cancellationToken) {
        if(context.Node is not InvocationExpressionSyntax invocation) {
            return null;
        }

        if(context.SemanticModel.GetSymbolInfo(invocation,cancellationToken).Symbol is not IMethodSymbol method) {
            return null;
        }

        var interceptableLocation = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetInterceptableLocation(context.SemanticModel,invocation,cancellationToken);
        if(interceptableLocation is null) {
            return null;
        }

        var definition = method.ReducedFrom ?? method;
        if(SupportsChainableTemplateMethod(definition)) {
            var path = invocation.SyntaxTree.FilePath;
            if(!SupportsExpandedInterceptFile(path) || !IsOutermostLinqInvocation(invocation)) {
                return null;
            }
        } else if(!Linqメソッド判定(definition)) {
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
        var sourceIsLinqInvocation = InvocationSourceがLinq呼び出しか(context.SemanticModel,invocation,cancellationToken);
        if(method.ReducedFrom is null) {
            return null;
        }

        var parameters = method.Parameters;
        var arguments = invocation.ArgumentList.Arguments;
        if(parameters.Length!=arguments.Count) {
            return null;
        }

        var parameterSpecs = new InterceptParameterSpec[parameters.Length];
        for(var index = 0; index<parameters.Length; index++) {
            var parameter = parameters[index];
            var argumentExpression = arguments[index].Expression;
            if(IsSecondarySequenceType(parameter.Type)) {
                return null;
            }

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda, parameter.Type, out var lambdaSpec)) {
                    return null;
                }

                var isEffectivelyStatic = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);

                parameterSpecs[index] = new InterceptParameterSpec(
                    parameter.Name,
                    parameter.Type.ToDisplayString(完全修飾形式),
                    lambdaSpec,
                    isEffectivelyStatic,
                    lambda);

                continue;
            }

            if(IsInlineDelegateType(parameter.Type)) {
                return null;
            }

            parameterSpecs[index]=new InterceptParameterSpec(
                parameter.Name,
                parameter.Type.ToDisplayString(完全修飾形式));
        }

        return new Linq傍受対象型(
            identifier,
            containingMethod.Name,
            interceptsLocationAttributeText,
            method,
            definition,
            sourceIsLinqInvocation,
            invocation,
            context.SemanticModel,
            parameterSpecs);
    }

    private static bool Linqメソッド判定(IMethodSymbol method) {
        if(!method.IsExtensionMethod
            ||method.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        if(method.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1
            ||!IsIEnumerableType(sourceType)) {
            return false;
        }

        if(ContainsAnonymousType(method.ReturnType)
            || method.Parameters.Any(static parameter => ContainsAnonymousType(parameter.Type))) {
            return false;
        }

        return SupportsPlainLambdaFragments(method);
    }

    private static string インターセプターソース構築(ImmutableArray<Linq傍受対象型> targets) {
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
            if(SupportsChainableTemplateMethod(target.TemplateMethod)
                && SupportsExpandedInterceptFile(target.Invocation.SyntaxTree.FilePath)) {
                if(!TryCreateExpandedInvocationSource(target,index,out var expandedStaticMethods,out var expandedBody)) {
                    index++;
                    continue;
                }

                builder.AppendLine($"        /// <summary>Intercepts {target.Method.Name} inside {target.ContainingMethodName}.</summary>");
                if(!string.IsNullOrWhiteSpace(expandedStaticMethods)) {
                    インデント付き追記(builder,expandedStaticMethods,"        ");
                }
                builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
                builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}(this global::System.Collections.Generic.IEnumerable<{target.SourceElementTypeName}> source{target.MethodParameterList}) {{");
                インデント付き追記(builder,expandedBody,"            ");
                builder.AppendLine("        }");
                builder.AppendLine();
                index++;
                continue;
            }

            if(!TryCreateLinqFragment(target,index,out var fragment)) {
                index++;
                continue;
            }

            builder.AppendLine($"        /// <summary>Intercepts {target.Method.Name} inside {target.ContainingMethodName}.</summary>");
            if(!string.IsNullOrWhiteSpace(fragment.staticメソッド)) {
                インデント付き追記(builder,fragment.staticメソッド,"        ");
            }
            builder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            builder.AppendLine($"        {target.InterceptsLocationAttributeText}");
            builder.AppendLine($"        public static {target.ReturnTypeName} {target.MethodName}(this global::System.Collections.Generic.IEnumerable<{target.SourceElementTypeName}> source{target.MethodParameterList}) {{");
            インデント付き追記(builder,RenderFragmentBody(fragment,target.ReturnTypeName),"            ");
            builder.AppendLine("        }");
            builder.AppendLine();
            index++;
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static bool TryCreateExpandedInvocationSource(Linq傍受対象型 target,int uniqueNumber,out string staticMethods,out string body) {
        staticMethods=string.Empty;
        body=string.Empty;
        uniqueNumber*=32;

        if(!TryRenderExpandedInvocation(target,target.Invocation,isOutermostInvocation: true,ref uniqueNumber,out staticMethods,out var expressionText)) {
            return false;
        }

        body=$"return {expressionText};";
        return true;
    }

    private static bool TryRenderExpandedInvocation(
        Linq傍受対象型 target,
        InvocationExpressionSyntax invocation,
        bool isOutermostInvocation,
        ref int uniqueNumber,
        out string staticMethods,
        out string expressionText) {
        staticMethods=string.Empty;
        expressionText=string.Empty;

        if(target.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.ReducedFrom is null
            || !SupportsChainableTemplateMethod(method.ReducedFrom)
            || invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            return false;
        }

        if(sourceExpression is InvocationExpressionSyntax nestedInvocation
            && target.SemanticModel.GetSymbolInfo(nestedInvocation).Symbol is IMethodSymbol nestedMethod
            && nestedMethod.ReducedFrom is not null
            && SupportsChainableTemplateMethod(nestedMethod.ReducedFrom)) {
            staticMethods=string.Empty;
        }

        var sourceText="source";

        var parameters = method.Parameters;
        var arguments = invocation.ArgumentList.Arguments;
        if(parameters.Length!=arguments.Count) {
            return false;
        }

        var renderedArguments = new string[arguments.Count+1];
        renderedArguments[0]=sourceText;
        for(var index = 0; index<arguments.Count; index++) {
            var argumentExpression = arguments[index].Expression;
            var parameter = parameters[index];

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda,parameter.Type,out var lambdaSpec)) {
                    return false;
                }

                var isStaticLambda = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
                if(isStaticLambda) {
                    var structName = $"Struct{uniqueNumber++}";
                    var methodName = parameter.Name;
                    var definition = $$"""
                        private struct {{structName}}{
                            public static {{lambdaSpec.ReturnTypeName}} {{methodName}}({{string.Join(", ",lambdaSpec.ParameterTypeNames.Zip(lambdaSpec.ParameterNames,(type,name) => $"{type} {name}"))}}) => {{lambdaSpec.Body}};
                        }
                        """;
                    staticMethods=string.Join(
                        "\n",
                        new[] { staticMethods,definition }.Where(static text => !string.IsNullOrWhiteSpace(text)));
                    renderedArguments[index+1]=$"{structName}.{methodName}";
                    continue;
                }

                if(!isOutermostInvocation) {
                    return false;
                }

                renderedArguments[index+1]=parameter.Name;
                continue;
            }

            renderedArguments[index+1]=NormalizeExpressionText(argumentExpression.ToString());
        }

        expressionText=$"global::System.Linq.Enumerable.{method.Name}({string.Join(", ",renderedArguments)})";
        return true;
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

    private static bool IsEffectivelyStaticLambda(LambdaExpressionSyntax lambda) {
        // 実質staticか判定: 明示的なstatic修飾があるか、非staticでも閉じ込める変数がない等の判定ロジックを実装
        // ここでは単純にstatic修飾有無を判定し、本来は閉じ込み変数の有無も検査すべき
        if(lambda.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            return true;
        }

        // 追加実装例: 変数の閉じ込みがないなら実質staticと見なす
        // 今は単純にfalse返す
        return false;
    }

    private static bool TryCreateLinqFragment(Linq傍受対象型 target,int uniqueNumber,out Template.Fragment fragment) {
        try {
            fragment=null!;
            var loopNumber = uniqueNumber*32;
            if(!TryCreateSourceFragment(target,ref loopNumber,out var sourceFragment)) {
                return false;
            }

            var template = new Template();
            if(!template.TryResolve(target.TemplateMethod,loopNumber,sourceFragment,out fragment,target.Parameters.Select(static parameter => parameter.TemplateArgument).ToArray())) {
                return false;
            }

            return true;
        } catch {
            fragment=null!;
            return false;
        }
    }

    private static bool TryCreateSourceFragment(Linq傍受対象型 target,ref int loopNumber,out Template.EnumerableFragment fragment) {
        if(target.Invocation.Expression is not MemberAccessExpressionSyntax { Expression: var sourceExpression }) {
            fragment=null!;
            return false;
        }

        return TryCreateSourceFragment(target.SemanticModel,sourceExpression,target.SourceElementTypeName,ref loopNumber,out fragment);
    }

    private static bool TryCreateSourceFragment(SemanticModel semanticModel,ExpressionSyntax sourceExpression,string elementTypeName,ref int loopNumber,out Template.EnumerableFragment fragment) {
        if(sourceExpression is InvocationExpressionSyntax sourceInvocation
            && semanticModel.GetSymbolInfo(sourceInvocation).Symbol is IMethodSymbol sourceMethod
            && sourceMethod.ReducedFrom is not null
            && (Linqメソッド判定(sourceMethod.ReducedFrom) || SupportsChainableTemplateMethod(sourceMethod.ReducedFrom))) {
            if(sourceInvocation.Expression is not MemberAccessExpressionSyntax { Expression: var nestedSourceExpression }) {
                fragment=null!;
                return false;
            }

            if(!TryCreateSourceFragment(semanticModel,nestedSourceExpression,GetSourceElementTypeName(sourceMethod),ref loopNumber,out var nestedSourceFragment)) {
                fragment=null!;
                return false;
            }

            if(!TryCreateTemplateArguments(semanticModel,sourceMethod,sourceInvocation.ArgumentList.Arguments,ref loopNumber,out var templateArguments)) {
                fragment=null!;
                return false;
            }

            try {
                var template = new Template();
                if(!template.TryResolve(sourceMethod.ReducedFrom,loopNumber++,nestedSourceFragment,out var sourceFragment,templateArguments)
                    ||sourceFragment is not Template.EnumerableFragment enumerableFragment) {
                    fragment=null!;
                    return false;
                }

                fragment=enumerableFragment;
                return true;
            } catch {
                fragment=null!;
                return false;
            }
        }

        fragment=CreateRootEnumerableFragment("source",elementTypeName,loopNumber++);
        return true;
    }

    private static bool TryCreateTemplateArguments(SemanticModel semanticModel,IMethodSymbol method,SeparatedSyntaxList<ArgumentSyntax> arguments,ref int loopNumber,out object[] templateArguments) {
        var parameters = method.Parameters;
        if(parameters.Length!=arguments.Count) {
            templateArguments=Array.Empty<object>();
            return false;
        }

        templateArguments = new object[parameters.Length];
        for(var index = 0; index<parameters.Length; index++) {
            var parameter = parameters[index];
            var argumentExpression = arguments[index].Expression;

            if(argumentExpression is LambdaExpressionSyntax lambda) {
                if(!TryCreateLambdaFuncFragment(lambda,parameter.Type,out var lambdaSpec)) {
                    return false;
                }

                var isEffectivelyStatic = lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsEffectivelyStaticLambda(lambda);
                templateArguments[index] = isEffectivelyStatic
                    ? CreateStaticTemplateArgument(lambdaSpec)
                    : CreateParameterTemplateArgument(parameter.Name,lambdaSpec);
                continue;
            }

            templateArguments[index]=NormalizeExpressionText(argumentExpression.ToString());
        }

        return true;
    }

    private static object CreateStaticTemplateArgument(LambdaSpec lambda) {
        return new Template.LambdaFuncFragment {
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
            DefineLambda=$" => {lambda.Body};"
        };
    }

    private static object CreateParameterTemplateArgument(string parameterName,LambdaSpec lambda) {
        return new Template.ParameterFuncFragment {
            ParameterName=parameterName,
            ReturnTypeName=lambda.ReturnTypeName,
            ParameterTypeNames=lambda.ParameterTypeNames,
            ParameterNames=lambda.ParameterNames,
        };
    }

    private static string GetSourceElementTypeName(IMethodSymbol method) {
        var definition = method.ReducedFrom ?? method;
        if(definition.Parameters.Length==0
            ||definition.Parameters[0].Type is not INamedTypeSymbol sourceType
            ||sourceType.TypeArguments.Length!=1) {
            throw new InvalidOperationException($"Could not determine source element type for '{method.ToDisplayString(完全修飾形式)}'.");
        }

        return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
    }

    private static Template.EnumerableFragment CreateRootEnumerableFragment(string result,string elementTypeName,int 番号) {
        var e = $"e{番号}";
        var Start = $"Start{番号}";
        var Head = $"Head{番号}";
        var Element = $"item{番号}";
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

    private static bool TryCreateLambdaFuncFragment(LambdaExpressionSyntax lambda,ITypeSymbol delegateType,out LambdaSpec fragment) {
        fragment=null!;
        if(!TryGetLambdaExpressionBody(lambda,out var body)) {
            return false;
        }

        var parameters = GetLambdaParameters(lambda);
        if(delegateType is not INamedTypeSymbol funcType
            ||!IsInlineDelegateType(funcType)) {
            return false;
        }

        if(funcType.TypeArguments.Any(ContainsAnonymousType)) {
            return false;
        }

        var typeArguments = funcType.TypeArguments.Select(typeArgument => typeArgument.ToDisplayString(完全修飾形式)).ToArray();
        var parameterTypeNames = typeArguments.Take(typeArguments.Length-1).ToArray();
        if(parameters.Count!=parameterTypeNames.Length) {
            return false;
        }

        fragment=new LambdaSpec(
            typeArguments[^1],
            parameterTypeNames,
            parameters.ToArray(),
            NormalizeExpressionText(body.ToString()));
        return true;
    }

    private static bool IsInlineDelegateType(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && IsInlineDelegateType(namedType);

    private static bool IsInlineDelegateType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System"
            && namedType.MetadataName is "Func`2" or "Func`3"
            && !IsSecondarySequenceType(namedType.TypeArguments[^1]);

    private static bool IsIEnumerableType(INamedTypeSymbol namedType)
        => namedType.ContainingNamespace.ToDisplayString()=="System.Collections.Generic"
            && namedType.MetadataName=="IEnumerable`1";

    private static bool IsSecondarySequenceType(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && IsIEnumerableType(namedType);

    private static bool ContainsAnonymousType(ITypeSymbol type) {
        if(type.IsAnonymousType) {
            return true;
        }

        return type switch {
            IArrayTypeSymbol arrayType => ContainsAnonymousType(arrayType.ElementType),
            INamedTypeSymbol namedType => namedType.TypeArguments.Any(ContainsAnonymousType),
            _ => false,
        };
    }

    private static bool SupportsPlainLambdaFragments(IMethodSymbol method)
        => method.Name=="Aggregate";

    private static bool SupportsChainableTemplateMethod(IMethodSymbol method)
        => method.Name is "Select" or "Where" or "Take";

    private static bool SupportsExpandedInterceptFile(string path)
        => path.EndsWith("EnumerableMethod2.cs",StringComparison.Ordinal);

    private static bool IsOutermostLinqInvocation(InvocationExpressionSyntax invocation)
        => invocation.Parent is not MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax };

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

    private sealed class Linq傍受対象型 {
        public Linq傍受対象型(string identifier,string containingMethodName,string interceptsLocationAttributeText,IMethodSymbol method,IMethodSymbol templateMethod,bool sourceIsLinqInvocation,InvocationExpressionSyntax invocation,SemanticModel semanticModel,InterceptParameterSpec[] parameters) {
            this.Identifier=identifier;
            this.ContainingMethodName=containingMethodName;
            this.InterceptsLocationAttributeText=interceptsLocationAttributeText;
            this.Method=method;
            this.TemplateMethod=templateMethod;
            this.SourceIsLinqInvocation=sourceIsLinqInvocation;
            this.Invocation=invocation;
            this.SemanticModel=semanticModel;
            this.Parameters=parameters;
        }

        public string Identifier { get; }
        public string ContainingMethodName { get; }
        public string InterceptsLocationAttributeText { get; }
        public IMethodSymbol Method { get; }
        public IMethodSymbol TemplateMethod { get; }
        public bool SourceIsLinqInvocation { get; }
        public InvocationExpressionSyntax Invocation { get; }
        public SemanticModel SemanticModel { get; }
        public InterceptParameterSpec[] Parameters { get; }
        public string SourceElementTypeName {
            get {
                if(this.Method.TypeArguments.Length>0) {
                    return this.Method.TypeArguments[0].ToDisplayString(完全修飾形式);
                }

                var parameters = this.TemplateMethod.Parameters;
                if(parameters.Length==0
                    ||parameters[0].Type is not INamedTypeSymbol sourceType
                    ||sourceType.TypeArguments.Length!=1) {
                    throw new InvalidOperationException($"Could not determine source element type for '{this.TemplateMethod.ToDisplayString(完全修飾形式)}'.");
                }

                return sourceType.TypeArguments[0].ToDisplayString(完全修飾形式);
            }
        }
        public string ReturnTypeName => this.Method.ReturnType.ToDisplayString(完全修飾形式);
        public string MethodParameterList => this.Parameters.Length==0
            ? string.Empty
            : ", "+string.Join(", ",this.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"));
        public string MethodName => $"OptimizedLinq_{this.Identifier}";
    }

    private sealed class InterceptParameterSpec {
        public InterceptParameterSpec(string name,string typeName,LambdaSpec? lambda = null,bool lambdaIsStatic = false, LambdaExpressionSyntax? lambdaSyntax = null) {
            this.Name=name;
            this.TypeName=typeName;
            this.Lambda=lambda;
            this.LambdaIsStatic=lambdaIsStatic;
            this.LambdaSyntax=lambdaSyntax;
        }

        public string Name { get; }
        public string TypeName { get; }
        public LambdaSpec? Lambda { get; }

        // 追加: staticかどうか判定を保存
        public bool LambdaIsStatic { get; }

        // 追加: ラムダ構文の保存
        public LambdaExpressionSyntax? LambdaSyntax { get; }

        public object TemplateArgument => this.Lambda is null
            ? this.Name
            : this.LambdaIsStatic
                ? new Template.LambdaFuncFragment {
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                    DefineLambda=$" => {this.Lambda.Body};"
                }
                : new Template.ParameterFuncFragment {
                    ParameterName=this.Name,
                    ReturnTypeName=this.Lambda.ReturnTypeName,
                    ParameterTypeNames=this.Lambda.ParameterTypeNames,
                    ParameterNames=this.Lambda.ParameterNames,
                };
    }

    private sealed class LambdaSpec {
        public LambdaSpec(string returnTypeName,string[] parameterTypeNames,string[] parameterNames,string body) {
            this.ReturnTypeName=returnTypeName;
            this.ParameterTypeNames=parameterTypeNames;
            this.ParameterNames=parameterNames;
            this.Body=body;
        }

        public string ReturnTypeName { get; }
        public string[] ParameterTypeNames { get; }
        public string[] ParameterNames { get; }
        public string Body { get; }
    }

    private sealed class LinqInterceptTargetComparer : IEqualityComparer<Linq傍受対象型> {
        public static readonly LinqInterceptTargetComparer Instance = new();

        public bool Equals(Linq傍受対象型? x,Linq傍受対象型? y) {
            if(ReferenceEquals(x,y)) {
                return true;
            }
            if(x is null || y is null) {
                return false;
            }
            return x.InterceptsLocationAttributeText==y.InterceptsLocationAttributeText;
        }

        public int GetHashCode(Linq傍受対象型 obj) {
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
