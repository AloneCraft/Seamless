using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator {
    private static bool TryBuildCounterOnlyLocalMethodBody(GenerationState state,Linq傍受対象型 target,out string staticMethods,out string body,HashSet<string>? emittedUnsafeAccessors = null) {
        staticMethods=string.Empty;
        body=string.Empty;

        if(!target.CounterInstrumentation
            ||target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        var unsafeAccessors = state.UnsafeAccessorCodes;
        unsafeAccessors.Clear();
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            var expressionText = RenderCounterOnlyExpression(expressionBody,target.SemanticModel,target.EnclosingType,unsafeAccessors);
            body=target.Method.ReturnsVoid
                ? $"{expressionText};"
                : $"return {expressionText};";
        } else if(methodSyntax.Body is { } bodySyntax
            &&bodySyntax.Statements.Count==1
            &&bodySyntax.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression }) {
            var expressionText = RenderCounterOnlyExpression(returnExpression,target.SemanticModel,target.EnclosingType,unsafeAccessors);
            body=target.Method.ReturnsVoid
                ? $"{expressionText};"
                : $"return {expressionText};";
        } else if(methodSyntax.Body is { } blockBody) {
            if(!TryRenderCounterOnlyBlockBody(blockBody,target.SemanticModel,target.EnclosingType,unsafeAccessors,out body)) {
                return false;
            }
        } else {
            return false;
        }

        staticMethods=string.Join(
            "\n",
            unsafeAccessors
                .Distinct(StringComparer.Ordinal)
                .Where(code => emittedUnsafeAccessors?.Add(code)??true));
        return true;
    }

    private static bool TryRenderCounterOnlyBlockBody(
        BlockSyntax blockBody,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors,
        out string body) {
        var builder = new StringBuilder();
        foreach(var statement in blockBody.Statements) {
            switch(statement) {
                case LocalDeclarationStatementSyntax localDeclaration:
                    builder.AppendLine(RenderCounterOnlyLocalDeclaration(localDeclaration,semanticModel,containingType,unsafeAccessors));
                    break;
                case ExpressionStatementSyntax expressionStatement:
                    builder.AppendLine($"{RenderCounterOnlyExpression(expressionStatement.Expression,semanticModel,containingType,unsafeAccessors)};");
                    break;
                case ReturnStatementSyntax { Expression: { } returnExpression }:
                    builder.AppendLine($"return {RenderCounterOnlyExpression(returnExpression,semanticModel,containingType,unsafeAccessors)};");
                    break;
                case ReturnStatementSyntax:
                    builder.AppendLine("return;");
                    break;
                default:
                    body=string.Empty;
                    return false;
            }
        }

        body=builder.ToString().TrimEnd();
        return true;
    }

    private static string RenderCounterOnlyLocalDeclaration(
        LocalDeclarationStatementSyntax statement,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors) {
        var declaration = statement.Declaration;
        var typeName = declaration.Type is IdentifierNameSyntax { IsVar: true }
            ? "var"
            : semanticModel.GetTypeInfo(declaration.Type).Type?.ToDisplayString(完全修飾形式)
                ??declaration.Type.ToString();
        var variables = declaration.Variables.Select(variable => {
            if(variable.Initializer?.Value is not { } initializer) {
                return variable.ToString();
            }

            return $"{variable.Identifier.ValueText}={RenderCounterOnlyExpression(initializer,semanticModel,containingType,unsafeAccessors)}";
        });
        var usingPrefix = statement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) ? "using " : string.Empty;
        return $"{usingPrefix}{typeName} {string.Join(", ",variables)};";
    }

    private static string RenderCounterOnlyExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors) {
        if(expression is MemberAccessExpressionSyntax anchorAccess
            &&AnchorAnalysis.IsAnchorAccess(anchorAccess,semanticModel)) {
            return RenderCounterOnlyExpression(anchorAccess.Expression,semanticModel,containingType,unsafeAccessors);
        }

        if(expression is InvocationExpressionSyntax invocation
            &&semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedMethod
            &&TryRenderCounterOnlyEnumerableInvocation(invokedMethod,invocation,semanticModel,containingType,unsafeAccessors,out var enumerableExpression)) {
            return enumerableExpression;
        }

        if(expression is InvocationExpressionSyntax {
            Expression: MemberAccessExpressionSyntax memberAccess
        } memberInvocation) {
            var receiver = RenderCounterOnlyExpression(memberAccess.Expression,semanticModel,containingType,unsafeAccessors);
            var arguments = memberInvocation.ArgumentList.Arguments
                .Select(argument => RenderCounterOnlyArgument(argument,semanticModel,containingType,unsafeAccessors));
            return $"{receiver}.{memberAccess.Name}({string.Join(", ",arguments)})";
        }

        return RenderExpressionForGeneratedMethod(expression,semanticModel,containingType,unsafeAccessors);
    }

    private static string RenderCounterOnlyArgument(
        ArgumentSyntax argument,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors) {
        var namePrefix = argument.NameColon is null ? string.Empty : $"{argument.NameColon} ";
        return $"{namePrefix}{RefKind接頭辞取得(argument.RefKindKeyword.Kind())}{RenderCounterOnlyExpression(argument.Expression,semanticModel,containingType,unsafeAccessors)}";
    }

    private static bool TryRenderCounterOnlyEnumerableInvocation(
        IMethodSymbol invokedMethod,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors,
        out string expressionText) {
        expressionText=string.Empty;

        var definition = invokedMethod.ReducedFrom??invokedMethod;
        if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable") {
            return false;
        }

        if(!TryCreateCounterOnlyEnumerableArgumentList(
            invokedMethod,
            definition,
            invocation,
            semanticModel,
            containingType,
            unsafeAccessors,
            out var arguments)) {
            return false;
        }

        // 匿名型は型引数として書けないため、含む場合は明示型引数を省略し引数から推論させる。
        var typeArgumentList = invokedMethod.TypeArguments.Any(GenerationPipeline.ContainsAnonymousType)
            ? string.Empty
            : RenderTypeArgumentList(invokedMethod);
        expressionText=$"global::System.Linq.Enumerable.{definition.Name}{typeArgumentList}({string.Join(", ",arguments)})";
        return true;
    }

    private static bool TryCreateCounterOnlyEnumerableArgumentList(
        IMethodSymbol invokedMethod,
        IMethodSymbol definition,
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors,
        out List<string> arguments) {
        arguments=new List<string>();
        var slots = new string?[definition.Parameters.Length];
        var nextParameterIndex = 0;

        if(invokedMethod.ReducedFrom is not null) {
            if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess||definition.Parameters.Length==0) {
                return false;
            }

            slots[0]=RenderCounterOnlyExpression(memberAccess.Expression,semanticModel,containingType,unsafeAccessors);
            nextParameterIndex=1;
        }

        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i = nextParameterIndex;i<definition.Parameters.Length;i++) {
                    if(definition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<slots.Length&&slots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }

            if(targetParameterIndex<0
                ||targetParameterIndex>=slots.Length
                ||slots[targetParameterIndex] is not null) {
                return false;
            }

            var renderedArgument = RenderCounterOnlyExpression(argument.Expression,semanticModel,containingType,unsafeAccessors);
            var parameter = GetCounterOnlyEnumerableParameter(invokedMethod,definition,targetParameterIndex);
            slots[targetParameterIndex]=parameter.Type.TypeKind==TypeKind.Delegate
                &&TryWrapCounterOnlyDelegate(parameter.Type,argument.Expression,renderedArgument,semanticModel,containingType,unsafeAccessors,out var wrappedDelegate)
                    ? $"{RefKind接頭辞取得(argument.RefKindKeyword.Kind())}{wrappedDelegate}"
                    : $"{RefKind接頭辞取得(argument.RefKindKeyword.Kind())}{renderedArgument}";
        }

        for(var i = 0;i<slots.Length;i++) {
            if(slots[i] is not null) {
                continue;
            }

            var parameter = GetCounterOnlyEnumerableParameter(invokedMethod,definition,i);
            if(parameter.IsParams&&parameter.Type is IArrayTypeSymbol paramsArray) {
                slots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                slots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                return false;
            }
        }

        arguments=slots.Select(static slot => slot!).ToList();
        return true;
    }

    private static IParameterSymbol GetCounterOnlyEnumerableParameter(IMethodSymbol invokedMethod,IMethodSymbol definition,int slotIndex) {
        if(invokedMethod.ReducedFrom is not null
            &&slotIndex>0
            &&slotIndex-1<invokedMethod.Parameters.Length) {
            return invokedMethod.Parameters[slotIndex-1];
        }

        if(invokedMethod.ReducedFrom is null
            &&slotIndex<invokedMethod.Parameters.Length) {
            return invokedMethod.Parameters[slotIndex];
        }

        return definition.Parameters[slotIndex];
    }

    private static bool TryWrapCounterOnlyDelegate(
        ITypeSymbol delegateType,
        ExpressionSyntax argumentExpression,
        string delegateExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors,
        out string wrappedExpression) {
        wrappedExpression=string.Empty;
        if(delegateType is not INamedTypeSymbol namedDelegate
            ||namedDelegate.DelegateInvokeMethod is not { } invokeMethod
            ||invokeMethod.ReturnsVoid
            ||invokeMethod.Parameters.Any(static parameter => parameter.RefKind!=RefKind.None)) {
            return false;
        }

        // 匿名型はFunc<...>のキャストとして書けないため、キャスト+Invokeを経由せずラムダ本体を直接埋め込んで型推論に任せる。
        if(namedDelegate.TypeArguments.Any(GenerationPipeline.ContainsAnonymousType)
            &&TryWrapCounterOnlyDelegateByInlining(argumentExpression,semanticModel,containingType,unsafeAccessors,out wrappedExpression)) {
            return true;
        }

        var delegateTypeName = namedDelegate.ToDisplayString(完全修飾形式);
        var argumentNames = invokeMethod.Parameters
            .Select(static (_,index) => $"__delegateArg{index}")
            .ToArray();
        wrappedExpression=
            $"(({delegateTypeName})(({string.Join(", ",argumentNames)}) => "+
            $"global::Seamless.Linq.LinqCounterScope.Counter.RecordPhysicalDelegate((({delegateTypeName})({delegateExpression})).Invoke({string.Join(", ",argumentNames)}))))";
        return true;
    }

    // 元のラムダの仮引数名をそのまま使い、本体を RecordPhysicalDelegate(...) で包むだけの形にする。
    // キャストも.Invoke()も経由しないので、引数/戻り値の型(匿名型を含む)を一切書かずに済む。
    // 式本体の単純ラムダのみ対応。block本体・メソッドグループ・デリゲート変数は非対応で false を返し、
    // 呼び出し元はその引数を計測なしの素のまま(renderedArgument)で通す。
    private static bool TryWrapCounterOnlyDelegateByInlining(
        ExpressionSyntax argumentExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol containingType,
        List<string> unsafeAccessors,
        out string wrappedExpression) {
        wrappedExpression=string.Empty;
        var expression = argumentExpression;
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }

        IReadOnlyList<string> parameterNames;
        ExpressionSyntax body;
        switch(expression) {
            case SimpleLambdaExpressionSyntax simple when simple.Body is ExpressionSyntax simpleBody:
                parameterNames=new[] { simple.Parameter.Identifier.ValueText };
                body=simpleBody;
                break;
            case ParenthesizedLambdaExpressionSyntax parenthesizedLambda when parenthesizedLambda.Body is ExpressionSyntax parenthesizedBody:
                parameterNames=parenthesizedLambda.ParameterList.Parameters
                    .Select(static parameter => parameter.Identifier.ValueText)
                    .ToArray();
                body=parenthesizedBody;
                break;
            default:
                return false;
        }

        var renderedBody = RenderCounterOnlyExpression(body,semanticModel,containingType,unsafeAccessors);
        wrappedExpression=$"(({string.Join(", ",parameterNames)}) => global::Seamless.Linq.LinqCounterScope.Counter.RecordPhysicalDelegate({renderedBody}))";
        return true;
    }
}
