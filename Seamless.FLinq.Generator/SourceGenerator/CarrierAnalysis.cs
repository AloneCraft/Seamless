using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq;
// selector / result selector が作る carrier の逆解析。
// carrier とは「データを保持するだけの形」= 匿名型 / タプル式 / ValueTuple / Tuple /
// record / record struct の位置引数コンストラクタ、およびそれらの入れ子。
//
// carrier を消費する式(述語や後続 selector)を、carrier が作られる前のパラメータ空間へ引き戻す。
//   producer: x => new{ A=x.P, B=x.Q }      carrier を作る側
//   consumer: t => t.A>0                    carrier を読む側
//   → 引き戻し結果: x.P>0
//
// 述語プッシュダウン(OptimizeVisitor)と射影融合(ProjectionMergeVisitor)の両方がこの機構を共有する。
// 引き戻せない形では必ず false を返す(安全側)。
internal static class CarrierAnalysis {
    /// <summary>
    /// carrier メンバをどこまで引き戻してよいかの方針。
    /// </summary>
    internal enum LiftPolicy {
        /// <summary>
        /// carrier メンバは <see cref="IsLiftableLeaf"/> を満たす式に限る。述語プッシュダウンの既定。
        /// メソッド呼び出しを構造的に排除しているため重複評価が問題になりにくい(出荷済みの挙動)。
        /// </summary>
        LeafOnly,
        /// <summary>
        /// carrier メンバに任意の式を許し、代わりに <see cref="CarrierTier"/> ごとの参照回数規則で守る。射影融合の既定。
        /// </summary>
        ProjectionMerge,
    }
    // carrier メンバ式を「引き戻したときに何が壊れうるか」で分類する。
    private enum CarrierTier {
        // 識別子 / リテラル / this / base / default / typeof。評価が自明に無副作用・無例外。
        // 捨ててもよく(0 回)、複製してもよい(2 回以上)。
        Pure,
        // メンバアクセス / 要素アクセス / キャスト。複製は既存の述語プッシュダウンが既に許容している前提。
        // ただし 0 回は禁止 — getter の副作用や NRE / IndexOutOfRange / InvalidCast が消えて意味が変わる。
        Liftable,
        // メソッド呼び出しを含むその他すべて。ちょうど 1 回のときだけ許す。
        Arbitrary,
    }
    /// <summary>
    /// <paramref name="consumer"/> の本体を <paramref name="producer"/> が作る carrier の逆方向へ展開し、
    /// producer のパラメータ空間の式へ引き戻す。
    /// </summary>
    /// <param name="referencedProducerParameters">引き戻した式が参照する producer パラメータ名の集合。押し下げ先の判定に使う。</param>
    internal static bool TryLift(
        LambdaExpressionSyntax consumer,
        LambdaExpressionSyntax producer,
        SemanticModel? model,
        LiftPolicy policy,
        out ExpressionSyntax liftedBody,
        out HashSet<string> referencedProducerParameters) {
        liftedBody=null!;
        referencedProducerParameters=new HashSet<string>(StringComparer.Ordinal);
        var consumerParameters = GetLambdaParameterNames(consumer);
        if(consumerParameters.Count!=1
            ||!TryGetLambdaBody(consumer,out var consumerBody)
            ||!TryGetLambdaBody(producer,out var producerBody)) {
            return false;
        }
        // 射影融合は評価回数・評価順を動かしうるので Anchor 契約に従って不発火にする。
        // 述語プッシュダウン側の Anchor 判定は呼び出し元(連言分割)が持っているのでここでは掛けない。
        if(policy==LiftPolicy.ProjectionMerge
            &&(AnchorAnalysis.ContainsAnchor(consumerBody,model)||AnchorAnalysis.ContainsAnchor(producerBody,model))) {
            return false;
        }
        if(!TryCreateShape(producerBody,model,policy,out var shape)) {
            return false;
        }
        const string sentinel = "__Tsavorite_carrier_root";
        if(new ParameterRenameRewriter(consumerParameters[0],sentinel).Visit(consumerBody) is not ExpressionSyntax renamed) {
            return false;
        }
        var rewriter = new CarrierAccessRewriter(sentinel,shape);
        if(rewriter.Visit(renamed) is not ExpressionSyntax lifted) {
            return false;
        }
        // 未解決の carrier 参照が残っていたら引き戻し失敗。
        if(ContainsIdentifier(lifted,sentinel)) {
            return false;
        }
        if(!SatisfiesLiftPolicy(shape,rewriter.ResolvedCounts,policy)) {
            return false;
        }
        referencedProducerParameters=CollectParameterReferences(lifted,GetLambdaParameterNames(producer));
        liftedBody=lifted;
        return true;
    }
    // carrier メンバごとの引き戻し回数を Tier 規則に照らす。
    private static bool SatisfiesLiftPolicy(CarrierShape shape,IReadOnlyDictionary<LeafCarrierShape,int> counts,LiftPolicy policy) {
        if(policy==LiftPolicy.LeafOnly) {
            // TryCreateShape が IsLiftableLeaf で既に絞り込んでいる。出荷済みの挙動を変えないためここでは追加制約を掛けない。
            return true;
        }
        var leaves = new List<LeafCarrierShape>();
        shape.CollectLeaves(leaves);
        var arbitraryCount = 0;
        foreach(var leaf in leaves) {
            if(!counts.TryGetValue(leaf,out var referenceCount)) {
                referenceCount=0;
            }
            var tier = ClassifyTier(leaf.Expression);
            if(tier==CarrierTier.Pure) {
                continue;
            }
            if(tier==CarrierTier.Liftable) {
                // 評価の消滅を禁止する。複製は許す。
                if(referenceCount<1) {
                    return false;
                }
                continue;
            }
            arbitraryCount++;
            // 評価回数の変化を禁止する。
            if(referenceCount!=1) {
                return false;
            }
        }
        // Arbitrary が 2 個以上あると、carrier 構築順と consumer の読み出し順が食い違って評価順が入れ替わる。
        return arbitraryCount<=1;
    }
    private static CarrierTier ClassifyTier(ExpressionSyntax expression) {
        expression=Strip(expression);
        if(expression is IdentifierNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax
            or BaseExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax) {
            return CarrierTier.Pure;
        }
        if(IsLiftableLeaf(expression)) {
            return CarrierTier.Liftable;
        }
        return CarrierTier.Arbitrary;
    }
    // --- carrier shape ---
    internal abstract class CarrierShape {
        public abstract bool TryResolve(IReadOnlyList<string> path,out LeafCarrierShape leaf);
        public abstract void CollectLeaves(List<LeafCarrierShape> into);
    }
    internal sealed class LeafCarrierShape(ExpressionSyntax expression):CarrierShape {
        public ExpressionSyntax Expression { get; } = expression;
        public override bool TryResolve(IReadOnlyList<string> path,out LeafCarrierShape leaf) {
            leaf=this;
            return path.Count==0;
        }
        public override void CollectLeaves(List<LeafCarrierShape> into) => into.Add(this);
    }
    internal sealed class ObjectCarrierShape(Dictionary<string,CarrierShape> members):CarrierShape {
        public override bool TryResolve(IReadOnlyList<string> path,out LeafCarrierShape leaf) {
            leaf=null!;
            if(path.Count==0||!members.TryGetValue(path[0],out var next)) {
                return false;
            }
            return next.TryResolve(path.Skip(1).ToArray(),out leaf);
        }
        public override void CollectLeaves(List<LeafCarrierShape> into) {
            foreach(var member in members.Values) {
                member.CollectLeaves(into);
            }
        }
    }
    internal static bool TryCreateShape(ExpressionSyntax expression,SemanticModel? model,LiftPolicy policy,out CarrierShape shape) {
        expression=Strip(expression);
        // 匿名型: new{p,q}
        if(expression is AnonymousObjectCreationExpressionSyntax anonymous) {
            var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
            foreach(var initializer in anonymous.Initializers) {
                var name = MemberName(initializer);
                if(string.IsNullOrWhiteSpace(name)||!TryCreateShape(initializer.Expression,model,policy,out var memberShape)) {
                    shape=null!;
                    return false;
                }
                members[name]=memberShape;
            }
            shape=new ObjectCarrierShape(members);
            return true;
        }
        // タプル式: (a,b)
        if(expression is TupleExpressionSyntax tuple) {
            return TryCreateTupleShape(
                tuple.Arguments.Select(static a => a.Expression).ToArray(),
                tuple.Arguments.Select(GetTupleAliases).ToArray(),
                model,
                policy,
                out shape);
        }
        // ValueTuple / Tuple コンストラクタ、あるいは record / record struct の位置引数コンストラクタ
        if(expression is ObjectCreationExpressionSyntax creation&&creation.ArgumentList is not null) {
            if(TryGetTupleTypeAliases(creation.Type,creation.ArgumentList.Arguments.Count,out var tupleAliases)) {
                return TryCreateTupleShape(
                    creation.ArgumentList.Arguments.Select(static a => a.Expression).ToArray(),
                    tupleAliases,
                    model,
                    policy,
                    out shape);
            }
            if(TryCreatePositionalRecordShape(creation,creation.ArgumentList.Arguments,model,policy,out shape)) {
                return true;
            }
        }
        // target-typed new(...)。型を確かめずタプル扱いすると誤解決しうるので、必ずシンボルで判別する。
        if(expression is ImplicitObjectCreationExpressionSyntax implicitCreation) {
            if(TryCreatePositionalRecordShape(implicitCreation,implicitCreation.ArgumentList.Arguments,model,policy,out shape)) {
                return true;
            }
            if(TryCreateTargetTypedTupleShape(implicitCreation,model,policy,out shape)) {
                return true;
            }
            shape=null!;
            return false;
        }
        if(policy==LiftPolicy.ProjectionMerge||IsLiftableLeaf(expression)) {
            shape=new LeafCarrierShape(expression);
            return true;
        }
        shape=null!;
        return false;
    }
    private static bool TryCreateTupleShape(ExpressionSyntax[] elements,string[][] aliases,SemanticModel? model,LiftPolicy policy,out CarrierShape shape) {
        var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
        for(var index = 0;index<elements.Length;index++) {
            if(!TryCreateShape(elements[index],model,policy,out var elementShape)) {
                shape=null!;
                return false;
            }
            members[$"Item{index+1}"]=elementShape;
            foreach(var alias in aliases[index]) {
                if(!string.IsNullOrWhiteSpace(alias)) {
                    members[alias]=elementShape;
                }
            }
        }
        shape=new ObjectCarrierShape(members);
        return true;
    }
    /// <summary>
    /// record / record struct の位置引数コンストラクタ(primary constructor)を carrier として認識する。
    /// コンパイラ生成の <c>Deconstruct</c> は位置パラメータ名をそのまま out パラメータ名として保つので、
    /// 「ctor パラメータ名列 == Deconstruct の out パラメータ名列(順序込み)」を primary constructor の判定に使う。
    /// 手書き ctor や引数 0 個の record は一致しないので不可へ倒れる。
    /// </summary>
    private static bool TryCreatePositionalRecordShape(
        SyntaxNode creation,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        SemanticModel? model,
        LiftPolicy policy,
        out CarrierShape shape) {
        shape=null!;
        if(model is null) {
            return false;
        }
        if(model.GetSymbolInfo(creation).Symbol is not IMethodSymbol constructor) {
            return false;
        }
        if(constructor.ContainingType is not { IsRecord: true } recordType) {
            return false;
        }
        if(constructor.Parameters.Length==0||constructor.Parameters.Length!=arguments.Count) {
            return false;
        }
        if(!IsPositionalRecordConstructor(recordType,constructor)) {
            return false;
        }
        // 名前付き引数と位置引数の混在は対応付けを誤りうるので、どちらかに統一されている場合だけ扱う。
        var namedCount = arguments.Count(static a => a.NameColon is not null);
        if(namedCount!=0&&namedCount!=arguments.Count) {
            return false;
        }
        var byParameterName = new Dictionary<string,ExpressionSyntax>(StringComparer.Ordinal);
        for(var index = 0;index<arguments.Count;index++) {
            var argument = arguments[index];
            if(argument.NameColon is not null) {
                byParameterName[argument.NameColon.Name.Identifier.ValueText]=argument.Expression;
            } else {
                byParameterName[constructor.Parameters[index].Name]=argument.Expression;
            }
        }
        var members = new Dictionary<string,CarrierShape>(StringComparer.Ordinal);
        foreach(var parameter in constructor.Parameters) {
            if(!byParameterName.TryGetValue(parameter.Name,out var argumentExpression)) {
                return false;
            }
            if(!TryCreateShape(argumentExpression,model,policy,out var memberShape)) {
                return false;
            }
            members[parameter.Name]=memberShape;
        }
        shape=new ObjectCarrierShape(members);
        return true;
    }
    private static bool IsPositionalRecordConstructor(INamedTypeSymbol recordType,IMethodSymbol constructor) {
        foreach(var candidate in recordType.GetMembers("Deconstruct").OfType<IMethodSymbol>()) {
            if(candidate.Parameters.Length!=constructor.Parameters.Length) {
                continue;
            }
            var matched = true;
            for(var index = 0;index<candidate.Parameters.Length;index++) {
                if(candidate.Parameters[index].RefKind!=RefKind.Out
                    ||!string.Equals(candidate.Parameters[index].Name,constructor.Parameters[index].Name,StringComparison.Ordinal)) {
                    matched=false;
                    break;
                }
            }
            if(!matched) {
                continue;
            }
            // 位置パラメータと同名の読み取り可能プロパティが実在することも確認して二重に固める。
            foreach(var parameter in constructor.Parameters) {
                if(!recordType.GetMembers(parameter.Name).OfType<IPropertySymbol>().Any(static p => p.GetMethod is not null)) {
                    return false;
                }
            }
            return true;
        }
        return false;
    }
    // new(a,b) が ValueTuple / Tuple を指しているときだけタプル carrier として扱う。
    // 要素の別名は登録しない(target-typed new の NameColon は item1/item2 であり宣言側の要素名とは一致しないため)。
    private static bool TryCreateTargetTypedTupleShape(
        ImplicitObjectCreationExpressionSyntax creation,
        SemanticModel? model,
        LiftPolicy policy,
        out CarrierShape shape) {
        shape=null!;
        if(model is null) {
            return false;
        }
        if(model.GetSymbolInfo(creation).Symbol is not IMethodSymbol constructor) {
            return false;
        }
        var containingType = constructor.ContainingType;
        if(containingType is null) {
            return false;
        }
        if(!containingType.IsTupleType
            &&!(containingType.Name is "ValueTuple" or "Tuple"
                &&string.Equals(containingType.ContainingNamespace?.ToDisplayString(),"System",StringComparison.Ordinal))) {
            return false;
        }
        var arguments = creation.ArgumentList.Arguments;
        var aliases = new string[arguments.Count][];
        for(var index = 0;index<aliases.Length;index++) {
            aliases[index]=Array.Empty<string>();
        }
        return TryCreateTupleShape(arguments.Select(static a => a.Expression).ToArray(),aliases,model,policy,out shape);
    }
    private static string[] GetTupleAliases(ArgumentSyntax argument) {
        if(argument.NameColon is not null) {
            return new[] { argument.NameColon.Name.Identifier.ValueText };
        }
        if(InferName(argument.Expression,out var name)) {
            return new[] { name };
        }
        return Array.Empty<string>();
    }
    private static bool TryGetTupleTypeAliases(TypeSyntax type,int count,out string[][] aliases) {
        aliases=new string[count][];
        for(var index = 0;index<count;index++) {
            aliases[index]=Array.Empty<string>();
        }
        type=type switch {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name,
            _ => type,
        };
        if(type is TupleTypeSyntax tupleType) {
            aliases=tupleType.Elements.Select(static e => string.IsNullOrWhiteSpace(e.Identifier.ValueText) ? Array.Empty<string>() : new[] { e.Identifier.ValueText }).ToArray();
            return tupleType.Elements.Count==count;
        }
        if(type is GenericNameSyntax generic&&generic.Identifier.ValueText is "ValueTuple" or "Tuple") {
            return generic.TypeArgumentList.Arguments.Count==count;
        }
        if(type is IdentifierNameSyntax identifier&&identifier.Identifier.ValueText is "ValueTuple" or "Tuple") {
            return true;
        }
        return false;
    }
    private static bool IsLiftableLeaf(ExpressionSyntax expression) {
        expression=Strip(expression);
        return expression switch {
            IdentifierNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax
                or BaseExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax => true,
            MemberAccessExpressionSyntax member => IsLiftableLeaf(member.Expression),
            ElementAccessExpressionSyntax element => IsLiftableLeaf(element.Expression)&&element.ArgumentList.Arguments.All(static a => IsLiftableLeaf(a.Expression)),
            CastExpressionSyntax cast => IsLiftableLeaf(cast.Expression),
            _ => false,
        };
    }
    // --- rewriters ---
    // consumer 中の carrier member access を carrier 生成側の元式へ戻す。
    // どの葉を何回引き戻したかを ResolvedCounts に記録し、Tier 規則の判定に使う。
    private sealed class CarrierAccessRewriter(string rootName,CarrierShape shape):CSharpSyntaxRewriter {
        private readonly Dictionary<LeafCarrierShape,int> resolvedCounts = new();
        public IReadOnlyDictionary<LeafCarrierShape,int> ResolvedCounts => this.resolvedCounts;
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            if(this.TryGetPath(node,out var path)&&shape.TryResolve(path,out var leaf)) {
                return this.Replace(leaf,node);
            }
            return base.VisitMemberAccessExpression(node);
        }
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,rootName,StringComparison.Ordinal)
                &&!(node.Parent is MemberAccessExpressionSyntax member&&member.Expression==node)
                &&shape.TryResolve(Array.Empty<string>(),out var leaf)) {
                return this.Replace(leaf,node);
            }
            return base.VisitIdentifierName(node);
        }
        // carrier 生成側から持ってきた式は元の位置のトリビアを持たない。差し替え先のトリビアを移さないと
        // 「x.Inner is null」が「i is null」ではなく「iis null」になる(生成コードは文字列として組み立てられる)。
        private ExpressionSyntax Replace(LeafCarrierShape leaf,ExpressionSyntax replaced) {
            this.resolvedCounts.TryGetValue(leaf,out var count);
            this.resolvedCounts[leaf]=count+1;
            return Wrap(leaf.Expression).WithTriviaFrom(replaced);
        }
        private bool TryGetPath(ExpressionSyntax expression,out IReadOnlyList<string> path) {
            expression=Strip(expression);
            if(expression is IdentifierNameSyntax identifier&&string.Equals(identifier.Identifier.ValueText,rootName,StringComparison.Ordinal)) {
                path=Array.Empty<string>();
                return true;
            }
            if(expression is MemberAccessExpressionSyntax member&&this.TryGetPath(member.Expression,out var parent)) {
                var parts = parent.ToList();
                parts.Add(member.Name.Identifier.ValueText);
                path=parts;
                return true;
            }
            path=Array.Empty<string>();
            return false;
        }
    }
    private sealed class ParameterRenameRewriter(string from,string to):CSharpSyntaxRewriter {
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(string.Equals(node.Identifier.ValueText,from,StringComparison.Ordinal)) {
                return SyntaxFactory.IdentifierName(to);
            }
            return base.VisitIdentifierName(node);
        }
    }
    // --- shared syntax helpers ---
    internal static HashSet<string> CollectParameterReferences(ExpressionSyntax expression,IReadOnlyList<string> parameterNames) {
        var candidates = new HashSet<string>(parameterNames,StringComparer.Ordinal);
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach(var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()) {
            if(candidates.Contains(identifier.Identifier.ValueText)) {
                matched.Add(identifier.Identifier.ValueText);
            }
        }
        return matched;
    }
    internal static bool ContainsIdentifier(ExpressionSyntax expression,string name) =>
        expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Any(identifier => string.Equals(identifier.Identifier.ValueText,name,StringComparison.Ordinal));
    internal static LambdaExpressionSyntax RecreateLambda(LambdaExpressionSyntax template,ExpressionSyntax body) {
        var parameters = GetLambdaParameterNames(template);
        if(parameters.Count==1&&template is SimpleLambdaExpressionSyntax) {
            return SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameters[0])),body);
        }
        return SyntaxFactory.ParenthesizedLambdaExpression(
            SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters.Select(static n => SyntaxFactory.Parameter(SyntaxFactory.Identifier(n))))),
            body);
    }
    internal static LambdaExpressionSyntax RecreateLambdaWithParameter(string name,ExpressionSyntax body) =>
        SyntaxFactory.SimpleLambdaExpression(SyntaxFactory.Parameter(SyntaxFactory.Identifier(name)),body);
    /// <summary>
    /// 元ラムダのパラメータリスト(型注釈を含む)を保ったまま本体だけ差し替える。
    /// 持ち上げた本体が新たな捕捉を導入しうるので <c>static</c> 修飾子は保守的に落とす。
    /// </summary>
    internal static LambdaExpressionSyntax ReplaceLambdaBody(LambdaExpressionSyntax lambda,ExpressionSyntax body) {
        var modifiers = SyntaxFactory.TokenList(lambda.Modifiers.Where(static m => !m.IsKind(SyntaxKind.StaticKeyword)));
        if(lambda is SimpleLambdaExpressionSyntax simple) {
            return simple.WithModifiers(modifiers).WithExpressionBody(body).WithBlock(null);
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesized) {
            return parenthesized.WithModifiers(modifiers).WithExpressionBody(body).WithBlock(null);
        }
        return RecreateLambda(lambda,body);
    }
    internal static bool TryGetLambdaBody(LambdaExpressionSyntax lambda,out ExpressionSyntax body) {
        if(lambda.Body is ExpressionSyntax expressionBody) {
            body=expressionBody;
            return true;
        }
        body=null!;
        return false;
    }
    internal static IReadOnlyList<string> GetLambdaParameterNames(LambdaExpressionSyntax lambda) {
        if(lambda is SimpleLambdaExpressionSyntax simple) {
            return new[] { simple.Parameter.Identifier.ValueText };
        }
        if(lambda is ParenthesizedLambdaExpressionSyntax parenthesized) {
            return parenthesized.ParameterList.Parameters.Select(static p => p.Identifier.ValueText).ToArray();
        }
        return Array.Empty<string>();
    }
    // トップレベル && を平坦化。括弧は「&& か」の判定にだけ剥がし、連言自体は元ノードのまま残して優先順位を保つ。
    // &(非短絡)は両辺を必ず評価させる意図的な記法なので対象にしない。
    internal static void CollectConjuncts(ExpressionSyntax expression,List<ExpressionSyntax> into) {
        if(Strip(expression) is BinaryExpressionSyntax binary&&binary.IsKind(SyntaxKind.LogicalAndExpression)) {
            CollectConjuncts(binary.Left,into);
            CollectConjuncts(binary.Right,into);
        } else {
            into.Add(expression);
        }
    }
    internal static ExpressionSyntax CombineConjuncts(IReadOnlyList<ExpressionSyntax> parts) {
        var combined = Wrap(parts[0]);
        for(var index = 1;index<parts.Count;index++) {
            combined=SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression,combined,Wrap(parts[index]));
        }
        return combined;
    }
    internal static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax source,string method,IEnumerable<ExpressionSyntax> arguments) {
        var memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,Wrap(source),SyntaxFactory.IdentifierName(method));
        return SyntaxFactory.InvocationExpression(
            memberAccess,
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(static a => SyntaxFactory.Argument(a)))));
    }
    internal static InvocationExpressionSyntax CreateSequenceCall(ExpressionSyntax source,string method,params ExpressionSyntax[] arguments) =>
        CreateSequenceCall(source,method,(IEnumerable<ExpressionSyntax>)arguments);
    internal static ExpressionSyntax Wrap(ExpressionSyntax expression) {
        expression=Strip(expression);
        if(expression is IdentifierNameSyntax or GenericNameSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax
            or LiteralExpressionSyntax or ThisExpressionSyntax or BaseExpressionSyntax
            or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax) {
            return expression;
        }
        return SyntaxFactory.ParenthesizedExpression(expression);
    }
    internal static ExpressionSyntax Strip(ExpressionSyntax expression) {
        while(expression is ParenthesizedExpressionSyntax parenthesized) {
            expression=parenthesized.Expression;
        }
        return expression;
    }
    // 匿名型プロパティ名を取得。NameEquals 優先、なければ式から推論。
    internal static string MemberName(AnonymousObjectMemberDeclaratorSyntax initializer) {
        if(initializer.NameEquals is not null) {
            return initializer.NameEquals.Name.Identifier.ValueText;
        }
        if(InferName(initializer.Expression,out var name)) {
            return name;
        }
        return string.Empty;
    }
    internal static bool InferName(ExpressionSyntax expression,out string name) {
        expression=Strip(expression);
        if(expression is IdentifierNameSyntax identifier) {
            name=identifier.Identifier.ValueText;
            return true;
        }
        if(expression is MemberAccessExpressionSyntax member) {
            name=member.Name.Identifier.ValueText;
            return true;
        }
        name=string.Empty;
        return false;
    }
    /// <summary>
    /// reduced extension 形の <c>System.Linq.Enumerable</c> 呼び出しの分解結果。
    /// 静的形 <c>SystemEnumerable.X(...)</c> は intercept されない参照なので対象にしない。
    /// </summary>
    internal readonly struct SequenceCallInfo(InvocationExpressionSyntax invocation,string methodName,ExpressionSyntax source,ArgumentSyntax[] arguments) {
        public InvocationExpressionSyntax Invocation { get; } = invocation;
        public string MethodName { get; } = methodName;
        public ExpressionSyntax Source { get; } = source;
        public IReadOnlyList<ArgumentSyntax> Arguments { get; } = arguments;
    }
    internal static bool TryGetLinqSequenceCall(InvocationExpressionSyntax invocation,SemanticModel model,INamedTypeSymbol? enumerableType,out SequenceCallInfo call) {
        call=default;
        if(invocation.Expression is not MemberAccessExpressionSyntax memberAccess) {
            return false;
        }
        if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method||method.ReducedFrom is null) {
            return false;
        }
        if(enumerableType is null||!SymbolEqualityComparer.Default.Equals(method.ContainingType,enumerableType)) {
            return false;
        }
        call=new SequenceCallInfo(invocation,method.Name,Strip(memberAccess.Expression),invocation.ArgumentList.Arguments.ToArray());
        return true;
    }
}
