namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private string RenderTransactionalGroupOrder(string actualVariableName,string actualTypeName,ProviderIterateTerminal sink){
        var root=(TransactionRowsRoot)pipeline.Source;var stage=pipeline.Stages[this.transactionalGroupIndex];var projection=this.transactionalGroupProjection;
        var keyType=stage.KeyType??stage.Func!.ReturnTypeName;var elementType=stage.SecondFunc?.ReturnTypeName??stage.InputType??root.ElementType;
        var projectionType=projection?.ReturnTypeName??$"global::System.Linq.IGrouping<{keyType},{elementType}>";
        var tail=this.transactionalGroupTail;var order=this.transactionalGroupOrder;
        var inputType=projectionType;if(order>0){inputType=tail[order-1].OutputType;}
        var scope=$"__groupResources{this.result}";var lookup=$"__partitionedGroups{this.result}";var result=$"__groupResult{this.result}";
        var functions=new[]{stage.Func,stage.SecondFunc,projection}.Where(static func=>func is not null).Select(static func=>func!).Concat(this.TransactionalOutputFunctions(tail)).Distinct().ToArray();
        var allowParallel=IsParallelValueType(keyType)&&(string.IsNullOrWhiteSpace(stage.Value)||stage.Value=="null");
        var builder=new StringBuilder();
        builder.AppendLine("// [GroupBy/整列/取引] native producerと有界格納worker、整列、結果構築、全解放を同じsnapshotのoperationで保護する。");
        builder.AppendLine($"// [取引終端] terminal={terminalKind}; 通常LINQの終端へ接続し、整列の短絡とcallbackの評価順を保つ。");
        builder.AppendLine($"var {this.TransactionalGroupSource}={root.SourceExpression};");
        builder.AppendLine($"{actualTypeName} {actualVariableName}={this.TransactionalGroupSource}.ExecuteQuery(()=>global::Seamless.Execution.CpuScheduler.Shared.Run(()=>{{");
        builder.AppendLine(this.HoistedLocalDeclarations(functions));builder.AppendLine(this.StaticDelegateLocalDeclarations(functions));
        builder.AppendLine($"var __groupComparer=(global::System.Collections.Generic.IEqualityComparer<{keyType}>?)({stage.Value??"null"});");
        var parallelArgument=this.AppendParallelSlotArgument(builder,allowParallel);
        this.AppendTransactionalGroupArguments(builder,result,actualTypeName);
        builder.AppendLine($"using var {scope}={this.TransactionalGroupSource}.CreateQueryScope();");
        this.AppendTransactionalGroupMaterializer(builder);
        builder.AppendLine("// [並列/キュー] selectorはproducer上。確認済みの比較/codecだけをworkerへ渡し、不明な処理は逐次格納。有界batchとworker専用scopeを全て排出する。");
        builder.AppendLine($"using var {lookup}=new global::Seamless.Linq.PartitionedLookupBuilder<{keyType},{elementType}>({scope},__groupComparer,{parallelArgument});");
        builder.AppendLine($"{lookup}.Build(__target=>{{");builder.AppendLine(sink.GetDirectExecutionStatement("__target","void"));builder.AppendLine("});");
        builder.AppendLine($"global::System.Collections.Generic.IEnumerable<{inputType}> __orderInput(){{");
        builder.AppendLine($"foreach(var __group in {lookup}.Groups()){{");
        var beforeOrder=tail.Take(order).ToArray();
        // 窓のない入力にif(!(true&&true)){yield break;}を出さない。常に到達不能な分岐は不要。
        var continuation=new[]{this.TransactionalContinue(result,beforeOrder),this.TransactionalContinue(result+"Groups",this.transactionalGroupBeforeProjection)}.Where(static condition=>condition!="true").ToArray();
        if(continuation.Length>0){builder.AppendLine($"if(!({string.Join("&&",continuation)})){{yield break;}}");}
        this.AppendTransactionalGroupWindow(builder,result+"Groups");
        builder.AppendLine($"{projectionType} __projected=default!;");this.AppendTransactionalGroupProjection(builder,scope,projection,"__projected","__group");
        var value="__projected";
        for(var index=0;index<order;index++){
            var item=tail[index];
            if(item.Kind==PipelineStageKind.Where){builder.AppendLine($"bool __keep{index}=false;");builder.AppendLine(this.FuncAssignStatic(item.Func!,"Where",$"__keep{index}",value));builder.AppendLine($"if(!__keep{index}){{continue;}}");}
            else if(item.Kind==PipelineStageKind.Select){var next=$"__beforeOrder{index}";builder.AppendLine($"{item.OutputType} {next}=default!;");builder.AppendLine(this.FuncAssignStatic(item.Func!,"Select",next,value));value=next;}
            else if(item.Kind==PipelineStageKind.Skip){builder.AppendLine($"if({result}Count{index}<{result}Limit{index}){{{result}Count{index}++;continue;}}");}
            else if(item.Kind==PipelineStageKind.Take){builder.AppendLine($"if({result}Count{index}>={result}Limit{index}){{yield break;}}{result}Count{index}++;");}
        }
        builder.AppendLine($"yield return {value};}}");builder.AppendLine("}");
        this.AppendTransactionalOrder(builder,scope,result,actualTypeName,inputType,tail,order,"__orderInput()");
        builder.AppendLine("}));");return builder.ToString();
    }
}
