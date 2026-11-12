namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private const int JoinPointProbeLimit=64;
    private (ProviderIterateTerminal Outer,ProviderIterateTerminal Inner)? transactionalJoinSinks;
    private int transactionalJoinOrder=-1;
    private string TransactionJoinOuter=>$"__transactionJoinOuter{this.result}";
    private string TransactionJoinInner=>$"__transactionJoinInner{this.result}";
    private static (int Minimum,int Maximum)? DenseGroupJoinRange(JoinBase join)=>
        join is GroupJoin&&join.TransactionComparer=="null"&&(join.TransactionResult.ReadsGroupCountOnly||join.TransactionResult.ReturnsGroupLongCount)
        &&!join.TransactionResult.ContainsAnchor?join.TransactionInnerKey.SmallIntKeyRange:null;
    private string TransactionalJoinFallbackComment(Join join){
        var tail=pipeline.Stages.ToArray();var order=-1;
        var supportedTail=IsTransactionalTail(tail)||TryTransactionalOrder(tail,out order);
        return $"// [Join/取引接続] 専用終端未接続: terminal={terminalKind},出力対応={this.SupportsTransactionalOutput},後段対応={supportedTail},外側={join.TransactionOuter.GetType().Name}/{TransactionJoinInput(join.TransactionOuter,out _,out _)},内側={join.TransactionInner.GetType().Name}/{TransactionJoinInput(join.TransactionInner,out _,out _)},結果型引数={join.TransactionResult.RequiresResultTypeParameter},外側キー型引数={join.TransactionOuterKey.RequiresResultTypeParameter},内側キー型引数={join.TransactionInnerKey.RequiresResultTypeParameter},内側キーhoist={join.TransactionInnerKey.HoistedLocals.Length},段={string.Join(",",tail.Select(static stage=>stage.Kind))}。一般経路の同期・予算をこの専用経路の保証へ読み替えない。";
    }
    private static bool TransactionJoinInput(Enumerable source,out TransactionRowsRoot root,out PipelineStage[] stages){
        stages=[];
        if(source is PipelineEnumerable input){source=input.Source;stages=input.Stages.ToArray();}
        root=source as TransactionRowsRoot??null!;
        return root is not null&&stages.All(static stage=>PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind)&&stage.Value is null&&stage.Func is not{RequiresStatementInlining:true}&&stage.Func is not{RequiresResultTypeParameter:true}&&stage.Func is not{HoistedLocals.Length:>0});
    }
    private (ProviderIterateTerminal Outer,ProviderIterateTerminal Inner)? TransactionalJoinSinks(){
        if(this.transactionalJoinSinks is not null){return this.transactionalJoinSinks;}
        var tail=pipeline.Stages.ToArray();var order=-1;
        if(!this.SupportsTransactionalOutput||pipeline.Source is not JoinBase join||join is not (Join or GroupJoin)
            ||join is GroupJoin groupJoin&&groupJoin.CanSkipLookup()
            ||(!IsTransactionalTail(tail)&&!TryTransactionalOrder(tail,out order))
            ||join.TransactionResult.RequiresResultTypeParameter||join.TransactionOuterKey.RequiresResultTypeParameter||join.TransactionInnerKey.RequiresResultTypeParameter||join.TransactionInnerKey.HoistedLocals.Length!=0
            ||!TransactionJoinInput(join.TransactionOuter,out var outer,out var outerStages)
            ||!TransactionJoinInput(join.TransactionInner,out var inner,out var innerStages)){return null;}
        var outerRoot=outer.WithSource(番号,this.TransactionJoinOuter,outer.ElementType,$"global::System.Collections.Generic.IEnumerator<{outer.ElementType}>");
        var innerRoot=inner.WithSource(join.TransactionNumber,this.TransactionJoinInner,inner.ElementType,$"global::System.Collections.Generic.IEnumerator<{inner.ElementType}>");
        var outerSink=ProviderIterateTerminal.Consumer(番号,new PipelineEnumerable(番号,outerRoot,outerStages,join.TransactionOuter.ElementType),outerRoot);
        var innerPipeline=new PipelineEnumerable(join.TransactionNumber,innerRoot,innerStages,join.TransactionInner.ElementType);
        var innerSink=order<0&&(pipeline.ParallelSlots is null or "1")&&DenseGroupJoinRange(join) is not null
            ?ProviderIterateTerminal.Consumer(join.TransactionNumber,innerPipeline,innerRoot)
            :ProviderIterateTerminal.JoinBuilder(join.TransactionNumber,innerPipeline,innerRoot,join.TransactionInnerKey);
        outerSink.ApplyRenderContext(this.RenderContext);innerSink.ApplyRenderContext(this.RenderContext);
        this.transactionalJoinOrder=order;return this.transactionalJoinSinks=(outerSink,innerSink);
    }
    private string? TryRenderTransactionalJoin(string actualVariableName,string actualTypeName){
        var sinks=this.TransactionalJoinSinks();if(sinks is null){return null;}
        if(this.transactionalJoinOrder>=0){return this.RenderTransactionalJoinOrder(actualVariableName,actualTypeName,sinks.Value.Inner);}
        var join=(JoinBase)pipeline.Source;
        TransactionJoinInput(join.TransactionOuter,out var outer,out _);TransactionJoinInput(join.TransactionInner,out var inner,out _);
        var keyType=join.TransactionInnerKey.ReturnTypeName;var innerType=join.TransactionInner.ElementType;
        string? indexKey=null;
        if(join is Join&&join.TransactionComparer=="null"&&inner.KeyType==keyType
            &&ReferenceEquals(join.TransactionInner,inner)&&!inner.TreeContainsAnchor()
            &&!join.TransactionInnerKey.ContainsAnchor&&pipeline.ParallelSlots is null or "1"){
            var selector=join.TransactionInnerKey;
            if(selector.IsDirectJoinKeyMethod){indexKey=selector.DelegateConstructorArg;}
            else if(inner.DeclaredJoinKey is{} declared&&SymbolEqualityComparer.Default.Equals(declared.Member,selector.DirectJoinKeyMember)){
                indexKey=declared.Method;
            }
        }
        var denseRange=pipeline.ParallelSlots is null or "1"?DenseGroupJoinRange(join):null;
        var selectors=denseRange is null?new[]{join.TransactionOuterKey,join.TransactionResult}:new[]{join.TransactionOuterKey,join.TransactionInnerKey,join.TransactionResult};
        var functions=selectors.Concat(this.TransactionalOutputFunctions(pipeline.Stages)).Distinct().ToArray();
        var scope=$"__joinResources{this.result}";var lookup=$"__joinLookup{this.result}";var result=$"__joinResult{this.result}";var built=$"__joinBuilt{this.result}";
        var lookupType=$"global::Seamless.Linq.PartitionedLookupBuilder<{keyType},{innerType}>";
        var lookupAccess=indexKey is null?lookup:lookup+"!";
        var allowParallel=IsParallelValueType(keyType)&&join.TransactionComparer=="null";
        var builder=new StringBuilder();
        if(join is Join&&indexKey is null&&join.TransactionInnerKey.CanInvokeAsDelegate){
            builder.AppendLine($"// [Join/主キー候補] 内側は {inner.KeyType??"不明"} 型の索引、セレクター戻り値は {keyType}、未加工行={ReferenceEquals(join.TransactionInner,inner)}、直接キー関数={join.TransactionInnerKey.IsDirectJoinKeyMethod}、内側Anchor={inner.TreeContainsAnchor()||join.TransactionInnerKey.ContainsAnchor}。");
        }
        if(join is GroupJoin){builder.AppendLine("// [GroupJoin/取引] 外側順で一致列を取得し、借用集計または所有化をproducer上で実行する。");}
        if(outer is DatabaseRowsRoot||inner is DatabaseRowsRoot){builder.AppendLine("// [Join/DB行] 版付きGatewayは所有snapshotのoperation、従来方式は既存の読取sessionを使い、producer/格納worker/照合/結果解放まで保持する。");}
        else{builder.AppendLine("// [Join/取引] 同じsnapshotをnative走査し、producer/格納worker/照合/結果解放までoperationを保持する。");}
        builder.AppendLine($"var {this.TransactionJoinOuter}={outer.SourceExpression};");builder.AppendLine($"var {this.TransactionJoinInner}={inner.SourceExpression};");
        builder.AppendLine($"{actualTypeName} {actualVariableName}={this.TransactionJoinOuter}.ExecuteQuery(()=>global::Seamless.Execution.CpuScheduler.Shared.Run(()=>{{");
        builder.AppendLine(this.HoistedLocalDeclarations(functions));builder.AppendLine(this.StaticDelegateLocalDeclarations(functions));
        if(join is Join){builder.AppendLine("static bool __joinHasKey<TKey>(TKey key)=>key is not null;");}
        if(denseRange is null){builder.AppendLine($"var {lookup}Comparer=(global::System.Collections.Generic.IEqualityComparer<{keyType}>?)({join.TransactionComparer});");}
        var parallelArgument=this.AppendParallelSlotArgument(builder,allowParallel);
        this.AppendTransactionalOutputPreamble(builder,result,actualTypeName,pipeline.Stages);
        builder.AppendLine($"using var {scope}={this.TransactionJoinOuter}.CreateQueryScope();");
        if(denseRange is null){AppendGroupJoinOwner(builder,join,scope);}
        this.AppendTransactionalOutputState(builder,scope,result,actualTypeName,join.ElementType,pipeline.Stages);
        if(denseRange is{} range){
            builder.AppendLine($"// [GroupJoin/件数配列] 既定比較の整数キー範囲 {range.Minimum}..{range.Maximum}。一致行を格納せず、producerが予約付き配列のカウンターを加算する。");
            builder.AppendLine($"using var {lookup}=new global::Seamless.Linq.DenseGroupCounts<{innerType}>({scope},{range.Minimum},{range.Maximum});");
        }else if(indexKey is not null){
            builder.AppendLine($"// [Join/主キー] キー関数と符号化が一致する読取り専用の表を最大{JoinPointProbeLimit}回点照合する。超過後は同じsnapshotのLookupへ移り、外側行を再評価しない。");
            builder.AppendLine($"{lookupType}? {lookup}=null;");
            builder.AppendLine($"global::Seamless.Transactions.Collections.TransactionRows<{keyType},{innerType}>.JoinIndex __joinIndex=default;");
            builder.AppendLine("bool __joinUseIndex=false,__joinIndexHasRows=false;");
            builder.AppendLine("int __joinPointProbes=0;");
            builder.AppendLine($"void __joinBuildLookup(){{{lookup}=new {lookupType}({scope},{lookup}Comparer,{parallelArgument});");
            builder.AppendLine($"{lookup}.Build(__target=>{{");
            builder.AppendLine(sinks.Value.Inner.GetDirectExecutionStatement("__target","void"));
            builder.AppendLine("});}");
        }else{
            builder.AppendLine($"using var {lookup}=new {lookupType}({scope},{lookup}Comparer,{parallelArgument});");
        }
        builder.AppendLine($"var {built}=false;");
        this.AppendTransactionalOutputConsumer(builder,result,join.ElementType,pipeline.Stages);
        builder.AppendLine("// [評価順序] 最初の外側行を得てから内側を構築。外側が空なら内側のselectorは呼ばず、内側のnullキーは格納しない。");
        builder.AppendLine($"global::System.Func<{join.TransactionOuter.ElementType},bool> __joinConsume=__outer=>{{");
        if(denseRange is not null){
            builder.AppendLine($"if(!{built}){{{built}=true;");
            builder.AppendLine($"bool __countInner({innerType} __denseRow){{int __denseKey=default;");
            builder.AppendLine(this.FuncAssignStatic(join.TransactionInnerKey,"GroupJoin","__denseKey","__denseRow"));
            builder.AppendLine($"{lookup}.AddKey(__denseKey);return true;}}");
            builder.AppendLine(sinks.Value.Inner.GetDirectExecutionStatement("__countInner","void"));
            builder.AppendLine("}");
        }else if(indexKey is not null){
            builder.AppendLine($"if(!{built}){{{built}=true;");
            builder.AppendLine($"__joinUseIndex={this.TransactionJoinInner}.TryGetJoinIndex({indexKey},out __joinIndex);");
            builder.AppendLine($"if(__joinUseIndex){{using var __firstInner={this.TransactionJoinInner}.GetEnumerator();__joinIndexHasRows=__firstInner.MoveNext();}}else{{__joinBuildLookup();}}}}");
        }else{
            builder.AppendLine($"if(!{built}){{{built}=true;{lookup}.Build(__target=>{{");builder.AppendLine(sinks.Value.Inner.GetDirectExecutionStatement("__target","void"));builder.AppendLine("});}");
        }
        if(join is Join){
            var noRows=indexKey is null?$"{lookup}.LongCount==0":$"(__joinUseIndex?!__joinIndexHasRows:{lookupAccess}.LongCount==0)";
            builder.AppendLine($"if({noRows}){{return false;}}");
        }
        builder.AppendLine($"{keyType} __outerKey=default!;");builder.AppendLine(this.FuncAssignStatic(join.TransactionOuterKey,"Join","__outerKey","__outer"));
        if(join is GroupJoin grouping){
            if(denseRange is not null){
                if(join.TransactionResult.ReturnsGroupLongCount){
                    builder.AppendLine($"// [GroupJoin/LongCount] 保持した64 bit件数をそのまま照合し、intへの変換や一致列の走査をしない。");
                    builder.AppendLine($"long __joined={lookup}.GetLongCount(__outerKey);");
                }else{
                    builder.AppendLine($"var __matches={lookup}.SelectCount(__outerKey);");
                    builder.AppendLine($"{join.ElementType} __joined=default!;");
                    builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"GroupJoin","__joined","__outer","__matches"));
                }
            }else{
                this.AppendGroupJoinResult(builder,grouping,lookup);
            }
            builder.AppendLine($"return {result}Consume(__joined);}};");
        }else{
            builder.AppendLine("if(!__joinHasKey(__outerKey)){return true;}");
            if(indexKey is not null){
                builder.AppendLine($"if(__joinUseIndex&&__joinPointProbes=={JoinPointProbeLimit}){{__joinBuildLookup();__joinUseIndex=false;}}");
                // 欠損も点検索のコストを払う。上限には成功・欠損を両方含める。
                builder.AppendLine("if(__joinUseIndex){__joinPointProbes++;if(!__joinIndex.TryGetValue(__outerKey,out var __pointInner)){return true;}");
                builder.AppendLine($"{join.ElementType} __pointJoined=default!;");
                builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"Join","__pointJoined","__outer","__pointInner"));
                builder.AppendLine($"return {result}Consume(__pointJoined);}}");
            }
            builder.AppendLine("// [照合/順序] 格納worker終了後、元の外側順と内側一致順で結果selectorを実行。未知のcallbackはworkerへ移さない。");
            builder.AppendLine($"foreach(var __inner in {lookupAccess}.Match(__outerKey)){{");
            builder.AppendLine($"{join.ElementType} __joined=default!;");builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"Join","__joined","__outer","__inner"));
            builder.AppendLine($"if(!{result}Consume(__joined)){{return false;}}");builder.AppendLine("}return true;};");
        }
        builder.AppendLine(denseRange is null
            ?"// [キュー/資源] native strategyは一つのproducer callbackへ渡す。内側格納のみ有界queueと専用scopeへ分担し、usingは全workerを排出して予約/退避を返す。"
            :"// [同期/資源] 内外のselectorと件数配列は同じproducerが所有する。取消・例外でもusingで予約を返す。");
        if(indexKey is not null){builder.AppendLine("try{");}
        builder.AppendLine(sinks.Value.Outer.GetDirectExecutionStatement("__joinConsume","void"));
        if(indexKey is not null){builder.AppendLine($"}}finally{{{lookup}?.Dispose();}}");}
        this.AppendTransactionalOutputReturn(builder,result,actualTypeName,pipeline.Stages);builder.AppendLine("}));");return builder.ToString();
    }
    private static void AppendGroupJoinOwner(StringBuilder builder,JoinBase join,string scope){
        if(join is GroupJoin {BorrowsGroupOnly:false}){
            var type=$"global::Seamless.Linq.QueryGrouping.Materializer<{join.TransactionInnerKey.ReturnTypeName},{join.TransactionInner.ElementType}>";
            builder.AppendLine($"{type}? __joinGroupOwner=null;");
            builder.AppendLine($"{type} __getJoinGroupOwner()=>__joinGroupOwner??=new({scope});");
        }
    }
    private void AppendGroupJoinResult(StringBuilder builder,GroupJoin join,string lookup){
        var innerType=join.TransactionInner.ElementType;
        builder.AppendLine($"global::System.Collections.Generic.IEnumerable<{innerType}> __matches=global::System.Array.Empty<{innerType}>();");
        builder.AppendLine($"if({lookup}.FindGroup(__outerKey,out var __groupIdentity) is {{}} __matchedGroup){{");
        if(join.BorrowsGroupOnly){
            builder.AppendLine("// [GroupJoin/借用] 同期の標準集計だけ。Countは保持件数を使い、所有コピーを作らない。");
            builder.AppendLine("__matches=__matchedGroup;");
        }else{
            builder.AppendLine("// [GroupJoin/所有] callbackの前に所有化。同じgroup識別子の配列を共有し、配列とcacheを予算へ計上する。");
            builder.AppendLine("__matches=__getJoinGroupOwner().MaterializeShared(__groupIdentity,__matchedGroup);");
        }
        builder.AppendLine("}");
        builder.AppendLine($"{join.ElementType} __joined=default!;");
        builder.AppendLine(this.FuncAssignStatic(join.TransactionResult,"GroupJoin","__joined","__outer","__matches"));
    }
}
