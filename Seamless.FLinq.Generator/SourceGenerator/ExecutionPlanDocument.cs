using System.Reflection;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.FLinq.Generator;

internal sealed record ExecutionPlanNode(string Id,string Kind,string Label,string[] Inputs,string? QueryId,string? SourcePath,int Start,int Length,Dictionary<string,string> Properties);
internal sealed record ExecutionPlanDocument(int SchemaVersion,string MethodKey,string GeneratorVersion,string SourceHash,string GeneratedBodyHash,ExecutionPlanNode[] Nodes,OptimizationTraceDocument Trace,string GeneratorBuild);
internal sealed record ExecutionPlanOrigin(string QueryId,string Path,int Start,int Length,string Level,string Expression);

/// <summary>採用されたFragmentからのみスナップショットを作る。生成候補や説明コメントを逆解析しない。</summary>
internal static class ExecutionPlanWriter{
    internal static void Append(StringBuilder builder,FLinqIncrementalGenerator.Linq傍受対象型 target,GenerationState state,string body){
        var nodes=new List<ExecutionPlanNode>();
        var source=target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if(source is not null){
            var semanticModel=target.SemanticModel.Compilation.GetSemanticModel(source.SyntaxTree);
            foreach(var invocation in source.DescendantNodes().OfType<InvocationExpressionSyntax>()){
                if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol symbol||(symbol.ReducedFrom??symbol).ContainingType.ToDisplayString()!="System.Linq.Enumerable"){continue;}
                var inputs=new List<string>();var candidates=new List<ExpressionSyntax>();if(invocation.Expression is MemberAccessExpressionSyntax access){candidates.Add(access.Expression);}candidates.AddRange(invocation.ArgumentList.Arguments.Select(argument=>argument.Expression));foreach(var candidate in candidates.OfType<InvocationExpressionSyntax>()){if(semanticModel.GetSymbolInfo(candidate).Symbol is IMethodSymbol child&&(child.ReducedFrom??child).ContainingType.ToDisplayString()=="System.Linq.Enumerable"){inputs.Add("source"+candidate.SpanStart+"_"+candidate.Span.Length);}}
                nodes.Add(new("source"+invocation.SpanStart+"_"+invocation.Span.Length,"SourceOperator",symbol.Name,inputs.Distinct().ToArray(),"method",source.SyntaxTree.FilePath,invocation.SpanStart,invocation.Span.Length,new(){{"Expression",invocation.ToString()},{"Role","Original source; not a selected physical algorithm"}}));
            }
        }
        var seen=new Dictionary<Fragment,string>();
        string Visit(Fragment fragment,ExecutionPlanOrigin? origin){
            if(seen.TryGetValue(fragment,out var existing)){return existing;}
            var id="fragment"+seen.Count;seen.Add(fragment,id);var inputs=new List<string>();var properties=new Dictionary<string,string>();
            for(var type=fragment.GetType();type is not null&&typeof(Fragment).IsAssignableFrom(type);type=type.BaseType){
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)){
                    var value=field.GetValue(fragment);var key=field.Name;if(key.StartsWith("<",StringComparison.Ordinal)&&key.IndexOf('>')>1){key=key.Substring(1,key.IndexOf('>')-1);}
                    if(value is bool||value is Enum||value is string){properties["GeneratorState."+key]=value.ToString()??"";}
                    if(value is JoinConsumptionPlan||value is GroupByConsumptionPlan){properties["SelectedPlan."+key]=value.ToString()??"";}
                    if(typeof(Fragment).IsAssignableFrom(field.FieldType)&&value is Fragment child){inputs.Add(Visit(child,origin));}
                    else if(field.GetValue(fragment) is IEnumerable<PipelineStage> stages){
                        foreach(var stage in stages){
                            var stageInputs=new List<string>();
                            if(inputs.Count>0){stageInputs.Add(inputs[inputs.Count-1]);}
                            if(stage.SecondSource is not null){stageInputs.Add(Visit(stage.SecondSource,origin));}
                            var stageId="stage"+nodes.Count;
                            nodes.Add(new(stageId,"PipelineStage",stage.Kind.ToString(),stageInputs.ToArray(),origin?.QueryId,origin?.Path,origin?.Start??0,origin?.Length??0,new(){{"InputType",stage.InputType},{"OutputType",stage.OutputType},{"Implementation","Selected pipeline stage"},{"InnerIsArray",stage.InnerIsArray.ToString()},{"InnerIsList",stage.InnerIsList.ToString()},{"UsesIndex",stage.UsesIndex.ToString()},{"Descending",stage.Descending.ToString()},{"AggregatePlan",stage.AggregatePlan?.ToString()??"Unknown / not fused"}}));
                            inputs.Add(stageId);
                        }
                    }
                }
            }
            var label=fragment.GetType().Name;var arity=label.IndexOf('`');if(arity>=0){label=label.Substring(0,arity);}
            properties["Implementation"]=fragment.GetType().FullName??label;properties["AlgorithmDetails"]="Only explicitly recorded decisions are known";properties["SourceMapping"]=origin?.Level??"Unknown";properties["LoweredExpression"]=origin?.Expression??"";
            nodes.Add(new(id,"Fragment",label,inputs.Distinct().ToArray(),origin?.QueryId,origin?.Path,origin?.Start??0,origin?.Length??0,properties));
            return id;
        }
        foreach(var fragment in state.GeneratedFragments){state.PlanOrigins.TryGetValue(fragment,out var origin);Visit(fragment,origin);}
        foreach(var decision in state.Trace.CreateDocument().Events){
            if(decision.Layer!=OptimizationLayer.Pipeline||!decision.RuleId.EndsWith("Plan",StringComparison.Ordinal)){continue;}
            nodes.Add(new("decision"+nodes.Count,"LoweringDecision",decision.RuleId,[],null,null,0,0,new(){{"Before",decision.Before},{"After",decision.After},{"NodePath",decision.NodePath}}));
        }
        // 生成本文の制御構造は実際の条件を添えるための補助情報。LINQ/物理方式の復元には使わない。
        var bodyTree=CSharpSyntaxTree.ParseText("class C{void M(){"+body+"}}",new CSharpParseOptions(LanguageVersion.Preview));
        foreach(var condition in bodyTree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>()){
            nodes.Add(new("branch"+nodes.Count,"RuntimeCondition",condition.Condition.ToString(),[],"method",null,0,0,new(){{"Role","Generated runtime branch; selected branch is not measured"}}));
        }
        var document=new ExecutionPlanDocument(1,target.Method.ToDisplayString(),GeneratedExecutionAnnotations.ToolVersion,Hash(source?.ToFullString()??""),Hash(body),nodes.ToArray(),state.Trace.CreateDocument(),GeneratedExecutionAnnotations.ToolBuild);
        var literal=Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(NetStandardJson.Serialize(document),true);
        builder.AppendLine("    [global::Seamless.FLinq.ExecutionPlanAttribute(1,"+literal+")]");
    }
    private static string Hash(string value){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
}
