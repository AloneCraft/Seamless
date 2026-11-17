using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

// 匿名型の中間値をValueTupleへ写像し、必要な出口で元の型を再構築する。
public sealed partial class FLinqIncrementalGenerator {

    internal sealed class AnonymousTupleRegistry {
        /// <summary>型に匿名型が含まれるかを再帰的に判定する。</summary>
        public static bool ContainsAnonymous(ITypeSymbol type) => GenerationPipeline.ContainsAnonymousType(type);

        /// <summary>
        /// 型を「匿名型を閉じたValueTuple 型名へ置換した」完全修飾型名へ変換する。
        /// 例: 匿名型 { int Price, string Name } → (int Price,string Name)。
        /// 入れ子の匿名型も再帰的に置換する。
        /// </summary>
        public string ResolveClosedName(ITypeSymbol type) {
            if(type.IsAnonymousType&&type is INamedTypeSymbol anon) {
                var members = AnonymousMembers(anon);
                if(members.Count==0){
                    return "global::System.ValueTuple";
                }
                if(members.Count==1){
                    return $"global::System.ValueTuple<{this.ResolveClosedName(members[0].Type)}>";
                }
                return "("+string.Join(",",members.Select((member,index)=>{
                    var typeName=this.ResolveClosedName(member.Type);
                    if(IsImplicitTupleMember(member.Name)){return typeName;}
                    return typeName+" "+EscapeIdentifier(member.Name);
                }))+")";
            }
            if(type is IArrayTypeSymbol arrayType) {
                // 多次元は元の表現に任せる（匿名要素配列は稀）。1 次元のみ特別扱い。
                if(arrayType.Rank==1) {
                    return $"{this.ResolveClosedName(arrayType.ElementType)}[]";
                }
                return type.ToDisplayString(完全修飾形式);
            }
            if(type is INamedTypeSymbol {IsTupleType:true} tuple&&tuple.TupleElements.Any(static item=>ContainsAnonymous(item.Type))){
                return "("+string.Join(",",tuple.TupleElements.Select(item=>{
                    var typeName=this.ResolveClosedName(item.Type);
                    if(IsImplicitTupleMember(item.Name)){return typeName;}
                    return typeName+" "+EscapeIdentifier(item.Name);
                }))+")";
            }
            if(type is INamedTypeSymbol named&&named.TypeArguments.Any(GenerationPipeline.ContainsAnonymousType)) {
                var open = OpenGenericTypeName(named);
                var args = string.Join(", ",named.TypeArguments.Select(this.ResolveClosedName));
                return $"{open}<{args}>";
            }
            return type.ToDisplayString(完全修飾形式);
        }

        /// <summary>本体を保持したデリゲートの戻り値から、内部のタプル表現を構築する。</summary>
        internal string? ProjectAnonymousValue(ITypeSymbol type,string value){
            if(!ContainsAnonymous(type)){return value;}
            if(type is not INamedTypeSymbol {IsAnonymousType:true} anonymous){return null;}
            var values=new List<string>();
            foreach(var member in AnonymousMembers(anonymous)){
                var projected=this.ProjectAnonymousValue(member.Type,value+"."+EscapeIdentifier(member.Name));
                if(projected is null){return null;}
                values.Add(projected);
            }
            if(values.Count<=1){return "global::System.ValueTuple.Create("+string.Join(",",values)+")";}
            return "("+string.Join(",",values)+")";
        }

        /// <summary>
        /// 匿名オブジェクト生成式 <c>new { a = e0, b = e1 }</c> を
        /// <c>(a:e0,b:e1)</c>へ変換する。0項目と1項目にはValueTuple.Createを使う。
        /// <br/>入れ子の <c>new { ... }</c> は再帰的に変換し、それ以外の値式は <paramref name="renderLeaf"/> でレンダリングする。
        /// </summary>
        public string RenderAnonymousCreation(
            AnonymousObjectCreationExpressionSyntax node,
            SemanticModel semanticModel,
            Func<ExpressionSyntax,string> renderLeaf) {
            if(semanticModel.GetTypeInfo(node).Type is not INamedTypeSymbol sym) {
                return "default!";
            }
            var members=AnonymousMembers(sym);
            var args = node.Initializers.Select(init => {
                var value = StripParentheses(init.Expression);
                if(value is AnonymousObjectCreationExpressionSyntax innerAnon) {
                    return this.RenderAnonymousCreation(innerAnon,semanticModel,renderLeaf);
                }
                return renderLeaf(init.Expression);
            });
            var values=args.ToArray();
            if(values.Length<=1){
                return "global::System.ValueTuple.Create("+string.Join(",",values)+")";
            }
            return "("+string.Join(",",values.Select((value,index)=>{
                var name=members[index].Name;
                if(IsImplicitTupleMember(name)){return value;}
                return EscapeIdentifier(name)+":"+value;
            }))+")";
        }

        internal static IReadOnlyDictionary<string,string> MemberReplacements(ExpressionSyntax expression,SemanticModel model){
            var replacements=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var access in expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>()){
                if(IsInsideNameof(access)){continue;}
                var mapped=ResolveMemberName(access,model);
                if(access.Name.ToString()!=mapped){
                    replacements[NormalizeGeneratedExpressionText(access.ToString())]=mapped;
                }
            }
            return replacements;
        }

        internal static string RewriteMembers(ExpressionSyntax original,SemanticModel model,string rendered){
            var replacements=MemberReplacements(original,model);
            if(replacements.Count==0){
                return rendered;
            }
            return new TupleMemberRewriter(replacements).Visit(SyntaxFactory.ParseExpression(rendered))!.ToFullString();
        }

        private sealed class TupleMemberRewriter(IReadOnlyDictionary<string,string> replacements):CSharpSyntaxRewriter{
            public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node){
                var rendered=(MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
                if(replacements.TryGetValue(NormalizeGeneratedExpressionText(node.ToString()),out var mapped)){
                    return rendered.WithName(SyntaxFactory.IdentifierName(mapped).WithTriviaFrom(node.Name));
                }
                return rendered;
            }
        }

        internal static string[] HashMembers(ITypeSymbol type){
            if(type is not INamedTypeSymbol {IsAnonymousType:true} anonymous||!RequiresFullHash(anonymous)){
                return [];
            }
            return HashMembers(anonymous,string.Empty).ToArray();
        }

        private static bool RequiresFullHash(INamedTypeSymbol anonymous){
            var members=AnonymousMembers(anonymous);
            return members.Count>8||members.Any(static member=>
                member.Type is INamedTypeSymbol {IsAnonymousType:true} nested&&RequiresFullHash(nested));
        }

        private static IEnumerable<string> HashMembers(INamedTypeSymbol anonymous,string prefix){
            var members=AnonymousMembers(anonymous);
            for(var index=0;index<members.Count;index++){
                var path=prefix+TupleMemberName(members,index);
                if(members[index].Type is INamedTypeSymbol {IsAnonymousType:true} nested){
                    foreach(var child in HashMembers(nested,path+".")){
                        yield return child;
                    }
                }else{
                    yield return path;
                }
            }
        }

        internal static bool IsSafeTarget(Linq傍受対象型 target,out string reason){
            reason=string.Empty;
            if(target.Method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not SyntaxNode declaration){
                return true;
            }
            var model=target.SemanticModel.Compilation.GetSemanticModel(declaration.SyntaxTree);
            var memberPaths=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var expression in declaration.DescendantNodes().OfType<ExpressionSyntax>()){
                if(IsInsideNameof(expression)){continue;}
                var info=model.GetTypeInfo(expression);
                var anonymous=info.Type is not null&&ContainsAnonymous(info.Type);
                var anonymousDefault=expression.Kind() is SyntaxKind.NullLiteralExpression or SyntaxKind.DefaultLiteralExpression
                    &&info.ConvertedType is not null&&ContainsAnonymous(info.ConvertedType);
                if(anonymousDefault||anonymous&&(expression is ConditionalExpressionSyntax or ConditionalAccessExpressionSyntax
                    ||info.ConvertedType is not null&&!ContainsAnonymous(info.ConvertedType)
                    ||expression.Parent is BinaryExpressionSyntax or InterpolationSyntax or CastExpressionSyntax or IsPatternExpressionSyntax or SwitchExpressionSyntax
                    ||expression.Parent?.IsKind(SyntaxKind.CoalesceAssignmentExpression)==true)){
                    reason="匿名型のnull、参照同一性、型または文字列表現を観測するため元の経路を使用。";
                    return false;
                }
                if(expression is MemberAccessExpressionSyntax access){
                    var original=NormalizeGeneratedExpressionText(access.ToString());
                    var mapped=ResolveMemberName(access,model);
                    if(memberPaths.TryGetValue(original,out var previous)&&previous!=mapped){
                        reason="同名のアクセスが別の型を指すため元の経路を使用。";
                        return false;
                    }
                    memberPaths[original]=mapped;
                }
                if(expression is not InvocationExpressionSyntax invocation||model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method){
                    continue;
                }
                var definition=method.ReducedFrom??method;
                if(invocation.Expression is MemberAccessExpressionSyntax receiver
                    &&model.GetTypeInfo(receiver.Expression).Type is {IsAnonymousType:true}
                    &&method.MethodKind==MethodKind.Ordinary){
                    reason="匿名型自身のメソッドを呼ぶため元の経路を使用。";
                    return false;
                }
                if(!method.TypeArguments.Any(ContainsAnonymous)){continue;}
                if(definition.ContainingType.ToDisplayString()=="System.Linq.Enumerable"
                    &&method.Name is "DefaultIfEmpty" or "FirstOrDefault" or "LastOrDefault" or "SingleOrDefault" or "ElementAtOrDefault" or "Min" or "Max" or "MinBy" or "MaxBy"
                    &&ContainsAnonymous(method.ReturnType)){
                    reason="空の列で匿名型の既定値がnullになるため元の経路を使用。";
                    return false;
                }
                if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
                    ||(method.Name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending"
                        &&ContainsAnonymous(method.TypeArguments.Last()))
                    ||invocation.ArgumentList.Arguments.Any(argument=>
                        model.GetOperation(argument) is global::Microsoft.CodeAnalysis.Operations.IArgumentOperation {Parameter.Name:"comparer"}
                        &&!argument.Expression.IsKind(SyntaxKind.NullLiteralExpression))){
                    reason="外部処理、整列キーまたは独自比較器で匿名型を使うため元の経路を使用。";
                    return false;
                }
            }
            return true;
        }

        private static string ResolveMemberName(MemberAccessExpressionSyntax access,SemanticModel model){
            var name=access.Name.ToString();
            if(model.GetSymbolInfo(access).Symbol is IPropertySymbol property&&property.ContainingType.IsAnonymousType){
                var members=AnonymousMembers(property.ContainingType);
                for(var index=0;index<members.Count;index++){
                    if(SymbolEqualityComparer.Default.Equals(members[index],property)){
                        name=TupleMemberName(members,index);
                        break;
                    }
                }
            }
            return name;
        }

        private static string TupleMemberName(IReadOnlyList<IPropertySymbol> members,int index){
            var name=members[index].Name;
            if(members.Count==1||IsImplicitTupleMember(name)){
                return "Item"+(index+1);
            }
            return EscapeIdentifier(name);
        }

        private static bool IsImplicitTupleMember(string name)=>
            name is "Rest" or "ToString" or "Equals" or "GetHashCode" or "Deconstruct" or "CompareTo"
            ||name.StartsWith("Item",StringComparison.Ordinal)&&int.TryParse(name.Substring(4),out _);

        /// <summary>
        /// ValueTuple 式から元の匿名型を再構成する C# 式文字列を返す。
        /// 例: structRecordExpr が "rec"、originalAnonType が {p:int} → "new{p=rec.p}"
        /// 入れ子の匿名型も再帰的に再構成する。
        /// </summary>
        public string ReconstructAnonymousCreation(ITypeSymbol originalAnonType,string structRecordExpr) {
            if(!originalAnonType.IsAnonymousType||originalAnonType is not INamedTypeSymbol anon) {
                return structRecordExpr;
            }
            var members = AnonymousMembers(anon);
            var parts = members.Select((m,index) => {
                var memberAccess = $"{structRecordExpr}.{TupleMemberName(members,index)}";
                return $"{EscapeIdentifier(m.Name)}={this.ReconstructAnonymousCreation(m.Type,memberAccess)}";
            });
            return $"new{{{string.Join(",",parts)}}}";
        }

        /// <summary>
        /// <c>new {{ a = e0, b = e1 }}</c> を ValueTuple ファクトリ呼び出しではなく
        /// 生の匿名型生成式 <c>new {{ a = renderLeaf(e0), b = renderLeaf(e1) }}</c> として展開する。
        /// 出口ステップ（最終的に匿名型を返す必要がある箇所）で使用する。
        /// 入れ子の <c>new {{ ... }}</c> は再帰的に処理する。
        /// </summary>
        public string RenderAnonymousCreationRaw(
            AnonymousObjectCreationExpressionSyntax node,
            SemanticModel semanticModel,
            Func<ExpressionSyntax,string> renderLeaf) {
            if(semanticModel.GetTypeInfo(node).Type is not INamedTypeSymbol sym) {
                return "default!";
            }
            var members = AnonymousMembers(sym);
            var parts = node.Initializers.Select((init,i) => {
                var memberName = members[i].Name;
                var value = StripParentheses(init.Expression);
                var rendered = value is AnonymousObjectCreationExpressionSyntax inner
                    ? this.RenderAnonymousCreationRaw(inner,semanticModel,renderLeaf)
                    : renderLeaf(init.Expression);
                return $"{EscapeIdentifier(memberName)}={rendered}";
            });
            return $"new{{{string.Join(",",parts)}}}";
        }

        // 匿名型のメンバー（プロパティ）を宣言順で返す。
        private static IReadOnlyList<IPropertySymbol> AnonymousMembers(INamedTypeSymbol anon)
            => anon.GetMembers().OfType<IPropertySymbol>().ToList();

    }

    private static ExpressionSyntax StripParentheses(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }

    // C# キーワードと衝突するメンバー名は @ でエスケープする。
    private static string EscapeIdentifier(string name) {
        return SyntaxFacts.GetKeywordKind(name)!=SyntaxKind.None
            ||SyntaxFacts.GetContextualKeywordKind(name)!=SyntaxKind.None
            ? "@"+name
            : name;
    }

}
