using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    public static QueryPlanSelection SelectQueryPlans(Definition definition,string objectId,IReadOnlyList<ExecutionPlanDocument> plans){
        var compilation=Compilation(definition);
        IMethodSymbol? method;
        if(definition.Kind==DefinitionKind.Query){
            if(objectId!=definition.Id&&objectId!=definition.EntryMember){throw new ManagementException("NotFound","問い合わせがありません。");}
            method=PlanTargetMethod(compilation.GetTypeByMetadataName(definition.EntryType)?.GetMembers(definition.EntryMember).OfType<IMethodSymbol>()??[]);
        }else{
            var item=Objects(definition).SingleOrDefault(item=>item.Id==objectId)??throw new ManagementException("NotFound","問い合わせがありません。");
            if(item.Kind is not ("View" or "ScalarFunction" or "TableFunction" or "StoredProcedure")){throw new ManagementException("InvalidRequest","実行計画の対象にはビュー・関数・クエリを指定してください。");}
            var container=Container(compilation,definition);
            // ビュー・関数は IReader への拡張メンバー(ビューはプロパティの getter)。
            if(FindQueryMember(compilation,definition,item.Id) is{} query){
                method=query.Member as IMethodSymbol;
                if(query.Member is IPropertySymbol view){method=view.GetMethod;}
            }else{
                var members=Owner(container,item.Schema).GetMembers(item.Name);
                if(members.Length>1){throw new ManagementException("Unsupported","実行計画の対象メンバーが複数あります。オーバーロードを区別できる宣言を指定してください。");}
                var member=members.SingleOrDefault();
                method=member as IMethodSymbol;
                if(member is IPropertySymbol property){method=property.GetMethod;}
            }
        }
        if(method is null){return new(objectId,[],[],"対象メソッドと元ソースの対応を取得できません。");}
        var methods=new Dictionary<string,string>(StringComparer.Ordinal);
        var callSites=new HashSet<(string Method,ExecutionPlanCallSite Site)>();
        var pending=new Queue<IMethodSymbol>();pending.Enqueue(method);
        var visited=new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        string? originIssue=null;
        while(pending.TryDequeue(out var current)){
            current=(current.ReducedFrom??current).OriginalDefinition;
            if(!visited.Add(current)){continue;}
            foreach(var reference in current.DeclaringSyntaxReferences){
                var syntax=reference.GetSyntax();
                methods.TryAdd(current.ToDisplayString(),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(syntax.ToFullString()))));
                var semantic=compilation.GetSemanticModel(syntax.SyntaxTree);
                // 定義されただけのデリゲートやローカル関数を、対象の実参照へ混ぜない。
                var nodes=syntax.DescendantNodesAndSelf(node=>node==syntax||PlanTraversableBody(node,semantic));
                foreach(var node in nodes){
                    if(node is InvocationExpressionSyntax invocation){
                        var info=semantic.GetSymbolInfo(invocation);
                        if(info.Symbol is IMethodSymbol called&&called.DeclaringSyntaxReferences.Length>0){
                            var target=(called.ReducedFrom??called).OriginalDefinition;
                            callSites.Add((target.ToDisplayString(),new(invocation.SyntaxTree.FilePath.Replace('\\','/'),invocation.SpanStart,invocation.Span.Length,current.ToDisplayString())));
                            pending.Enqueue(called);
                        }
                        else if(info.CandidateSymbols.OfType<IMethodSymbol>().Skip(1).Any()){originIssue="呼び出し先のオーバーロードが未確定のため、一部の元ソースとの対応を取得できません。";}
                    }
                    if(node is ExpressionSyntax expression&&semantic.GetOperation(expression) is IPropertyReferenceOperation property){
                        if(property.Parent is INameOfOperation){continue;}
                        if(property.Parent is ISimpleAssignmentOperation assignment&&assignment.Target==property){continue;}
                        if(property.Property.GetMethod is {DeclaringSyntaxReferences.Length:>0} getter){pending.Enqueue(getter);}
                    }
                }
            }
        }
        var selected=new List<ExecutionPlanDocument>();
        foreach(var group in plans.Where(plan=>methods.TryGetValue(plan.MethodKey,out var hash)&&hash==plan.SourceHash).GroupBy(plan=>(plan.MethodKey,plan.SourceHash))){
            foreach(var plan in group.Where(plan=>plan.CallSite is not null)){
                var site=plan.CallSite!;
                var path=ResolvePlanSourcePath(definition.Source,site.Path);
                if(path is not null&&callSites.Contains((plan.MethodKey,site with{Path=path}))){selected.Add(plan);}
            }
            var legacy=group.Where(plan=>plan.CallSite is null).ToArray();
            if(legacy.Length==0){continue;}
            if(legacy.Length==1&&group.Count()==1){
                selected.Add(legacy[0]);originIssue="呼び出し位置が未収集の計画を含みます。対象への対応はメソッド単位です。";
            }else{originIssue="呼び出し位置が未収集のため、同じメソッドの複数計画を対象へ一意に対応付けできません。";}
        }
        string? reason=originIssue;
        if(selected.Count==0&&reason is null){reason="この対象に対応するFLinq展開計画がありません。定数を返す処理などは生成対象外です。対応するLINQ演算を含む場合は、ソース版とSDKでのビルド・取り込み・明示適用を確認してください。";}
        return new(objectId,methods.Keys.ToArray(),selected.ToArray(),reason);
    }
    private static IMethodSymbol? PlanTargetMethod(IEnumerable<IMethodSymbol> candidates){
        var methods=candidates.Take(2).ToArray();
        if(methods.Length>1){throw new ManagementException("Unsupported","実行計画の対象メソッドが複数あります。オーバーロードを区別できる宣言を指定してください。");}
        return methods.SingleOrDefault();
    }
    private static bool PlanTraversableBody(SyntaxNode node,SemanticModel semantic){
        if(node is LocalFunctionStatementSyntax){return false;}
        if(node is not AnonymousFunctionExpressionSyntax lambda){return true;}
        if(lambda.Parent is not ArgumentSyntax argument||argument.Expression!=lambda||argument.Parent?.Parent is not InvocationExpressionSyntax invocation){return false;}
        if(semantic.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method){
            // ビュー・関数の本体の受け手(生成される IReader)は、生成器を動かさない設計器のコンパイルでは型が決まらない。
            // 受け手の型が決まらない呼び出しは、名前が LINQ の演算ならラムダの中もたどる。
            return invocation.Expression is MemberAccessExpressionSyntax{Name.Identifier.ValueText:var name}
                &&semantic.GetTypeInfo(((MemberAccessExpressionSyntax)invocation.Expression).Expression).Type is null or IErrorTypeSymbol
                &&typeof(System.Linq.Enumerable).GetMethods().Any(item=>item.Name==name);
        }
        method=method.ReducedFrom??method;
        var owner=method.ContainingType.ToDisplayString();
        if(owner is "System.Linq.Enumerable" or "Seamless.Linq.Enumerable" or "Seamless.Linq.Set"){return true;}
        return owner is "Seamless.Database.DatabaseContainer" or "Seamless.Database.DatabaseSchema"
            &&method.Name is "StoredProcedure";
    }
}
