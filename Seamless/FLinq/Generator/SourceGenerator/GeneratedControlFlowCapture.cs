using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

/// <summary>説明付与後の実メソッドを束縛し、属性外の本文相対範囲だけを保存する。</summary>
internal static class GeneratedControlFlowCapture{
    private const int BodyLimit=65536,BlockLimit=2048,OperationLimit=8192,RegionLimit=2048,DetailLimit=4096,ByteLimit=2*1024*1024;
    internal static string Complete(string source,ImmutableArray<FLinqIncrementalGenerator.Linq傍受対象型> targets,ExecutionPlanDraft[] drafts){
        if(drafts.Length==0||targets.Length==0){return source;}
        var options=targets[0].Invocation.SyntaxTree.Options as CSharpParseOptions??new CSharpParseOptions(LanguageVersion.Preview);
        var tree=CSharpSyntaxTree.ParseText(source,options,path:targets[0].SourcePath+".FLinq.control.g.cs");
        var root=tree.GetRoot();
        var compilation=targets[0].SemanticModel.Compilation.AddSyntaxTrees(tree);
        var model=compilation.GetSemanticModel(tree);
        var plans=new Dictionary<string,ExecutionPlanDraft>(StringComparer.Ordinal);
        foreach(var draft in drafts){if(!plans.ContainsKey(draft.OriginalJson)){plans.Add(draft.OriginalJson,draft);}}
        var replacements=new Dictionary<AttributeSyntax,AttributeSyntax>();
        foreach(var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>()){
            foreach(var attribute in method.AttributeLists.SelectMany(list=>list.Attributes)){
                if(!attribute.Name.ToString().EndsWith(".ExecutionPlanAttribute",StringComparison.Ordinal)||attribute.ArgumentList?.Arguments.Count!=2||attribute.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax literal||!plans.TryGetValue(literal.Token.ValueText,out var draft)){continue;}
                var captured=Capture(method,model,tree);
                GeneratedExecutionCode? profiled=null;
                if(draft.ProfiledMethodName is{} name){
                    var profileMethod=method.Parent?.ChildNodes().OfType<MethodDeclarationSyntax>().SingleOrDefault(candidate=>candidate.Identifier.ValueText==name);
                    profiled=Empty("Unknown",null,null,null,"GeneratedCode.ProfiledBodyUnavailable");
                    if(profileMethod is not null){profiled=Capture(profileMethod,model,tree);}
                }
                var json=Serialize(draft.Document,captured,profiled);
                var expression=SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(json));
                var argument=attribute.ArgumentList.Arguments[1].WithExpression(expression);
                replacements.Add(attribute,attribute.WithArgumentList(attribute.ArgumentList.WithArguments(attribute.ArgumentList.Arguments.Replace(attribute.ArgumentList.Arguments[1],argument))));
            }
        }
        return root.ReplaceNodes(replacements.Keys,(original,_)=>replacements[original]).ToFullString();
    }
    private static GeneratedExecutionCode Capture(MethodDeclarationSyntax method,SemanticModel model,SyntaxTree tree){
        var symbol=model.GetDeclaredSymbol(method);
        var key=symbol?.ToDisplayString();
        if(key is{Length:>DetailLimit}){key=null;}
        if(method.Body is null){return Empty("Unknown",key,null,null,"GeneratedCode.BodyUnavailable");}
        var text=tree.GetText().ToString(method.Body.Span);
        if(text.Length>BodyLimit){return Empty("Limited",key,null,null,"GeneratedCode.BodyLimit=65536");}
        var hash=Hash(text);
        if(symbol is null||key is null){return Empty("Unknown",key,hash,text,"GeneratedCode.MethodSymbolUnavailable");}
        var errors=model.GetDiagnostics(method.Span).Where(item=>item.Severity==DiagnosticSeverity.Error).Take(127).Select(item=>"GeneratedCode.BindingError="+item.Id).Distinct().ToArray();
        if(errors.Length>0){return new("Unknown",key,hash,text,[],[],[],errors);}
        if(model.GetOperation(method) is not IMethodBodyOperation body){return Empty("Unknown",key,hash,text,"GeneratedCode.MethodBodyOperationUnavailable");}
        try{
            var graph=ControlFlowGraph.Create(body);
            var collector=new Collector(method.Body.Span,tree);
            return collector.Read(graph,key,hash,text);
        }catch(GraphLimit error){return Empty("Limited",key,hash,text,error.Message) with{OmittedBlockCount=error.Blocks,OmittedOperationCount=error.Operations};}
        catch(ArgumentException){return Empty("Unknown",key,hash,text,"GeneratedCode.ControlFlowGraphUnsupportedArgument");}
        catch(InvalidOperationException){return Empty("Unknown",key,hash,text,"GeneratedCode.ControlFlowGraphUnavailable");}
        catch(NotSupportedException){return Empty("Unknown",key,hash,text,"GeneratedCode.ControlFlowGraphUnsupported");}
    }
    private static GeneratedExecutionCode Empty(string status,string? key,string? hash,string? text,string diagnostic)=>new(status,key,hash,text,[],[],[],[diagnostic]);
    private static string Hash(string text){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
    private static string Serialize(ExecutionPlanDocument plan,GeneratedExecutionCode code,GeneratedExecutionCode? profiled=null){
        var result=plan with{GeneratedCode=code,ProfiledGeneratedCode=profiled};
        if(code.Status!="Available"){result=result with{MetadataComplete=false};}
        if(profiled is not null&&profiled.Status!="Available"){result=result with{MetadataComplete=false};}
        while(true){
            var json=NetStandardJson.Serialize(result);
            if(Encoding.UTF8.GetByteCount(json)<=ByteLimit){return json;}
            if(result.ProfiledGeneratedCode?.BodyText is not null){result=result with{ProfiledGeneratedCode=Empty("Limited",null,null,null,"GeneratedCode.ProfiledMetadataByteLimit=2097152"),MetadataComplete=false};continue;}
            if(result.GeneratedCode?.BodyText is not null){result=result with{GeneratedCode=Empty("Limited",null,null,null,"GeneratedCode.MetadataByteLimit=2097152"),MetadataComplete=false};continue;}
            if(result.Nodes.Length>0){
                var count=Math.Max(1,result.Nodes.Length/4);var nodes=result.Nodes.Take(result.Nodes.Length-count).ToArray();var ids=new HashSet<string>(nodes.Select(node=>node.Id),StringComparer.Ordinal);
                result=result with{Nodes=nodes.Select(node=>node with{Inputs=node.Inputs.Where(ids.Contains).ToArray()}).ToArray(),Edges=result.Edges.Where(edge=>ids.Contains(edge.From)&&ids.Contains(edge.To)).ToArray(),OmittedNodeCount=result.OmittedNodeCount+count,MetadataComplete=false};continue;
            }
            result=result with{Trace=result.Trace with{Events=[],LayerTerminations=[],Summary=result.Trace.Summary with{AppliedRuleNames=[]}}};
            if(Encoding.UTF8.GetByteCount(NetStandardJson.Serialize(result))>ByteLimit){return NetStandardJson.Serialize(plan with{GeneratedCode=null});}
        }
    }
    private sealed class Collector(Microsoft.CodeAnalysis.Text.TextSpan span,SyntaxTree tree){
        private readonly Dictionary<ControlFlowRegion,int> regionIds=new();
        private readonly List<GeneratedControlRegion> regions=new();
        private readonly HashSet<string> diagnostics=new(StringComparer.Ordinal);
        private int operationCount,detailCharacters,localCount;
        private bool limited;
        internal GeneratedExecutionCode Read(ControlFlowGraph graph,string key,string hash,string text){
            if(graph.Blocks.Length>BlockLimit){throw new GraphLimit("GeneratedCode.BlockLimit=2048",graph.Blocks.Length,0);}
            if(graph.LocalFunctions.Length>0){this.diagnostics.Add("GeneratedCode.LocalFunctionBodiesNotExpanded");}
            this.Regions(graph.Root);
            var blocks=new List<GeneratedBasicBlock>();var edges=new List<GeneratedControlEdge>();
            foreach(var block in graph.Blocks){
                var operations=new List<GeneratedOperation>();
                foreach(var operation in block.Operations){operations.AddRange(this.Operations(operation));}
                GeneratedOperation? branch=null;
                if(block.BranchValue is not null){var values=this.Operations(block.BranchValue);branch=values[0];operations.AddRange(values.Skip(1));}
                blocks.Add(new(block.Ordinal,block.Kind.ToString(),block.IsReachable,block.ConditionKind.ToString(),operations.ToArray(),branch));
                this.Edge(block.Ordinal,block.FallThroughSuccessor,"FallThrough",edges);
                this.Edge(block.Ordinal,block.ConditionalSuccessor,"Conditional",edges);
            }
            var status="Available";if(this.limited){status="Limited";}
            return new(status,key,hash,text,blocks.ToArray(),edges.ToArray(),this.regions.ToArray(),this.diagnostics.OrderBy(value=>value,StringComparer.Ordinal).Take(128).ToArray());
        }
        private string? Text(string? value){
            if(value is null){return null;}
            if(value.Length>DetailLimit||this.detailCharacters+value.Length>131072){this.limited=true;this.diagnostics.Add("GeneratedCode.DetailLimit=4096;Total=131072");return null;}
            this.detailCharacters+=value.Length;return value;
        }
        private void Regions(ControlFlowRegion root){
            var pending=new Queue<(ControlFlowRegion Region,int? Parent)>();pending.Enqueue((root,null));this.regionIds.Add(root,0);
            while(pending.Count>0){
                var item=pending.Dequeue();var locals=new List<string>();
                foreach(var local in item.Region.Locals){if(++this.localCount>OperationLimit){throw new GraphLimit("GeneratedCode.LocalLimit=8192",0,0);}var value=this.Text(local.ToDisplayString());if(value is not null){locals.Add(value);}}
                this.regions.Add(new(this.regionIds[item.Region],item.Region.Kind.ToString(),item.Region.FirstBlockOrdinal,item.Region.LastBlockOrdinal,item.Parent,locals.ToArray()));
                foreach(var nested in item.Region.NestedRegions){if(this.regionIds.Count>=RegionLimit){throw new GraphLimit("GeneratedCode.RegionLimit=2048",0,0);}this.regionIds.Add(nested,this.regionIds.Count);pending.Enqueue((nested,this.regionIds[item.Region]));}
            }
        }
        private int[] RegionIds(ImmutableArray<ControlFlowRegion> regions)=>regions.Select(region=>this.regionIds[region]).ToArray();
        private void Edge(int from,ControlFlowBranch? edge,string kind,List<GeneratedControlEdge> output){
            if(edge is null){return;}
            int? destination=null;if(edge.Destination is not null){destination=edge.Destination.Ordinal;}
            output.Add(new(from,destination,kind,edge.Semantics.ToString(),this.RegionIds(edge.EnteringRegions),this.RegionIds(edge.LeavingRegions),this.RegionIds(edge.FinallyRegions)));
        }
        private List<GeneratedOperation> Operations(IOperation root){
            var result=new List<GeneratedOperation>();var pending=new Stack<(IOperation Operation,int Depth)>();pending.Push((root,0));
            while(pending.Count>0){
                var item=pending.Pop();var operation=item.Operation;
                if(++this.operationCount>OperationLimit){throw new GraphLimit("GeneratedCode.OperationLimit=8192",0,1);}
                if(item.Depth>128){throw new GraphLimit("GeneratedCode.OperationDepthLimit=128",0,1);}
                var start=-1;var length=0;
                if(operation.Syntax.SyntaxTree==tree&&span.Contains(operation.Syntax.Span)){start=operation.Syntax.SpanStart-span.Start;length=operation.Syntax.Span.Length;}
                var symbol=Symbol(operation);
                if(operation is IFlowAnonymousFunctionOperation){this.diagnostics.Add("GeneratedCode.AnonymousFunctionBodiesNotExpanded");}
                if(symbol is IMethodSymbol method&&method.Locations.Any(location=>location.SourceTree==tree&&!span.Contains(location.SourceSpan))){this.diagnostics.Add("GeneratedCode.HelperBodiesNotExpanded");}
                var reference=this.Text(symbol?.ToDisplayString());
                if(operation is IFlowCaptureOperation capture){reference="Capture "+capture.Id;}
                else if(operation is IFlowCaptureReferenceOperation captured){reference="Capture "+captured.Id;}
                result.Add(new("operation"+this.operationCount,operation.Kind.ToString(),start,length,this.Text(operation.Type?.ToDisplayString()),reference,operation.IsImplicit));
                foreach(var child in operation.ChildOperations.Reverse()){pending.Push((child,item.Depth+1));}
            }
            return result;
        }
        private static ISymbol? Symbol(IOperation operation){
            switch(operation){
                case IInvocationOperation value:return value.TargetMethod;
                case IMethodReferenceOperation value:return value.Method;
                case ILocalReferenceOperation value:return value.Local;
                case IParameterReferenceOperation value:return value.Parameter;
                case IFieldReferenceOperation value:return value.Field;
                case IPropertyReferenceOperation value:return value.Property;
                case IEventReferenceOperation value:return value.Event;
                case IVariableDeclaratorOperation value:return value.Symbol;
                case IObjectCreationOperation value:return value.Constructor;
                case IConversionOperation value:return value.OperatorMethod;
                case IBinaryOperation value:return value.OperatorMethod;
                case IUnaryOperation value:return value.OperatorMethod;
                case IBranchOperation value:return value.Target;
                case ILabeledOperation value:return value.Label;
                default:return null;
            }
        }
    }
    private sealed class GraphLimit(string message,int blocks,int operations):Exception(message){
        internal int Blocks{get;}=blocks;
        internal int Operations{get;}=operations;
    }
}
