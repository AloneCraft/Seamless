using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

// 中間段の追加。
internal static partial class PipelineBuilder {
    private static bool TryAppendStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        var methodName = (method.ReducedFrom??method).Name;
        switch(methodName) {
            case "Where":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)) {
                    return false;
                }
                if(!services.CanUseFunc(predicate)) {
                    return false;
                }
                stages.Add(new PipelineStage(PipelineStageKind.Where,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
                return true;
            case "Select":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } selectorType) {
                    return false;
                }
                if(stages.Count>0&&stages[stages.Count-1].Kind==PipelineStageKind.GroupBy&&stages[stages.Count-1].ThirdFunc is null&&stages[stages.Count-1].AggregatePlan is null) {
                    var lastGroupBy = stages[stages.Count-1];
                    if(invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax selectLambda
                        &&selectorType.TypeArguments.Length==2
                        &&!services.ContainsAnonymousType(selectorType.TypeArguments[1])) {
                        if(services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selectFunc)
                            &&services.CanUseFunc(selectFunc)) {
                            if(GroupByAggregateFusion.TryAnalyzeSelect(selectLambda,selectFunc,semanticModel,lastGroupBy.KeyType!,lastGroupBy.InnerElementType!,services,ref uniqueNumber,out var aggPlan)) {
                                // 融合は後で決める(GroupByFusionPass)。ここでは、候補を持つ通常の Select の段にする。
                                stages.Add(new PipelineStage(PipelineStageKind.Select,selectFunc,null,currentElementType,selectFunc.ReturnTypeName,aggregateCandidate: aggPlan));
                                currentElementType=selectFunc.ReturnTypeName;
                                return true;
                            }
                            if(GroupBySupport.SelectIgnoresGroup(selectFunc)) {
                                stages[stages.Count-1]=new PipelineStage(
                                    PipelineStageKind.GroupBy,
                                    lastGroupBy.Func,
                                    lastGroupBy.Value,
                                    lastGroupBy.InputType,
                                    selectFunc.ReturnTypeName,
                                    secondFunc: lastGroupBy.SecondFunc,
                                    thirdFunc: selectFunc,
                                    innerElementType: lastGroupBy.InnerElementType,
                                    keyType: lastGroupBy.KeyType,
                                    containsAnchor: lastGroupBy.ContainsAnchor||selectFunc.ContainsAnchor);
                                currentElementType=selectFunc.ReturnTypeName;
                                return true;
                            }
                        }
                    }
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,selectorType,ref uniqueNumber,out var selector)) {
                    return false;
                }
                if(!services.CanUseFunc(selector)) {
                    return false;
                }
                var selectElementType = selector.ReturnTypeName;
                stages.Add(new PipelineStage(PipelineStageKind.Select,selector,null,currentElementType,selectElementType,usesIndex: selectorType.TypeArguments.Length==3));
                currentElementType=selectElementType;
                return true;
            case "SelectMany":
                return TryAppendSelectManyStage(semanticModel,invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Distinct":
                return TryAppendDistinctStage(invocation,method,services,stages,currentElementType);
            case "DistinctBy":
                return TryAppendDistinctByStage(invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Union":
                return TryAppendBinarySetStage(PipelineStageKind.Union,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Except":
                return TryAppendBinarySetStage(PipelineStageKind.Except,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Intersect":
                return TryAppendBinarySetStage(PipelineStageKind.Intersect,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "UnionBy":
                return TryAppendBinarySetByStage(PipelineStageKind.UnionBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ExceptBy":
                return TryAppendBinarySetByStage(PipelineStageKind.ExceptBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "IntersectBy":
                return TryAppendBinarySetByStage(PipelineStageKind.IntersectBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Concat":
                return TryAppendConcatStage(invocation,services,ref uniqueNumber,stages,currentElementType);
            case "Append":
                return TryAppendElementStage(PipelineStageKind.Append,invocation,services,stages,currentElementType);
            case "Prepend":
                return TryAppendElementStage(PipelineStageKind.Prepend,invocation,services,stages,currentElementType);
            case "OrderBy":
                return TryAppendOrderStage(PipelineStageKind.OrderBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "OrderByDescending":
                return TryAppendOrderStage(PipelineStageKind.OrderByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenBy":
                return TryAppendOrderStage(PipelineStageKind.ThenBy,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "ThenByDescending":
                return TryAppendOrderStage(PipelineStageKind.ThenByDescending,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "GroupBy":
                return TryAppendGroupByStage(semanticModel,invocation,method,services,ref uniqueNumber,stages,ref currentElementType);
            case "Reverse":
                stages.Add(new PipelineStage(PipelineStageKind.Reverse,null,null,currentElementType,currentElementType));
                return true;
            case "DefaultIfEmpty":
                return TryAppendDefaultIfEmptyStage(invocation,services,stages,currentElementType);
            case "AsEnumerable":
                stages.Add(new PipelineStage(PipelineStageKind.AsEnumerable,null,null,currentElementType,currentElementType));
                return true;
            case "Skip":
                return TryAppendCountStage(PipelineStageKind.Skip,invocation,method,services,stages,currentElementType);
            case "TakeWhile":
                return TryAppendPredicateStage(PipelineStageKind.TakeWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "SkipWhile":
                return TryAppendPredicateStage(PipelineStageKind.SkipWhile,invocation,method,services,ref uniqueNumber,stages,currentElementType);
            case "Cast":
                return TryAppendTypeStage(PipelineStageKind.Cast,method,services,stages,ref currentElementType);
            case "OfType":
                return TryAppendTypeStage(PipelineStageKind.OfType,method,services,stages,ref currentElementType);
            case "Order":
            case "OrderDescending": {
                var descending = methodName=="OrderDescending";
                var kind = descending ? PipelineStageKind.OrderByDescending : PipelineStageKind.OrderBy;
                var comparer = "null";
                var containsAnchor = false;
                if(invocation.ArgumentList.Arguments.Count==1
                    &&method.Parameters.Length>=1) {
                    var comparerExpression = invocation.ArgumentList.Arguments[0].Expression;
                    comparer=services.RenderExpression(comparerExpression);
                    containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
                } else if(invocation.ArgumentList.Arguments.Count>1) {
                    return false;
                }
                // Func=null signals identity key (element itself)
                stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,keyType: currentElementType,descending: descending,containsAnchor: containsAnchor));
                return true;
            }
            case "SkipLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var skipLastExpression = invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.SkipLast,null,services.RenderExpression(skipLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(skipLastExpression,null)));
                return true;
            case "TakeLast":
                if(invocation.ArgumentList.Arguments.Count!=1
                    ||method.Parameters.Length==0
                    ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
                    return false;
                }
                var takeLastExpression = invocation.ArgumentList.Arguments[0].Expression;
                stages.Add(new PipelineStage(PipelineStageKind.TakeLast,null,services.RenderExpression(takeLastExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeLastExpression,null)));
                return true;
            case "Take":
                if(invocation.ArgumentList.Arguments.Count!=1||method.Parameters.Length==0) {
                    return false;
                }
                if(method.Parameters[0].Type.SpecialType==SpecialType.System_Int32) {
                    return TryAppendCountStage(PipelineStageKind.Take,invocation,method,services,stages,currentElementType);
                }
                // Range overload
                if(method.Parameters[0].Type.ToDisplayString()=="global::System.Range"
                    ||method.Parameters[0].Type.ToDisplayString()=="System.Range") {
                    var takeRangeExpression = invocation.ArgumentList.Arguments[0].Expression;
                    stages.Add(new PipelineStage(PipelineStageKind.TakeRange,null,services.RenderExpression(takeRangeExpression),currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(takeRangeExpression,null)));
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool TryAppendCountStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type.SpecialType!=SpecialType.System_Int32) {
            return false;
        }
        var countExpression = invocation.ArgumentList.Arguments[0].Expression;
        var count = services.RenderExpression(countExpression);
        stages.Add(new PipelineStage(kind,null,count,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(countExpression,null)));
        return true;
    }

    private static bool TryAppendPredicateStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1
            ||method.Parameters.Length==0
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 or 3 } predicateType) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,predicateType,ref uniqueNumber,out var predicate)
            ||!services.CanUseFunc(predicate)) {
            return false;
        }
        stages.Add(new PipelineStage(kind,predicate,null,currentElementType,currentElementType,usesIndex: predicateType.TypeArguments.Length==3));
        return true;
    }

    private static bool TryAppendTypeStage(
        PipelineStageKind kind,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(!services.TryGetEnumerableElementType(method.ReturnType,out var resultElementType)
            ||services.ContainsAnonymousType(resultElementType)) {
            return false;
        }
        var outputType = resultElementType.ToDisplayString(services.FullyQualifiedFormat);
        stages.Add(new PipelineStage(kind,null,null,currentElementType,outputType));
        currentElementType=outputType;
        return true;
    }

    private static bool TryAppendSelectManyStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        // 匿名型要素は型名を生成コードに書けず ToList 等の終端で破綻するため、
        // SelectMany インライン化（record struct 置換 + 出口 Select）のフォールバック経路へ委ねる。
        if(method.TypeArguments.Any(services.ContainsAnonymousType)) {
            return false;
        }
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length!=invocation.ArgumentList.Arguments.Count
            ||method.Parameters[0].Type is not INamedTypeSymbol collectionSelectorType) {
            return false;
        }
        var typeArgumentLength = collectionSelectorType.TypeArguments.Length;
        if(typeArgumentLength is not (2 or 3)) {
            return false;
        }
        var usesIndex = typeArgumentLength==3;
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var collectionSelector)
            ||!services.CanUseFunc(collectionSelector)) {
            return false;
        }
        if(!TryGetSequenceElementType(collectionSelector.ReturnTypeName,out var innerElementType)) {
            return false;
        }
        // Repeat(e,1) の判定は、ラムダの中の LINQ のために文として展開が必要と判定される前に行う(本体は使わず e だけを使う)。
        if(!usesIndex
            &&TryCreateSingleRepeatSelect(semanticModel,invocation,method,services,ref uniqueNumber,collectionSelector,innerElementType,out var singleSelect,out var singleOutputType)) {
            stages.Add(new PipelineStage(PipelineStageKind.Select,singleSelect,null,currentElementType,singleOutputType));
            currentElementType=singleOutputType;
            return true;
        }
        if(collectionSelector.RequiresStatementInlining) {
            return false;
        }
        var collectionSelectorExpr = invocation.ArgumentList.Arguments[0].Expression;
        ITypeSymbol? innerReturnType = null;
        if(collectionSelectorExpr is LambdaExpressionSyntax lambdaExpr) {
            if(lambdaExpr.Body is ExpressionSyntax bodyExpr) {
                innerReturnType=semanticModel.GetTypeInfo(bodyExpr).Type;
            } else if(semanticModel.GetSymbolInfo(lambdaExpr).Symbol is IMethodSymbol lambdaMethod) {
                innerReturnType=lambdaMethod.ReturnType;
            }
        }

        var innerIsArray = innerReturnType is IArrayTypeSymbol;
        var innerIsList = innerReturnType is INamedTypeSymbol listType
            &&listType.OriginalDefinition.ToDisplayString()=="System.Collections.Generic.List<T>";
        var innerIsImmutableArray = innerReturnType is INamedTypeSymbol immType
            &&immType.OriginalDefinition.ToDisplayString()=="System.Collections.Immutable.ImmutableArray<T>";

        FuncFragment? resultSelector = null;
        var outputType = innerElementType;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }) {
                return false;
            }
            if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out resultSelector)
                ||!services.CanUseFunc(resultSelector)) {
                return false;
            }
            outputType=resultSelector.ReturnTypeName;
        }

        stages.Add(new PipelineStage(
            PipelineStageKind.SelectMany,
            collectionSelector,
            null,
            currentElementType,
            outputType,
            secondFunc: resultSelector,
            innerElementType: innerElementType,
            keyType: null,
            usesIndex: usesIndex,
            innerIsArray: innerIsArray,
            innerIsList: innerIsList,
            innerIsImmutableArray: innerIsImmutableArray));
        currentElementType=outputType;
        return true;
    }

    // SelectMany(x => Enumerable.Repeat(e,1)[,(x,y) => r]) は、要素ごとにちょうど1つを流すので Select(x => e) / Select(x => r(x,e)) と同じ。
    // FLinq の契約(.Anchor の無い式は純粋)で e は1回評価すればよい。結果選択子の中で y を何度使っても e は同じ値になる。
    // e は、ラムダの引数・定数・演算子・型変換だけの式に限る(そのまま生成コードに書ける形)。
    private static bool TryCreateSingleRepeatSelect(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        FuncFragment collectionSelector,
        string innerElementType,
        out LambdaFunc select,
        out string outputType) {
        select=null!;
        outputType=innerElementType;
        if(invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax { Body: InvocationExpressionSyntax repeatInvocation } lambda
            ||semanticModel.GetSymbolInfo(repeatInvocation).Symbol is not IMethodSymbol { Name: "Repeat" } repeat
            ||repeat.ContainingType?.ToDisplayString()!="System.Linq.Enumerable"
            ||repeatInvocation.ArgumentList.Arguments.Count!=2
            ||semanticModel.GetConstantValue(repeatInvocation.ArgumentList.Arguments[1].Expression) is not { HasValue: true,Value: 1 }
            ||collectionSelector.ContainsAnchor
            ||collectionSelector.ParameterNames.Length!=1) {
            return false;
        }
        var parameter = collectionSelector.ParameterNames[0];
        var elementExpression = repeatInvocation.ArgumentList.Arguments[0].Expression;
        if(!IsParameterOnlyExpression(elementExpression,parameter)) {
            return false;
        }
        var element = $"(({innerElementType})({elementExpression}))";
        FuncFragment? resultSelector = null;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 3 }
                ||!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out resultSelector)
                ||!services.CanUseFunc(resultSelector)
                ||resultSelector.ContainsAnchor
                ||resultSelector.CanInvokeAsDelegate
                ||!resultSelector.CanInlineAssign
                ||resultSelector.RequiresStatementInlining) {
                return false;
            }
            outputType=resultSelector.ReturnTypeName;
        }
        string body;
        try {
            body=resultSelector is null ? element : resultSelector.RenderInlineExpression(parameter,element);
        } catch(InvalidOperationException) {
            return false;
        }
        select=new LambdaFunc {
            ReturnTypeName=outputType,
            ParameterTypeNames=collectionSelector.ParameterTypeNames.ToArray(),
            ParameterNames=[parameter],
            CanGenerateMethod=resultSelector?.CanGenerateMethod??true,
            CanInlineAssign=true,
            InlineBody=body,
            CapturedLocals=resultSelector?.CapturedLocals??[],
            HoistedLocals=resultSelector?.HoistedLocals??[],
            BodyUnsafeAccessorCodes=resultSelector?.BodyUnsafeAccessorCodes??[],
            SupportingMemberSources=resultSelector?.SupportingMemberSources??string.Empty,
        };
        return true;
    }

    private static bool IsParameterOnlyExpression(ExpressionSyntax expression,string parameter) =>
        expression.DescendantNodesAndSelf().All(node => node switch {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText==parameter,
            PredefinedTypeSyntax or LiteralExpressionSyntax or ParenthesizedExpressionSyntax or CastExpressionSyntax
                or BinaryExpressionSyntax or PrefixUnaryExpressionSyntax => true,
            _ => false,
        });

    private static bool TryGetSequenceElementType(string sequenceTypeName,out string elementTypeName) {
        elementTypeName=string.Empty;
        if(sequenceTypeName.EndsWith("[]",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName.Substring(0,sequenceTypeName.Length-2);
            return true;
        }
        const string enumerablePrefix = "global::System.Collections.Generic.IEnumerable<";
        if(sequenceTypeName.StartsWith(enumerablePrefix,StringComparison.Ordinal)
            &&sequenceTypeName.EndsWith(">",StringComparison.Ordinal)) {
            elementTypeName=sequenceTypeName.Substring(enumerablePrefix.Length,sequenceTypeName.Length-enumerablePrefix.Length-1);
            return true;
        }
        return false;
    }

    private static bool TryAppendDistinctStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            if(method.Parameters.Length<1) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[0].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.Distinct,null,comparer,currentElementType,currentElementType,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendDistinctByStage(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DistinctBy,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendBinarySetByStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (2 or 3)
            ||method.Parameters.Length<2
            ||method.Parameters[1].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[1].Expression,method.Parameters[1].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==3) {
            if(method.Parameters.Length<3) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[2].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,secondSource: secondSource,keyType: keySelector.ReturnTypeName,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendConcatStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        if(!services.BuildEnumerable(invocation.ArgumentList.Arguments[0].Expression,ref uniqueNumber,out var secondSource,out _)) {
            return false;
        }
        stages.Add(new PipelineStage(PipelineStageKind.Concat,null,null,currentElementType,currentElementType,secondSource: secondSource));
        return true;
    }

    private static bool TryAppendElementStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count!=1) {
            return false;
        }
        var elementExpression = invocation.ArgumentList.Arguments[0].Expression;
        var element = services.RenderExpression(elementExpression);
        stages.Add(new PipelineStage(kind,null,element,currentElementType,currentElementType,containsAnchor: AnchorAnalysis.ContainsAnchor(elementExpression,null)));
        return true;
    }

    private static bool TryAppendDefaultIfEmptyStage(
        InvocationExpressionSyntax invocation,
        PipelineBuildServices services,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count>1) {
            return false;
        }
        var defaultValue = "default!";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==1) {
            var defaultValueExpression = invocation.ArgumentList.Arguments[0].Expression;
            defaultValue=services.RenderExpression(defaultValueExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(defaultValueExpression,null);
        }
        stages.Add(new PipelineStage(PipelineStageKind.DefaultIfEmpty,null,defaultValue,currentElementType,currentElementType,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendOrderStage(
        PipelineStageKind kind,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is not (1 or 2)
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
            &&!stages.Any(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        var comparer = "null";
        var containsAnchor = false;
        if(invocation.ArgumentList.Arguments.Count==2) {
            if(method.Parameters.Length<2) {
                return false;
            }
            var comparerExpression = invocation.ArgumentList.Arguments[1].Expression;
            comparer=services.RenderExpression(comparerExpression);
            containsAnchor=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
        }
        var descending = kind is PipelineStageKind.OrderByDescending or PipelineStageKind.ThenByDescending;
        // 恒等のキー選択子(x => x)は Order()/OrderDescending() と同じ(キーを計算しない経路)にする。.Anchor 付きは評価を残す。
        if(keySelector.IsIdentity
            &&!keySelector.ContainsAnchor
            &&keySelector.ReturnTypeName==currentElementType) {
            stages.Add(new PipelineStage(kind,null,comparer,currentElementType,currentElementType,keyType: currentElementType,descending: descending,containsAnchor: containsAnchor));
            return true;
        }
        stages.Add(new PipelineStage(kind,keySelector,comparer,currentElementType,currentElementType,keyType: keySelector.ReturnTypeName,descending: descending,containsAnchor: containsAnchor));
        return true;
    }

    private static bool TryAppendGroupByStage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        PipelineBuildServices services,
        ref int uniqueNumber,
        List<PipelineStage> stages,
        ref string currentElementType) {
        if(invocation.ArgumentList.Arguments.Count is <1 or >4
            ||method.Parameters.Length<1
            ||method.Parameters[0].Type is not INamedTypeSymbol { TypeArguments.Length: 2 }) {
            return false;
        }
        if(!services.BuildFunc(invocation.ArgumentList.Arguments[0].Expression,method.Parameters[0].Type,ref uniqueNumber,out var keySelector)
            ||!services.CanUseFunc(keySelector)) {
            return false;
        }
        FuncFragment? elementSelector = null;
        FuncFragment? resultSelector = null;
        var comparer = "null";
        var containsAnchor = false;
        for(var index = 1;index<invocation.ArgumentList.Arguments.Count;index++) {
            if(method.Parameters.Length<=index) {
                return false;
            }
            if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 2 } elementFuncType) {
                if(elementSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,elementFuncType,ref uniqueNumber,out elementSelector)
                    ||!services.CanUseFunc(elementSelector)) {
                    return false;
                }
            } else if(method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 3 } resultFuncType) {
                if(resultSelector is not null) {
                    return false;
                }
                if(!services.BuildFunc(invocation.ArgumentList.Arguments[index].Expression,resultFuncType,ref uniqueNumber,out resultSelector)
                    ||!services.CanUseFunc(resultSelector)) {
                    return false;
                }
            } else {
                var comparerExpression = invocation.ArgumentList.Arguments[index].Expression;
                comparer=services.RenderExpression(comparerExpression);
                containsAnchor|=AnchorAnalysis.ContainsAnchor(comparerExpression,null);
            }
        }
        var groupElementType = elementSelector?.ReturnTypeName??currentElementType;
        var outputType = resultSelector?.ReturnTypeName??$"global::System.Linq.IGrouping<{keySelector.ReturnTypeName},{groupElementType}>";
        GroupByAggregatePlan? aggregateCandidate = null;
        if(resultSelector is not null) {
            for(var index = 1;index<invocation.ArgumentList.Arguments.Count;index++) {
                if(invocation.ArgumentList.Arguments[index].Expression is LambdaExpressionSyntax resLambda
                    &&method.Parameters[index].Type is INamedTypeSymbol { TypeArguments.Length: 3 } resultFuncType
                    &&!services.ContainsAnonymousType(resultFuncType.TypeArguments[2])
                    &&GroupByAggregateFusion.TryAnalyzeResultSelector(resLambda,resultSelector,semanticModel,keySelector.ReturnTypeName,groupElementType,services,ref uniqueNumber,out var plan)) {
                    // 融合は後で決める(GroupByFusionPass)。
                    aggregateCandidate=plan;
                    break;
                }
            }
        }
        stages.Add(new PipelineStage(
            PipelineStageKind.GroupBy,
            keySelector,
            comparer,
            currentElementType,
            outputType,
            secondFunc: elementSelector,
            thirdFunc: resultSelector,
            innerElementType: groupElementType,
            keyType: keySelector.ReturnTypeName,
            containsAnchor: containsAnchor,
            aggregateCandidate: aggregateCandidate));
        currentElementType=outputType;
        return true;
    }

    // 結果選択子がグループを使わない GroupBy は、キー選択子の Select・Distinct・キーから結果を作る Select の3段に置き換える。
    // グループを作らず、要素選択子も評価しない(FLinq の契約。.Anchor の条件は GroupBySupport.CanStreamDistinctKeys と同じで、
    // 逐次流すと後段が途中で止めたときに前段とソースの評価が減るので、前段かソースに .Anchor があれば置き換えない)。
    private static List<PipelineStage> StreamGroupByKeysWhenResultIgnoresGroup(Enumerable source,List<PipelineStage> stages) {
        var rewritten = new List<PipelineStage>(stages.Count+4);
        for(var index = 0;index<stages.Count;index++) {
            var stage = stages[index];
            if(stage.Kind!=PipelineStageKind.GroupBy
                ||stage.Func is null
                ||stage.KeyType is not { } keyType
                ||stage.ContainsAnchor
                ||stages.Take(index).Any(static previous => previous.ContainsAnchor)
                ||source.TreeContainsAnchor()) {
                rewritten.Add(stage);
                continue;
            }

            if(stage.ThirdFunc is LambdaFunc resultSelector&&GroupBySupport.ResultIgnoresGroup(resultSelector)) {
                var resultFromKey = new LambdaFunc {
                    ReturnTypeName=resultSelector.ReturnTypeName,
                    ParameterTypeNames=[resultSelector.ParameterTypeNames[0]],
                    ParameterNames=[resultSelector.ParameterNames[0]],
                    CanGenerateMethod=resultSelector.CanGenerateMethod,
                    CanInlineAssign=true,
                    InlineBody=resultSelector.InlineBody,
                    CapturedLocals=resultSelector.CapturedLocals,
                    HoistedLocals=resultSelector.HoistedLocals,
                    BodyUnsafeAccessorCodes=resultSelector.BodyUnsafeAccessorCodes,
                    SupportingMemberSources=resultSelector.SupportingMemberSources,
                };
                rewritten.Add(new PipelineStage(PipelineStageKind.Select,stage.Func,null,stage.InputType,keyType));
                rewritten.Add(new PipelineStage(PipelineStageKind.Distinct,null,stage.Value,keyType,keyType,keyType: keyType));
                rewritten.Add(new PipelineStage(PipelineStageKind.Select,resultFromKey,null,keyType,stage.OutputType));
                continue;
            }

            if(stage.ThirdFunc is LambdaFunc selectFunc&&GroupBySupport.SelectIgnoresGroup(selectFunc)) {
                var groupParam = selectFunc.ParameterNames[0];
                var newParamName = "__k";
                var replacedBody = System.Text.RegularExpressions.Regex.Replace(
                    selectFunc.InlineBody,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(groupParam)}\.Key\b",
                    newParamName,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant);

                var resultFromKey = new LambdaFunc {
                    ReturnTypeName=selectFunc.ReturnTypeName,
                    ParameterTypeNames=[keyType],
                    ParameterNames=[newParamName],
                    CanGenerateMethod=selectFunc.CanGenerateMethod,
                    CanInlineAssign=true,
                    InlineBody=replacedBody,
                    CapturedLocals=selectFunc.CapturedLocals,
                    HoistedLocals=selectFunc.HoistedLocals,
                    BodyUnsafeAccessorCodes=selectFunc.BodyUnsafeAccessorCodes,
                    SupportingMemberSources=selectFunc.SupportingMemberSources,
                };
                rewritten.Add(new PipelineStage(PipelineStageKind.Select,stage.Func,null,stage.InputType,keyType));
                rewritten.Add(new PipelineStage(PipelineStageKind.Distinct,null,stage.Value,keyType,keyType,keyType: keyType));
                rewritten.Add(new PipelineStage(PipelineStageKind.Select,resultFromKey,null,keyType,stage.OutputType));
                continue;
            }

            rewritten.Add(stage);
        }
        return rewritten;
    }
}
