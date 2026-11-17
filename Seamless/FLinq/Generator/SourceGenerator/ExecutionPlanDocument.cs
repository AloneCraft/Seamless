using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

internal sealed record ExecutionPlanNode(string Id,string Kind,string Label,string[] Inputs,string? QueryId,string? SourcePath,int Start,int Length,Dictionary<string,string> Properties){
    public string Layer{get;init;}="Unknown";
    public string? OutputType{get;init;}
    public string Nullability{get;init;}="Unknown";
    public string Measurement{get;init;}="NotCollected";
}
internal sealed record ExecutionPlanEdge(string From,string To,string Kind,string? InputRole=null);
internal sealed record ExecutionPlanCallSite(string Path,int Start,int Length,string Caller);
internal sealed record ExecutionPlanDocument(int SchemaVersion,string MethodKey,string GeneratorVersion,string SourceHash,string GeneratedBodyHash,ExecutionPlanNode[] Nodes,OptimizationTraceDocument Trace,string GeneratorBuild){
    public ExecutionPlanCallSite? CallSite{get;init;}
    public GeneratedExecutionCode? GeneratedCode{get;init;}
    public GeneratedExecutionCode? ProfiledGeneratedCode{get;init;}
    public ExecutionPlanEdge[] Edges{get;init;}=Array.Empty<ExecutionPlanEdge>();
    public bool MetadataComplete{get;init;}=true;
    public int OmittedNodeCount{get;init;}
    public string[] Diagnostics{get;init;}=Array.Empty<string>();
}
internal sealed record ExecutionPlanOrigin(string QueryId,string Path,int Start,int Length,string Level,string Expression);

/// <summary>採用されたFragmentからのみスナップショットを作る。生成候補や説明コメントを逆解析しない。</summary>
internal static class ExecutionPlanWriter{
    internal static void Append(StringBuilder builder,FLinqIncrementalGenerator.Linq傍受対象型 target,GenerationState state,string body,List<ExecutionPlanDraft> drafts,out ExecutionProfileBinding? profile){
        profile=null;
        var nodes=new List<ExecutionPlanNode>();
        var executionNotes=new List<string>();
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
                    if(value is FuncFragment {ExecutionNote:not null} function){executionNotes.Add(function.ExecutionNote);}
                    if(value is bool||value is Enum||value is string){properties["GeneratorState."+key]=value.ToString()??"";}
                    if(value is JoinConsumptionPlan||value is GroupByConsumptionPlan){properties["SelectedPlan."+key]=value.ToString()??"";}
                    if(typeof(Fragment).IsAssignableFrom(field.FieldType)&&value is Fragment child){inputs.Add(Visit(child,origin));}
                    else if(field.GetValue(fragment) is IEnumerable<PipelineStage> stages){
                        foreach(var stage in stages){
                            var stageInputs=new List<string>();
                            if(inputs.Count>0){stageInputs.Add(inputs[inputs.Count-1]);}
                            if(stage.SecondSource is not null){stageInputs.Add(Visit(stage.SecondSource,origin));}
                            var stageId="stage"+nodes.Count;
                            foreach(var callback in new[]{stage.Func,stage.SecondFunc,stage.ThirdFunc}){
                                if(callback?.ExecutionNote is not null){executionNotes.Add(callback.ExecutionNote);}
                            }
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
        foreach(var note in executionNotes.Distinct(StringComparer.Ordinal)){
            builder.AppendLine("    // "+GeneratedExecutionAnnotations.Clean(note));
        }
        foreach(var decision in state.Trace?.CreateDocument().Events ?? Array.Empty<OptimizationTraceEvent>()){
            if(decision.Layer!=OptimizationLayer.Pipeline||!decision.RuleId.EndsWith("Plan",StringComparison.Ordinal)){continue;}
            nodes.Add(new("decision"+nodes.Count,"LoweringDecision",decision.RuleId,[],null,null,0,0,new(){{"Before",decision.Before},{"After",decision.After},{"NodePath",decision.NodePath}}));
        }
        // 生成本文の制御構造は実際の条件を添えるための補助情報。LINQ/物理方式の復元には使わない。
        var bodyTree=CSharpSyntaxTree.ParseText("class C{void M(){"+body+"}}",new CSharpParseOptions(LanguageVersion.Preview));
        foreach(var condition in bodyTree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>()){
            nodes.Add(new("branch"+nodes.Count,"RuntimeCondition",condition.Condition.ToString(),[],"method",null,0,0,new(){{"Role","Generated runtime branch; selected branch is not measured"}}));
        }
        var semantics=new SemanticCapture(nodes);
        if(source is not null){semantics.ReadExpressions(source,target.SemanticModel.Compilation.GetSemanticModel(source.SyntaxTree));}
        var caller=target.SemanticModel.GetEnclosingSymbol(target.Invocation.SpanStart);
        while(caller is IMethodSymbol{MethodKind:MethodKind.AnonymousFunction}){caller=caller.ContainingSymbol;}
        var document=new ExecutionPlanDocument(2,target.Method.ToDisplayString(),GeneratedExecutionAnnotations.ToolVersion,Hash(source?.ToFullString()??""),Hash(body),Array.Empty<ExecutionPlanNode>(),semantics.LimitTrace((state.Trace??new OptimizationTraceCollector(target.Method.ToDisplayString())).CreateDocument()),GeneratedExecutionAnnotations.ToolBuild){
            CallSite=new(target.SourcePath,target.Invocation.SpanStart,target.Invocation.Span.Length,caller?.ToDisplayString()??target.ContainingMethodName)
        };
        var json=semantics.Serialize(document,out var captured);
        if(json is null){builder.AppendLine("    // Execution plan omitted: essential metadata exceeds the 2 MiB budget.");return;}
        drafts.Add(new(json,captured!));
        if(state.GeneratedFragments.Count==1&&seen.TryGetValue(state.GeneratedFragments[0],out var rootId)&&captured!.Nodes.Any(node=>node.Id==rootId)){profile=new(captured,rootId);}
        var literal=Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(json,true);
        builder.AppendLine("    [global::Seamless.FLinq.ExecutionPlanAttribute(2,"+literal+")]");
    }
    private sealed class SemanticCapture{
        private const int NodeLimit=2048;
        private const int EdgeLimit=8192;
        private const int ExpressionLimit=256;
        private const int DepthLimit=24;
        private const int TextLimit=2048;
        private const int ByteLimit=2*1024*1024;
        private readonly List<ExecutionPlanNode> nodes=new();
        private readonly List<ExecutionPlanEdge> edges=new();
        private readonly HashSet<ExecutionPlanEdge> uniqueEdges=new();
        private readonly HashSet<string> diagnostics=new(StringComparer.Ordinal);
        private readonly Dictionary<(int Start,int Length),string> expressions=new();
        private int omitted;

        internal SemanticCapture(List<ExecutionPlanNode> original){
            this.omitted=Math.Max(0,original.Count-NodeLimit);
            if(this.omitted>0){this.diagnostics.Add("Metadata.NodeLimit=2048");}
            foreach(var node in original.Take(NodeLimit)){
                var layer="Unknown";var edgeKind="Unknown";
                switch(node.Kind){
                    case "SourceOperator":layer="Original";edgeKind="OriginalDataFlow";break;
                    case "Fragment":case "PipelineStage":layer="Selected";edgeKind="SelectedReference";break;
                    case "LoweringDecision":layer="Optimization";break;
                    case "RuntimeCondition":layer="GeneratedControl";break;
                }
                node.Properties.TryGetValue("OutputType",out var outputType);
                this.nodes.Add(this.LimitNode(node with{Layer=layer,OutputType=outputType}));
                foreach(var input in node.Inputs){this.Edge(input,node.Id,edgeKind);}
            }
            var sourceNodes=this.nodes.Where(node=>node.Kind=="SourceOperator").ToDictionary(node=>(node.SourcePath,node.Start,node.Length),node=>node.Id);
            foreach(var node in this.nodes.Where(node=>node.Layer=="Selected")){
                if(sourceNodes.TryGetValue((node.SourcePath,node.Start,node.Length),out var source)){this.Edge(source,node.Id,"SourceMapping","Lowered from");}
            }
        }

        private void Edge(string from,string to,string kind,string? role=null){
            var edge=new ExecutionPlanEdge(from,to,kind,role);
            if(this.uniqueEdges.Contains(edge)){return;}
            if(this.edges.Count>=EdgeLimit){this.diagnostics.Add("Metadata.EdgeLimit=8192");return;}
            this.uniqueEdges.Add(edge);this.edges.Add(edge);
        }

        private string Text(string value){
            if(value.Length<=TextLimit){return value;}
            this.diagnostics.Add("Metadata.TextLimit=2048");return value.Substring(0,TextLimit-1)+"…";
        }

        private ExecutionPlanNode LimitNode(ExecutionPlanNode node){
            var path=node.SourcePath;var outputType=node.OutputType;
            if(path is{Length:>TextLimit}){path=null;this.diagnostics.Add("Metadata.SourcePathOmittedByLimit");}
            if(outputType is{Length:>TextLimit}){outputType=null;this.diagnostics.Add("Metadata.OutputTypeOmittedByLimit");}
            return node with{Label=this.Text(node.Label),SourcePath=path,OutputType=outputType,Properties=node.Properties.ToDictionary(pair=>pair.Key,pair=>this.Text(pair.Value),StringComparer.Ordinal)};
        }

        internal void ReadExpressions(SyntaxNode source,SemanticModel model){
            var operators=this.nodes.Where(node=>node.Kind=="SourceOperator").ToDictionary(node=>(node.SourcePath,node.Start,node.Length),node=>node.Id);
            foreach(var invocation in source.DescendantNodes().OfType<InvocationExpressionSyntax>()){
                if(!operators.TryGetValue((source.SyntaxTree.FilePath,invocation.SpanStart,invocation.Span.Length),out var sourceId)){continue;}
                if(model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method){
                    if(method.ReducedFrom is{} extension&&invocation.Expression is MemberAccessExpressionSyntax receiver){
                        this.InputRole(receiver.Expression,extension.Parameters[0].Name,sourceId);
                    }
                    foreach(var argument in invocation.ArgumentList.Arguments){
                        if(model.GetOperation(argument) is IArgumentOperation{Parameter:{} parameter}){this.InputRole(argument.Expression,parameter.Name,sourceId);}
                    }
                }
                foreach(var argument in invocation.ArgumentList.Arguments){
                    if(argument.Expression is LambdaExpressionSyntax lambda){
                        if(lambda.Body is ExpressionSyntax expression){
                            var id=this.Expression(expression,model,0);
                            if(id is not null){this.Edge(id,sourceId,"SourceMapping","Lambda body");}
                        }else if(lambda.Body is BlockSyntax block){
                            this.diagnostics.Add("Metadata.StatementLambdaPartiallyExpanded");
                            foreach(var returned in block.DescendantNodes().OfType<ReturnStatementSyntax>()){
                                if(returned.Expression is null){continue;}
                                var id=this.Expression(returned.Expression,model,0);
                                if(id is not null){this.Edge(id,sourceId,"SourceMapping","Lambda return");}
                            }
                        }
                    }else if(model.GetTypeInfo(argument.Expression).ConvertedType?.TypeKind==TypeKind.Delegate){
                        var id=this.Expression(argument.Expression,model,0);
                        if(id is not null){this.Edge(id,sourceId,"SourceMapping","Delegate argument; body not expanded");}
                    }
                }
                var nodeIndex=this.nodes.FindIndex(node=>node.Id==sourceId);
                this.nodes[nodeIndex]=this.LimitNode(this.nodes[nodeIndex] with{OutputType=model.GetTypeInfo(invocation).Type?.ToDisplayString()});
            }
        }

        private void InputRole(ExpressionSyntax expression,string role,string target){
            if(expression is not InvocationExpressionSyntax){return;}
            var id="source"+expression.SpanStart+"_"+expression.Span.Length;
            var index=this.edges.FindIndex(edge=>edge.From==id&&edge.To==target&&edge.Kind=="OriginalDataFlow");
            if(index<0){return;}
            var previous=this.edges[index];var current=previous with{InputRole=role};
            this.uniqueEdges.Remove(previous);this.uniqueEdges.Add(current);this.edges[index]=current;
        }

        private string? Expression(ExpressionSyntax expression,SemanticModel model,int depth){
            var key=(expression.SpanStart,expression.Span.Length);
            if(this.expressions.TryGetValue(key,out var existing)){return existing;}
            if(depth>DepthLimit||this.expressions.Count>=ExpressionLimit||this.nodes.Count>=NodeLimit){
                this.omitted++;this.diagnostics.Add("Metadata.ExpressionLimit=256;Depth=24;Nodes=2048");return null;
            }
            var id="expression"+expression.SpanStart+"_"+expression.Span.Length;this.expressions.Add(key,id);
            var inputs=new List<string>();
            var properties=new Dictionary<string,string>{{"Expression",expression.ToString()},{"Language","C#"},{"Evaluation","Original expression; not an independent runtime measurement"}};
            var operation=model.GetOperation(expression);var label=operation?.Kind.ToString()??expression.Kind().ToString();
            void Input(ExpressionSyntax child,string role){var childId=this.Expression(child,model,depth+1);if(childId is not null){inputs.Add(childId);this.Edge(childId,id,"ExpressionDependency",role);}}
            switch(expression){
                case BinaryExpressionSyntax binary:
                    label=BinaryLabel(binary,operation as IBinaryOperation);Input(binary.Left,"Left");Input(binary.Right,"Right");
                    if(operation is IBinaryOperation binaryOperation){
                        properties["OperatorKind"]=binaryOperation.OperatorKind.ToString();properties["Checked"]=binaryOperation.IsChecked.ToString();properties["Lifted"]=binaryOperation.IsLifted.ToString();
                        if(binaryOperation.OperatorMethod is{} operatorMethod){properties["OperatorMethod"]=operatorMethod.ToDisplayString();}
                    }
                    if(binary.IsKind(SyntaxKind.LogicalAndExpression)||binary.IsKind(SyntaxKind.LogicalOrExpression)||binary.IsKind(SyntaxKind.CoalesceExpression)){properties["Evaluation"]="ShortCircuit";}
                    break;
                case PrefixUnaryExpressionSyntax unary:
                    Input(unary.Operand,"Operand");break;
                case ParenthesizedExpressionSyntax parentheses:
                    label="Parentheses";Input(parentheses.Expression,"Value");break;
                case CastExpressionSyntax cast:
                    label="Convert";Input(cast.Expression,"Value");break;
                case ConditionalExpressionSyntax conditional:
                    label="Conditional";properties["Evaluation"]="Conditional";Input(conditional.Condition,"Condition");Input(conditional.WhenTrue,"When true");Input(conditional.WhenFalse,"When false");break;
                case MemberAccessExpressionSyntax member:
                    label=member.Name.Identifier.ValueText;Input(member.Expression,"Receiver");break;
                case ElementAccessExpressionSyntax element:
                    label="ElementAccess";Input(element.Expression,"Receiver");foreach(var argument in element.ArgumentList.Arguments){Input(argument.Expression,"Index");}break;
                case InvocationExpressionSyntax invocation:
                    label="Call";
                    if(model.GetSymbolInfo(invocation).Symbol is IMethodSymbol calledMethod){properties["Method"]=calledMethod.ToDisplayString();if(calledMethod.MethodKind==MethodKind.DelegateInvoke){label="DelegateCall";}}
                    if(invocation.Expression is MemberAccessExpressionSyntax receiver&&model.GetSymbolInfo(receiver.Expression).Symbol is not INamedTypeSymbol){Input(receiver.Expression,"Receiver");}
                    foreach(var argument in invocation.ArgumentList.Arguments){Input(argument.Expression,"Argument");}break;
                case IdentifierNameSyntax identifier:
                    label=identifier.Identifier.ValueText;break;
                case LiteralExpressionSyntax literal:
                    label=literal.Token.ValueText;break;
                case AnonymousObjectCreationExpressionSyntax anonymous:
                    label="AnonymousObject";foreach(var initializer in anonymous.Initializers){Input(initializer.Expression,initializer.NameEquals?.Name.Identifier.ValueText??"Member");}break;
                case ObjectCreationExpressionSyntax creation:
                    label="New";if(creation.ArgumentList is not null){foreach(var argument in creation.ArgumentList.Arguments){Input(argument.Expression,"Argument");}}
                    if(creation.Initializer is not null){foreach(var value in creation.Initializer.Expressions){Input(value,"Initializer");}}break;
                case ImplicitObjectCreationExpressionSyntax implicitCreation:
                    label="New";foreach(var argument in implicitCreation.ArgumentList.Arguments){Input(argument.Expression,"Argument");}
                    if(implicitCreation.Initializer is not null){foreach(var value in implicitCreation.Initializer.Expressions){Input(value,"Initializer");}}break;
                case TupleExpressionSyntax tuple:
                    label="Tuple";foreach(var argument in tuple.Arguments){Input(argument.Expression,argument.NameColon?.Name.Identifier.ValueText??"Element");}break;
                case ConditionalAccessExpressionSyntax access:
                    label="ConditionalAccess";properties["Evaluation"]="NullConditional";Input(access.Expression,"Receiver");Input(access.WhenNotNull,"When not null");break;
                case ThisExpressionSyntax:
                    label="this";break;
                default:
                    properties["Expansion"]="Not expanded";this.diagnostics.Add("Metadata.ExpressionKindNotExpanded="+expression.Kind());break;
            }
            var typeInfo=model.GetTypeInfo(expression);var type=typeInfo.Type??typeInfo.ConvertedType;var nullability="Unknown";
            if(type is not null){
                if(type.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T||type.NullableAnnotation==NullableAnnotation.Annotated){nullability="Nullable";}
                else if(type.IsValueType||type.NullableAnnotation==NullableAnnotation.NotAnnotated){nullability="NotNullable";}
                properties["NullableAnnotation"]=type.NullableAnnotation.ToString();
            }
            if(expression.IsKind(SyntaxKind.NullLiteralExpression)){nullability="Nullable";}
            if(this.nodes.Count>=NodeLimit){this.omitted++;this.diagnostics.Add("Metadata.NodeLimit=2048");return null;}
            this.nodes.Add(this.LimitNode(new(id,"Expression",label,inputs.ToArray(),"method",expression.SyntaxTree.FilePath,expression.SpanStart,expression.Span.Length,properties){Layer="OriginalExpression",OutputType=type?.ToDisplayString(),Nullability=nullability,Measurement="NotIndependentlyMeasured"}));
            return id;
        }

        private static string BinaryLabel(BinaryExpressionSyntax syntax,IBinaryOperation? operation){
            if(syntax.IsKind(SyntaxKind.EqualsExpression)){return "Equal";}
            if(syntax.IsKind(SyntaxKind.NotEqualsExpression)){return "NotEqual";}
            return operation?.OperatorKind.ToString()??syntax.Kind().ToString();
        }

        internal OptimizationTraceDocument LimitTrace(OptimizationTraceDocument trace){
            if(trace.Events.Length>256||trace.LayerTerminations.Length>256||trace.Summary.AppliedRuleNames.Length>256){this.diagnostics.Add("Metadata.OptimizationTraceLimit=256");}
            return trace with{
                Events=trace.Events.Take(256).Select(item=>item with{Before=this.Text(item.Before),After=this.Text(item.After),Phase=this.Text(item.Phase),RuleId=this.Text(item.RuleId),NodeId=this.Text(item.NodeId),NodePath=this.Text(item.NodePath),ParentKind=this.Text(item.ParentKind)}).ToArray(),
                LayerTerminations=trace.LayerTerminations.Take(256).ToArray(),
                Summary=trace.Summary with{AppliedRuleNames=trace.Summary.AppliedRuleNames.Take(256).Select(this.Text).ToArray()}
            };
        }

        internal string? Serialize(ExecutionPlanDocument document,out ExecutionPlanDocument? captured){
            captured=null;
            var traceCleared=false;
            while(true){
                var ids=new HashSet<string>(this.nodes.Select(node=>node.Id),StringComparer.Ordinal);
                var outputNodes=this.nodes.Select(node=>node with{Inputs=node.Inputs.Where(ids.Contains).ToArray()}).ToArray();
                var outputEdges=this.edges.Where(edge=>ids.Contains(edge.From)&&ids.Contains(edge.To)).ToArray();
                var output=document with{Nodes=outputNodes,Edges=outputEdges,MetadataComplete=this.diagnostics.Count==0,OmittedNodeCount=this.omitted,Diagnostics=this.diagnostics.OrderBy(item=>item,StringComparer.Ordinal).ToArray()};
                var json=NetStandardJson.Serialize(output);
                if(Encoding.UTF8.GetByteCount(json)<=ByteLimit){captured=output;return json;}
                this.diagnostics.Add("Metadata.ByteLimit=2097152");
                if(this.nodes.Count>0){var remove=Math.Max(1,this.nodes.Count/4);this.nodes.RemoveRange(this.nodes.Count-remove,remove);this.omitted+=remove;continue;}
                if(traceCleared){return null;}
                document=document with{Trace=document.Trace with{Events=Array.Empty<OptimizationTraceEvent>(),LayerTerminations=Array.Empty<OptimizationLayerTermination>(),Summary=document.Trace.Summary with{AppliedRuleNames=Array.Empty<string>()}}};
                traceCleared=true;
            }
        }
    }

    private static string Hash(string value){using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
}
