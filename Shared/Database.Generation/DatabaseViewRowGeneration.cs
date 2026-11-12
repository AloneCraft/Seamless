using static Shared.Database.Generation.DatabaseGenerationHelpers;
using Microsoft.CodeAnalysis.CSharp;

namespace Shared.Database.Generation;

/// <summary>ビューの行型をQuery本体の結果から生成する。</summary>
/// <remarks>
/// 利用者は Views 名前空間の partial record struct に、列を書かずに static Query を直接書き、戻り値を IEnumerable&lt;object&gt; とする。
/// 本体の実際の要素型が匿名型なら、その名前・型・順序で行型の位置引数を生成する。
/// 実行用には本体を写した Rows を同じ型の partial に生成し、結果要素の匿名型生成式だけを行型のコンストラクター呼出しへ置き換える。
/// 同じ型・同じ名前空間・同じ using で写すので、名前解決と private アクセスは元の本体と同じになる。
/// Rows は生成コード専用の GeneratedView を返す形にして、遅延評価・重複除去・子scope・循環検出とFLinqの展開経路を共有する。
/// 利用者の Query は解析用の定義であり、実行時には呼ばれない。
/// </remarks>
internal sealed class GeneratedViewRow{
    internal const string RowsMethodName="Rows";
    internal static readonly DiagnosticDescriptor CannotInfer=new(
        id:"SDB004",
        title:"View result columns cannot be inferred",
        messageFormat:"View '{0}' result columns cannot be inferred: {1}",
        category:"Seamless.Database.Generator",
        defaultSeverity:DiagnosticSeverity.Error,
        isEnabledByDefault:true);

    private GeneratedViewRow(string? source,Location location,string? failure){this.Source=source;this.Location=location;this.Failure=failure;}
    /// <summary>生成する .g.cs の全文。推論できない場合は null。</summary>
    internal string? Source{get;}
    internal Location Location{get;}
    /// <summary>推論できない理由。生成できた場合は null。</summary>
    internal string? Failure{get;}

    /// <summary>この宣言が生成対象の形でなければ null を返す。対象だが推論できなければ Failure を持つ結果を返す。</summary>
    /// <remarks>
    /// Query は static・引数1個・IEnumerable&lt;T&gt; を返す形を対象にする。
    /// T が object なら結果の匿名型から列を生成する(列の手書きは不可)。T が行型自身なら手書きの列をそのまま使い、本体も置き換えずに写す。
    /// 本体は単一の式、またはブロック。ブロックでは入れ子の関数を除くすべての return を結果として扱う。
    /// </remarks>
    internal static GeneratedViewRow? TryCreate(TypeDeclarationSyntax declaration,INamedTypeSymbol symbol,SemanticModel model){
        if(declaration is not RecordDeclarationSyntax record||!record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
            ||!record.Modifiers.Any(SyntaxKind.PartialKeyword)||symbol.ContainingType is not null||symbol.Arity!=0){return null;}
        var namespaceName=NamespaceName(symbol.ContainingNamespace);var segments=namespaceName.Split('.');
        var viewsIndex=Array.LastIndexOf(segments,"Views");
        if(viewsIndex<0){return null;}
        var query=record.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(static method=>method.Identifier.ValueText=="Query"&&method.Modifiers.Any(SyntaxKind.StaticKeyword));
        if(query is null||model.GetDeclaredSymbol(query) is not IMethodSymbol querySymbol||querySymbol.Parameters.Length!=1){return null;}
        if(querySymbol.ReturnType is not INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Collections_Generic_IEnumerable_T} returned){return null;}
        var inferred=returned.TypeArguments[0].SpecialType==SpecialType.System_Object;
        if(!inferred&&!SymbolEqualityComparer.Default.Equals(returned.TypeArguments[0],symbol)){return null;}
        var location=query.Identifier.GetLocation();
        var declaresColumns=symbol.DeclaringSyntaxReferences.Select(static reference=>reference.GetSyntax()).OfType<RecordDeclarationSyntax>().Any(static part=>part.ParameterList is not null);
        if(inferred&&declaresColumns){return new(null,location,"columns are declared by hand; return IEnumerable of the row type instead of IEnumerable<object>");}
        SyntaxNode? body=query.ExpressionBody?.Expression;
        if(body is null){body=query.Body;}
        if(body is null){return new(null,location,"the Query method needs a body");}
        var results=body is BlockSyntax block?Returns(block).ToArray():[(ExpressionSyntax)body];
        if(results.Length==0||results.Any(static result=>result is null)){return new(null,location,"every return must have a result");}
        var rowType="global::"+namespaceName+".@"+symbol.Name;
        IPropertySymbol[] columns=[];
        if(inferred){
            var elements=results.Select(result=>ElementType(model.GetTypeInfo(result!).Type)).Distinct(SymbolEqualityComparer.Default).ToArray();
            if(elements.Length!=1||elements[0] is not ITypeSymbol{IsAnonymousType:true} element){
                return new(null,location,"every result must be a sequence of one anonymous type (an empty collection or mixed shapes cannot be inferred)");
            }
            columns=element.GetMembers().OfType<IPropertySymbol>().ToArray();
            if(columns.Length==0){return new(null,location,"the anonymous result has no members");}
            if(columns.Any(static column=>ContainsAnonymous(column.Type))){return new(null,location,"a result column uses another anonymous type");}
            var creations=body.DescendantNodesAndSelf().OfType<AnonymousObjectCreationExpressionSyntax>()
                .Where(node=>SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(node).Type,element)).ToArray();
            body=body.ReplaceNodes(creations,(_,current)=>SyntaxFactory.ParseExpression(
                "new "+rowType+"("+string.Join(",",current.Initializers.Select(static initializer=>initializer.Expression.ToFullString()))+")")
                .WithTriviaFrom(current));
        }
        var schema=string.Join(".",segments.Skip(viewsIndex+1));var viewName=symbol.Name;
        if(schema.Length>0){viewName=schema+"."+symbol.Name;}
        var parameter=querySymbol.Parameters[0];var parameterName="@"+parameter.Name;
        var builder=new StringBuilder("// <auto-generated/>\n#nullable enable\n");
        foreach(var directive in ((CompilationUnitSyntax)declaration.SyntaxTree.GetRoot()).Usings.Where(static directive=>!directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))){
            builder.Append(directive.ToString()).Append('\n');
        }
        builder.Append("namespace ").Append(namespaceName).Append("{\n");
        foreach(var scope in declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()){
            foreach(var directive in scope.Usings){builder.Append(directive.ToString()).Append('\n');}
        }
        builder.Append(string.Join(" ",record.Modifiers.Select(static modifier=>modifier.ValueText))).Append(" record struct @").Append(symbol.Name);
        if(inferred){
            builder.Append('(').Append(string.Join(",",columns.Select(static column=>column.Type.ToDisplayString(完全修飾形式)+" @"+column.Name))).Append(')');
        }else{
        }
        builder.Append("{\n");
        builder.Append("    // [ビュー/生成] Queryの本体を写した実行用の定義。列を推論した場合は結果の匿名型だけを行型へ置き換える。Queryは解析用で実行時には呼ばない。\n");
        var lambda="{return "+body.ToFullString().Trim()+";}";
        if(body is BlockSyntax){
            lambda=body.ToFullString().Trim();
        }else if(body is ThrowExpressionSyntax){
            lambda="{"+body.ToFullString().Trim()+";}";
        }
        builder.Append("    internal static global::Seamless.Collections.ISet<").Append(rowType).Append("> ").Append(RowsMethodName).Append('(')
            .Append(parameter.Type.ToDisplayString(完全修飾形式)).Append(' ').Append(parameterName).Append(")=>")
            .Append(parameterName).Append(".GeneratedView<").Append(rowType).Append(">(()=>").Append(lambda).Append(',')
            .Append(SymbolDisplay.FormatLiteral(viewName,true)).Append(");\n");
        builder.Append("}\n}\n");
        return new(builder.ToString(),location,null);
    }

    private static IEnumerable<ExpressionSyntax?> Returns(BlockSyntax block)=>
        block.DescendantNodes(static node=>node is not(AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)).OfType<ReturnStatementSyntax>().Select(static statement=>statement.Expression);

    private static ITypeSymbol? ElementType(ITypeSymbol? type){
        if(type is INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Collections_Generic_IEnumerable_T} sequence){return sequence.TypeArguments[0];}
        if(type is IArrayTypeSymbol array){return array.ElementType;}
        var sequences=type?.AllInterfaces.Where(static item=>item.OriginalDefinition.SpecialType==SpecialType.System_Collections_Generic_IEnumerable_T).ToArray()??[];
        if(sequences.Length==1){return sequences[0].TypeArguments[0];}
        return null;
    }
    private static bool ContainsAnonymous(ITypeSymbol type){
        if(type.IsAnonymousType){return true;}
        if(type is IArrayTypeSymbol array){return ContainsAnonymous(array.ElementType);}
        if(type is INamedTypeSymbol named){return named.TypeArguments.Any(ContainsAnonymous);}
        return false;
    }
    private static string NamespaceName(INamespaceSymbol symbol){
        if(symbol.IsGlobalNamespace){return string.Empty;}
        return symbol.ToDisplayString();
    }
}
