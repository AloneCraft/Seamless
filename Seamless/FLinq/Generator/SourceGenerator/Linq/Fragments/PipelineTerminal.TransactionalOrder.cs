using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    internal bool CanApplyParallelHint(object? target)=>
        target is JoinBase&&ReferenceEquals(target,pipeline.Source)&&this.TransactionalJoinSinks() is not null
        ||target is PipelineStage&&this.TransactionalGroupSink() is not null&&ReferenceEquals(target,pipeline.Stages[this.transactionalGroupIndex]);

    private string AppendParallelSlotArgument(StringBuilder builder,bool supportsParallelLookup){
        var requestedSlots=pipeline.ParallelSlots;
        if(requestedSlots is null or "1"){
            var reason=requestedSlots is null?"指定なしの既定値":"コンパイル時に確定";
            builder.AppendLine($"// [Lookup 実行枠] producer 1枠で逐次構築する（{reason}）。");
            return "allowParallel:false";
        }
        builder.AppendLine(supportsParallelLookup
            ?"// [Lookup 実行枠] 指定値はproducer込み。設定上限、CPU枠、codecで決定し、追加ワーカーにはバッチをキューで渡す。"
            :"// [Lookup 実行枠] キー型または比較器の条件により逐次構築する。指定式の評価と検証は維持する。");
        var slots=$"__parallelSlots{this.result}";
        builder.AppendLine($"var {slots}=(int)({requestedSlots});");
        builder.AppendLine($"global::System.ArgumentOutOfRangeException.ThrowIfNegativeOrZero({slots},\"maxExecutionSlots\");");
        return supportsParallelLookup?$"maxExecutionSlots:{slots}":"allowParallel:false";
    }
    internal bool UsesTransactionalOrdering=>(this.TransactionalGroupSink() is not null&&this.transactionalGroupOrder>=0)||(this.TransactionalJoinSinks() is not null&&this.transactionalJoinOrder>=0);
    private static bool IsTransactionalOrderStage(PipelineStage stage)=>stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending;
    private static string TransactionalOrderKeyType(PipelineStage stage)=>stage.Func?.ReturnTypeName??stage.KeyType??stage.InputType;
    private static bool TryTransactionalOrder(PipelineStage[] tail,out int order){
        order=Array.FindIndex(tail,static stage=>stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending);
        if(order<0||!IsTransactionalTail(tail.Take(order).ToArray())){return false;}
        for(var index=order;index<tail.Length;index++){
            var stage=tail[index];
            // Func=nullはIRで恒等キーを表す。Order/OrderDescendingと、正規化したx=>xも同じ予約付き経路へ接続する。
            if(IsTransactionalOrderStage(stage)){if(stage.Func is{RequiresResultTypeParameter:true} or{HoistedLocals.Length:>0}){return false;}}
            else if(!IsTransactionalTail([stage])){return false;}
        }
        return true;
    }
    private void AppendTransactionalOrder(StringBuilder builder,string scope,string result,string actualTypeName,string inputType,PipelineStage[] tail,int start,string inputExpression){
        var orders=global::System.Linq.Enumerable.Range(start,tail.Length-start).Where(index=>IsTransactionalOrderStage(tail[index])).ToArray();
        var workspaceOrders=orders.Where(index=>index>start&&tail[index].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending).ToArray();
        var materializes=terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList;
        var lastSelect=Array.FindLastIndex(tail,static item=>item.Kind==PipelineStageKind.Select);
        builder.AppendLine("// [整列/資源] 入力を先に全件配列化せず、BCLの短絡とcallbackの交互評価を維持。要素を渡す前に入力コピー・位置map・全キー配列の予約を増やし、返却キーのpayloadも計上する。");
        foreach(var index in orders){builder.AppendLine($"using var __orderKeys{index}=new global::Seamless.Linq.QueryOrdering.KeyBudget<{TransactionalOrderKeyType(tail[index])}>({scope},0);");}
        var budgets=orders.Select(static index=>$"__orderKeys{index}").ToList();
        foreach(var index in workspaceOrders){
            builder.AppendLine($"// [整列{index}/資源] 入力を包み直さずBCLの列の能力を維持。最初の入力件数を上界として後続整列の作業領域を先に予約する。");
            builder.AppendLine($"using var __orderWorkspace{index}=new global::Seamless.Linq.QueryOrdering.InputBudget<{tail[index].InputType}>({scope},[]);");budgets.Add($"__orderWorkspace{index}");
        }
        if(materializes){builder.AppendLine($"using var __orderOutputBudget=new global::Seamless.Linq.QueryOrdering.KeyBudget<{pipeline.ElementType}>({scope},0L,materializedOutput:true);");budgets.Add("__orderOutputBudget");}
        var selections=new Dictionary<int,string>();
        for(var index=start;index<tail.Length;index++){
            if(tail[index].Kind!=PipelineStageKind.Select){continue;}
            if(materializes&&index==lastSelect){selections[index]="__orderOutputBudget";}
            else if(workspaceOrders.Any(next=>next>index)){
                var name=$"__orderValueBudget{index}";builder.AppendLine($"// [投影{index}/保持] 後続整列に保持される中間値のpayloadもquery scopeへ計上する。");
                builder.AppendLine($"using var {name}=new global::Seamless.Linq.QueryOrdering.KeyBudget<{tail[index].OutputType}>({scope},0);");budgets.Add(name);selections[index]=name;
            }
        }
        builder.AppendLine($"using var __orderInputBudget=new global::Seamless.Linq.QueryOrdering.InputBudget<{inputType}>({scope},[{string.Join(",",budgets)}]);var __orderedSource=__orderInputBudget.Read({inputExpression});");
        var current="__orderedSource";
        builder.AppendLine("// [評価/例外/安定性] 通常LINQの整列と終端を使用。全キー段の評価順、同値キーの元順、First/Lastの短絡を維持し、callbackをworkerへ移さない。");
        if(materializes){
            builder.AppendLine("// [結果/配列] BCLのToArray/ToListを直接呼び、1要素のときに整列キーを呼ばない最適化も維持。出力と有界な拡大用配列を割当前に予約する。");
        }
        for(var index=start;index<tail.Length;index++){
            var item=tail[index];if(item.Kind==PipelineStageKind.AsEnumerable){continue;}
            var next=$"__ordered{index}";string argument;
            if(IsTransactionalOrderStage(item)){
                if(item.Func is{} function){argument=this.TransactionalOrderLambda(function,$"__orderKeys{index}",item.Kind.ToString());}
                else{builder.AppendLine("// [整列/恒等キー] 正規化済みのx=>xを直接渡し、キー配列と参照payloadの予約を維持する。");argument=$"({item.InputType} __identityOrderItem)=>__orderKeys{index}.Capture(__identityOrderItem)";}
                builder.AppendLine($"var {next}=global::System.Linq.Enumerable.{item.Kind}({current},{argument},__orderComparer{index});");current=next;continue;
            }
            if(item.Kind is PipelineStageKind.Select or PipelineStageKind.Where){selections.TryGetValue(index,out var outputBudget);argument=this.TransactionalOrderLambda(item.Func!,outputBudget,item.Kind.ToString());}
            else{argument=$"{result}Limit{index}";}
            builder.AppendLine($"var {next}=global::System.Linq.Enumerable.{item.Kind}({current},{argument});");current=next;
        }
        if(materializes){builder.AppendLine($"return global::System.Linq.Enumerable.{terminalKind}({current});");}
        else if(terminalKind is PipelineTerminalKind.ToDictionary or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy){
            this.AppendTransactionalOutputState(builder,scope,result,actualTypeName,pipeline.ElementType,[]);
            this.AppendTransactionalOutputConsumer(builder,result,pipeline.ElementType,[]);
            builder.AppendLine($"foreach(var __output in {current}){{{result}Consume(__output);}}");this.AppendTransactionalOutputReturn(builder,result,actualTypeName,[]);
        }else{builder.AppendLine($"return ({actualTypeName})global::System.Linq.Enumerable.{terminalKind}({current});");}
    }
    private string TransactionalOrderLambda(FuncFragment function,string? keys,string tag){
        var arguments=function.ParameterTypeNames.Select(static (_,index)=>$"__orderItem{index}").ToArray();
        var parameters=string.Join(",",function.ParameterTypeNames.Select((type,index)=>type+" "+arguments[index]));
        var builder=new StringBuilder();builder.AppendLine($"({parameters})=>{{{function.ReturnTypeName} __orderKey=default!;");
        builder.AppendLine(this.FuncAssignStatic(function,tag,"__orderKey",arguments));
        if(keys is null){builder.AppendLine("return __orderKey;}");}else{builder.AppendLine($"return {keys}.Capture(__orderKey);}}");}
        return builder.ToString();
    }
    private void AppendTransactionalOrderArguments(StringBuilder builder,string prefix,string actualType,PipelineStage[] before,PipelineStage[] tail,int manualPrefix){
        builder.AppendLine($"// [取引終端] terminal={terminalKind}; 引数は入力演算後の元の呼出し順で一度保存。比較器式の副作用で、先行するSkip/Takeの値を読み替えない。");
        var empty=new List<string>();
        void Window(PipelineStage stage,string window,int index,bool count){
            if(stage.Kind is not(PipelineStageKind.Skip or PipelineStageKind.Take)){return;}
            builder.AppendLine($"int {window}Limit{index}={stage.Value};");
            if(count){builder.AppendLine($"int {window}Count{index}=0;");}
            if(stage.Kind==PipelineStageKind.Take){empty.Add($"{window}Limit{index}<=0");}
        }
        for(var index=0;index<before.Length;index++){Window(before[index],prefix+"Groups",index,true);}
        for(var index=0;index<tail.Length;index++){
            var stage=tail[index];
            if(IsTransactionalOrderStage(stage)){builder.AppendLine($"var __orderComparer{index}=(global::System.Collections.Generic.IComparer<{TransactionalOrderKeyType(stage)}>?)({stage.Value??"null"});");}
            Window(stage,prefix,index,manualPrefix<0||index<manualPrefix);
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary){builder.AppendLine($"var {prefix}Comparer=(global::System.Collections.Generic.IEqualityComparer<{func1!.ReturnTypeName}>?)({comparerValue??"null"});");}
        if(empty.Count>0){builder.AppendLine("// [短絡/評価] 後続の比較器式も評価してから、入力/selector/Lookup/workerを開始せず空の終端へ進む。");builder.AppendLine($"if({string.Join("||",empty)}){{{this.TransactionalEmpty(prefix,actualType)}}}");}
    }
}
