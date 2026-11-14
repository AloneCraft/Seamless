using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Seamless.FLinq.Generator;

/// <summary>
/// 名前を付けて書いたメンバー(プロパティ・メソッド)の本体を、呼び出し側の式へ取り込む(利用者の判断 2026-11-14)。
/// ビューはプロパティ、関数はメソッドとして IReader への拡張メンバーに書くので、取り込めば呼び出し側の LINQ と1つのループになる
/// (docs/refactoring-designs/20261112-0017-database-access-model.md の手順3)。
/// <list type="bullet">
/// <item><description>対象: 拡張メンバー(C# 14 の extension ブロック)・従来の拡張メソッド・同じ型のメンバーで、本体が式1つ(または return 1文)、
/// 中に LINQ の呼び出しかほかの取り込み対象があるもの。仮想・総称・ref/out・params・async は対象外。[LinqIntercept(false)] を付けたものも対象外。</description></item>
/// <item><description>受け手と引数は、定数・this・書き換えられない変数や引数・readonly のフィールドのときだけ、そのまま差し込む(評価の回数と順序が変わらない)。</description></item>
/// <item><description>1か所ずつ取り込んで型を付け直し、エラーが出る・呼び出し先が変わる(using の違いなど)ときは取り込まない。</description></item>
/// <item><description>取り込めなかったのがビュー・関数(DB の IReader への拡張メンバー)なら、理由を <see cref="Failure"/> に残す(呼び出し側でコンパイルエラーにする)。</description></item>
/// </list>
/// </summary>
internal static class MemberBodyInlining{
    internal sealed record Failure(string Member,string Site,string Reason);

    private const string BindingAnnotationKind="SeamlessInlineBinding";
    private const int MaximumInlines=64;
    private const string SystemEnumerableTypeName="System.Linq.Enumerable";
    private const string SeamlessEnumerableTypeName="Seamless.Linq.Enumerable";
    private const string SeamlessSetTypeName="Seamless.Linq.Set";
    private const string DatabaseContainerTypeName="Seamless.Database.DatabaseContainer";

    internal static bool TryInline(MethodDeclarationSyntax method,SemanticModel model,List<Failure> failures,out MethodDeclarationSyntax inlined,out SemanticModel inlinedModel){
        inlined=method;inlinedModel=model;
        var changed=false;
        var rejected=new HashSet<string>(StringComparer.Ordinal);
        for(var count=0;;count++){
            var candidate=FindCandidate(inlined,inlinedModel,rejected,out var site);
            if(candidate is null){break;}
            if(count>=MaximumInlines){
                // 取り込みが止まらない(自分自身を使う定義など)。残ったビュー・関数は呼び出し側でエラーにする。
                if(IsDatabaseQueryMember(candidate.Value.Member)){failures.Add(new(candidate.Value.Member.ToDisplayString(),site,"取り込みが続いて止まりません(定義が自分自身を使っていないか確かめてください)。"));}
                break;
            }
            var key=candidate.Value.Member.ToDisplayString()+"|"+site;
            if(!TryBuildReplacement(candidate.Value,inlinedModel,inlined,out var replacement,out var reason)
                ||!TryRebind(inlined,inlinedModel,candidate.Value.Node,replacement,out var nextMethod,out var nextModel,out reason)){
                rejected.Add(key);
                if(IsDatabaseQueryMember(candidate.Value.Member)){failures.Add(new(candidate.Value.Member.ToDisplayString(),site,reason));}
                continue;
            }
            inlined=nextMethod;inlinedModel=nextModel;changed=true;
        }
        return changed;
    }

    /// <summary>呼び出し側の対象メソッドの中に、取り込みの対象になるメンバーの呼び出しがあるか(傍受の対象を決めるとき使う)。</summary>
    // ビュー・関数は取り込めなくても対象にする(呼び出し側でコンパイルエラーにするため)。それ以外は引数の条件まで満たすものだけ。
    internal static bool ContainsInlinableMember(SyntaxNode body,SemanticModel model){
        if(body.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not {} method){return false;}
        foreach(var node in body.DescendantNodesAndSelf()){
            if(!TryGetCandidate(node,model,out var candidate)){continue;}
            if(IsDatabaseQueryMember(candidate.Member)||TryBuildReplacement(candidate,model,method,out _,out _)){return true;}
        }
        return false;
    }

    private readonly record struct Candidate(SyntaxNode Node,ISymbol Member,ExpressionSyntax Body,SemanticModel BodyModel,IParameterSymbol? ReceiverParameter,ExpressionSyntax? Receiver);

    private static Candidate? FindCandidate(MethodDeclarationSyntax method,SemanticModel model,HashSet<string> rejected,out string site){
        foreach(var node in method.DescendantNodes()){
            if(!TryGetCandidate(node,model,out var candidate)){continue;}
            site=Normalize(node.ToString());
            if(rejected.Contains(candidate.Member.ToDisplayString()+"|"+site)){continue;}
            return candidate;
        }
        site=string.Empty;
        return null;
    }

    private static bool TryGetCandidate(SyntaxNode node,SemanticModel model,out Candidate candidate){
        candidate=default;
        ISymbol? member=null;
        ExpressionSyntax? receiver=null;
        switch(node){
            case InvocationExpressionSyntax invocation:
                if(model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method){return false;}
                member=method;
                if(invocation.Expression is MemberAccessExpressionSyntax invokedAccess){receiver=invokedAccess.Expression;}
                break;
            case MemberAccessExpressionSyntax access when access.Parent is not InvocationExpressionSyntax{Expression:var called}||called!=access:
                if(model.GetSymbolInfo(access).Symbol is not IPropertySymbol accessed){return false;}
                member=accessed;receiver=access.Expression;
                break;
            case IdentifierNameSyntax identifier when identifier.Parent is not MemberAccessExpressionSyntax{Name:var name}||name!=identifier:
                if(identifier.Parent is InvocationExpressionSyntax{Expression:var invoked}&&invoked==identifier){return false;}
                if(model.GetSymbolInfo(identifier).Symbol is not IPropertySymbol implicitProperty){return false;}
                member=implicitProperty;
                break;
            default:
                return false;
        }
        if(IsWrittenOrNotAValue(node)){return false;}
        if(!TryGetBody(member,model.Compilation,out var body,out var bodyModel)){return false;}
        IParameterSymbol? receiverParameter=null;
        var containing=member.ContainingType;
        if(containing.IsExtension){
            receiverParameter=containing.ExtensionParameter;
            if(receiverParameter is null||string.IsNullOrEmpty(receiverParameter.Name)||receiver is null||member.IsStatic){return false;}
        }else if(member is IMethodSymbol{ReducedFrom:{} reducedFrom}){
            receiverParameter=reducedFrom.Parameters[0];
            if(receiver is null){return false;}
        }else{
            // 同じ型のメンバーだけ(受け手は暗黙の this・this・静的)。
            var callerType=model.GetEnclosingSymbol(node.SpanStart)?.ContainingType;
            if(!SymbolEqualityComparer.Default.Equals(containing,callerType)){return false;}
            if(receiver is not (null or ThisExpressionSyntax)&&!(member.IsStatic&&model.GetSymbolInfo(receiver).Symbol is INamedTypeSymbol)){return false;}
            receiver=null;
        }
        candidate=new(node,member,body,bodyModel,receiverParameter,receiver);
        return true;
    }

    // 代入の左辺・ref/out・nameof・式木の中は取り込まない。
    private static bool IsWrittenOrNotAValue(SyntaxNode node){
        SyntaxNode current=node;
        while(current.Parent is ParenthesizedExpressionSyntax parenthesized){current=parenthesized;}
        if(current.Parent is AssignmentExpressionSyntax assignment&&assignment.Left==current){return true;}
        if(current.Parent is ArgumentSyntax{RefOrOutKeyword.RawKind:not 0}){return true;}
        if(current.Parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax or RefExpressionSyntax){return true;}
        foreach(var ancestor in node.Ancestors()){
            if(ancestor is InvocationExpressionSyntax{Expression:IdentifierNameSyntax{Identifier.ValueText:"nameof"}}){return true;}
            if(ancestor is TypeOfExpressionSyntax){return true;}
        }
        return false;
    }

    private static bool TryGetBody(ISymbol member,Compilation compilation,out ExpressionSyntax body,out SemanticModel bodyModel){
        body=null!;bodyModel=null!;
        if(member.IsVirtual||member.IsAbstract||member.IsOverride||member.IsExtern||member.DeclaringSyntaxReferences.Length!=1){return false;}
        if(HasInterceptOff(member)){return false;}
        var declaration=member.DeclaringSyntaxReferences[0].GetSyntax();
        ExpressionSyntax? expression=null;
        switch(member){
            case IMethodSymbol{MethodKind:MethodKind.Ordinary,IsGenericMethod:false,IsAsync:false,ReturnsVoid:false,ReturnsByRef:false,ReturnsByRefReadonly:false} method
                when method.Parameters.All(static parameter=>parameter.RefKind is RefKind.None or RefKind.In&&!parameter.IsParams)
                    &&declaration is MethodDeclarationSyntax syntax:
                expression=syntax.ExpressionBody?.Expression??SingleReturn(syntax.Body);
                break;
            case IPropertySymbol{IsIndexer:false,ReturnsByRef:false,ReturnsByRefReadonly:false,GetMethod:not null} property
                when declaration is PropertyDeclarationSyntax syntax:
                expression=syntax.ExpressionBody?.Expression;
                if(expression is null&&syntax.AccessorList?.Accessors.SingleOrDefault(static accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) is {} getter){
                    expression=getter.ExpressionBody?.Expression??SingleReturn(getter.Body);
                }
                break;
        }
        if(expression is null){return false;}
        if(member.ContainingType.IsExtension&&member.ContainingType.TypeParameters.Length>0){return false;}
        bodyModel=compilation.GetSemanticModel(expression.SyntaxTree);
        // 中に LINQ の呼び出しかほかのメンバーの呼び出しがあるものだけ(取り込んでも速くならない本体は、そのまま呼ぶ)。
        var model=bodyModel;
        if(!expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(invocation=>IsLinqInvocation(invocation,model))
            &&!expression.DescendantNodesAndSelf().Any(node=>node is MemberAccessExpressionSyntax access&&model.GetSymbolInfo(access).Symbol is IPropertySymbol{ContainingType.IsExtension:true})){
            return false;
        }
        body=expression;
        return true;
    }

    private static ExpressionSyntax? SingleReturn(BlockSyntax? block)=>
        block is {Statements.Count:1}&&block.Statements[0] is ReturnStatementSyntax{Expression:{} returned}?returned:null;

    private static bool IsLinqInvocation(InvocationExpressionSyntax invocation,SemanticModel model)=>
        model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method
        &&(method.ReducedFrom??method).ContainingType.ToDisplayString() is SystemEnumerableTypeName or SeamlessEnumerableTypeName or SeamlessSetTypeName;

    private static bool HasInterceptOff(ISymbol member){
        foreach(var attribute in member.GetAttributes()){
            if(attribute.AttributeClass?.Name!="LinqInterceptAttribute"){continue;}
            if(attribute.ConstructorArguments.Length>0&&attribute.ConstructorArguments[0].Value is false){return true;}
        }
        return false;
    }

    /// <summary>DB の生成器が作る入口(IReader など)への拡張メンバー、つまりビュー・関数か。取り込めなければコンパイルエラーにする。</summary>
    private static bool IsDatabaseQueryMember(ISymbol member)=>
        member.ContainingType is {IsExtension:true,ExtensionParameter.Type:INamedTypeSymbol{TypeKind:TypeKind.Interface} reader}
        &&reader.Name.EndsWith("Reader",StringComparison.Ordinal)
        &&InheritsDatabaseContainer(reader.ContainingType);

    private static bool InheritsDatabaseContainer(INamedTypeSymbol? type){
        for(var current=type;current is not null;current=current.BaseType){
            if(current.ToDisplayString()==DatabaseContainerTypeName){return true;}
        }
        return type?.ContainingType is {} outer&&InheritsDatabaseContainer(outer);
    }

    private static bool TryBuildReplacement(Candidate candidate,SemanticModel model,MethodDeclarationSyntax callerMethod,out ExpressionSyntax replacement,out string reason){
        replacement=null!;reason=string.Empty;
        var arguments=new Dictionary<ISymbol,ExpressionSyntax>(SymbolEqualityComparer.Default);
        if(candidate.ReceiverParameter is not null){arguments[candidate.ReceiverParameter]=candidate.Receiver!;}
        if(candidate.Node is InvocationExpressionSyntax invocation){
            if(model.GetOperation(invocation) is not IInvocationOperation operation){reason="呼び出しの引数を読めません。";return false;}
            var method=(IMethodSymbol)candidate.Member;
            var parameters=(method.ReducedFrom??method).Parameters;
            foreach(var argument in operation.Arguments){
                if(argument.Parameter is null){reason="引数の対応を読めません。";return false;}
                if(candidate.ReceiverParameter is not null&&SymbolEqualityComparer.Default.Equals(argument.Parameter.OriginalDefinition,candidate.ReceiverParameter.OriginalDefinition)){
                    continue;
                }
                var parameter=parameters.FirstOrDefault(item=>item.Ordinal==argument.Parameter.Ordinal&&item.Name==argument.Parameter.Name);
                if(parameter is null){reason="引数の対応を読めません。";return false;}
                if(argument.ArgumentKind==ArgumentKind.DefaultValue){
                    if(!parameter.HasExplicitDefaultValue||!TryLiteral(parameter.ExplicitDefaultValue,parameter.Type,out var literal)){reason=$"省略した引数 '{parameter.Name}' の既定値を書けません。";return false;}
                    arguments[parameter]=literal;
                }else if(argument.Value.Syntax is ExpressionSyntax value){
                    arguments[parameter]=value;
                }else{
                    reason=$"引数 '{parameter.Name}' を読めません。";return false;
                }
            }
        }
        foreach(var pair in arguments){
            if(!IsStable(pair.Value,model,callerMethod)){
                reason=$"'{Normalize(pair.Value.ToString())}' は取り込むと評価の回数や順序が変わります。変数に入れてから渡してください。";
                return false;
            }
        }
        var callerNames=new HashSet<string>(callerMethod.DescendantTokens().Where(static token=>token.IsKind(SyntaxKind.IdentifierToken)).Select(static token=>token.ValueText),StringComparer.Ordinal);
        var rewriter=new BodyRewriter(candidate.BodyModel,arguments,callerNames);
        var rewritten=(ExpressionSyntax)rewriter.Visit(candidate.Body)!;
        if(rewriter.Failure is {} failure){reason=failure;return false;}
        ExpressionSyntax result=SyntaxFactory.ParenthesizedExpression(rewritten.WithoutTrivia());
        // 本体の型と宣言の戻り値の型が違う(暗黙の変換がある)ときは、宣言の型へ変換して意味を保つ。
        var declaredType=candidate.Member switch{IMethodSymbol method=>method.ReturnType,IPropertySymbol property=>property.Type,_=>null};
        var bodyType=candidate.BodyModel.GetTypeInfo(candidate.Body).Type;
        if(declaredType is not null&&bodyType is not null&&!SymbolEqualityComparer.Default.Equals(declaredType,bodyType)){
            result=SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(
                SyntaxFactory.ParseTypeName(declaredType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),result));
        }
        replacement=result.WithTriviaFrom(candidate.Node);
        return true;
    }

    private static bool TryLiteral(object? value,ITypeSymbol type,out ExpressionSyntax literal){
        literal=value switch{
            null=>SyntaxFactory.ParseExpression("default("+type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+")"),
            bool flag=>SyntaxFactory.LiteralExpression(flag?SyntaxKind.TrueLiteralExpression:SyntaxKind.FalseLiteralExpression),
            string text=>SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(text)),
            char character=>SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression,SyntaxFactory.Literal(character)),
            int number=>SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(number)),
            long number=>SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(number)),
            decimal number=>SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(number)),
            double number=>SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,SyntaxFactory.Literal(number)),
            _=>null!,
        };
        if(literal is null){return false;}
        if(value is not null&&type.SpecialType is not (SpecialType.System_Boolean or SpecialType.System_String or SpecialType.System_Char)){
            literal=SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),literal));
        }
        return true;
    }

    // 何度読んでも同じ値で、読んでも何も起きない式か(定数・this・書き換えられない変数や引数・readonly のフィールド)。
    private static bool IsStable(ExpressionSyntax expression,SemanticModel model,MethodDeclarationSyntax callerMethod){
        while(expression is ParenthesizedExpressionSyntax parenthesized){expression=parenthesized.Expression;}
        if(model.GetConstantValue(expression).HasValue){return true;}
        switch(expression){
            case ThisExpressionSyntax:
            case LiteralExpressionSyntax:
            case DefaultExpressionSyntax:
                return true;
            case IdentifierNameSyntax identifier:
                return model.GetSymbolInfo(identifier).Symbol switch{
                    ILocalSymbol{IsRef:false} local=>!IsWritten(local,model,callerMethod,allowDeclaration:true),
                    IParameterSymbol{RefKind:RefKind.None or RefKind.In} parameter=>!IsWritten(parameter,model,callerMethod,allowDeclaration:false),
                    IFieldSymbol{IsReadOnly:true} or IFieldSymbol{IsConst:true}=>true,
                    _=>false,
                };
            case MemberAccessExpressionSyntax access:
                return model.GetSymbolInfo(access).Symbol is IFieldSymbol{IsReadOnly:true} or IFieldSymbol{IsConst:true}
                    &&(access.Expression is ThisExpressionSyntax||model.GetSymbolInfo(access.Expression).Symbol is INamedTypeSymbol);
            default:
                return false;
        }
    }

    private static bool IsWritten(ISymbol symbol,SemanticModel model,MethodDeclarationSyntax method,bool allowDeclaration){
        foreach(var node in method.DescendantNodes()){
            ExpressionSyntax? target=node switch{
                AssignmentExpressionSyntax assignment=>assignment.Left,
                PrefixUnaryExpressionSyntax{RawKind:(int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression} prefix=>prefix.Operand,
                PostfixUnaryExpressionSyntax postfix=>postfix.Operand,
                ArgumentSyntax{RefOrOutKeyword.RawKind:not 0} argument=>argument.Expression,
                RefExpressionSyntax reference=>reference.Expression,
                _=>null,
            };
            if(target is null){continue;}
            foreach(var name in target.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()){
                if(name.Identifier.ValueText==symbol.Name&&SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol,symbol)){return true;}
            }
        }
        _=allowDeclaration;
        return false;
    }

    /// <summary>
    /// 本体の写しを作る。引数の名前を渡された式へ置き換え、型や静的メンバーは完全な名前で書き、呼び出し側の名前とぶつかる局所の名前を付け替える。
    /// 呼び出しには元の呼び出し先を目印として付け、取り込んだ後に同じ呼び出し先へ結び付いたかを確かめる。
    /// </summary>
    private sealed class BodyRewriter(SemanticModel model,Dictionary<ISymbol,ExpressionSyntax> arguments,HashSet<string> callerNames):CSharpSyntaxRewriter{
        private readonly Dictionary<ISymbol,string> renamed=new(SymbolEqualityComparer.Default);
        private int nextName;
        public string? Failure{get;private set;}

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node){
            var visited=base.VisitInvocationExpression(node)!;
            if(model.GetSymbolInfo(node).Symbol is IMethodSymbol method){
                var id=(method.ReducedFrom??method).OriginalDefinition.GetDocumentationCommentId();
                if(id is not null){visited=visited.WithAdditionalAnnotations(new SyntaxAnnotation(BindingAnnotationKind,id));}
            }
            return visited;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node){
            var symbol=model.GetSymbolInfo(node).Symbol;
            if(symbol is null){return base.VisitIdentifierName(node);}
            if(symbol is IParameterSymbol parameter&&arguments.TryGetValue(parameter.OriginalDefinition,out var argument)
                ||symbol is IParameterSymbol other&&arguments.TryGetValue(other,out argument)){
                return Parenthesize(argument.WithoutTrivia()).WithTriviaFrom(node);
            }
            if(this.renamed.TryGetValue(symbol,out var newName)||this.TryRename(symbol,out newName)){
                return node.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(node.Identifier));
            }
            var isMemberName=node.Parent is MemberAccessExpressionSyntax access&&access.Name==node
                ||node.Parent is MemberBindingExpressionSyntax binding&&binding.Name==node
                ||node.Parent is NameColonSyntax or NameEqualsSyntax
                ||node.Parent is QualifiedNameSyntax qualified&&qualified.Right==node;
            if(isMemberName){return base.VisitIdentifierName(node);}
            if(symbol is INamedTypeSymbol type){
                return SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithTriviaFrom(node);
            }
            if(symbol is INamespaceSymbol){return base.VisitIdentifierName(node);}
            if(symbol is IFieldSymbol or IPropertySymbol or IMethodSymbol or IEventSymbol){
                if(!symbol.IsStatic){
                    // 拡張の本体・別の型の本体から、受け手を書かずに自分の型のインスタンスのメンバーを読むことはできない。
                    if(symbol.ContainingType is {IsExtension:true}){this.Failure="受け手を書かずにメンバーを読む本体は取り込めません。";}
                    return base.VisitIdentifierName(node);
                }
                return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseTypeName(symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    SyntaxFactory.IdentifierName(node.Identifier.ValueText)).WithTriviaFrom(node);
            }
            return base.VisitIdentifierName(node);
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node){
            if(node.Parent is not (MemberAccessExpressionSyntax or QualifiedNameSyntax or MemberBindingExpressionSyntax)
                &&model.GetSymbolInfo(node).Symbol is INamedTypeSymbol type){
                return SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithTriviaFrom(node);
            }
            return base.VisitGenericName(node);
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node){
            if(model.GetSymbolInfo(node).Symbol is INamedTypeSymbol type){
                return SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithTriviaFrom(node);
            }
            return base.VisitQualifiedName(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node){
            // 名前空間で修飾した型(Views.ActiveRow など)は、型として完全な名前にする。
            if(model.GetSymbolInfo(node).Symbol is INamedTypeSymbol type){
                return SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).WithTriviaFrom(node);
            }
            return base.VisitMemberAccessExpression(node);
        }

        public override SyntaxNode? VisitParameter(ParameterSyntax node){
            var visited=(ParameterSyntax)base.VisitParameter(node)!;
            if(model.GetDeclaredSymbol(node) is {} symbol&&(this.renamed.TryGetValue(symbol,out var name)||this.TryRename(symbol,out name))){
                return visited.WithIdentifier(SyntaxFactory.Identifier(name).WithTriviaFrom(node.Identifier));
            }
            return visited;
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node){
            var visited=(VariableDeclaratorSyntax)base.VisitVariableDeclarator(node)!;
            if(model.GetDeclaredSymbol(node) is {} symbol&&(this.renamed.TryGetValue(symbol,out var name)||this.TryRename(symbol,out name))){
                return visited.WithIdentifier(SyntaxFactory.Identifier(name).WithTriviaFrom(node.Identifier));
            }
            return visited;
        }

        public override SyntaxNode? VisitSingleVariableDesignation(SingleVariableDesignationSyntax node){
            if(model.GetDeclaredSymbol(node) is {} symbol&&(this.renamed.TryGetValue(symbol,out var name)||this.TryRename(symbol,out name))){
                return node.WithIdentifier(SyntaxFactory.Identifier(name).WithTriviaFrom(node.Identifier));
            }
            return base.VisitSingleVariableDesignation(node);
        }

        // 本体の中で宣言した局所(ラムダの引数・変数)のうち、呼び出し側の名前とぶつかるものに新しい名前を付ける。
        private bool TryRename(ISymbol symbol,out string name){
            name=string.Empty;
            if(symbol is not (ILocalSymbol or IParameterSymbol{ContainingSymbol:IMethodSymbol{MethodKind:MethodKind.AnonymousFunction or MethodKind.LambdaMethod}} or IRangeVariableSymbol)){return false;}
            if(!callerNames.Contains(symbol.Name)){return false;}
            do{name=symbol.Name+"__inline"+this.nextName++;}while(callerNames.Contains(name));
            this.renamed[symbol]=name;
            return true;
        }

        private static ExpressionSyntax Parenthesize(ExpressionSyntax expression)=>
            expression is IdentifierNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax or ParenthesizedExpressionSyntax or MemberAccessExpressionSyntax
                ? expression
                : SyntaxFactory.ParenthesizedExpression(expression);
    }

    private static bool TryRebind(MethodDeclarationSyntax method,SemanticModel model,SyntaxNode site,ExpressionSyntax replacement,
        out MethodDeclarationSyntax rebound,out SemanticModel reboundModel,out string reason){
        rebound=method;reboundModel=model;reason=string.Empty;
        var annotation=new SyntaxAnnotation("SeamlessMemberInline");
        var replaced=method.ReplaceNode(site,replacement).WithAdditionalAnnotations(annotation);
        var root=method.SyntaxTree.GetRoot();
        var newRoot=root.ReplaceNode(method,replaced);
        var newTree=method.SyntaxTree.WithRootAndOptions(newRoot,method.SyntaxTree.Options);
        var compilation=model.Compilation.ReplaceSyntaxTree(method.SyntaxTree,newTree);
        var newModel=compilation.GetSemanticModel(newTree);
        if(newTree.GetRoot().GetAnnotatedNodes(annotation).OfType<MethodDeclarationSyntax>().SingleOrDefault() is not {} annotated){
            reason="取り込んだ結果を読み直せません。";return false;
        }
        var errors=newModel.GetDiagnostics(annotated.Span).Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        if(errors.Length>0){
            // 本体は呼び出し側に置くので、本体が使う private なメンバーは呼び出し側から見えない。
            if(errors.FirstOrDefault(static error=>error.Id=="CS0122") is {} inaccessible){
                reason="本体が使うメンバーが呼び出し側から見えません(private など)。internal 以上にしてください: "+inaccessible.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }
            reason="取り込むとコンパイルできません: "+errors[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture);
            return false;
        }
        // 取り込む前と同じ呼び出し先へ結び付いたか(using の違いで Set.Where が Enumerable.Where になる、など)。
        foreach(var node in annotated.GetAnnotatedNodes(BindingAnnotationKind)){
            var expected=node.GetAnnotations(BindingAnnotationKind).First().Data;
            var actual=newModel.GetSymbolInfo(node).Symbol is IMethodSymbol bound?(bound.ReducedFrom??bound).OriginalDefinition.GetDocumentationCommentId():null;
            if(actual!=expected){
                reason=$"取り込むと '{Normalize(node.ToString())}' の呼び出し先が変わります(呼び出し側に足りない using がないか確かめてください)。";
                return false;
            }
        }
        rebound=annotated;reboundModel=newModel;
        return true;
    }

    private static string Normalize(string text)=>string.Join(" ",text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
}
