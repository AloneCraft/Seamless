using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

// 匿名型を「名指し可能なジェネリック record struct」へ透過置換するためのレジストリ。
//
// 背景: 生成コード上に匿名型の型名（<>f__AnonymousType0 等）は書けないため、従来は匿名型を返すラムダを
// delegate フィールドへ保持し TResult 推論で逃がしていた（AnonymousDelegateFuncFragment）。
// しかしメソッドチェーン中間の匿名型は後続ステージ（Join の key selector の TOuter 等）で「名指し可能な型」が
// 必要になり、そこで構築が破綻していた（TPC-H の Category B 失敗）。
//
// 本レジストリは、匿名型のメンバー名列をキーとしてジェネリック record struct を 1 つ割り当て、
// 匿名型を常にその閉じたジェネリック型名へ置換する。同じ形（メンバー名列が同一）は同じ record struct を再利用し、
// メンバー型の違いはジェネリック型引数で吸収する。これにより匿名型は内部的に常に名指し可能になる。
public sealed partial class FLinqIncrementalGenerator {

    internal sealed class AnonymousStructRecordRegistry(string fileToken) {
        private readonly string _holderName = $"__TsavoriteAnonRecords_{fileToken}";
        // キー（メンバー名を "|" で連結）→ shape 情報
        private readonly Dictionary<string,Shape> _shapes = new(StringComparer.Ordinal);
        // 既に割り当てた TypeId 集合（別キーへの衝突回避用）
        private readonly HashSet<string> _usedIds = new(StringComparer.Ordinal);

        public bool IsEmpty => this._shapes.Count==0;

        private sealed class Shape {
            public required string TypeId { get; init; }
            public required string[] MemberNames { get; init; }
            public int Arity => this.MemberNames.Length;
        }

        /// <summary>型に匿名型が含まれるかを再帰的に判定する。</summary>
        public static bool ContainsAnonymous(ITypeSymbol type) => GenerationPipeline.ContainsAnonymousType(type);

        /// <summary>
        /// 型を「匿名型を閉じたジェネリック record struct 型名へ置換した」完全修飾型名へ変換する。
        /// 例: 匿名型 { int Price, string Name } → global::__TsavoriteAnonRecords_xxx.__Anon2_Price_Name&lt;global::System.Int32, global::System.String&gt;
        /// 入れ子の匿名型も再帰的に置換する。
        /// </summary>
        public string ResolveClosedName(ITypeSymbol type) {
            if(type.IsAnonymousType&&type is INamedTypeSymbol anon) {
                var members = AnonymousMembers(anon);
                var shape = this.RegisterShape(members.Select(static m => m.Name).ToArray());
                if(shape.Arity==0) {
                    return $"global::{this._holderName}.{shape.TypeId}";
                }
                var args = string.Join(", ",members.Select(m => this.ResolveClosedName(m.Type)));
                return $"global::{this._holderName}.{shape.TypeId}<{args}>";
            }
            if(type is IArrayTypeSymbol arrayType) {
                // 多次元は元の表現に任せる（匿名要素配列は稀）。1 次元のみ特別扱い。
                if(arrayType.Rank==1) {
                    return $"{this.ResolveClosedName(arrayType.ElementType)}[]";
                }
                return type.ToDisplayString(完全修飾形式);
            }
            if(type is INamedTypeSymbol named&&named.TypeArguments.Any(GenerationPipeline.ContainsAnonymousType)) {
                var open = OpenGenericTypeName(named);
                var args = string.Join(", ",named.TypeArguments.Select(this.ResolveClosedName));
                return $"{open}<{args}>";
            }
            return type.ToDisplayString(完全修飾形式);
        }

        /// <summary>
        /// 匿名オブジェクト生成式 <c>new { a = e0, b = e1 }</c> を
        /// <c>global::__TsavoriteAnonRecords_xxx.__New2_a_b(e0', e1')</c>（ジェネリックファクトリ呼び出し）へ変換した文字列を返す。
        /// <br/>ファクトリは型推論で閉じるため本体に型名を出さない。これにより、後続の Join/SelectMany 等の
        /// パラメータ名テキスト置換（例 <c>region</c> → <c>__it9</c>）が型引数を破壊するのを防ぐ。
        /// <br/>入れ子の <c>new { ... }</c> は再帰的に変換し、それ以外の値式は <paramref name="renderLeaf"/> でレンダリングする。
        /// </summary>
        public string RenderAnonymousCreation(
            AnonymousObjectCreationExpressionSyntax node,
            SemanticModel semanticModel,
            Func<ExpressionSyntax,string> renderLeaf) {
            if(semanticModel.GetTypeInfo(node).Type is not INamedTypeSymbol sym) {
                return "default!";
            }
            var shape = this.RegisterShape(AnonymousMembers(sym).Select(static m => m.Name).ToArray());
            var args = node.Initializers.Select(init => {
                var value = StripParentheses(init.Expression);
                if(value is AnonymousObjectCreationExpressionSyntax innerAnon) {
                    return this.RenderAnonymousCreation(innerAnon,semanticModel,renderLeaf);
                }
                return renderLeaf(init.Expression);
            });
            return $"global::{this._holderName}.{FactoryName(shape)}({string.Join(", ",args)})";
        }

        /// <summary>
        /// このファイルで割り当てた全 record struct 定義を 1 つの internal static ホルダークラスとして出力する。
        /// 何も割り当てられていなければ空文字列。
        /// </summary>
        public string RenderHolder() {
            if(this.IsEmpty) {
                return string.Empty;
            }
            var builder = new StringBuilder();
            builder.AppendLine($"internal static class {this._holderName} {{");
            foreach(var shape in this._shapes.Values.OrderBy(static s => s.TypeId,StringComparer.Ordinal)) {
                if(shape.Arity==0) {
                    builder.AppendLine($"    internal readonly record struct {shape.TypeId}();");
                    builder.AppendLine($"    internal static {shape.TypeId} {FactoryName(shape)}() => new {shape.TypeId}();");
                    continue;
                }
                var typeParams = string.Join(", ",System.Linq.Enumerable.Range(0,shape.Arity).Select(static i => $"T{i}"));
                var ctorParams = string.Join(", ",shape.MemberNames.Select((name,i) => $"T{i} {EscapeIdentifier(name)}"));
                var ctorArgs = string.Join(", ",shape.MemberNames.Select(EscapeIdentifier));
                builder.AppendLine($"    internal readonly record struct {shape.TypeId}<{typeParams}>({ctorParams});");
                // ジェネリックファクトリ: 呼び出し側で型推論させ、生成本体に型名を出さないためのもの。
                builder.AppendLine($"    internal static {shape.TypeId}<{typeParams}> {FactoryName(shape)}<{typeParams}>({ctorParams}) => new {shape.TypeId}<{typeParams}>({ctorArgs});");
            }
            builder.AppendLine("}");
            return builder.ToString();
        }

        /// <summary>
        /// struct record 式から元の匿名型を再構成する C# 式文字列を返す。
        /// 例: structRecordExpr が "rec"、originalAnonType が {p:int} → "new{p=rec.p}"
        /// 入れ子の匿名型も再帰的に再構成する。
        /// </summary>
        public string ReconstructAnonymousCreation(ITypeSymbol originalAnonType,string structRecordExpr) {
            if(!originalAnonType.IsAnonymousType||originalAnonType is not INamedTypeSymbol anon) {
                return structRecordExpr;
            }
            var members = AnonymousMembers(anon);
            var parts = members.Select(m => {
                var memberAccess = $"{structRecordExpr}.{EscapeIdentifier(m.Name)}";
                return $"{EscapeIdentifier(m.Name)}={this.ReconstructAnonymousCreation(m.Type,memberAccess)}";
            });
            return $"new{{{string.Join(",",parts)}}}";
        }

        /// <summary>
        /// <c>new {{ a = e0, b = e1 }}</c> を struct record ファクトリ呼び出しではなく
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

        private Shape RegisterShape(string[] memberNames) {
            var key = string.Join("|",memberNames);
            if(this._shapes.TryGetValue(key,out var existing)) {
                return existing;
            }
            var baseId = $"__Anon{memberNames.Length}_{string.Join("_",memberNames.Select(SanitizeIdentifier))}";
            var id = baseId;
            var counter = 1;
            while(!this._usedIds.Add(id)) {
                id=$"{baseId}_{counter++}";
            }
            var shape = new Shape { TypeId=id,MemberNames=memberNames };
            this._shapes.Add(key,shape);
            return shape;
        }

        // 匿名型のメンバー（プロパティ）を宣言順で返す。
        private static IReadOnlyList<IPropertySymbol> AnonymousMembers(INamedTypeSymbol anon)
            => anon.GetMembers().OfType<IPropertySymbol>().ToList();

        // ファクトリメソッド名。struct 名（__AnonN_...）と衝突しないよう __NewN_... にする。
        private static string FactoryName(Shape shape) => "__New"+shape.TypeId.Substring("__Anon".Length);
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

    // SourcePath からファイル単位で一意な識別子トークンを作る。
    internal static string AnonRecordFileToken(string sourcePath) {
        var name = System.IO.Path.GetFileNameWithoutExtension(sourcePath??string.Empty);
        var hash = 安定ハッシュ(sourcePath??string.Empty);
        return $"{SanitizeIdentifier(name)}_{hash:x8}";
    }

    // 生成コードへ埋め込む識別子なので、プロセス間で安定したハッシュでなければならない。
    // string.GetHashCode / StringComparer.Ordinal.GetHashCode は .NET のランダム化文字列ハッシュを
    // 使うためプロセスごとに値が変わり、同じ入力から毎回違う型名が生成されてしまう
    // （決定的ビルドが壊れ、生成物の差分比較もできなくなる）。FNV-1a で固定する。
    private static uint 安定ハッシュ(string 値) {
        unchecked {
            var ハッシュ = 2166136261u;
            foreach(var 文字 in 値) {
                ハッシュ^=文字;
                ハッシュ*=16777619u;
            }
            return ハッシュ;
        }
    }
}
