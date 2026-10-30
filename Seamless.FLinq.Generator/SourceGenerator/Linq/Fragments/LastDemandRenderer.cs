using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.FLinq.Generator;

// 元のterminal overloadを保持する。predicateをWhere stageへ変換しない。
internal readonly record struct LastTerminalContract(bool OrDefault,FuncFragment? Predicate){
    internal bool HasPredicate=>this.Predicate is not null;
    internal string NoMatchThrow{
        get{
            if(this.HasPredicate){
                return "throw new global::System.InvalidOperationException(\"Sequence contains no matching element\");";
            }
            return "throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
        }
    }
}

// generator内部だけの描画器。実行時delegate/protocol/public interfaceは追加しない。
internal sealed class LastDemandRenderer(int number):Fragment(number){
    private sealed record Step(bool IsWhere,FuncFragment Function,string OutputType);
    public override string StructMethod=>string.Empty;
    public override string GetEnumeratorStruct=>string.Empty;
    public override string GetTryGetNextBody=>string.Empty;

    internal static string? TryRender(Fragment owner,Enumerable input,LastTerminalContract contract,string destination,string destinationType){
        var steps=new List<Step>();
        if(!Collect(input,steps,out var root)){
            return null;
        }
        // Select→Whereはforward境界。raw array/Listだけはtail需要を分離して描ける。
        var seenSelect=false;
        var forwardPrefix=false;
        foreach(var step in steps){
            if(!step.IsWhere){seenSelect=true;}
            else if(seenSelect){forwardPrefix=true;}
            if(!CanRender(step.Function)){return null;}
        }
        // custom/opaque rootのprefix iterator EOF/Dispose境界は別契約として未対応。
        if(forwardPrefix&&(contract.HasPredicate||root is not (ArrayRoot or ListRoot))){return null;}
        if(contract.Predicate is { } predicate&&!CanRender(predicate)){
            return null;
        }
        var renderer=new LastDemandRenderer(owner.FragmentIndex);
        renderer.ApplyRenderContext(owner.RenderContext);
        return renderer.Render(root,steps,contract,destination,destinationType,forwardPrefix);
    }

    private static bool CanRender(FuncFragment function)=>
        (function.CanInlineAssign||function.RequiresStatementInlining)
        &&!function.CanInvokeAsDelegate
        &&!function.RequiresResultTypeParameter
        &&function.HoistedLocals.Length==0;

    private static bool Collect(Enumerable source,List<Step> steps,out SourceRootBase root){
        root=null!;
        if(source.GetType()==typeof(Where_predicate)){
            var where=(Where_predicate)source;
            if(!Collect(where.Source,steps,out root)){return false;}
            steps.Add(new Step(true,where.Predicate,where.ElementType));
            return true;
        }
        if(source.GetType()==typeof(Select_selector)){
            var select=(Select_selector)source;
            if(select.RequiresResultTypeParameter||!Collect(select.Source,steps,out root)){return false;}
            steps.Add(new Step(false,select.Selector,select.ElementType));
            return true;
        }
        if(source is PipelineEnumerable pipeline){
            if(!Collect(pipeline.Source,steps,out root)){return false;}
            foreach(var stage in pipeline.Stages){
                if(stage.UsesIndex||stage.Func is null
                    ||stage.Kind is not (PipelineStageKind.Where or PipelineStageKind.Select)){
                    return false;
                }
                steps.Add(new Step(stage.Kind==PipelineStageKind.Where,stage.Func,stage.OutputType));
            }
            return true;
        }
        if(source is SourceRootBase sourceRoot&&sourceRoot.BindLastSource("__lastBindingProbe") is not null){
            root=sourceRoot;
            return true;
        }
        return false;
    }

    private string Local(string role,string suffix=""){
        var preferred=$"__last{role}{this.FragmentIndex}{suffix}";
        return GeneratedNames.Current?.StableLocal($"LastDemand:{this.FragmentIndex}:{role}:{suffix}",preferred)??preferred;
    }
    private static string MarkFound(LastTerminalContract contract,string found){
        // predicate付きOrDefaultは結果変数の既定値をそのまま返せる。
        if(contract.OrDefault&&contract.HasPredicate){return string.Empty;}
        return $"{found}=true;";
    }
    private string Render(SourceRootBase root,IReadOnlyList<Step> steps,LastTerminalContract contract,string destination,string destinationType,bool forwardPrefix){
        var source=this.Local("Source");
        var found=this.Local("Found");
        var builder=new StringBuilder();
        builder.AppendLine($"{destinationType} {destination}=default!;");
        builder.AppendLine("{");
        builder.AppendLine(IndentLines(root.BindLastSource(source)!.NormalizeWhitespace().ToFullString(),"    "));
        builder.AppendLine($"    if({source} is null) throw new global::System.ArgumentNullException(\"source\");");
        if(!contract.OrDefault||!contract.HasPredicate){builder.AppendLine($"    var {found}=false;");}
        var array=this.Local("Array");
        var list=this.Local("List");
        var span=this.Local("Span");
        var whereCount=steps.Count(static step=>step.IsWhere);
        if(forwardPrefix){
            var forwardSource=source;
            // SystemのListSelect iteratorはList<T>の非virtual GetEnumeratorを使う。
            if(root is ListRoot){forwardSource=$"((global::System.Collections.Generic.List<{root.ElementType}>){source})";}
            this.AppendForward(builder,forwardSource,root.ElementType,steps,contract,destination,found,true);
        }else if(contract.HasPredicate&&steps.Count==0){
            this.AppendRawPredicate(builder,source,root.ElementType,contract,destination,found);
        }else if(contract.HasPredicate){
            // predicate終端は上流iteratorを通常列挙する。TryGetLast専用需要は渡さない。
            this.AppendForward(builder,source,root.ElementType,steps,contract,destination,found,false);
        }else if(whereCount==0){
            this.AppendRawLastWithTail(builder,source,root.ElementType,steps,contract,destination,found);
        }else{
            builder.AppendLine($"    if({source} is {root.ElementType}[] {array}){{");
            this.AppendBackwardWhere(builder,$"{array}",$"{array}.Length",steps,destination,found,"        ");
            builder.AppendLine($"    }}else if({source} is global::System.Collections.Generic.List<{root.ElementType}> {list}){{");
            builder.AppendLine($"        var {span}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({list});");
            this.AppendBackwardWhere(builder,$"{span}",$"{span}.Length",steps,destination,found,"        ");
            builder.AppendLine("    }else{");
            // 一般IList/Range相当をarray/List専用Whereへ分類しない。
            this.AppendForward(builder,source,root.ElementType,steps,contract,destination,found,true,"        ");
            builder.AppendLine("    }");
        }
        if(!contract.OrDefault){builder.AppendLine($"    if(!{found}) {contract.NoMatchThrow}");}
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private void AppendRawPredicate(StringBuilder builder,string source,string elementType,LastTerminalContract contract,string destination,string found){
        var list=this.Local("Indexed");
        var index=this.Local("Index");
        var item=this.Local("Item");
        builder.AppendLine($"    if({source} is global::System.Collections.Generic.IList<{elementType}> {list}){{");
        builder.AppendLine($"        for(var {index}={list}.Count-1;{index}>=0;{index}--){{");
        // IListはlive indexer。predicate前の値を保持して成功時に返す。
        builder.AppendLine($"            var {item}={list}[{index}];");
        var keep=this.AppendPredicate(builder,contract.Predicate!,item,"            ","Terminal");
        builder.AppendLine($"            if({keep}){{{destination}={item};{MarkFound(contract,found)}break;}}");
        builder.AppendLine("        }");
        builder.AppendLine("    }else{");
        this.AppendForward(builder,source,elementType,Array.Empty<Step>(),contract,destination,found,false,"        ");
        builder.AppendLine("    }");
    }

    private void AppendRawLastWithTail(StringBuilder builder,string source,string elementType,IReadOnlyList<Step> steps,LastTerminalContract contract,string destination,string found){
        var list=this.Local("Indexed");
        var count=this.Local("Count");
        var item=this.Local("Item");
        builder.AppendLine($"    if({source} is global::System.Collections.Generic.IList<{elementType}> {list}){{");
        builder.AppendLine($"        var {count}={list}.Count;");
        builder.AppendLine($"        if({count}>0){{");
        builder.AppendLine($"            var {item}={list}[{count}-1];");
        var selected=this.AppendSelects(builder,steps,0,item,"            ");
        builder.AppendLine($"            {destination}={selected};{found}=true;");
        builder.AppendLine("        }");
        builder.AppendLine("    }else{");
        this.AppendForward(builder,source,elementType,steps,contract,destination,found,true,"        ");
        builder.AppendLine("    }");
    }

    private void AppendBackwardWhere(StringBuilder builder,string location,string count,IReadOnlyList<Step> steps,string destination,string found,string indent){
        var index=this.Local("Index");
        var item=this.Local("Item");
        builder.AppendLine($"{indent}for(var {index}={count}-1;{index}>=0;{index}--){{");
        builder.AppendLine($"{indent}    var {item}={location}[{index}];");
        var firstSelect=0;
        while(firstSelect<steps.Count&&steps[firstSelect].IsWhere){
            var keep=this.AppendPredicate(builder,steps[firstSelect].Function,item,indent+"    ","Where"+firstSelect);
            builder.AppendLine($"{indent}    if(!{keep}) continue;");
            firstSelect++;
        }
        // System Array/ListWhereの成功値はpredicate後に同じ位置を再読込する。
        var matched=this.Local("Matched");
        builder.AppendLine($"{indent}    var {matched}={location}[{index}];");
        var selected=this.AppendSelects(builder,steps,firstSelect,matched,indent+"    ");
        builder.AppendLine($"{indent}    {destination}={selected};{found}=true;break;");
        builder.AppendLine($"{indent}}}");
    }

    private void AppendForward(StringBuilder builder,string source,string elementType,IReadOnlyList<Step> steps,LastTerminalContract contract,string destination,string found,bool deferTail,string indent="    "){
        var enumerator=this.Local("Enumerator");
        var item=this.Local("Item");
        var matched=this.Local("Matched");
        var prefixCount=steps.Count;
        if(deferTail){
            while(prefixCount>0&&!steps[prefixCount-1].IsWhere){prefixCount--;}
        }
        builder.AppendLine($"{indent}using(var {enumerator}={source}.GetEnumerator()){{");
        var matchedType=elementType;
        if(prefixCount>0){matchedType=steps[prefixCount-1].OutputType;}
        if(deferTail){builder.AppendLine($"{indent}    {matchedType} {matched}=default!;");}
        builder.AppendLine($"{indent}    while({enumerator}.MoveNext()){{");
        builder.AppendLine($"{indent}        var {item}={enumerator}.Current;");
        var current=item;
        for(var index=0;index<prefixCount;index++){
            var step=steps[index];
            if(step.IsWhere){
                var keep=this.AppendPredicate(builder,step.Function,current,indent+"        ","Where"+index);
                builder.AppendLine($"{indent}        if(!{keep}) continue;");
            }else{
                current=this.AppendSelect(builder,step,index,current,indent+"        ");
            }
        }
        if(contract.Predicate is { } predicate){
            var keep=this.AppendPredicate(builder,predicate,current,indent+"        ","Terminal");
            builder.AppendLine($"{indent}        if(!{keep}) continue;");
        }
        if(deferTail){builder.AppendLine($"{indent}        {matched}={current};{found}=true;");}
        else{builder.AppendLine($"{indent}        {destination}={current};{MarkFound(contract,found)}");}
        builder.AppendLine($"{indent}    }}");
        if(deferTail){
            builder.AppendLine($"{indent}    if({found}){{");
            var selected=this.AppendSelects(builder,steps,prefixCount,matched,indent+"        ");
            builder.AppendLine($"{indent}        {destination}={selected};");
            builder.AppendLine($"{indent}    }}");
        }
        builder.AppendLine($"{indent}}}");
    }

    private string AppendPredicate(StringBuilder builder,FuncFragment predicate,string item,string indent,string suffix){
        var keep=this.Local("Keep","_"+suffix);
        if(suffix.StartsWith("Where",StringComparison.Ordinal)){
            builder.AppendLine(indent+this.CounterCall("IncrementLogicalWhere"));
        }
        builder.AppendLine($"{indent}bool {keep}=default!;");
        builder.AppendLine(this.FuncAssignStaticIndented(indent,predicate,"Last",keep,item));
        return keep;
    }

    private string AppendSelect(StringBuilder builder,Step step,int index,string item,string indent){
        var selected=this.Local("Selected","_"+index);
        builder.AppendLine(indent+this.CounterCall("IncrementLogicalSelect"));
        builder.AppendLine($"{indent}{step.OutputType} {selected}=default!;");
        builder.AppendLine(this.FuncAssignStaticIndented(indent,step.Function,"Last",selected,item));
        return selected;
    }

    private string AppendSelects(StringBuilder builder,IReadOnlyList<Step> steps,int start,string item,string indent){
        var current=item;
        for(var index=start;index<steps.Count;index++){
            current=this.AppendSelect(builder,steps[index],index,current,indent);
        }
        return current;
    }
}
