using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.FLinq;

internal enum OptimizationLayer{
    Syntax,
    Pipeline,
}

internal sealed record OptimizationTraceEvent(
    int Sequence,
    int RunId,
    OptimizationLayer Layer,
    string Phase,
    string RuleId,
    int Round,
    int Pass,
    int VisitNumber,
    string NodeId,
    string NodePath,
    int? Depth,
    int? StageIndex,
    string ParentKind,
    bool Left,
    bool Right,
    bool Inner,
    string Before,
    string After,
    bool Changed,
    bool Revisited);

internal sealed record OptimizationLayerTermination(
    int RunId,
    OptimizationLayer Layer,
    int RoundCount,
    OptimizationTermination Termination);

internal sealed record OptimizationTraceSummary(
    int RewriteCount,
    int MaxRewriteDepth,
    int OptimizationPassCount,
    bool ReachedFixedPoint,
    string[] AppliedRuleNames,
    bool RevisitedAfterRewrite,
    bool LeftSubtreeOptimized,
    bool RightSubtreeOptimized);

internal sealed record OptimizationTraceDocument(
    int SchemaVersion,
    string MethodKey,
    OptimizationTraceEvent[] Events,
    OptimizationLayerTermination[] LayerTerminations,
    OptimizationTraceSummary Summary);

internal sealed class OptimizationTraceCollector{
    private const int SchemaVersion=1;
    private readonly string methodKey;
    private readonly List<OptimizationTraceEvent> events =new();
    private readonly List<OptimizationLayerTermination> terminations =new();
    private readonly HashSet<string> visitedNodePaths=new(StringComparer.Ordinal);
    private int nextRunId;
    private int passCount;

    public OptimizationTraceCollector(string methodKey){
        this.methodKey=methodKey;
    }

    public int BeginRun(OptimizationLayer layer){
        _=layer;
        return ++this.nextRunId;
    }

    public int Checkpoint=>this.events.Count;

    public void MarkPass(){
        this.passCount++;
    }

    public void Rollback(int checkpoint){
        if(checkpoint<0||checkpoint>this.events.Count){
            throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }
        if(checkpoint==this.events.Count){
            return;
        }

        this.events.RemoveRange(checkpoint,this.events.Count-checkpoint);
        this.visitedNodePaths.Clear();
        foreach(var traceEvent in this.events){
            this.visitedNodePaths.Add($"{traceEvent.RunId}:{traceEvent.NodePath}");
        }
    }

    public void RecordSyntax(
        int runId,
        string phase,
        string ruleId,
        int round,
        int pass,
        int visitNumber,
        SyntaxNode root,
        SyntaxNode node,
        SyntaxNode rewritten){
        var nodePath=GetNodePath(root,node,out var depth);
        var nodeKey=$"{runId}:{nodePath}";
        var revisited=!this.visitedNodePaths.Add(nodeKey);
        GetNodeRelations(root,node,out var left,out var right,out var inner);
        this.events.Add(new(
            this.events.Count+1,
            runId,
            OptimizationLayer.Syntax,
            phase,
            ruleId,
            round,
            pass,
            visitNumber,
            $"S{runId}:{nodePath}",
            nodePath,
            depth,
            null,
            node.Parent?.Kind().ToString()??string.Empty,
            left,
            right,
            inner,
            OptimizationTraceText.Syntax(node),
            OptimizationTraceText.Syntax(rewritten),
            true,
            revisited));
    }

    public void RecordSyntaxChanges(
        int runId,
        string phase,
        string ruleId,
        int round,
        int pass,
        SyntaxNode before,
        SyntaxNode after){
        var visitNumber=0;
        this.RecordSyntaxChangesCore(
            runId,phase,ruleId,round,pass,before,before,after,ref visitNumber);
    }

    private void RecordSyntaxChangesCore(
        int runId,
        string phase,
        string ruleId,
        int round,
        int pass,
        SyntaxNode root,
        SyntaxNode before,
        SyntaxNode after,
        ref int visitNumber){
        if(SyntaxFactory.AreEquivalent(before,after)){
            return;
        }

        var beforeChildren=before.ChildNodes().ToArray();
        var afterChildren=after.ChildNodes().ToArray();
        var childChangeCount=0;
        if(beforeChildren.Length==afterChildren.Length){
            for(var index=0;index<beforeChildren.Length;index++){
                if(SyntaxFactory.AreEquivalent(beforeChildren[index],afterChildren[index])){
                    continue;
                }
                childChangeCount++;
                this.RecordSyntaxChangesCore(
                    runId,
                    phase,
                    ruleId,
                    round,
                    pass,
                    root,
                    beforeChildren[index],
                    afterChildren[index],
                    ref visitNumber);
            }
        }
        if(childChangeCount>0){
            return;
        }

        visitNumber++;
        this.RecordSyntax(
            runId,phase,ruleId,round,pass,visitNumber,root,before,after);
    }
    public void RecordPipeline(
        int runId,
        string phase,
        string ruleId,
        int round,
        int pass,
        int visitNumber,
        int stageIndex,
        string before,
        string after){
        var nodePath=$"/stage[{stageIndex}]";
        var nodeKey=$"{runId}:{nodePath}";
        var revisited=!this.visitedNodePaths.Add(nodeKey);
        this.events.Add(new(
            this.events.Count+1,
            runId,
            OptimizationLayer.Pipeline,
            phase,
            ruleId,
            round,
            pass,
            visitNumber,
            $"P{runId}:{nodePath}",
            nodePath,
            null,
            stageIndex,
            "Pipeline",
            false,
            false,
            false,
            before,
            after,
            true,
            revisited));
    }

    public void EndRun(int runId,OptimizationLayer layer,int roundCount,OptimizationTermination termination){
        this.terminations.Add(new(runId,layer,roundCount,termination));
    }

    public OptimizationTraceDocument CreateDocument(){
        var appliedRuleNames=this.events
            .Select(static traceEvent => traceEvent.RuleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var summary=new OptimizationTraceSummary(
            this.events.Count,
            this.events.Where(static traceEvent => traceEvent.Depth.HasValue)
                .Select(static traceEvent => traceEvent.Depth!.Value)
                .DefaultIfEmpty(0)
                .Max(),
            this.passCount,
            this.terminations.Count>0&&this.terminations.All(static termination => termination.Termination==OptimizationTermination.FixedPoint),
            appliedRuleNames,
            this.events.Any(static traceEvent => traceEvent.Revisited),
            this.events.Any(static traceEvent => traceEvent.Left),
            this.events.Any(static traceEvent => traceEvent.Right));
        return new(
            SchemaVersion,
            this.methodKey,
            this.events.ToArray(),
            this.terminations.ToArray(),
            summary);
    }

    public static string RenderSource(IEnumerable<OptimizationTraceDocument> documents){
        var builder=new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        foreach(var document in documents){
            var json=NetStandardJson.Serialize(document);
            var encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            builder.Append("// FLinqOptimizationTrace:").AppendLine(encoded);
        }
        return builder.ToString();
    }

    private static string GetNodePath(SyntaxNode root,SyntaxNode node,out int depth){
        if(ReferenceEquals(root,node)){
            depth=0;
            return "/";
        }

        var segments=new List<int>();
        var current=node;
        while(current.Parent is { } parent){
            var index=0;
            foreach(var child in parent.ChildNodes()){
                if(ReferenceEquals(child,current)){
                    break;
                }
                index++;
            }
            segments.Add(index);
            current=parent;
            if(ReferenceEquals(current,root)){
                segments.Reverse();
                depth=segments.Count;
                return "/"+string.Join("/",segments.Select(static segment => $"node[{segment}]"));
            }
        }

        depth=node.Ancestors().Count();
        return "/detached";
    }

    private static void GetNodeRelations(
        SyntaxNode root,
        SyntaxNode node,
        out bool left,
        out bool right,
        out bool inner){
        left=false;
        right=false;
        inner=!ReferenceEquals(root,node);
        var current=node;
        while(current.Parent is { } parent){
            if(parent is BinaryExpressionSyntax binary){
                if(ReferenceEquals(binary.Left,current)){
                    left=true;
                }
                if(ReferenceEquals(binary.Right,current)){
                    right=true;
                }
            }
            current=parent;
            if(ReferenceEquals(current,root)){
                return;
            }
        }
    }
}

internal static class OptimizationTraceText{
    public static string Syntax(SyntaxNode node)=>
        node.NormalizeWhitespace(elasticTrivia:false).ToFullString();

    public static string Pipeline(IReadOnlyList<PipelineStage> stages){
        var snapshots=stages.Select(static stage => CreateStageSnapshot(stage)).ToArray();
        return NetStandardJson.Serialize(snapshots);
    }

    public static int FirstChangedStageIndex(IReadOnlyList<PipelineStage> before,IReadOnlyList<PipelineStage> after){
        var count=Math.Min(before.Count,after.Count);
        for(var index=0;index<count;index++){
            if(!string.Equals(Stage(before[index]),Stage(after[index]),StringComparison.Ordinal)){
                return index;
            }
        }
        return count;
    }

    private static string Stage(PipelineStage stage)=>
        NetStandardJson.Serialize(CreateStageSnapshot(stage));

    private static PipelineStageSnapshot CreateStageSnapshot(PipelineStage stage)=>
        new(
            stage.Kind.ToString(),
            CreateFuncSnapshot(stage.Func),
            CreateFuncSnapshot(stage.SecondFunc),
            CreateFuncSnapshot(stage.ThirdFunc),
            stage.Value,
            stage.InputType,
            stage.OutputType,
            stage.SecondSource?.GetType().FullName,
            stage.SecondSource?.ElementType,
            stage.InnerElementType,
            stage.KeyType,
            stage.UsesIndex,
            stage.Descending,
            stage.InnerIsArray,
            stage.ContainsAnchor);

    private static FuncSnapshot? CreateFuncSnapshot(FuncFragment? func){
        if(func is null){
            return null;
        }
        return new(
            func.GetType().FullName??func.GetType().Name,
            func.ReturnTypeName,
            func.ParameterTypeNames,
            func.ParameterNames,
            func.CanGenerateMethod,
            func.CanInlineAssign,
            func.InlineBody,
            func.CapturedLocals.Select(static local => $"{local.Name}:{local.TypeName}").ToArray(),
            func.HoistedLocals.Select(static local => $"{local.Name}:{local.TypeName}:{local.ExpressionText}").ToArray(),
            func.BodyUnsafeAccessorCodes,
            func.SupportingMemberSources,
            func.RequiresStatementInlining,
            func.ContainsAnchor,
            func.DelegateTypeName,
            func.DelegateFieldName,
            func.DelegateInvokeExpr,
            func.DelegateConstructorArg,
            func is BlockFunc block ? block.BlockBody : null);
    }

    private sealed record PipelineStageSnapshot(
        string Kind,
        FuncSnapshot? Func,
        FuncSnapshot? SecondFunc,
        FuncSnapshot? ThirdFunc,
        string? Value,
        string InputType,
        string OutputType,
        string? SecondSourceType,
        string? SecondSourceElementType,
        string? InnerElementType,
        string? KeyType,
        bool UsesIndex,
        bool Descending,
        bool InnerIsArray,
        bool ContainsAnchor);

    private sealed record FuncSnapshot(
        string Type,
        string ReturnTypeName,
        string[] ParameterTypeNames,
        string[] ParameterNames,
        bool CanGenerateMethod,
        bool CanInlineAssign,
        string InlineBody,
        string[] CapturedLocals,
        string[] HoistedLocals,
        string[] BodyUnsafeAccessorCodes,
        string SupportingMemberSources,
        bool RequiresStatementInlining,
        bool ContainsAnchor,
        string? DelegateTypeName,
        string? DelegateFieldName,
        string? DelegateInvokeExpr,
        string? DelegateConstructorArg,
        string? BlockBody);
}
