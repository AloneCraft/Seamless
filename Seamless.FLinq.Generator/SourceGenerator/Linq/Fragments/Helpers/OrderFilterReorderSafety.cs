using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

// 整列前へ述語を移すと、鍵の回数と述語の順序が変わる。無副作用・無例外と証明できる形だけを許す。
internal static class OrderFilterReorderSafety{
    internal static bool CanMoveFilter(InvocationExpressionSyntax order,LambdaExpressionSyntax predicate,SemanticModel model){
        if(!IsSafeLambda(predicate,model)
            ||model.GetSymbolInfo(order).Symbol is not IMethodSymbol method){return false;}
        var definition=method.ReducedFrom??method;
        if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"){return false;}
        if(definition.Name=="Reverse"){return order.ArgumentList.Arguments.Count==0;}
        if(definition.Name is "Order" or "OrderDescending"){
            return order.ArgumentList.Arguments.Count==0&&method.TypeArguments.Length==1&&IsPrimitive(method.TypeArguments[0]);
        }
        if(definition.Name is not ("OrderBy" or "OrderByDescending")
            ||order.ArgumentList.Arguments.Count!=1
            ||order.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax selector){return false;}
        return IsSafeLambda(selector,model);
    }

    private static bool IsSafeLambda(LambdaExpressionSyntax lambda,SemanticModel model){
        if(lambda.Body is not ExpressionSyntax body||AnchorAnalysis.ContainsAnchor(body,model)){return false;}
        var parameters=new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if(lambda is SimpleLambdaExpressionSyntax simple){
            if(model.GetDeclaredSymbol(simple.Parameter) is not { } symbol){return false;}
            parameters.Add(symbol);
        }else if(lambda is ParenthesizedLambdaExpressionSyntax parenthesized){
            foreach(var parameter in parenthesized.ParameterList.Parameters){
                if(model.GetDeclaredSymbol(parameter) is not { } symbol){return false;}
                parameters.Add(symbol);
            }
        }else{return false;}
        return IsSafeOperation(model.GetOperation(body),parameters,model);
    }

    private static bool IsSafeOperation(IOperation? operation,HashSet<ISymbol> parameters,SemanticModel model){
        if(operation is null||!IsPrimitive(operation.Type)){return false;}
        if(operation.Syntax is ExpressionSyntax expression){
            var peeled=TransparentCalls.Peel(expression,model);
            if(!ReferenceEquals(peeled,expression)){return IsSafeOperation(model.GetOperation(peeled),parameters,model);}
        }
        if(operation.ConstantValue.HasValue){return true;}
        return operation switch{
            IParameterReferenceOperation parameter=>parameter.Parameter.RefKind==RefKind.None&&parameters.Contains(parameter.Parameter),
            IDefaultValueOperation=>true,
            IParenthesizedOperation parenthesized=>IsSafeOperation(parenthesized.Operand,parameters,model),
            IUnaryOperation unary=>!unary.IsChecked&&unary.OperatorMethod is null&&IsSafeOperation(unary.Operand,parameters,model),
            IBinaryOperation binary=>!binary.IsChecked&&binary.OperatorMethod is null
                &&binary.OperatorKind is not (BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder)
                &&IsSafeOperation(binary.LeftOperand,parameters,model)&&IsSafeOperation(binary.RightOperand,parameters,model),
            IConversionOperation conversion=>!conversion.IsChecked&&!conversion.IsTryCast&&conversion.OperatorMethod is null
                &&!conversion.Conversion.IsUserDefined&&IsSafeOperation(conversion.Operand,parameters,model),
            IConditionalOperation conditional=>!conditional.IsRef&&IsSafeOperation(conditional.Condition,parameters,model)
                &&IsSafeOperation(conditional.WhenTrue,parameters,model)&&IsSafeOperation(conditional.WhenFalse,parameters,model),
            _=>false,
        };
    }

    private static bool IsPrimitive(ITypeSymbol? type)=>type?.SpecialType is
        SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte
        or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
        or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_IntPtr or SpecialType.System_UIntPtr
        or SpecialType.System_Single or SpecialType.System_Double;
}
