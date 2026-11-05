namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private (ProviderIterateTerminal Outer,ProviderIterateTerminal Inner)? transactionalJoinSinks;
    private string TransactionJoinOuter=>$"__transactionJoinOuter{this.result}";
    private string TransactionJoinInner=>$"__transactionJoinInner{this.result}";
    private static bool TransactionJoinInput(Enumerable source,out TransactionRowsRoot root,out PipelineStage[] stages){
        stages=[];
        if(source is PipelineEnumerable input){source=input.Source;stages=input.Stages.ToArray();}
        root=source as TransactionRowsRoot??null!;
        return root is not null&&stages.All(static stage=>PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind)&&stage.Value is null&&stage.Func is not{RequiresStatementInlining:true}&&stage.Func is not{RequiresResultTypeParameter:true}&&stage.Func is not{HoistedLocals.Length:>0});
    }
    private (ProviderIterateTerminal Outer,ProviderIterateTerminal Inner)? TransactionalJoinSinks(){
        if(this.transactionalJoinSinks is not null){return this.transactionalJoinSinks;}
        if(!this.SupportsTransactionalOutput||pipeline.Source is not Join join
            ||!IsTransactionalTail(pipeline.Stages)
            ||join.TransactionResult.RequiresResultTypeParameter||join.TransactionOuterKey.RequiresResultTypeParameter||join.TransactionInnerKey.RequiresResultTypeParameter||join.TransactionInnerKey.HoistedLocals.Length!=0
            ||!TransactionJoinInput(join.TransactionOuter,out var outer,out var outerStages)
            ||!TransactionJoinInput(join.TransactionInner,out var inner,out var innerStages)){return null;}
        var outerRoot=outer.WithSource(番号,this.TransactionJoinOuter,outer.ElementType,$"global::System.Collections.Generic.IEnumerator<{outer.ElementType}>");
        var innerRoot=inner.WithSource(join.TransactionNumber,this.TransactionJoinInner,inner.ElementType,$"global::System.Collections.Generic.IEnumerator<{inner.ElementType}>");
        var outerSink=ProviderIterateTerminal.Consumer(番号,new PipelineEnumerable(番号,outerRoot,outerStages,join.TransactionOuter.ElementType),outerRoot);
        var innerSink=ProviderIterateTerminal.JoinBuilder(join.TransactionNumber,new PipelineEnumerable(join.TransactionNumber,innerRoot,innerStages,join.TransactionInner.ElementType),innerRoot,join.TransactionInnerKey);
        outerSink.ApplyRenderContext(this.RenderContext);innerSink.ApplyRenderContext(this.RenderContext);
        return this.transactionalJoinSinks=(outerSink,innerSink);
    }
    private string? TryRenderTransactionalJoin(string actualVariableName,string actualTypeName){
        var sinks=this.TransactionalJoinSinks();if(sinks is null){return null;}
        var join=(Join)pipeline.Source;
        TransactionJoinInput(join.TransactionOuter,out var outer,out _);TransactionJoinInput(join.TransactionInner,out var inner,out _);
        var keyType=join.TransactionInnerKey.ReturnTypeName;var innerType=join.TransactionInner.ElementType;
        var functions=new[]{join.TransactionOuterKey,join.TransactionResult}.Concat(this.TransactionalOutputFunctions(pipeline.Stages)).Distinct().ToArray();
        var scope=$"__joinResources{this.result}";var lookup=$"__joinLookup{this.result}";var result=$"__joinResult{this.result}";var built=$"__joinBuilt{this.result}";
        var allowParallel=IsParallelValueType(keyType)&&join.TransactionComparer=="null";
        var builder=new StringBuilder();
        if(outer is DatabaseRowsRoot||inner is DatabaseRowsRoot){builder.AppendLine("// [Join/DB行] 版付きGatewayは所有snapshotのoperation、従来方式は既存の読取sessionを使い、producer/格納worker/照合/結果解放まで保持する。");}
        else{builder.AppendLine("// [Join/取引] 同じsnapshotをnative走査し、producer/格納worker/照合/結果解放までoperationを保持する。");}
        builder.AppendLine($"var {this.TransactionJoinOuter}={outer.SourceExpression};");builder.AppendLine($"var {this.TransactionJoinInner}={inner.SourceExpression};");
        builder.AppendLine($"{actualTypeName} {actualVariableName}={this.TransactionJoinOuter}.ExecuteQuery(()=>global::Seamless.Execution.CpuScheduler.Shared.Run(()=>{{");
        builder.AppendLine(this.HoistedLocalDeclarations(functions));builder.AppendLine(this.StaticDelegateLocalDeclarations(functions));
        builder.AppendLine("static bool __joinHasKey<TKey>(TKey key)=>key is not null;");
        builder.AppendLine($"var {lookup}Comparer=(global::System.Collections.Generic.IEqualityComparer<{keyType}>?)({join.TransactionComparer});");
        this.AppendTransactionalOutputPreamble(builder,result,actualTypeName,pipeline.Stages);
        builder.AppendLine($"using var {scope}={this.TransactionJoinOuter}.CreateQueryScope();");
        this.AppendTransactionalOutputState(builder,scope,result,actualTypeName,join.ElementType,pipeline.Stages);
        builder.AppendLine($"using var {lookup}=new global::Seamless.Linq.PartitionedLookupBuilder<{keyType},{innerType}>({scope},{lookup}Comparer,allowParallel:{allowParallel.ToString().ToLowerInvariant()});");
        builder.AppendLine($"var {built}=false;");
        this.AppendTransactionalOutputConsumer(builder,result,join.ElementType,pipeline.Stages);
        builder.AppendLine("// [評価順序] 最初の外側行を得てから内側を構築。外側が空なら内側のselectorは呼ばず、内側のnullキーは格納しない。");
        builder.AppendLine($"global::System.Func<{join.TransactionOuter.ElementType},bool> __joinConsume=__outer=>{{");
        builder.AppendLine($"if(!{built}){{{built}=true;{lookup}.Build(__target=>{{");builder.AppendLine(sinks.Value.Inner.GetDirectExecutionStatement("__target","void"));builder.AppendLine("});}");
        builder.AppendLine($"if({lookup}.LongCount==0){{return false;}}");
        builder.AppendLine($"{keyType} __outerKey=default!;");builder.AppendLine(this.FuncAssignStatic(join.TransactionOuterKey,"Join","__outerKey","__outer"));
        builder.AppendLine("if(!__joinHasKey(__outerKey)){return true;}");
        builder.AppendLine("// [照合/順序] 格納worker終了後、元の外側順と内側一致順で結果selectorを実行。未知のcallbackはworkerへ移さない。");
        builder.AppendLine($"foreach(var __inner in {lookup}.Match(__outerKey)){{");
        builder.AppendLine($"{join.ElementType} __joined=default!;");builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"Join","__joined","__outer","__inner"));
        builder.AppendLine($"if(!{result}Consume(__joined)){{return false;}}");builder.AppendLine("}return true;};");
        builder.AppendLine("// [キュー/資源] native strategyは一つのproducer callbackへ渡す。内側格納のみ有界queueと専用scopeへ分担し、usingは全workerを排出して予約/退避を返す。");
        builder.AppendLine(sinks.Value.Outer.GetDirectExecutionStatement("__joinConsume","void"));
        this.AppendTransactionalOutputReturn(builder,result,actualTypeName,pipeline.Stages);builder.AppendLine("}));");return builder.ToString();
    }
}
