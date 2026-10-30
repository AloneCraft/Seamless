using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

// 単一OrderByのpredicate終端需要。sort列挙ではなく元source順のP→合格時Kを描く。
// ThenBy/custom comparer/上流graphの所有境界は未対応として呼出し側の既存経路に残す。
internal sealed class OrderedLastDemandRenderer(int number):Fragment(number){
    public override string StructMethod=>string.Empty;
    public override string GetEnumeratorStruct=>string.Empty;
    public override string GetTryGetNextBody=>string.Empty;

    internal static string? TryRender(Fragment owner,Enumerable input,LastTerminalContract contract,string destination,string destinationType){
        if(contract.Predicate is not { } predicate||!CanRender(predicate)){
            return null;
        }
        SourceRootBase? root=null;
        FuncFragment? key=null;
        var descending=false;
        if(input is OrderingBase order
            &&!order.OwnIsThenBy
            &&order.MaxTakeCountExpression is null
            &&order.OwnSource is SourceRootBase orderRoot
            &&IsNullComparer(order.OwnRawComparer)){
            root=orderRoot;
            key=order.OwnKeySelector;
            descending=order.OwnDescending;
        }else if(input is PipelineEnumerable pipeline
            &&pipeline.Stages.Count==1
            &&pipeline.Source is SourceRootBase pipelineRoot){
            var stage=pipeline.Stages[0];
            if(stage.Kind is not (PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)
                ||stage.Func is null||stage.UsesIndex||!IsNullComparer(stage.Value)){
                return null;
            }
            root=pipelineRoot;
            key=stage.Func;
            descending=stage.Descending;
        }
        if(root is null||key is null||!CanRender(key)||root.BindLastSource("__orderedLastBindingProbe") is null){
            return null;
        }
        var renderer=new OrderedLastDemandRenderer(owner.FragmentIndex);
        renderer.ApplyRenderContext(owner.RenderContext);
        return renderer.Render(root,key,predicate,descending,contract,destination,destinationType);
    }

    private static bool CanRender(FuncFragment function)=>
        (function.CanInlineAssign||function.RequiresStatementInlining)
        &&!function.CanInvokeAsDelegate
        &&!function.RequiresResultTypeParameter
        &&function.HoistedLocals.Length==0;

    private static bool IsNullComparer(string? expression){
        if(expression is null){return false;}
        ExpressionSyntax syntax=SyntaxFactory.ParseExpression(expression);
        if(syntax.ContainsDiagnostics){return false;}
        while(syntax is ParenthesizedExpressionSyntax parenthesized){syntax=parenthesized.Expression;}
        return syntax.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private string Local(string role){
        var preferred=$"__orderedLast{role}{this.FragmentIndex}";
        return GeneratedNames.Current?.StableLocal($"OrderedLastDemand:{this.FragmentIndex}:{role}",preferred)??preferred;
    }

    private string Render(SourceRootBase root,FuncFragment key,FuncFragment predicate,bool descending,LastTerminalContract contract,string destination,string destinationType){
        var source=this.Local("Source");
        var enumerator=this.Local("Enumerator");
        var item=this.Local("Item");
        var found=this.Local("Found");
        var keep=this.Local("Keep");
        var bestKey=this.Local("BestKey");
        var currentKey=this.Local("Key");
        var comparer=this.Local("Comparer");
        var keyType=FuncReturnTypeName(key);
        var comparison=$"{comparer}.Compare({currentKey},{bestKey})";
        if(descending){comparison=$"{comparer}.Compare({bestKey},{currentKey})";}
        var builder=new StringBuilder();
        builder.AppendLine($"{destinationType} {destination}=default!;");
        builder.AppendLine("{");
        builder.AppendLine(IndentLines(root.BindLastSource(source)!.NormalizeWhitespace().ToFullString(),"    "));
        builder.AppendLine($"    if({source} is null) throw new global::System.ArgumentNullException(\"source\");");
        builder.AppendLine($"    var {comparer}=global::System.Collections.Generic.Comparer<{keyType}>.Default;");
        builder.AppendLine($"    var {found}=false;");
        builder.AppendLine($"    {keyType} {bestKey}=default!;");
        builder.AppendLine($"    using(var {enumerator}={source}.GetEnumerator()){{");
        builder.AppendLine($"        while({enumerator}.MoveNext()){{");
        builder.AppendLine($"            var {item}={enumerator}.Current;");
        builder.AppendLine($"            bool {keep}=default!;");
        builder.AppendLine(this.FuncAssignStaticIndented("            ",predicate,"Last",keep,item));
        builder.AppendLine($"            if(!{keep}) continue;");
        builder.AppendLine($"            {keyType} {currentKey}=default!;");
        builder.AppendLine(this.FuncAssignStaticIndented("            ",key,"OrderBy",currentKey,item));
        // 初回だけ比較しない。以後は等値も後の候補を採る(Systemのstable Last)。
        builder.AppendLine($"            if(!{found}||{comparison}>=0){{");
        builder.AppendLine($"                {bestKey}={currentKey};{destination}={item};{found}=true;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        if(!contract.OrDefault){builder.AppendLine($"    if(!{found}) {contract.NoMatchThrow}");}
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }
}
