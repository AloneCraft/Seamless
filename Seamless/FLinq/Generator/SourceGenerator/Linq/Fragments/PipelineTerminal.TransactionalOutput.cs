using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal sealed partial class PipelineTerminal{
    private bool SupportsTransactionalOutput=>terminalValue is null
        &&terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.ToDictionary
            or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any
            or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault or PipelineTerminalKind.Sum
            or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy
        &&(terminalKind is not(PipelineTerminalKind.ToDictionary or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy)||func1 is not null)
        &&(terminalKind!=PipelineTerminalKind.Sum||TransactionalSumType(pipeline.ElementType) is not null)
        &&new[]{func1,func2}.All(static function=>function is null||!function.RequiresResultTypeParameter);
    private static string? TransactionalSumType(string type){
        type=type.Replace("global::",string.Empty).Trim();
        if(type.EndsWith("?",StringComparison.Ordinal)){type=type.Substring(0,type.Length-1);}
        else if(type.StartsWith("System.Nullable<",StringComparison.Ordinal)&&type.EndsWith(">",StringComparison.Ordinal)){type=type.Substring(16,type.Length-17);}
        switch(type){
            case "int":case "System.Int32":return "int";
            case "long":case "System.Int64":return "long";
            case "decimal":case "System.Decimal":return "decimal";
            case "float":case "System.Single":return "float";
            case "double":case "System.Double":return "double";
            default:return null;
        }
    }
    private static bool IsTransactionalTail(IReadOnlyList<PipelineStage> stages)=>stages.All(static stage=>{
        if(stage.Kind==PipelineStageKind.AsEnumerable){return true;}
        if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select){return !stage.UsesIndex&&stage.Func is{RequiresResultTypeParameter:false,HoistedLocals.Length:0};}
        if(stage.Kind is not(PipelineStageKind.Skip or PipelineStageKind.Take)||stage.Value is null){return false;}
        var expression=SyntaxFactory.ParseExpression(stage.Value);
        return expression is IdentifierNameSyntax or LiteralExpressionSyntax or PrefixUnaryExpressionSyntax{RawKind:(int)SyntaxKind.UnaryMinusExpression,Operand:LiteralExpressionSyntax};
    });
    private IEnumerable<FuncFragment> TransactionalOutputFunctions(IReadOnlyList<PipelineStage> stages)=>stages.Select(static stage=>stage.Func).Concat(new[]{func1,func2}).Where(static function=>function is not null).Select(static function=>function!);
    private string TransactionalContinue(string prefix,IReadOnlyList<PipelineStage> stages){
        var limits=stages.Select(static(stage,index)=>(stage,index)).Where(static item=>item.stage.Kind==PipelineStageKind.Take).Select(item=>$"{prefix}Count{item.index}<{prefix}Limit{item.index}").ToArray();
        if(limits.Length==0){return "true";}return string.Join("&&",limits);
    }
    private static int TransactionalLastPrefix(IReadOnlyList<PipelineStage> stages){for(var index=stages.Count-1;index>=0;index--){if(stages[index].Kind==PipelineStageKind.Where){return index+1;}}return 0;}
    private string TransactionalEmpty(string prefix,string actualType){
        switch(terminalKind){
            case PipelineTerminalKind.ToArray:return $"return global::System.Array.Empty<{pipeline.ElementType}>();";
            case PipelineTerminalKind.ToList:return $"return new global::System.Collections.Generic.List<{pipeline.ElementType}>();";
            case PipelineTerminalKind.ToDictionary:return $"return new global::System.Collections.Generic.Dictionary<{func1!.ReturnTypeName},{func2?.ReturnTypeName??pipeline.ElementType}>({prefix}Comparer);";
            case PipelineTerminalKind.First:case PipelineTerminalKind.Last:case PipelineTerminalKind.MinBy:case PipelineTerminalKind.MaxBy:return "throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
            case PipelineTerminalKind.Sum:case PipelineTerminalKind.Count:case PipelineTerminalKind.LongCount:return $"return ({actualType})0;";
            case PipelineTerminalKind.Any:return "return false;";
            default:return $"return default({actualType})!;";
        }
    }
    private void AppendTransactionalOutputPreamble(StringBuilder builder,string prefix,string actualType,IReadOnlyList<PipelineStage> stages,bool initializeComparer=true,string? resultPrefix=null){
        if(initializeComparer){builder.AppendLine($"// [取引終端] terminal={terminalKind}; 後段は元順のproducer上。スカラーには全件配列を作らず、早期終了後のcallbackを呼ばない。");}
        else{builder.AppendLine("// [グループ範囲] 正規化後のSkip/Takeを投影前で評価し、捨てるgroupの集計を呼ばない。");}
        if(initializeComparer&&terminalKind==PipelineTerminalKind.ToDictionary){builder.AppendLine($"var {prefix}Comparer=(global::System.Collections.Generic.IEqualityComparer<{func1!.ReturnTypeName}>?)({comparerValue??"null"});");}
        for(var index=0;index<stages.Count;index++){
            if(stages[index].Kind is not(PipelineStageKind.Skip or PipelineStageKind.Take)){continue;}
            builder.AppendLine($"int {prefix}Limit{index}={stages[index].Value};int {prefix}Count{index}=0;");
            if(stages[index].Kind==PipelineStageKind.Take){builder.AppendLine($"if({prefix}Limit{index}<=0){{{this.TransactionalEmpty(resultPrefix??prefix,actualType)}}}");}
        }
    }
    private void AppendTransactionalOutputState(StringBuilder builder,string scope,string prefix,string actualType,string inputType,IReadOnlyList<PipelineStage> stages){
        if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList){builder.AppendLine($"using var {prefix}=new global::Seamless.Execution.QueryResultBuffer<{pipeline.ElementType}>({scope});");return;}
        if(terminalKind==PipelineTerminalKind.ToDictionary){builder.AppendLine($"using var {prefix}=new global::Seamless.Execution.QueryDictionaryBuffer<{func1!.ReturnTypeName},{func2?.ReturnTypeName??pipeline.ElementType}>({scope},{prefix}Comparer);");return;}
        if(terminalKind==PipelineTerminalKind.Sum){
            var accumulator=TransactionalSumType(pipeline.ElementType)!;if(accumulator=="float"){accumulator="double";}
            builder.AppendLine($"{accumulator} {prefix}Value=0;");return;
        }
        var storedType=actualType;
        if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault){var lastPrefix=TransactionalLastPrefix(stages);storedType=inputType;if(lastPrefix>0){storedType=stages[lastPrefix-1].OutputType;}}
        builder.AppendLine($"{storedType} {prefix}Value=default!;");
        if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy){builder.AppendLine($"bool {prefix}Found=false;");}
        if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy){builder.AppendLine($"{func1!.ReturnTypeName} {prefix}Key=default!;");}
    }
    private void AppendTransactionalOutputConsumer(StringBuilder builder,string prefix,string inputType,IReadOnlyList<PipelineStage> stages){
        var more=this.TransactionalContinue(prefix,stages);
        builder.AppendLine("// [呼出し] 終端は直接呼ぶlocal functionへ融合し、結果一件ごとのdelegate呼出しと追加delegate確保を作らない。");
        builder.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        var modifier=string.Empty;
        if(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseTypeName(inputType) is Microsoft.CodeAnalysis.CSharp.Syntax.TupleTypeSyntax
            ||inputType.StartsWith("global::System.ValueTuple",StringComparison.Ordinal)){
            modifier="in ";
            builder.AppendLine("// [中間値/参照] producerの局所値を同期終端へ読取り専用参照で渡す。保持する結果は値としてコピーする。");
        }
        builder.AppendLine($"bool {prefix}Consume({modifier}{inputType} __output){{");var value="__output";
        for(var index=0;index<stages.Count;index++){
            var stage=stages[index];builder.AppendLine($"// [後段{index}] {stage.Kind}: 評価位置と入力順を維持し、変換/述語をworkerへ移さない。");
            if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault&&stage.Kind==PipelineStageKind.Select&&index>=TransactionalLastPrefix(stages)){builder.AppendLine("// [末尾/投影] 後続述語がない投影は、最後に選ばれた値へだけ実行する。");continue;}
            if(stage.Kind==PipelineStageKind.Where){builder.AppendLine($"bool __keep{index}=false;");builder.AppendLine(this.FuncAssignStatic(stage.Func!,"Where",$"__keep{index}",value));builder.AppendLine($"if(!__keep{index}){{return {more};}}");}
            else if(stage.Kind==PipelineStageKind.Select){var next=$"__output{index}";builder.AppendLine($"{stage.OutputType} {next}=default!;");builder.AppendLine(this.FuncAssignStatic(stage.Func!,"Select",next,value));value=next;}
            else if(stage.Kind==PipelineStageKind.Skip){builder.AppendLine($"if({prefix}Count{index}<{prefix}Limit{index}){{{prefix}Count{index}++;return {more};}}");}
            else if(stage.Kind==PipelineStageKind.Take){builder.AppendLine($"if({prefix}Count{index}>={prefix}Limit{index}){{return false;}}{prefix}Count{index}++;");}
        }
        if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList){builder.AppendLine($"{prefix}.Add({value});");}
        else if(terminalKind==PipelineTerminalKind.ToDictionary){
            builder.AppendLine($"{func1!.ReturnTypeName} __outputKey=default!;");builder.AppendLine(this.FuncAssignStatic(func1,"ToDictionary","__outputKey",value));
            if(func2 is not null){builder.AppendLine($"{func2.ReturnTypeName} __outputValue=default!;");builder.AppendLine(this.FuncAssignStatic(func2,"ToDictionary","__outputValue",value));value="__outputValue";}
            builder.AppendLine($"{prefix}.Add(__outputKey,{value});");
        }else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount){builder.AppendLine($"{prefix}Value=checked({prefix}Value+1);");}
        else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy){
            // 一般経路の MinBy/MaxBy と同じく既定の比較器で比べ、同じキーなら先に来た値を残す。キーは1件に1回だけ計算する。
            var comparison=terminalKind==PipelineTerminalKind.MaxBy?">0":"<0";
            builder.AppendLine($"{func1!.ReturnTypeName} __outputKey=default!;");builder.AppendLine(this.FuncAssignStatic(func1,terminalKind.ToString(),"__outputKey",value));
            builder.AppendLine($"if(!{prefix}Found||global::System.Collections.Generic.Comparer<{func1.ReturnTypeName}>.Default.Compare(__outputKey,{prefix}Key){comparison}){{{prefix}Value={value};{prefix}Key=__outputKey;{prefix}Found=true;}}");
        }
        else if(terminalKind==PipelineTerminalKind.Sum){
            var nullable=pipeline.ElementType.EndsWith("?",StringComparison.Ordinal)||pipeline.ElementType.Contains("Nullable<");
            if(nullable){builder.AppendLine($"if({value}.HasValue){{{prefix}Value=checked({prefix}Value+{value}.GetValueOrDefault());}}");}
            else{builder.AppendLine($"{prefix}Value=checked({prefix}Value+{value});");}
        }else{
            if(terminalKind==PipelineTerminalKind.Any){builder.AppendLine($"{prefix}Value=true;return false;");}
            else{
                builder.AppendLine($"{prefix}Value={value};");
                if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault){if(terminalKind==PipelineTerminalKind.First){builder.AppendLine($"{prefix}Found=true;");}builder.AppendLine("return false;");}
                else if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault){builder.AppendLine($"{prefix}Found=true;");}
            }
        }
        if(terminalKind is not(PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault)){builder.AppendLine($"return {more};");}
        builder.AppendLine("}");
    }
    private void AppendTransactionalOutputReturn(StringBuilder builder,string prefix,string actualType,IReadOnlyList<PipelineStage> stages){
        if(terminalKind==PipelineTerminalKind.ToArray){builder.AppendLine($"return {prefix}.ToArray();");}
        else if(terminalKind==PipelineTerminalKind.ToList){builder.AppendLine($"return {prefix}.ToList();");}
        else if(terminalKind==PipelineTerminalKind.ToDictionary){builder.AppendLine($"return {prefix}.ToDictionary();");}
        else{
            if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault){
                builder.AppendLine("// [末尾/結果] 必要な述語までを通った最後の値だけを投影。選ばれない値や空入力のselectorは呼ばない。");
                builder.AppendLine($"if({prefix}Found){{");var current=prefix+"Value";
                for(var index=TransactionalLastPrefix(stages);index<stages.Count;index++){
                    var stage=stages[index];if(stage.Kind!=PipelineStageKind.Select){continue;}
                    var next=$"{prefix}Last{index}";builder.AppendLine($"{stage.OutputType} {next}=default!;");builder.AppendLine(this.FuncAssignStatic(stage.Func!,"Select",next,current));current=next;
                }
                builder.AppendLine($"return ({actualType}){current};}}");
                if(terminalKind==PipelineTerminalKind.Last){builder.AppendLine("throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");}
                else{builder.AppendLine($"return default({actualType})!;");}return;
            }
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.Last or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy){builder.AppendLine($"if(!{prefix}Found){{throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();}}");}
            builder.AppendLine($"return ({actualType}){prefix}Value;");
        }
    }
}
