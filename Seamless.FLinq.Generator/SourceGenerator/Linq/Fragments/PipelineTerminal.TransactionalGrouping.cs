using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private ProviderIterateTerminal? transactionalGroupSink;
    private int transactionalGroupIndex=-1;
    private FuncFragment? transactionalGroupProjection;
    private PipelineStage[] transactionalGroupBeforeProjection=[];
    private PipelineStage[] transactionalGroupTail=[];
    private string TransactionalGroupSource=>$"__transactionGroupSource{this.result}";
    private ProviderIterateTerminal? TransactionalGroupSink(){
        if(this.transactionalGroupSink is not null){return this.transactionalGroupSink;}
        if(!this.SupportsTransactionalOutput||pipeline.Source is not TransactionRowsRoot root){return null;}
        var index=-1;
        for(var item=0;item<pipeline.Stages.Count;item++){
            if(pipeline.Stages[item].Kind==PipelineStageKind.GroupBy){if(index>=0){return null;}index=item;}
        }
        // 集計候補はgroup自体を返さないと解析済み。未解析のgroup逃避を、usingで閉じるLookupへ接続しない。
        if(index<0||pipeline.Stages[index] is not{Func:{} keySelector} stage){return null;}
        var projection=stage.ThirdFunc;var after=index+1;
        var aggregate=stage.AggregatePlan??stage.AggregateCandidate;
        var beforeProjection=new List<PipelineStage>();
        if(projection is null){while(after<pipeline.Stages.Count&&pipeline.Stages[after].Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Skip or PipelineStageKind.Take){beforeProjection.Add(pipeline.Stages[after]);after++;}}
        if(projection is null&&after<pipeline.Stages.Count&&pipeline.Stages[after] is{Kind:PipelineStageKind.Select,UsesIndex:false,Func:{} select} selected){projection=select;aggregate??=selected.AggregateCandidate;after++;}
        if(aggregate is null){return null;}
        if(projection is null||projection.RequiresResultTypeParameter){return null;}
        if(!IsTransactionalTail(beforeProjection)){return null;}
        var tail=pipeline.Stages.Skip(after).ToArray();if(!IsTransactionalTail(tail)){return null;}
        if(tail.Concat(beforeProjection).Any(static item=>item.Kind==PipelineStageKind.Take)&&stage.Value is not null&&SyntaxFactory.ParseExpression(stage.Value) is not(IdentifierNameSyntax or LiteralExpressionSyntax)){return null;}
        var before=pipeline.Stages.Take(index).ToArray();
        if(keySelector.RequiresResultTypeParameter||stage.SecondFunc is{RequiresResultTypeParameter:true}
            ||before.Any(static item=>!PipelineStageCapabilityTable.SupportsProviderNativeIterate(item.Kind)||item.Value is not null||item.Func is{RequiresStatementInlining:true} or{RequiresResultTypeParameter:true} or{HoistedLocals.Length:>0})){return null;}
        var inputType=root.ElementType;if(before.Length>0){inputType=before[before.Length-1].OutputType;}
        var nativeRoot=root.WithSource(番号,this.TransactionalGroupSource,root.ElementType,$"global::System.Collections.Generic.IEnumerator<{root.ElementType}>");
        var input=new PipelineEnumerable(番号,nativeRoot,before,inputType);
        this.transactionalGroupSink=ProviderIterateTerminal.GroupBuilder(番号,input,nativeRoot,keySelector,stage.SecondFunc);this.transactionalGroupSink.ApplyRenderContext(this.RenderContext);
        this.transactionalGroupIndex=index;this.transactionalGroupProjection=projection;this.transactionalGroupBeforeProjection=beforeProjection.ToArray();this.transactionalGroupTail=tail;return this.transactionalGroupSink;
    }
    private string? TryRenderTransactionalGrouping(string actualVariableName,string actualTypeName){
        var sink=this.TransactionalGroupSink();if(sink is null){return null;}
        var root=(TransactionRowsRoot)pipeline.Source;var stage=pipeline.Stages[this.transactionalGroupIndex];var projection=this.transactionalGroupProjection!;
        var keyType=stage.KeyType??stage.Func!.ReturnTypeName;var elementType=stage.SecondFunc?.ReturnTypeName??stage.InputType??root.ElementType;
        var scope=$"__groupResources{this.result}";var lookup=$"__partitionedGroups{this.result}";var result=$"__groupResult{this.result}";
        var functions=new[]{stage.Func,stage.SecondFunc,projection}.Where(static func=>func is not null).Select(static func=>func!).Concat(this.TransactionalOutputFunctions(this.transactionalGroupTail)).Distinct().ToArray();
        var allowParallel=IsParallelValueType(keyType)&&string.IsNullOrWhiteSpace(stage.Value);
        if(stage.Value=="null"){allowParallel=IsParallelValueType(keyType);}
        var builder=new StringBuilder();
        if(root is DatabaseRowsRoot){builder.AppendLine("// [GroupBy/DB行] 版付きGatewayは所有snapshotのoperation、従来方式は既存の読取sessionでproducer/worker/結果合成/解放を保護。入力方式を独立した取引へ切り替えない。");}
        else{builder.AppendLine("// [GroupBy/取引] producer/worker/結果合成/解放を一つのoperationで保護。同じsnapshotと自己変更をnative走査する。");}
        builder.AppendLine($"var {this.TransactionalGroupSource}={root.SourceExpression};");
        builder.AppendLine($"{actualTypeName} {actualVariableName}={this.TransactionalGroupSource}.ExecuteQuery(()=>global::Seamless.Execution.CpuScheduler.Shared.Run(()=>{{");
        builder.AppendLine(this.HoistedLocalDeclarations(functions));builder.AppendLine(this.StaticDelegateLocalDeclarations(functions));
        if(this.transactionalGroupTail.Concat(this.transactionalGroupBeforeProjection).Any(static item=>item.Kind==PipelineStageKind.Take)){builder.AppendLine("// [GroupBy/要求上限] Takeの引数を一度評価。0以下なら入力/selector/Lookup/workerを開始しない。上限後の集計投影も呼ばない。");}
        this.AppendTransactionalOutputPreamble(builder,result,actualTypeName,this.transactionalGroupTail);
        this.AppendTransactionalOutputPreamble(builder,result+"Groups",actualTypeName,this.transactionalGroupBeforeProjection,false,result);
        builder.AppendLine($"using var {scope}={this.TransactionalGroupSource}.CreateQueryScope();");
        this.AppendTransactionalOutputState(builder,scope,result,actualTypeName,projection.ReturnTypeName,this.transactionalGroupTail);
        builder.AppendLine("// [並列判断] selectorは元順のproducer上。scalarキーの比較と既定codecのfield読取りを確認してworkerへ分担し、参照graph/独自policy/custom codec/CPU上限1は逐次へ戻る。");
        builder.AppendLine($"using var {lookup}=new global::Seamless.Linq.PartitionedLookupBuilder<{keyType},{elementType}>({scope},{stage.Value??"null"},allowParallel:{allowParallel.ToString().ToLowerInvariant()});");
        builder.AppendLine("// [キュー/資源] 有界batchとworker専用scope/Lookupを所有。格納を予算へ計上し、参照を含まない値は退避できる。参照を持つ要素は複製せず予算内で保持。全worker終了後に初出順へ合成。");
        builder.AppendLine($"{lookup}.Build(__target=>{{");builder.AppendLine(sink.GetDirectExecutionStatement("__target","void"));builder.AppendLine("});");
        if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault&&this.transactionalGroupTail.All(static item=>item.Kind!=PipelineStageKind.Where)){
            builder.AppendLine("// [末尾/評価] 全groupの構築後、Skip/Takeで選ぶ最後のgroupだけを保持して投影。通常LINQと同じcallback回数を維持し、groupをquery外へ返さない。");
            builder.AppendLine($"global::System.Linq.IGrouping<{keyType},{elementType}>? __lastGroup=null;");
            builder.AppendLine($"foreach(var __candidate in {lookup}.Groups()){{");
            this.AppendTransactionalGroupWindow(builder,result+"Groups");
            for(var index=0;index<this.transactionalGroupTail.Length;index++){
                var item=this.transactionalGroupTail[index];
                if(item.Kind==PipelineStageKind.Skip){builder.AppendLine($"if({result}Count{index}<{result}Limit{index}){{{result}Count{index}++;continue;}}");}
                else if(item.Kind==PipelineStageKind.Take){builder.AppendLine($"if({result}Count{index}>={result}Limit{index}){{break;}}{result}Count{index}++;");}
            }
            builder.AppendLine($"__lastGroup=__candidate;if(!({this.TransactionalContinue(result,this.transactionalGroupTail)}&&{this.TransactionalContinue(result+"Groups",this.transactionalGroupBeforeProjection)})){{break;}}}}");
            builder.AppendLine("if(__lastGroup is not null){");
            if(projection.ParameterNames.Length==2){builder.AppendLine(this.FuncAssignStatic(projection,"GroupBy",result+"Value","__lastGroup.Key","__lastGroup"));}
            else{builder.AppendLine(this.FuncAssignStatic(projection,"GroupBy",result+"Value","__lastGroup"));}
            builder.AppendLine(result+"Found=true;");
            builder.AppendLine("}");this.AppendTransactionalOutputReturn(builder,result,actualTypeName,this.transactionalGroupTail);builder.AppendLine("}));");return builder.ToString();
        }
        this.AppendTransactionalOutputConsumer(builder,result,projection.ReturnTypeName,this.transactionalGroupTail);
        builder.AppendLine($"foreach(var __group in {lookup}.Groups()){{");
        this.AppendTransactionalGroupWindow(builder,result+"Groups");
        builder.AppendLine("// [数値/例外順序] 元のgroup投影を一度呼ぶ。複数の集計/短絡/selectorを行ごとに再配列せず、部分和を再結合しない。");
        builder.AppendLine($"{projection.ReturnTypeName} __projected=default!;");
        if(projection.ParameterNames.Length==2){builder.AppendLine(this.FuncAssignStatic(projection,"GroupBy","__projected","__group.Key","__group"));}
        else{builder.AppendLine(this.FuncAssignStatic(projection,"GroupBy","__projected","__group"));}
        builder.AppendLine($"if(!{result}Consume(__projected)||!({this.TransactionalContinue(result+"Groups",this.transactionalGroupBeforeProjection)})){{break;}}");
        builder.AppendLine("}");
        this.AppendTransactionalOutputReturn(builder,result,actualTypeName,this.transactionalGroupTail);builder.AppendLine("}));");return builder.ToString();
    }
    private void AppendTransactionalGroupWindow(StringBuilder builder,string prefix){
        for(var index=0;index<this.transactionalGroupBeforeProjection.Length;index++){
            var item=this.transactionalGroupBeforeProjection[index];
            if(item.Kind==PipelineStageKind.Skip){builder.AppendLine($"if({prefix}Count{index}<{prefix}Limit{index}){{{prefix}Count{index}++;continue;}}");}
            else if(item.Kind==PipelineStageKind.Take){builder.AppendLine($"if({prefix}Count{index}>={prefix}Limit{index}){{break;}}{prefix}Count{index}++;");}
        }
    }
    private static bool IsParallelValueType(string type){
        type=type.Replace("global::",string.Empty).Trim();
        if(type.EndsWith("?",StringComparison.Ordinal)){return IsParallelValueType(type.Substring(0,type.Length-1));}
        if(type.StartsWith("System.Nullable<",StringComparison.Ordinal)&&type.EndsWith(">",StringComparison.Ordinal)){return IsParallelValueType(type.Substring(16,type.Length-17));}
        if(SyntaxFactory.ParseTypeName(type) is TupleTypeSyntax tuple){return tuple.Elements.All(static element=>IsParallelValueType(element.Type.ToString()));}
        return type is "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "decimal" or "char" or "string"
            or "System.Boolean" or "System.Byte" or "System.SByte" or "System.Int16" or "System.UInt16" or "System.Int32" or "System.UInt32" or "System.Int64" or "System.UInt64" or "System.Single" or "System.Double" or "System.Decimal" or "System.Char" or "System.String" or "System.Guid" or "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan";
    }
}
