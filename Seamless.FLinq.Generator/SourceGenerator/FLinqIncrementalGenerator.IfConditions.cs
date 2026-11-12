using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.FLinq.Generator;

public sealed partial class FLinqIncrementalGenerator{
    internal sealed partial class GenerationPipeline{
        private bool TryRenderIfCondition(
            ExpressionSyntax condition,SemanticModel semanticModel,INamedTypeSymbol containingType,
            ref int uniqueNumber,bool methodIsStatic,ref string staticMethods,
            List<string> unsafeAccessors,List<Fragment> fragments,StringBuilder builder,
            ref int invocationCount,HashSet<string> usedNames,out string rendered){
            rendered=string.Empty;
            var invocations=this.外側LINQ呼び出し取得_式(condition,semanticModel);
            if(invocations.Count==0){
                rendered=RenderExpressionForGeneratedMethod(condition,semanticModel,containingType,unsafeAccessors);
                return true;
            }
            if(condition.DescendantNodesAndSelf().Any(static node=>node is DeclarationExpressionSyntax or SingleVariableDesignationSyntax or AwaitExpressionSyntax or ConditionalAccessExpressionSyntax)){
                return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionDeclarationsAwaitOrConditionalAccessNotSupported",out rendered);
            }
            if(condition.DescendantNodesAndSelf().Any(static node=>node is CheckedExpressionSyntax)
                ||condition.Ancestors().TakeWhile(static node=>node is not MethodDeclarationSyntax).Any(static node=>node is CheckedExpressionSyntax or CheckedStatementSyntax)){
                return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionExplicitOverflowContextNotSupported",out rendered);
            }
            if(!methodIsStatic&&containingType.IsValueType){
                return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionStructReceiverCaptureNotSupported",out rendered);
            }
            var method=condition.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if(method?.Body is null){
                return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionRequiresMethodBlock",out rendered);
            }
            var calls=new Dictionary<InvocationExpressionSyntax,string>(InvocationSyntaxReferenceComparer.Instance);
            var declarations=new List<StatementSyntax>();
            var functions=new StringBuilder();
            foreach(var invocation in invocations){
                var resultType=semanticModel.GetTypeInfo(invocation).Type;
                if(resultType is null||!IsConditionScalarType(resultType)){
                    return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionRequiresScalarTerminal",out rendered);
                }
                var flow=semanticModel.AnalyzeDataFlow(invocation);
                if(flow is null||!flow.Succeeded){
                    return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionCaptureAnalysisUnavailable",out rendered);
                }
                foreach(var captured in flow.DataFlowsIn){
                    ITypeSymbol? capturedType=null;
                    if(captured is IParameterSymbol parameter){
                        if(parameter.RefKind!=RefKind.None){
                            return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionByReferenceCaptureNotSupported",out rendered);
                        }
                        capturedType=parameter.Type;
                    }else if(captured is ILocalSymbol local){
                        if(local.RefKind!=RefKind.None){
                            return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionByReferenceCaptureNotSupported",out rendered);
                        }
                        capturedType=local.Type;
                    }
                    if(capturedType is INamedTypeSymbol{IsRefLikeType:true}){
                        return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionRefLikeCaptureNotSupported",out rendered);
                    }
                    if(capturedType is ITypeParameterSymbol{AllowsRefLikeType:true}){
                        return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionRefLikeTypeParameterCaptureNotSupported",out rendered);
                    }
                }
            }
            // Probe support in an independent state. Failed attempts must not add
            // fragments, counters, trace events or allocated names to this method.
            var trialState=new GenerationState(this.state.Target);
            trialState.UsedIdentifiers.UnionWith(usedNames);
            trialState.BlockScopeIdentifiers.UnionWith(this.state.BlockScopeIdentifiers);
            trialState.UserIdentifiers.UnionWith(this.state.UserIdentifiers);
            var trial=new GenerationPipeline(trialState);
            var trialNumber=uniqueNumber;
            var trialCount=0;
            var trialMethods=string.Empty;
            var supported=true;
            using(GeneratedNames.Begin(trialState)){
                foreach(var invocation in invocations){
                    var trialBody=new StringBuilder();
                    if(!trial.LINQ呼び出し差し替え式本体生成(invocation,semanticModel,containingType,ref trialNumber,methodIsStatic,false,ref trialMethods,trialState.UnsafeAccessorCodes,trialState.GeneratedFragments,trialBody,ref trialCount,trialState.UsedIdentifiers)){
                        supported=false;
                        break;
                    }
                }
            }
            if(!supported){
                return this.PreserveIfCondition(condition,semanticModel,containingType,unsafeAccessors,builder,"IfConditionTerminalExpansionUnavailable",out rendered);
            }
            foreach(var invocation in invocations){
                var resultType=semanticModel.GetTypeInfo(invocation).Type!;
                var name=AllocateName(usedNames,"conditionTerminal");
                var body=new StringBuilder();
                if(!this.LINQ呼び出し差し替え式本体生成(invocation,semanticModel,containingType,ref uniqueNumber,methodIsStatic,false,ref staticMethods,unsafeAccessors,fragments,body,ref invocationCount,usedNames)){
                    this.state.FailureReason="IfConditionTerminalExpansionUnavailable";
                    return false;
                }
                var typeName=resultType.ToDisplayString(完全修飾形式);
                functions.AppendLine($"{typeName} {name}(){{");
                functions.Append(body);
                functions.AppendLine("}");
                calls.Add(invocation,name);
                // The virtual declaration is used only to bind the surrounding expression.
                // The emitted function above contains the actual Fragment execution.
                declarations.Add(SyntaxFactory.ParseStatement($"{typeName} {name}()=>default!;"));
            }
            var annotation=new SyntaxAnnotation();
            var rewritten=condition.ReplaceNodes(invocations,(original,_)=>
                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(calls[original])).WithTriviaFrom(original))
                .WithAdditionalAnnotations(annotation);
            var rewrittenMethod=method.ReplaceNode(condition,rewritten);
            rewrittenMethod=rewrittenMethod.WithBody(rewrittenMethod.Body!.WithStatements(rewrittenMethod.Body.Statements.InsertRange(0,declarations)));
            var tree=semanticModel.SyntaxTree;
            var newRoot=tree.GetRoot().ReplaceNode(method,rewrittenMethod);
            var newTree=tree.WithRootAndOptions(newRoot,tree.Options);
            var compilation=semanticModel.Compilation.ReplaceSyntaxTree(tree,newTree);
            var model=compilation.GetSemanticModel(newTree);
            var expression=newTree.GetRoot().GetAnnotatedNodes(annotation).OfType<ExpressionSyntax>().Single();
            var newContainingType=model.GetEnclosingSymbol(expression.SpanStart)?.ContainingType;
            if(newContainingType is null){
                this.state.FailureReason="IfConditionRebindingUnavailable";
                return false;
            }
            rendered=RenderExpressionForGeneratedMethod(expression,model,newContainingType,unsafeAccessors);
            builder.Append(functions);
            return true;
        }

        private bool PreserveIfCondition(ExpressionSyntax condition,SemanticModel model,INamedTypeSymbol containingType,
            List<string> unsafeAccessors,StringBuilder builder,string reason,out string rendered){
            this.state.FailureReason=reason;
            builder.AppendLine("// [条件/未展開] "+reason+"。元の条件と評価位置を保持する。");
            rendered=RenderExpressionForGeneratedMethod(condition,model,containingType,unsafeAccessors);
            return true;
        }

        private static bool IsConditionScalarType(ITypeSymbol type){
            if(type is INamedTypeSymbol named&&named.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T){
                type=named.TypeArguments[0];
            }
            return type.TypeKind==TypeKind.Enum||type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Char
                or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16
                or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64
                or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal or SpecialType.System_String;
        }
    }
}
