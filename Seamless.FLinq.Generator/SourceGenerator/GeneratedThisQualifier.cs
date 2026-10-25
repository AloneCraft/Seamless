using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

/// <summary>
/// 生成した構造体の中で、自分のメンバー(フィールド・プロパティ・メソッド)を使うときに <c>this.</c> を付ける。
/// 生成コードは、断片ごとに <c>this.</c> を書く所と書かない所が混在している(約400か所)ため、断片を直さず、
/// 生成が終わった後で構文を見て揃える(名前の付け替え <see cref="GeneratedNameRenamer"/> と同じ後処理の方式)。
/// <para>
/// 付けない場合(元のコードの意味を変えないため):
/// 同じ名前の引数・ローカル変数・ラムダの引数がそのメンバーの中で宣言されているとき(コンストラクタの <c>this.x = x;</c> の右辺など)、
/// すでに <c>a.b</c> の <c>b</c> や名前つき引数・初期化子の左辺・<c>nameof</c> の中のとき、静的なメンバーの中、ラムダの中
/// (構造体のラムダの中でメンバーを使うと CS1673 になる)、型の位置にある名前。
/// </para>
/// <para>
/// ラムダが構造体のフィールド(取り込んだ変数など)を使う形は、そのままでは CS1673 になる。フィールドが構造体の中で書き換えられないとき、
/// メンバーの先頭で <c>var 名前 = this.名前;</c> と同じ名前のローカルに写して、ラムダにはそのローカルを取り込ませる。
/// </para>
/// </summary>
internal static class GeneratedThisQualifier {
    internal static string Qualify(string text) {
        if(text.IndexOf("struct ",StringComparison.Ordinal)<0&&text.IndexOf("class ",StringComparison.Ordinal)<0) {
            return text;
        }
        var tree = CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(LanguageVersion.Latest));
        var rewritten = new Rewriter().Visit(tree.GetRoot());
        return rewritten.ToFullString();
    }

    private sealed class Rewriter:CSharpSyntaxRewriter {
        private HashSet<string> members = [];
        private HashSet<string> active = [];
        private HashSet<string> fields = [];
        private HashSet<string> mutated = [];

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) => this.VisitType(node,() => base.VisitStructDeclaration(node));

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => this.VisitType(node,() => base.VisitClassDeclaration(node));

        private SyntaxNode? VisitType(TypeDeclarationSyntax node,Func<SyntaxNode?> visit) {
            var savedMembers = this.members;
            var savedActive = this.active;
            var savedFields = this.fields;
            var savedMutated = this.mutated;
            this.members=InstanceMemberNames(node);
            this.fields=node is StructDeclarationSyntax ? InstanceFieldNames(node) : [];
            this.mutated=node is StructDeclarationSyntax ? MutatedFieldNames(node,this.fields) : [];
            this.active=[];
            var result = visit();
            this.members=savedMembers;
            this.active=savedActive;
            this.fields=savedFields;
            this.mutated=savedMutated;
            return result;
        }

        private static HashSet<string> InstanceMemberNames(TypeDeclarationSyntax type) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var member in type.Members) {
                switch(member) {
                    case FieldDeclarationSyntax field when !IsStaticOrConst(field.Modifiers):
                        foreach(var variable in field.Declaration.Variables) {
                            names.Add(variable.Identifier.ValueText);
                        }
                        break;
                    case PropertyDeclarationSyntax property when !IsStaticOrConst(property.Modifiers)&&property.ExplicitInterfaceSpecifier is null:
                        names.Add(property.Identifier.ValueText);
                        break;
                    case MethodDeclarationSyntax method when !IsStaticOrConst(method.Modifiers)&&method.ExplicitInterfaceSpecifier is null:
                        names.Add(method.Identifier.ValueText);
                        break;
                }
            }
            return names;
        }

        private static HashSet<string> InstanceFieldNames(TypeDeclarationSyntax type) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var member in type.Members) {
                if(member is FieldDeclarationSyntax field&&!IsStaticOrConst(field.Modifiers)) {
                    foreach(var variable in field.Declaration.Variables) {
                        names.Add(variable.Identifier.ValueText);
                    }
                }
            }
            return names;
        }

        // コンストラクタの外で、代入・++/--・ref/out の引数になるフィールド(書き換えられるフィールド)。これらはローカルに写すと値がずれるので写さない。
        private static HashSet<string> MutatedFieldNames(TypeDeclarationSyntax type,HashSet<string> fields) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var member in type.Members) {
                if(member is ConstructorDeclarationSyntax) {
                    continue;
                }
                foreach(var node in member.DescendantNodes()) {
                    ExpressionSyntax? target = node switch {
                        AssignmentExpressionSyntax assignment => assignment.Left,
                        PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression)||prefix.IsKind(SyntaxKind.PreDecrementExpression) => prefix.Operand,
                        PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.PostIncrementExpression)||postfix.IsKind(SyntaxKind.PostDecrementExpression) => postfix.Operand,
                        ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)||argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) => argument.Expression,
                        _ => null,
                    };
                    var name = target switch {
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
                        _ => null,
                    };
                    if(name is not null&&fields.Contains(name)) {
                        names.Add(name);
                    }
                }
            }
            return names;
        }

        private static bool IsStaticOrConst(SyntaxTokenList modifiers) =>
            modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword)||modifier.IsKind(SyntaxKind.ConstKeyword));

        // 本体を持つメンバーごとに、付ける名前(メンバーの名前 − そのメンバーの中で宣言された名前)を決める。
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) {
            if(IsStaticOrConst(node.Modifiers)||node.Body is null) {
                return this.VisitMember(node,IsStaticOrConst(node.Modifiers),() => base.VisitMethodDeclaration(node));
            }
            var shadows = this.FieldsUsedByLambdas(node);
            if(shadows.Count==0) {
                return this.VisitMember(node,false,() => base.VisitMethodDeclaration(node));
            }
            // 写した名前はローカルになるので、このメンバーの中では this. を付けない。
            var visited = (MethodDeclarationSyntax)this.VisitMember(node,false,() => base.VisitMethodDeclaration(node),shadows)!;
            var copies = shadows.Select(static name => (StatementSyntax)SyntaxFactory.ParseStatement($"var {name} = this.{name};")).ToList();
            return visited.WithBody(visited.Body!.WithStatements(visited.Body.Statements.InsertRange(0,copies)));
        }

        // ラムダの中で使われる、書き換えられないフィールド(メンバーの中で同じ名前が宣言されていないもの)。
        private List<string> FieldsUsedByLambdas(MethodDeclarationSyntax method) {
            var declared = DeclaredNames(method);
            var result = new List<string>();
            foreach(var lambda in method.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>()) {
                foreach(var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>()) {
                    var name = identifier.Identifier.ValueText;
                    if(this.fields.Contains(name)&&!this.mutated.Contains(name)&&!declared.Contains(name)&&!result.Contains(name)&&IsSimpleNameUse(identifier)) {
                        result.Add(name);
                    }
                }
            }
            return result;
        }

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
            this.VisitMember(node,IsStaticOrConst(node.Modifiers),() => base.VisitConstructorDeclaration(node));

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) =>
            this.VisitMember(node,IsStaticOrConst(node.Modifiers),() => base.VisitPropertyDeclaration(node));

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => node;

        private SyntaxNode? VisitMember(SyntaxNode node,bool isStatic,Func<SyntaxNode?> visit,IReadOnlyCollection<string>? shadowed = null) {
            var saved = this.active;
            if(isStatic||this.members.Count==0) {
                this.active=[];
            } else {
                var declared = DeclaredNames(node);
                this.active=new HashSet<string>(this.members.Where(name => !declared.Contains(name)&&(shadowed is null||!shadowed.Contains(name))),StringComparer.Ordinal);
            }
            var result = visit();
            this.active=saved;
            return result;
        }

        private static HashSet<string> DeclaredNames(SyntaxNode member) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach(var token in member.DescendantTokens()) {
                if(!token.IsKind(SyntaxKind.IdentifierToken)) {
                    continue;
                }
                var isDeclaration = token.Parent switch {
                    VariableDeclaratorSyntax declarator => declarator.Identifier==token,
                    ParameterSyntax parameter => parameter.Identifier==token,
                    ForEachStatementSyntax forEach => forEach.Identifier==token,
                    CatchDeclarationSyntax catchDeclaration => catchDeclaration.Identifier==token,
                    SingleVariableDesignationSyntax designation => designation.Identifier==token,
                    LocalFunctionStatementSyntax localFunction => localFunction.Identifier==token,
                    LabeledStatementSyntax labeled => labeled.Identifier==token,
                    _ => false,
                };
                if(isDeclaration) {
                    names.Add(token.ValueText);
                }
            }
            return names;
        }

        // ラムダの中は触らない(構造体のラムダがメンバーを使うと CS1673)。静的なローカル関数の中も同じ。
        public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;

        public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;

        public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => node;

        public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) =>
            IsStaticOrConst(node.Modifiers) ? node : base.VisitLocalFunctionStatement(node);

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node) {
            if(this.active.Count==0||!this.active.Contains(node.Identifier.ValueText)||!IsSimpleNameUse(node)) {
                return base.VisitIdentifierName(node);
            }
            var qualified = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ThisExpression(),
                node.WithoutTrivia());
            return qualified.WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia());
        }

        // 式の中の単純名の使用か(型の位置・メンバー名の位置・名前つき引数などは除く)。
        private static bool IsSimpleNameUse(IdentifierNameSyntax node) {
            var parent = node.Parent;
            switch(parent) {
                case MemberAccessExpressionSyntax access when access.Name==node:
                case MemberBindingExpressionSyntax:
                case QualifiedNameSyntax:
                case AliasQualifiedNameSyntax:
                case NameColonSyntax:
                case NameEqualsSyntax:
                case TypeArgumentListSyntax:
                case BaseTypeSyntax:
                case TypeConstraintSyntax:
                case ArrayTypeSyntax:
                case NullableTypeSyntax:
                case ObjectCreationExpressionSyntax creation when creation.Type==node:
                case CastExpressionSyntax cast when cast.Type==node:
                case DeclarationExpressionSyntax:
                case VariableDeclarationSyntax:
                case ParameterSyntax:
                case TypeOfExpressionSyntax:
                case DefaultExpressionSyntax:
                case SizeOfExpressionSyntax:
                case UsingDirectiveSyntax:
                case RefTypeSyntax:
                case TupleElementSyntax:
                    return false;
                case AssignmentExpressionSyntax assignment when assignment.Left==node&&assignment.Parent is InitializerExpressionSyntax initializer&&initializer.IsKind(SyntaxKind.ObjectInitializerExpression):
                    return false;
                case ArgumentSyntax argument when argument.Parent?.Parent is InvocationExpressionSyntax invocation&&invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }:
                    return false;
                case BinaryExpressionSyntax binary when (binary.IsKind(SyntaxKind.IsExpression)||binary.IsKind(SyntaxKind.AsExpression))&&binary.Right==node:
                    return false;
            }
            return true;
        }
    }
}
