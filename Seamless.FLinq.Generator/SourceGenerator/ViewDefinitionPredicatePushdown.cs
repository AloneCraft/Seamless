using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

internal sealed record ViewProjectionMap(string InputType,ISymbol Key,IReadOnlyDictionary<ISymbol,ISymbol> Columns);

internal static class ViewDefinitionPredicatePushdown{
    internal static bool TryApply(ViewDefinitionRoot view,PipelineStage stage,SemanticModel model,InvocationExpressionSyntax invocation,ref int number){
        view.RejectPredicate(view.ProjectionReason);
        if(view.Projection is not{} projection){return false;}
        if(stage.UsesIndex||stage.ContainsAnchor||stage.Func is not{CapturedLocals.Length:0,HoistedLocals.Length:0}
            ||model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol{Name:"Where",ReducedFrom:{} method}
            ||method.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
            ||invocation.ArgumentList.Arguments.Count!=1
            ||invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax{Body:ExpressionSyntax body} lambda
            ||model.GetOperation(lambda) is not IAnonymousFunctionOperation{Symbol.Parameters.Length:1} function){
            view.RejectPredicate("PredicateMustBeUnanchoredSingleParameterLambda");return false;
        }
        if(model.GetOperation(body) is not IBinaryOperation{OperatorKind:BinaryOperatorKind.Equals,OperatorMethod:null} equality){
            view.RejectPredicate("PredicateMustBeBuiltinKeyEquality");return false;
        }
        var column=equality.LeftOperand as IPropertyReferenceOperation;
        var constant=equality.RightOperand as ILiteralOperation;
        if(column is null||constant is null){column=equality.RightOperand as IPropertyReferenceOperation;constant=equality.LeftOperand as ILiteralOperation;}
        if(column?.Instance is not IParameterReferenceOperation parameter||constant is null
            ||!SymbolEqualityComparer.Default.Equals(parameter.Parameter,function.Symbol.Parameters[0])
            ||!projection.Columns.TryGetValue(column.Property,out var input)
            ||!SymbolEqualityComparer.Default.Equals(input,projection.Key)
            ||!SymbolEqualityComparer.Default.Equals(column.Type,constant.Type)){
            view.RejectPredicate("PredicateMustCompareMappedKeyWithSameTypeLiteral");return false;
        }
        var predicate=new LambdaFunc{
            ParameterNames=new[]{"__viewInput"},ParameterTypeNames=new[]{projection.InputType},ReturnTypeName="bool",
            InlineBody="__viewInput.@"+input.Name+"=="+constant.Syntax.ToString(),CanInlineAssign=true,
            ExecutionNote="[View/条件移動] 直接列と組込みキー等値を投影前へ移動。集合境界を維持し、点検索の選択は未実施。",
        };
        return view.PushPredicate(predicate,ref number,input);
    }
}

public sealed partial class FLinqIncrementalGenerator{
    internal sealed partial class GenerationPipeline{
        private static ViewProjectionMap? DescribeViewProjection(SemanticModel model,ExpressionSyntax body,out string reason){
            reason="DirectColumnProjectionNotProven";
            if(body is not InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax access} select
                ||model.GetSymbolInfo(select).Symbol is not IMethodSymbol{Name:"Select",ReducedFrom:{} method}
                ||method.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
                ||select.ArgumentList.Arguments.Count!=1
                ||select.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax{Body:ExpressionSyntax projection} lambda
                ||model.GetOperation(lambda) is not IAnonymousFunctionOperation{Symbol.Parameters.Length:1} function
                ||model.GetOperation(projection) is not IObjectCreationOperation{Type:INamedTypeSymbol result,Constructor:{} constructor} creation){return null;}
            // Filtering can cross Distinct only when construction and value equality have no observable user code.
            if(creation.Initializer is not null||!result.IsRecord||result.TypeKind!=TypeKind.Struct||!result.IsReadOnly||result.StaticConstructors.Length!=0
                ||constructor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not RecordDeclarationSyntax{ParameterList:{} parameters} declaration
                ||constructor.Parameters.Length!=parameters.Parameters.Count
                ||result.DeclaringSyntaxReferences.Any(static reference=>reference.GetSyntax() is not RecordDeclarationSyntax part
                    ||part.Members.Any(static member=>!member.Modifiers.Any(SyntaxKind.StaticKeyword)))){
                reason="ProjectionConstructorOrEqualityNotProvenTotal";return null;
            }
            var columns=new Dictionary<ISymbol,ISymbol>(SymbolEqualityComparer.Default);
            foreach(var argument in creation.Arguments){
                if(argument.Parameter is null||DirectJoinKeyMember(argument.Value,function.Symbol.Parameters[0]) is not{} input
                    ||!IsPushdownScalar(argument.Value.Type)
                    ||!SymbolEqualityComparer.Default.Equals(argument.Value.Type,argument.Parameter.Type)
                    ||result.GetMembers(argument.Parameter.Name).OfType<IPropertySymbol>().SingleOrDefault() is not{} property){return null;}
                columns.Add(property,input);
            }
            var source=access.Expression;
            while(source is InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax filterAccess} filter
                &&model.GetSymbolInfo(filter).Symbol is IMethodSymbol{Name:"Where",ReducedFrom:{} filterMethod}
                &&filterMethod.ContainingType.ToDisplayString()=="System.Linq.Enumerable"){
                source=filterAccess.Expression;
            }
            if(model.GetSymbolInfo(source).Symbol is not IPropertySymbol{Type:INamedTypeSymbol tableType} table
                ||!SymbolEqualityComparer.Default.Equals(tableType.OriginalDefinition,model.Compilation.GetTypeByMetadataName("Seamless.Database.Table`2"))
                ||tableType.TypeArguments.Length!=2||!IsPushdownScalar(tableType.TypeArguments[0])
                ||table.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not PropertyDeclarationSyntax{ExpressionBody.Expression:InvocationExpressionSyntax tableCall}){
                reason="DirectTableKeyNotProven";return null;
            }
            var tableModel=model.Compilation.GetSemanticModel(tableCall.SyntaxTree);
            if(tableCall.ArgumentList.Arguments.Count==0||tableModel.GetSymbolInfo(tableCall).Symbol is not IMethodSymbol{Name:"Table"} tableFactory
                ||tableFactory.ContainingType.ToDisplayString()!="Seamless.Database.DatabaseContainer"){
                reason="DirectTableKeyNotProven";return null;
            }
            ISymbol? key=null;
            if(GetDeclaredLambdaKey(tableModel,tableCall,default) is{} declared){key=declared.Member;}
            else{key=DirectJoinKeyMethodMember(tableCall.ArgumentList.Arguments[0].Expression,tableModel);}
            if(key is null||!columns.Values.Contains(key,SymbolEqualityComparer.Default)){
                reason="ProjectedSourceKeyNotProven";return null;
            }
            reason="MappedBuiltinSourceKey";
            return new(function.Symbol.Parameters[0].Type.ToDisplayString(完全修飾形式),key,columns);
        }
        private static bool IsPushdownScalar(ITypeSymbol? type)=>type?.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Boolean or SpecialType.System_Char;
    }
}
