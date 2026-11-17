using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

/// <summary>最初の計測対象は一つの同期Sum消費区間。元演算への時間配分や未訪問helperの計測を追加しない。</summary>
internal static class QueryProfileSource{
    private const string Runtime="global::Seamless.Execution.QueryProfiler";
    internal static bool Available(FLinqIncrementalGenerator.Linq傍受対象型 target)=>target.SemanticModel.Compilation.GetTypeByMetadataName("Seamless.Execution.QueryProfiler") is not null;
    internal static bool Write(StringBuilder output,FLinqIncrementalGenerator.Linq傍受対象型 target,GenerationState state,string body,string markedBody,string attributes,ExecutionProfileBinding? binding,out string? profiledName){
        profiledName=null;
        var source=target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
        if(!Available(target)||binding?.Document.CallSite is null||state.GeneratedFragments.Count!=1||target.Method.IsAsync||target.Method.ReturnsVoid||target.Method.ReturnsByRef||target.Method.ReturnsByRefReadonly||source is null){return false;}
        if(source.ExpressionBody is null&&(source.Body is null||source.Body.Statements.Count!=1||source.Body.Statements[0] is not ReturnStatementSyntax)){return false;}
        var root=state.GeneratedFragments[0];
        if(root is not(Sum or Sum_selector)&&root is not PipelineTerminal{SupportsQueryProfile:true}){return false;}
        var siteMetadata=binding.Document.CallSite;
        if(binding.Document.MethodKey.Length>4096||siteMetadata.Path.Length is 0 or >4096||siteMetadata.Caller.Length is 0 or >4096){return false;}
        // 計測版は目印付きの本体から作る。通常版(下の normal)は目印のない本体をそのまま出す。
        var block=SyntaxFactory.ParseStatement("{"+markedBody+"}") as BlockSyntax;if(block is null||block.ContainsDiagnostics){return false;}
        var parameters=SyntaxFactory.ParseParameterList("("+target.InterceptorParameterList+")");
        var used=new HashSet<string>(block.DescendantTokens().Where(token=>token.IsKind(SyntaxKind.IdentifierToken)).Select(token=>token.ValueText).Concat(parameters.Parameters.Select(parameter=>parameter.Identifier.ValueText)),StringComparer.Ordinal);
        string Name(string prefix){var name=prefix;var index=0;while(!used.Add(name)){name=prefix+(++index);}return name;}
        var normal=Name(target.MethodName+"__Normal");var profiled=Name(target.MethodName+"__Profiled");var profiler=Name("__flinqProfiler");var region=Name("__flinqRegion");var probe=Name("__flinqProbe");var localProbe=Name("__flinqLocalProbe");var result=Name("__flinqResult");var error=Name("__flinqError");
        var helperParameters=parameters.WithParameters(SyntaxFactory.SeparatedList(parameters.Parameters.Select(parameter=>parameter.WithModifiers(SyntaxFactory.TokenList(parameter.Modifiers.Where(token=>!token.IsKind(SyntaxKind.ThisKeyword))))))).ToString();
        var arguments=new List<string>();foreach(var parameter in parameters.Parameters){var modifier="";foreach(var token in parameter.Modifiers){if(token.IsKind(SyntaxKind.RefKeyword)||token.IsKind(SyntaxKind.OutKeyword)||token.IsKind(SyntaxKind.InKeyword)){modifier=token.Text+" ";break;}}arguments.Add(modifier+parameter.Identifier.Text);}
        var calls=string.Join(",",arguments);var profileCalls=calls;if(profileCalls.Length>0){profileCalls+=",";}profileCalls+=profiler;
        string? computeName=null;
        if(root is BufferedScalar){
            // Use the exact name allocated by GeneratedNameRenamer, including collision suffixes.
            var name="__compute"+root.FragmentIndex;if(state.ProfileBodyRenames.TryGetValue(name,out var renamedCompute)){name=renamedCompute;}
            if(block.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(function=>function.Identifier.ValueText==name)){computeName=name;}
        }
        // [計測点] 部品が出した目印(QueryProbeMarkers)の位置へだけ記録を差し込む。生成したコードの形から位置を推測しない。
        const string probeType="__TFlinqProbe";
        var rewriter=new Rewriter(probe,localProbe,probeType,computeName);var rewritten=(BlockSyntax)rewriter.Visit(block)!;
        var doc=binding.Document;var site=doc.CallSite!;
        string Literal(string value)=>Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value,true);
        var metadata="new global::Seamless.Execution.QueryProfileTarget("+Literal(doc.MethodKey)+","+Literal(doc.SourceHash)+","+Literal(doc.GeneratedBodyHash)+",new global::Seamless.Execution.QueryProfileCallSite("+Literal(site.Path)+","+site.Start+","+site.Length+","+Literal(site.Caller)+"),"+Literal(binding.NodeId)+")";
        var countInputs="false";if(rewriter.CounterLoops>0&&!rewriter.MissingInputBoundary){countInputs="true";}
        // [1本の本体] 本体は計測点を型引数で受け取る1本だけ。通常は中身が空の NoProbe、計測時は RegionProbe で呼ぶ。
        // JIT は値型の型引数ごとに機械語を作るので、NoProbe の版では計測点の呼び出しが消える。
        var typeParameters=target.MethodTypeParameterList.Length==0?"<"+probeType+">":target.MethodTypeParameterList.Substring(0,target.MethodTypeParameterList.Length-1)+","+probeType+">";
        string TypeArguments(string probeArgument)=>target.MethodTypeParameterList.Length==0?"<"+probeArgument+">":target.MethodTypeParameterList.Substring(0,target.MethodTypeParameterList.Length-1)+","+probeArgument+">";
        var bodyCalls=calls;if(bodyCalls.Length>0){bodyCalls+=",";}
        var bodyParameters=helperParameters.Substring(0,helperParameters.Length-1);if(parameters.Parameters.Count>0){bodyParameters+=",";}bodyParameters+=$"ref {probeType} {probe})";
        output.AppendLine("    // [実測選択] Currentの参照は呼出し入口だけ。通常本文とその要素ループへ計測の分岐や時計を挿入しない。");
        output.AppendLine("    "+target.InterceptsLocationAttributeText);
        output.AppendLine($"    public static {target.ReturnTypeName} {target.MethodName}{target.MethodTypeParameterList}({target.InterceptorParameterList}){target.TypeParameterConstraintClause}{{");
        output.AppendLine($"        var {profiler}={Runtime}.Current;");
        output.AppendLine($"        if({profiler} is null){{var {probe}=default(global::Seamless.Execution.NoProbe);return {normal}{TypeArguments("global::Seamless.Execution.NoProbe")}({bodyCalls}ref {probe});}}");
        output.AppendLine($"        return {profiled}{target.MethodTypeParameterList}({profileCalls});");output.AppendLine("    }");
        output.Append(attributes);
        output.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        output.AppendLine($"    private static {target.ReturnTypeName} {normal}{typeParameters}{bodyParameters}{target.TypeParameterConstraintClause} where {probeType}:struct,global::Seamless.Execution.IQueryProbe{{");
        output.AppendLine(rewritten.ToFullString());output.AppendLine("    }");
        var profileParameters=helperParameters.Substring(0,helperParameters.Length-1);if(parameters.Parameters.Count>0){profileParameters+=",";}profileParameters+=$"{Runtime} {profiler})";
        output.AppendLine($"    private static {target.ReturnTypeName} {profiled}{target.MethodTypeParameterList}{profileParameters}{target.TypeParameterConstraintClause}{{");
        output.AppendLine($"        using var {region}={profiler}.Enter({metadata},{countInputs});");
        output.AppendLine($"        try{{var {probe}=new global::Seamless.Execution.RegionProbe({region});var {result}={normal}{TypeArguments("global::Seamless.Execution.RegionProbe")}({bodyCalls}ref {probe});{region}.Output();{region}.Succeeded();return {result};}}");
        output.AppendLine($"        catch(global::System.Exception {error}){{{region}.Fail({error});throw;}}");output.AppendLine("    }");
        profiledName=profiled;return true;
    }
    // 目印の置き換え。目印の位置で計測点の口(ref の型引数)の Input を呼ぶ。根の部品の局所関数(computeName)には口を ref の引数で渡す。
    // ラムダと、根以外の局所関数の中の目印は数えない(別の消費者の値なので)。
    private sealed class Rewriter:CSharpSyntaxRewriter{
        private readonly string probe,localProbe,probeType;
        private readonly string? computeName;
        private string activeRegion;
        private int localDepth;
        internal Rewriter(string probe,string localProbe,string probeType,string? computeName){this.probe=probe;this.localProbe=localProbe;this.probeType=probeType;this.computeName=computeName;this.activeRegion=probe;}
        internal int CounterLoops{get;private set;}
        internal bool MissingInputBoundary{get;private set;}
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)=>node;
        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)=>node;
        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node){
            if(node.Identifier.ValueText!=this.computeName){return node;}
            var previous=this.activeRegion;this.activeRegion=this.localProbe;this.localDepth++;
            var next=(LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;this.localDepth--;this.activeRegion=previous;
            return next.WithParameterList(next.ParameterList.AddParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier(this.localProbe)).WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space))).WithType(SyntaxFactory.ParseTypeName(this.probeType).WithTrailingTrivia(SyntaxFactory.Space))));
        }
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node){
            var next=(InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if(node.Expression is IdentifierNameSyntax name&&name.Identifier.ValueText==this.computeName){next=next.WithArgumentList(next.ArgumentList.AddArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(this.activeRegion)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword).WithTrailingTrivia(SyntaxFactory.Space))));}
            return next;
        }
        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node){
            var next=(WhileStatementSyntax)base.VisitWhileStatement(node)!;
            var mark=this.Marker(next.CloseParenToken.TrailingTrivia);
            return next.WithCloseParenToken(next.CloseParenToken.WithTrailingTrivia(StripTrivia(next.CloseParenToken.TrailingTrivia))).WithStatement(this.MarkEmbedded(next.Statement,mark));
        }
        public override SyntaxNode? VisitForStatement(ForStatementSyntax node){
            var next=(ForStatementSyntax)base.VisitForStatement(node)!;
            var mark=this.Marker(next.CloseParenToken.TrailingTrivia);
            return next.WithCloseParenToken(next.CloseParenToken.WithTrailingTrivia(StripTrivia(next.CloseParenToken.TrailingTrivia))).WithStatement(this.MarkEmbedded(next.Statement,mark));
        }
        public override SyntaxNode? VisitBlock(BlockSyntax node){
            var next=(BlockSyntax)base.VisitBlock(node)!;
            if(this.localDepth>1){return next;}
            var statements=new List<StatementSyntax>();
            foreach(var statement in next.Statements){
                if(Has(statement,QueryProbeMarkers.Unknown)){this.MissingInputBoundary=true;}
                if(Has(statement,QueryProbeMarkers.Input)){this.CounterLoops++;statements.Add(this.InputStatement());statements.Add(Strip(statement));continue;}
                statements.Add(statement);
            }
            return next.WithStatements(SyntaxFactory.List(statements));
        }
        // ループの中の1文(波かっこなし)に付いた目印。宣言ではないので、記録と元の文をブロックにまとめてよい。
        // 同じ行で「)」の後ろに書いた注釈は、文の前ではなく「)」の後置に付くので、両方を見る。
        private StatementSyntax MarkEmbedded(StatementSyntax statement,string? closeParenMarker){
            if(this.localDepth>1||statement is BlockSyntax){return statement;}
            if(closeParenMarker==QueryProbeMarkers.Unknown||Has(statement,QueryProbeMarkers.Unknown)){this.MissingInputBoundary=true;}
            if(closeParenMarker!=QueryProbeMarkers.Input&&!Has(statement,QueryProbeMarkers.Input)){return statement;}
            this.CounterLoops++;
            return SyntaxFactory.Block(this.InputStatement(),Strip(statement));
        }
        private string? Marker(SyntaxTriviaList trivia)=>this.localDepth>1?null:trivia.Where(item=>item.IsKind(SyntaxKind.MultiLineCommentTrivia)).Select(item=>item.ToString()).FirstOrDefault(text=>text==QueryProbeMarkers.Input||text==QueryProbeMarkers.Unknown);
        private static SyntaxTriviaList StripTrivia(SyntaxTriviaList trivia)=>SyntaxFactory.TriviaList(trivia.Where(item=>!(item.IsKind(SyntaxKind.MultiLineCommentTrivia)&&item.ToString().StartsWith(QueryProbeMarkers.Prefix,StringComparison.Ordinal))));
        private StatementSyntax InputStatement()=>SyntaxFactory.ParseStatement(this.activeRegion+".Input();");
        private static bool Has(StatementSyntax statement,string marker)=>statement.GetLeadingTrivia().Any(trivia=>trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)&&trivia.ToString()==marker);
        private static StatementSyntax Strip(StatementSyntax statement)=>statement.WithLeadingTrivia(statement.GetLeadingTrivia().Where(trivia=>!(trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)&&trivia.ToString().StartsWith(QueryProbeMarkers.Prefix,StringComparison.Ordinal))));
    }
}
