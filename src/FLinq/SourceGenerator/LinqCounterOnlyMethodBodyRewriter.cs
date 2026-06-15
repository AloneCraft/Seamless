using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using static Tsavorite.SourceGenerator.GeneratorHelpers;

namespace Tsavorite.SourceGenerator;

public sealed partial class TsavoriteIncrementalGenerator {
    private static bool TryBuildCounterOnlyLocalMethodBody(Linq傍受対象型 target,out string staticMethods,out string body,HashSet<string>? emittedUnsafeAccessors = null) {
        staticMethods=string.Empty;
        body=string.Empty;

        if(!target.CounterInstrumentation
            || target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax methodSyntax) {
            return false;
        }

        var unsafeAccessors = new List<string>();
        if(methodSyntax.ExpressionBody is { Expression: var expressionBody }) {
            var expressionText=RenderCounterOnlyExpression(expressionBody,target.SemanticModel,target.EnclosingType,unsafeAccessors);
            body=target.Method.ReturnsVoid
                ? $"{expressionText};"
                : $"return {expressionText};";
        } else if(methodSyntax.Body is {
            Statements: [
                ReturnStatementSyntax { Expression: { } returnExpression }
            ]
        }) {
            var expressionText=RenderCounterOnlyExpression(returnExpression,target.SemanticModel,target.EnclosingType,unsafeAccessors);
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
                .Where(code=>emittedUnsafeAccessors?.Add(code) ?? true));
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
                ?? declaration.Type.ToString();
        var variables = declaration.Variables.Select(variable=> {
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
        if(expression is InvocationExpressionSyntax invocation
            && semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedMethod
            && TryRenderCounterOnlyEnumerableInvocation(invokedMethod,invocation,semanticModel,containingType,unsafeAccessors,out var enumerableExpression)) {
            return enumerableExpression;
        }

        if(expression is InvocationExpressionSyntax {
            Expression: MemberAccessExpressionSyntax memberAccess
        } memberInvocation) {
            var receiver = RenderCounterOnlyExpression(memberAccess.Expression,semanticModel,containingType,unsafeAccessors);
            var arguments = memberInvocation.ArgumentList.Arguments
                .Select(argument=>RenderCounterOnlyArgument(argument,semanticModel,containingType,unsafeAccessors));
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

        var definition = invokedMethod.ReducedFrom ?? invokedMethod;
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

        expressionText=$"global::System.Linq.Enumerable.{definition.Name}{RenderTypeArgumentList(invokedMethod)}({string.Join(", ",arguments)})";
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
        arguments=[];
        var slots = new string?[definition.Parameters.Length];
        var nextParameterIndex = 0;

        if(invokedMethod.ReducedFrom is not null) {
            if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess || definition.Parameters.Length==0) {
                return false;
            }

            slots[0]=RenderCounterOnlyExpression(memberAccess.Expression,semanticModel,containingType,unsafeAccessors);
            nextParameterIndex=1;
        }

        foreach(var argument in invocation.ArgumentList.Arguments) {
            var targetParameterIndex = -1;
            if(argument.NameColon is { Name.Identifier.ValueText: var argumentName }) {
                for(var i=nextParameterIndex;i<definition.Parameters.Length;i++) {
                    if(definition.Parameters[i].Name==argumentName) {
                        targetParameterIndex=i;
                        break;
                    }
                }
            } else {
                while(nextParameterIndex<slots.Length && slots[nextParameterIndex] is not null) {
                    nextParameterIndex++;
                }
                targetParameterIndex=nextParameterIndex++;
            }

            if(targetParameterIndex<0
                || targetParameterIndex>=slots.Length
                || slots[targetParameterIndex] is not null) {
                return false;
            }

            var renderedArgument = RenderCounterOnlyExpression(argument.Expression,semanticModel,containingType,unsafeAccessors);
            var parameter = GetCounterOnlyEnumerableParameter(invokedMethod,definition,targetParameterIndex);
            slots[targetParameterIndex]=parameter.Type.TypeKind==TypeKind.Delegate
                && TryWrapCounterOnlyDelegate(parameter.Type,renderedArgument,out var wrappedDelegate)
                    ? $"{RefKind接頭辞取得(argument.RefKindKeyword.Kind())}{wrappedDelegate}"
                    : $"{RefKind接頭辞取得(argument.RefKindKeyword.Kind())}{renderedArgument}";
        }

        for(var i=0;i<slots.Length;i++) {
            if(slots[i] is not null) {
                continue;
            }

            var parameter = GetCounterOnlyEnumerableParameter(invokedMethod,definition,i);
            if(parameter.IsParams && parameter.Type is IArrayTypeSymbol paramsArray) {
                slots[i]=$"global::System.Array.Empty<{paramsArray.ElementType.ToDisplayString(完全修飾形式)}>()";
            } else if(parameter.HasExplicitDefaultValue) {
                slots[i]=RenderConstantValue(parameter.ExplicitDefaultValue);
            } else {
                return false;
            }
        }

        arguments=slots.Select(static slot=>slot!).ToList();
        return true;
    }

    private static IParameterSymbol GetCounterOnlyEnumerableParameter(IMethodSymbol invokedMethod,IMethodSymbol definition,int slotIndex) {
        if(invokedMethod.ReducedFrom is not null
            && slotIndex>0
            && slotIndex-1<invokedMethod.Parameters.Length) {
            return invokedMethod.Parameters[slotIndex-1];
        }

        if(invokedMethod.ReducedFrom is null
            && slotIndex<invokedMethod.Parameters.Length) {
            return invokedMethod.Parameters[slotIndex];
        }

        return definition.Parameters[slotIndex];
    }

    private static bool TryWrapCounterOnlyDelegate(ITypeSymbol delegateType,string delegateExpression,out string wrappedExpression) {
        wrappedExpression=string.Empty;
        if(delegateType is not INamedTypeSymbol namedDelegate
            || namedDelegate.DelegateInvokeMethod is not { } invokeMethod
            || invokeMethod.ReturnsVoid
            || invokeMethod.Parameters.Any(static parameter=>parameter.RefKind!=RefKind.None)) {
            return false;
        }

        var delegateTypeName = namedDelegate.ToDisplayString(完全修飾形式);
        var argumentNames = invokeMethod.Parameters
            .Select(static (_,index)=>$"__delegateArg{index}")
            .ToArray();
        wrappedExpression=
            $"(({delegateTypeName})(({string.Join(", ",argumentNames)}) => " +
            $"global::Tsavorite.LinqCounterScope.Counter.RecordPhysicalDelegate((({delegateTypeName})({delegateExpression})).Invoke({string.Join(", ",argumentNames)}))))";
        return true;
    }
}
