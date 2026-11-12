using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator{
    internal sealed partial class GenerationPipeline{
        private readonly HashSet<ISymbol> activeViewDefinitions=new(SymbolEqualityComparer.Default);

        private bool TryBuildViewDefinitionRoot(SemanticModel model,ExpressionSyntax expression,INamedTypeSymbol containingType,
            ref int uniqueNumber,out Enumerable fragment,List<string>? unsafeAccessors){
            fragment=null!;
            if(model.GetSymbolInfo(expression).Symbol is not IPropertySymbol property
                ||property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not PropertyDeclarationSyntax declaration
                ||declaration.ExpressionBody?.Expression is not InvocationExpressionSyntax queryCall){return false;}
            var getterModel=model.Compilation.GetSemanticModel(declaration.SyntaxTree);
            if(getterModel.GetSymbolInfo(queryCall).Symbol is not IMethodSymbol{IsStatic:true,Name:"Query" or "Rows",Parameters.Length:1} query
                ||query.ContainingNamespace.ToDisplayString().Split('.').All(static segment=>segment!="Views")
                ||!IsViewDatabaseType(query.Parameters[0].Type)
                ||query.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax method
                ||!TryGetSingleExpressionBody(method,out var queryBody)
                ||queryBody is not InvocationExpressionSyntax factory){return false;}
            var definitionModel=model.Compilation.GetSemanticModel(method.SyntaxTree);
            if(definitionModel.GetOperation(factory) is not IInvocationOperation factoryCall
                ||factoryCall.TargetMethod.ContainingType.ToDisplayString()!="Seamless.Database.DatabaseContainer"){return false;}
            var factoryMethod=factoryCall.TargetMethod;
            // [ビュー/入口] DB生成器が作るRowsはGeneratedViewだけを返す。利用者が書くファクトリーの形は持たない。
            if(factoryMethod is not{Name:"GeneratedView",Arity:1}){return false;}
            var element=factoryMethod.TypeArguments[factoryMethod.TypeArguments.Length-1].ToDisplayString(完全修飾形式);
            var source=RenderExpressionForGeneratedMethod(expression,model,containingType,unsafeAccessors);
            string? unsupported=null;
            if(this.state.Target.Method.Parameters.Any(static parameter=>IsViewTransactionType(parameter.Type))){unsupported="ExplicitSnapshotNotBoundToDefinition";}
            if(unsupported is not null){
                fragment=new OpaqueViewRoot(uniqueNumber++,source,element,unsupported);
                return true;
            }
            if(!this.activeViewDefinitions.Add(query)){
                fragment=new OpaqueViewRoot(uniqueNumber++,source,element,"RecursiveDefinitionNotExpanded");
                return true;
            }
            try{
                if(factory.Expression is not MemberAccessExpressionSyntax access
                    ||!SymbolEqualityComparer.Default.Equals(definitionModel.GetSymbolInfo(access.Expression).Symbol,query.Parameters[0])
                    ||ViewFactoryArgument(factoryCall,0) is not ParenthesizedLambdaExpressionSyntax{ParameterList.Parameters.Count:0} lambda
                    ||!TryViewDefinitionBody(lambda,out var body)){
                    fragment=new OpaqueViewRoot(uniqueNumber++,source,element,"DefinitionRequiresSingleReturn");
                    return true;
                }
                var factoryArgumentsConstant=factoryCall.Arguments.All(static argument=>argument.Parameter?.Name is "rows"||argument.Value.ConstantValue.HasValue);
                var setEqualityIsDefault=HasDefaultSetEquality(factoryMethod.TypeArguments[factoryMethod.TypeArguments.Length-1]);
                if(!this.列挙式フラグメント生成(definitionModel,body,query.ContainingType,ref uniqueNumber,out var inner,out var methods,unsafeAccessors)){
                    fragment=new OpaqueViewRoot(uniqueNumber++,source,element,"DefinitionBodyUnsupported");
                    return true;
                }
                var projection=DescribeViewProjection(definitionModel,body,out var projectionReason);
                fragment=new ViewDefinitionRoot(uniqueNumber++,source,element,query.Parameters[0].Type.ToDisplayString(完全修飾形式),query.Parameters[0].Name,inner,methods){Projection=projection,ProjectionReason=projectionReason,
                    FactoryArgumentsConstant=factoryArgumentsConstant,SetEqualityIsDefault=setEqualityIsDefault};
                return true;
            }finally{
                this.activeViewDefinitions.Remove(query);
            }
        }

        private static ExpressionSyntax? ViewFactoryArgument(IInvocationOperation call,int ordinal)=>
            (call.Arguments.FirstOrDefault(argument=>argument.Parameter?.Ordinal==ordinal)?.Syntax as ArgumentSyntax)?.Expression;

        private static bool TryViewDefinitionBody(ParenthesizedLambdaExpressionSyntax lambda,out ExpressionSyntax body){
            if(lambda.Body is ExpressionSyntax expression){body=expression;return true;}
            if(lambda.Body is BlockSyntax{Statements.Count:1} block&&block.Statements[0] is ReturnStatementSyntax{Expression:{} returned}){
                body=returned;return true;
            }
            body=null!;return false;
        }
        // Viewの集合境界は保存表現の比較を使う。整数・bool・charだけの読取専用record structで利用者定義のメンバーがなければ、
        // 生成コードの通常のDistinct(既定の等価比較)と同じ結果になる。浮動小数点・decimal・文字列・参照型は正規化差があり得るため除く。
        private static bool HasDefaultSetEquality(ITypeSymbol type){
            if(type is not INamedTypeSymbol{IsRecord:true,TypeKind:TypeKind.Struct,IsReadOnly:true} record||record.StaticConstructors.Length!=0){return false;}
            var declarations=record.DeclaringSyntaxReferences.Select(static reference=>reference.GetSyntax()).ToArray();
            if(declarations.Length==0||declarations.Any(static syntax=>syntax is not RecordDeclarationSyntax part
                ||part.Members.Any(static member=>!member.Modifiers.Any(SyntaxKind.StaticKeyword)))){return false;}
            var parameters=declarations.OfType<RecordDeclarationSyntax>().Select(static part=>part.ParameterList).Where(static list=>list is not null).ToArray();
            if(parameters.Length!=1||parameters[0]!.Parameters.Count==0){return false;}
            var fields=record.GetMembers().OfType<IPropertySymbol>().Where(static property=>!property.IsStatic&&!property.IsIndexer).ToArray();
            return fields.Length==parameters[0]!.Parameters.Count&&fields.All(static field=>IsPushdownScalar(field.Type));
        }
        private static bool IsViewDatabaseType(ITypeSymbol type){
            for(var current=type as INamedTypeSymbol;current is not null;current=current.BaseType){
                if(current.ToDisplayString()=="Seamless.Database.DatabaseContainer"){return true;}
            }
            return false;
        }
        private static bool IsViewTransactionType(ITypeSymbol type)=>type is INamedTypeSymbol{Name:"TypedTransaction",ContainingType:{} owner}&&IsViewDatabaseType(owner);
    }
}
