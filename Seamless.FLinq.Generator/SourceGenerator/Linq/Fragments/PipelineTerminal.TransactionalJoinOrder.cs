namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private string RenderTransactionalJoinOrder(string actualVariableName,string actualTypeName,ProviderIterateTerminal innerSink){
        var join=(JoinBase)pipeline.Source;
        TransactionJoinInput(join.TransactionOuter,out var outer,out var outerStages);TransactionJoinInput(join.TransactionInner,out var inner,out _);
        var tail=pipeline.Stages.ToArray();var keyType=join.TransactionInnerKey.ReturnTypeName;var innerType=join.TransactionInner.ElementType;
        var functions=new[]{join.TransactionOuterKey,join.TransactionResult}.Concat(outerStages.Select(static stage=>stage.Func).Where(static function=>function is not null).Select(static function=>function!)).Concat(this.TransactionalOutputFunctions(tail)).Distinct().ToArray();
        var scope=$"__joinResources{this.result}";var lookup=$"__joinLookup{this.result}";var result=$"__joinResult{this.result}";
        var allowParallel=IsParallelValueType(keyType)&&join.TransactionComparer=="null";
        var builder=new StringBuilder();
        builder.AppendLine("// [Join/整列/取引] 両入力のsnapshot、Lookup、整列、結果構築、全解放を同じoperationとquery scopeで保護する。");
        builder.AppendLine($"var {this.TransactionJoinOuter}={outer.SourceExpression};");builder.AppendLine($"var {this.TransactionJoinInner}={inner.SourceExpression};");
        builder.AppendLine($"{actualTypeName} {actualVariableName}={this.TransactionJoinOuter}.ExecuteQuery(()=>global::Seamless.Execution.CpuScheduler.Shared.Run(()=>{{");
        builder.AppendLine(this.HoistedLocalDeclarations(functions));builder.AppendLine(this.StaticDelegateLocalDeclarations(functions));
        if(join is Join){builder.AppendLine("static bool __joinHasKey<TKey>(TKey key)=>key is not null;");}
        builder.AppendLine($"var {lookup}Comparer=(global::System.Collections.Generic.IEqualityComparer<{keyType}>?)({join.TransactionComparer});");
        var parallelArgument=this.AppendParallelSlotArgument(builder,allowParallel);
        this.AppendTransactionalOrderArguments(builder,result,actualTypeName,[],tail,0);
        builder.AppendLine($"using var {scope}={this.TransactionJoinOuter}.CreateQueryScope();");
        AppendGroupJoinOwner(builder,join,scope);
        builder.AppendLine("// [内側/並列/キュー] 最初の外側行の後に既存native producerから有界worker Lookupを構築。未知の比較器/codecは逐次格納し、全workerを排出する。");
        builder.AppendLine($"using var {lookup}=new global::Seamless.Linq.PartitionedLookupBuilder<{keyType},{innerType}>({scope},{lookup}Comparer,{parallelArgument});");
        var sequence=this.TransactionJoinOuter;
        for(var index=0;index<outerStages.Length;index++){
            var stage=outerStages[index];if(stage.Kind==PipelineStageKind.AsEnumerable){continue;}
            var next=$"__joinOuterStage{index}";
            builder.AppendLine($"var {next}=global::System.Linq.Enumerable.{stage.Kind}({sequence},{this.TransactionalOrderLambda(stage.Func!,null,stage.Kind.ToString())});");sequence=next;
        }
        builder.AppendLine($"var __joinBuilt=false;global::System.Collections.Generic.IEnumerable<{join.ElementType}> __joinedInput(){{");
        builder.AppendLine("// [外側/評価位置] snapshotの有界batch列挙を一件ずつ進める。result selectorと整列キーを交互に評価し、First/Lastのために先行配列化しない。callbackはproducer上。");
        builder.AppendLine($"foreach(var __outer in {sequence}){{");
        builder.AppendLine($"if(!__joinBuilt){{__joinBuilt=true;{lookup}.Build(__target=>{{");builder.AppendLine(innerSink.GetDirectExecutionStatement("__target","void"));builder.AppendLine("});}");
        if(join is Join){builder.AppendLine($"if({lookup}.LongCount==0){{yield break;}}");}
        builder.AppendLine($"{keyType} __outerKey=default!;");builder.AppendLine(this.FuncAssignStatic(join.TransactionOuterKey,"Join","__outerKey","__outer"));
        if(join is GroupJoin grouping){
            builder.AppendLine("// [GroupJoin/整列/取引] 一致列の寿命と整列作業領域をquery内で管理する。");
            this.AppendGroupJoinResult(builder,grouping,lookup);
            builder.AppendLine("yield return __joined;}}");
        }else{
            builder.AppendLine("if(!__joinHasKey(__outerKey)){continue;}");
            builder.AppendLine($"foreach(var __inner in {lookup}.Match(__outerKey)){{{join.ElementType} __joined=default!;");
            builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"Join","__joined","__outer","__inner"));builder.AppendLine("yield return __joined;}}}");
        }
        this.AppendTransactionalOrder(builder,scope,result,actualTypeName,join.ElementType,tail,0,"__joinedInput()");
        builder.AppendLine("}));");return builder.ToString();
    }
}
