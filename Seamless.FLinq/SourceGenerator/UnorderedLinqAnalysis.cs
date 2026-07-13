namespace Seamless.FLinq;

public sealed partial class FLinqIncrementalGenerator{
    private static bool TryGetUnorderedInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out string methodName,
        out string sourceTypeName,
        out bool explicitOptIn){
        methodName=string.Empty;
        sourceTypeName=string.Empty;
        explicitOptIn=ContainsInlineAsEnumerable(invocation);
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            ||!global::Seamless.SourceGenerator.UnorderedLinqOperatorPolicy.IsRestricted(method)){
            return false;
        }
        methodName=(method.ReducedFrom??method).Name;
        var immediateSource=GetInvocationSource(method,invocation);
        if(!TryGetRootSourceExpression(semanticModel,invocation,ref explicitOptIn,out var rootExpression)){
            return false;
        }
        var sourceType=semanticModel.GetTypeInfo(rootExpression).Type
            ??semanticModel.GetTypeInfo(rootExpression).ConvertedType;
        if(!IsUnorderedSourceType(sourceType)){
            return false;
        }
        if(immediateSource is not null) {
            var immediateType=semanticModel.GetTypeInfo(immediateSource).Type
                ??semanticModel.GetTypeInfo(immediateSource).ConvertedType;
            if(!IsUnorderedSourceType(immediateType)) {
                explicitOptIn=true;
            }
        }
        sourceTypeName=sourceType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return true;
    }

    private static ExpressionSyntax? GetInvocationSource(IMethodSymbol method,InvocationExpressionSyntax invocation){
        if(method.ReducedFrom is not null
            &&invocation.Expression is MemberAccessExpressionSyntax{Expression:var receiver}) {
            return receiver;
        }
        return invocation.ArgumentList.Arguments.Count>0
            ?invocation.ArgumentList.Arguments[0].Expression
            :null;
    }

    private static bool ContainsInlineAsEnumerable(InvocationExpressionSyntax invocation)=>
        invocation.DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Any(static candidate=>candidate.Expression is MemberAccessExpressionSyntax member
                &&member.Name.Identifier.ValueText=="AsEnumerable");

    private static bool TryGetRootSourceExpression(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        ref bool explicitOptIn,
        out ExpressionSyntax rootExpression){
        rootExpression=invocation;
        ExpressionSyntax current=invocation;
        while(current is InvocationExpressionSyntax currentInvocation){
            if(semanticModel.GetSymbolInfo(currentInvocation).Symbol is not IMethodSymbol currentMethod){
                return false;
            }
            var definition=currentMethod.ReducedFrom??currentMethod;
            if(IsMaterializingBoundary(definition.Name)){
                rootExpression=currentInvocation;
                return true;
            }
            if(global::Seamless.SourceGenerator.UnorderedLinqOperatorPolicy.IsExplicitOptIn(definition)){
                explicitOptIn=true;
            }
            if(currentMethod.ReducedFrom is not null
                &&currentInvocation.Expression is MemberAccessExpressionSyntax{Expression:var receiver}){
                current=StripUnorderedParentheses(receiver,semanticModel,ref explicitOptIn);
                continue;
            }
            if(!IsStaticLinqInvocation(definition)){
                rootExpression=currentInvocation;
                return true;
            }
            if(currentInvocation.ArgumentList.Arguments.Count==0){
                return false;
            }
            if(IsFullyQualifiedSystemLinqInvocation(currentInvocation,definition)){
                explicitOptIn=true;
            }
            current=StripUnorderedParentheses(currentInvocation.ArgumentList.Arguments[0].Expression,semanticModel,ref explicitOptIn);
        }
        rootExpression=current;
        return true;
    }

    private static bool IsStaticLinqInvocation(IMethodSymbol method){
        var containingType=method.ContainingType.ToDisplayString();
        return containingType is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable";
    }

    private static ExpressionSyntax StripUnorderedParentheses(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        ref bool explicitOptIn){
        while(expression is ParenthesizedExpressionSyntax parenthesized){
            expression=parenthesized.Expression;
        }
        if(expression is CastExpressionSyntax cast
            &&semanticModel.GetTypeInfo(cast.Type).Type is INamedTypeSymbol castType
            &&IsIEnumerableType(castType)){
            explicitOptIn=true;
            return StripUnorderedParentheses(cast.Expression,semanticModel,ref explicitOptIn);
        }
        return expression;
    }

    private static bool IsFullyQualifiedSystemLinqInvocation(InvocationExpressionSyntax invocation,IMethodSymbol method){
        if(method.ContainingType.ToDisplayString()!="System.Linq.Enumerable"){
            return false;
        }
        return invocation.Expression is MemberAccessExpressionSyntax member
            &&member.Expression.ToString().Contains("Enumerable",StringComparison.Ordinal);
    }

    private static bool IsMaterializingBoundary(string methodName)=>methodName is
        "ToArray" or "ToList" or "ToHashSet" or "ToDictionary" or "ToLookup";

    private static bool IsUnorderedSourceType(ITypeSymbol? type){
        return global::Seamless.SourceGenerator.UnorderedLinqOperatorPolicy.IsUnorderedSourceType(type);
    }
}
