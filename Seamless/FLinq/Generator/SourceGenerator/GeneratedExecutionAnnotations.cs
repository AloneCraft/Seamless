using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

/// <summary>実際に出力する文へ説明を付ける。実行順序・式・usingの構造は変更しない。</summary>
internal static class GeneratedExecutionAnnotations{
    internal static string ToolVersion=>typeof(FLinqIncrementalGenerator).Assembly.GetName().Version?.ToString()??"unknown";
    internal static string ToolBuild=>typeof(FLinqIncrementalGenerator).Module.ModuleVersionId.ToString("D");
    internal static string Clean(string value)=>string.Concat(value.Select(static character=>{
        if(char.IsControl(character)||character is '\u2028' or '\u2029'){return ' ';}return character;
    }));
    internal static string Apply(string source){
        if(string.IsNullOrWhiteSpace(source)){return source;}
        var root=CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
        return new Annotator().Visit(root)!.ToFullString();
    }
    internal static void MethodPlan(StringBuilder builder,FLinqIncrementalGenerator.Linq傍受対象型 target,OptimizationTraceDocument document){
        var location=target.Method.Locations.FirstOrDefault()?.GetLineSpan();
        builder.AppendLine("    // [生成計画] schema=1; generator="+ToolVersion+"; method="+Clean(target.Method.ToDisplayString()));
        if(location.HasValue){builder.AppendLine("    // [元ソース] "+Clean(location.Value.Path)+":"+(location.Value.StartLinePosition.Line+1));}
        var sourceTree=target.Method.Locations.FirstOrDefault()?.SourceTree;
        if(sourceTree is not null){
            var text=sourceTree.GetText();
            builder.AppendLine("    // [入力識別] "+text.ChecksumAlgorithm+"="+BitConverter.ToString(text.GetChecksum().ToArray()).Replace("-",string.Empty)+"; generator-mvid="+ToolBuild);
            if(sourceTree.Options is CSharpParseOptions parse){builder.AppendLine("    // [構文設定] language="+parse.LanguageVersion+"; symbols="+Clean(string.Join(",",parse.PreprocessorSymbolNames)));}
        }
        builder.AppendLine("    // [最適化設定] rules="+target.Configuration.Optimizations+"; convergence="+target.Configuration.ConvergenceMode);
        if(target.SemanticModel.Compilation.Options is CSharpCompilationOptions options){
            builder.AppendLine("    // [コンパイル設定] overflow="+options.CheckOverflow+"; nullable="+options.NullableContextOptions+"; optimization="+options.OptimizationLevel+"。局所的なchecked/uncheckedの範囲も維持。");
        }
        builder.AppendLine("    // [不変条件] 元のusing/例外/早期終了を保ち、ここに示す実行経路にないworker・キュー・同期を追加しない。");
        builder.AppendLine("    // [並列判断] DB読取専用の判定とcallbackの副作用・順序依存の判定は別。読取専用だけを根拠にworkerへ分配しない。");
        builder.AppendLine("    // [例外/取消] 利用者の例外と取引の中止理由を保持。自動の業務再実行を追加せず、using/finallyを経て呼出し側へ伝える。");
        builder.AppendLine("    // [資源契約] 明示的な予約・所有・退避は出力文またはprovider計画に記載。生成されていない予算管理を仮定しない。");
        foreach(var step in document.Events){
            var decision="維持";if(step.Changed){decision="適用";}
            builder.AppendLine("    // [変形 "+step.Sequence+"] "+decision+" "+step.Layer+"/"+Clean(step.RuleId)+" node="+Clean(step.NodePath));
            if(step.Changed){builder.AppendLine("    //     before: "+Clean(step.Before));builder.AppendLine("    //     after : "+Clean(step.After));}
        }
        builder.AppendLine("#if DEBUG");
        builder.AppendLine("    // [Debug] この計画だけを定数として格納。取引、行、codec、Typeの参照を保持しない。");
        var data=Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(NetStandardJson.Serialize(document),true);
        builder.AppendLine("    internal const string "+target.MethodName+"_DebugPlan="+data+";");
        builder.AppendLine("#endif");
    }
    private sealed class Annotator:CSharpSyntaxRewriter{
        private int statement;
        public override SyntaxNode? Visit(SyntaxNode? node){
            if(node is null){return null;}
            var comment=Description(node);var number=0;
            if(comment is not null){number=++this.statement;}
            var result=base.Visit(node);
            if(result is null){return null;}
            if(comment is null){return result;}
            var trivia=SyntaxFactory.ParseLeadingTrivia("\n// [手順 "+number+"] "+comment+"\n");
            return result.WithLeadingTrivia(trivia.AddRange(result.GetLeadingTrivia()));
        }
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node){
            var result=(MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            if(!node.AttributeLists.SelectMany(static list=>list.Attributes).Any(static attribute=>attribute.Name.ToString().Contains("InterceptsLocation"))){return result;}
            var attribute=SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::System.CodeDom.Compiler.GeneratedCodeAttribute"))
                .WithArgumentList(SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList([
                    SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal("Seamless.FLinq.Generator"))),
                    SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(ToolVersion)))])));
            return result.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute)).WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")));
        }
        private static string? Description(SyntaxNode node){
            switch(node){
                case LocalDeclarationStatementSyntax local when local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)&&local.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword):
                    return "[非同期資源] "+Clean(local.Declaration.ToString())+"の所有をこのスコープに限定。正常終了・例外・returnでもDisposeAsyncの完了をawaitする。";
                case LocalDeclarationStatementSyntax local when local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword):
                    return "[資源] "+Clean(local.Declaration.ToString())+" の所有をこのスコープに限定。例外・returnでもusingの解放を実行。";
                case LocalDeclarationStatementSyntax local:return "[状態/評価] "+Clean(local.Declaration.ToString())+"。この位置で一度評価し、同じ呼出し内の後続処理へ渡す。";
                case ExpressionStatementSyntax expression:return "[処理] "+Clean(expression.Expression.ToString())+"。詳細なアクセス・変形・同期契約は直前の生成計画に対応。";
                case ReturnStatementSyntax statement:return "[制御] "+Clean(statement.ToString())+"。Invokeのboolは走査の続行/打切り、queryのreturnは結果の返却。";
                case ThrowStatementSyntax statement:return "[例外] "+Clean(statement.ToString())+"。既存の例外を成功や自動再試行へ変えず、外側のfinally/usingへ伝播。";
                case IfStatementSyntax condition:return "[分岐] "+Clean(condition.Condition.ToString())+" をこの位置で判定し、評価順序と短絡を維持。";
                case ForEachStatementSyntax loop:return "[走査] "+Clean(loop.Expression.ToString())+" を列挙順に処理。ここ自体はworkerを生成しない。";
                case ForStatementSyntax loop:return "[ループ] "+Clean(loop.Condition?.ToString()??"条件なし")+"。分割・部分結果の合成はこのforだけでは行わない。";
                case WhileStatementSyntax loop:return "[反復] "+Clean(loop.Condition.ToString())+"。条件評価・終了位置を元の制御構造に保持。";
                case UsingStatementSyntax statement when statement.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword):return "[非同期資源] 本文の正常終了・例外・早期終了の全経路でDisposeAsyncの完了をawaitして所有資源を解放。";
                case UsingStatementSyntax:return "[資源] 本文の正常終了・例外・早期終了の全経路で所有資源を解放。";
                case CheckedStatementSyntax statement:return "[数値] "+statement.Keyword.Text+"の範囲を維持。途中の数値例外の発生位置を移動しない。";
                case FixedStatementSyntax:return "[参照寿命] この本文だけでmanaged領域を固定。ポインタをworker/キュー/awaitの先へ保持する契約を追加しない。";
                case YieldStatementSyntax statement when statement.IsKind(SyntaxKind.YieldReturnStatement):return "[遅延返却] 一要素を返して次回まで中断。列挙子が所有する資源は外側のusing/finallyの寿命に従う。";
                case YieldStatementSyntax:return "[遅延終了] 列挙を終え、所有資源の後始末へ進む。取引の中止を正常終了へ変換する用途には使わない。";
                case DoStatementSyntax loop:return "[反復] 本文の後で"+Clean(loop.Condition.ToString())+"を評価。条件の評価位置を前へ移さない。";
                case SwitchStatementSyntax statement:return "[分岐] "+Clean(statement.Expression.ToString())+"を一度評価し、case/pattern/guardの順序を保持。";
                case TryStatementSyntax:return "[後始末/例外境界] catchとfinallyの範囲を維持し、途中の失敗でも所有資源の解放を通す。";
                case CatchClauseSyntax:return "[例外処理] 例外型とfilterの判定位置を維持。filterや後始末の副作用を勝手に再実行しない。";
                case ArrowExpressionClauseSyntax:return "[式本体] このメンバーの呼出し時に式を評価。宣言時の先行評価や共有キャッシュへ変形しない。";
                case LockStatementSyntax statement:return "[同期] "+Clean(statement.Expression.ToString())+" のmonitorでこの本文だけを保護。";
                case BreakStatementSyntax:return "[終了] 現在のループ/分岐を終了し、外側の後始末を通す。";
                case ContinueStatementSyntax:return "[継続] 後続の要素へ進め、現在の要素の残りの処理を実行しない。";
                default:return null;
            }
        }
    }
}
