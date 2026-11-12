using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

/// <remarks>
/// ビューは Views 名前空間の partial record struct に static Query を直接書く。
/// 本体の結果が匿名型なら列は書かず、戻り値を IEnumerable&lt;object&gt; とする。列と実行用の Rows はDB生成器が作る。
/// 本体の結果が行型自身または空なら、列を位置引数として手書きし、戻り値を IEnumerable&lt;行型&gt; とする。
/// 設計器は本体を変えた後にこの二つの形を本体の結果へ合わせる。利用者ソースへ生成コードを書き込まない。
/// </remarks>
public static partial class DatabaseDesigner{
    private const string ViewObjectResult="global::System.Collections.Generic.IEnumerable<object>";

    private sealed record ViewQuery(INamedTypeSymbol Row,IMethodSymbol Query,MethodDeclarationSyntax Syntax,bool Inferred);

    private static ViewQuery? FindViewQuery(CSharpCompilation compilation,INamedTypeSymbol? row){
        if(row is null||!row.IsRecord||!row.IsValueType||row.Arity!=0){return null;}
        var methods=row.GetMembers("Query").OfType<IMethodSymbol>().Take(2).ToArray();
        if(methods.Length!=1||methods[0] is not{IsStatic:true,Parameters.Length:1} query
            ||query.ReturnType is not INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Collections_Generic_IEnumerable_T} result
            ||query.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() is not MethodDeclarationSyntax syntax){return null;}
        var inferred=result.TypeArguments[0].SpecialType==SpecialType.System_Object;
        if(!inferred&&!SymbolEqualityComparer.Default.Equals(result.TypeArguments[0],row)){return null;}
        return new(row,query,syntax,inferred);
    }

    /// <summary>本体のすべてのreturnに共通する要素型。空の集合など決められない場合はnull。</summary>
    /// <remarks>列の型にnullable参照型の注釈を残すため、推論だけnullable有効のコンパイルで読む。返す型はこのコンパイルの記号なので、行型との比較は型名で行う。</remarks>
    private static ITypeSymbol? ViewResultElement(CSharpCompilation compilation,MethodDeclarationSyntax syntax){
        var model=compilation.WithOptions(compilation.Options.WithNullableContextOptions(NullableContextOptions.Enable)).GetSemanticModel(syntax.SyntaxTree);
        ExpressionSyntax?[] results;
        if(syntax.ExpressionBody is{} arrow){results=[arrow.Expression];}
        else if(syntax.Body is{} block){results=block.DescendantNodes(static node=>node is not(AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)).OfType<ReturnStatementSyntax>().Select(static statement=>statement.Expression).ToArray();}
        else{return null;}
        if(results.Length==0||results.Any(static result=>result is null)){return null;}
        var elements=results.Select(result=>SequenceElement(model.GetTypeInfo(result!).Type)).Distinct(SymbolEqualityComparer.Default).ToArray();
        if(elements.Length!=1){return null;}
        return (ITypeSymbol?)elements[0];
    }
    private static ITypeSymbol? SequenceElement(ITypeSymbol? type){
        if(type is INamedTypeSymbol{OriginalDefinition.SpecialType:SpecialType.System_Collections_Generic_IEnumerable_T} sequence){return sequence.TypeArguments[0];}
        if(type is IArrayTypeSymbol array){return array.ElementType;}
        var sequences=type?.AllInterfaces.Where(static item=>item.OriginalDefinition.SpecialType==SpecialType.System_Collections_Generic_IEnumerable_T).ToArray()??[];
        if(sequences.Length==1){return sequences[0].TypeArguments[0];}
        return null;
    }

    /// <summary>列を推論する形のビューの結果列。推論する形でなければnull。</summary>
    private static ColumnDesign[]? InferredViewColumns(CSharpCompilation compilation,INamedTypeSymbol? row){
        if(FindViewQuery(compilation,row) is not{Inferred:true} view){return null;}
        if(ViewResultElement(compilation,view.Syntax) is not INamedTypeSymbol{IsAnonymousType:true} anonymous){return [];}
        return anonymous.GetMembers().OfType<IPropertySymbol>().Select(property=>new ColumnDesign(property.Name,TypeName(property.Type),false)).ToArray();
    }

    /// <summary>本体の結果に合わせて、列を推論する形と列を手書きする形を切り替える。</summary>
    private static Definition SynchronizeViewResult(Definition definition,string recordType,bool requireColumns=false){
        var compilation=Compilation(definition);
        var row=compilation.GetTypeByMetadataName(recordType.Replace("global::","",StringComparison.Ordinal));
        if(FindViewQuery(compilation,row) is not{} view){
            if(requireColumns){throw new ManagementException("Validation","ビューの結果列を決められません。名前を付けた匿名型の列挙を返してください。");}
            return definition;
        }
        var declarations=view.Row.DeclaringSyntaxReferences.Select(static reference=>reference.GetSyntax()).OfType<RecordDeclarationSyntax>().ToArray();
        var columns=declarations.Select(static declaration=>declaration.ParameterList).OfType<ParameterListSyntax>().Take(2).ToArray();
        if(columns.Length>1){throw new ManagementException("Unsupported","列の宣言が複数あります。C#で編集してください。");}
        var element=ViewResultElement(compilation,view.Syntax);
        bool inferred;
        if(element is INamedTypeSymbol{IsAnonymousType:true} anonymous){
            var properties=anonymous.GetMembers().OfType<IPropertySymbol>().ToArray();
            if(properties.Length==0||properties.Any(property=>property.Name is "Key" or "Query" or "Rows"||property.Name==view.Row.Name||ContainsAnonymousResult(property.Type)||property.Type.TypeKind==TypeKind.Error)){
                throw new ManagementException("Unsupported","ビューの結果列を宣言できません。列名を変更するか、入れ子の匿名型に名前付きの型を指定してください。");
            }
            inferred=true;
        }else if(element is not null&&TypeName(element)==TypeName(view.Row)||element is null&&columns.Length==1){
            if(columns.Length==0){throw new ManagementException("Validation","ビューの結果列を決められません。匿名型の列挙を返すか、行型に列を宣言してください。");}
            inferred=false;
        }else{
            throw new ManagementException("Validation","ビューの結果列を決められません。すべてのreturnで同じ列構成の匿名型を返すか、宣言済みの行型を返してください。");
        }
        if(inferred==view.Inferred&&(inferred?columns.Length==0:columns.Length==1)){return definition;}
        var returnType=ViewObjectResult;if(!inferred){returnType="global::System.Collections.Generic.IEnumerable<"+TypeName(view.Row)+">";}
        var files=definition.Source.Files.ToArray();
        foreach(var tree in declarations.Select(static declaration=>declaration.SyntaxTree).Append(view.Syntax.SyntaxTree).Distinct()){
            var targets=new List<SyntaxNode>();
            if(view.Syntax.SyntaxTree==tree){targets.Add(view.Syntax.ReturnType);}
            if(inferred){targets.AddRange(declarations.Where(declaration=>declaration.SyntaxTree==tree&&declaration.ParameterList is not null));}
            if(targets.Count==0){continue;}
            var root=tree.GetRoot().ReplaceNodes(targets,(original,_)=>{
                if(original is RecordDeclarationSyntax record){return record.WithParameterList(null).WithIdentifier(record.Identifier.WithTrailingTrivia(record.ParameterList!.GetTrailingTrivia()));}
                return SyntaxFactory.ParseTypeName(returnType).WithTriviaFrom(original);
            });
            var index=Array.FindIndex(files,file=>file.Path==tree.FilePath);
            files[index]=files[index] with{Text=root.ToFullString()};
        }
        var source=definition.Source with{Files=files};
        SourceEditor.Validate(source);
        return definition with{Source=source};
    }

    /// <summary>ビューのQuery本体を、設計画面で編集する文の列として読む。</summary>
    private static string ViewQueryBody(MethodDeclarationSyntax syntax){
        if(syntax.Body is{} block){return block.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(block.OpenBraceToken.Span.End,block.CloseBraceToken.SpanStart));}
        if(syntax.ExpressionBody is{} arrow){
            var prefix="return ";if(arrow.Expression is ThrowExpressionSyntax){prefix="";}
            var text=arrow.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(arrow.ArrowToken.Span.End,arrow.Expression.Span.End));
            return prefix+text+";"+arrow.Expression.GetTrailingTrivia().ToFullString();
        }
        throw new ManagementException("Unsupported","このビューのQueryには本体がありません。C#で編集してください。");
    }

    /// <summary>ビューのQuery本体を置き換える位置。式本体はブロックへ変える。</summary>
    private static (int Start,int Length,string Text) ViewQueryBodyEdit(MethodDeclarationSyntax syntax,string body){
        var newLine=FileNewLine(syntax);
        if(syntax.Body is{} block){return(block.OpenBraceToken.Span.End,block.CloseBraceToken.SpanStart-block.OpenBraceToken.Span.End,newLine+body+newLine);}
        if(syntax.ExpressionBody is{} arrow){
            var end=arrow.FullSpan.End;if(syntax.SemicolonToken.IsKind(SyntaxKind.SemicolonToken)){end=syntax.SemicolonToken.Span.End;}
            return(arrow.SpanStart,end-arrow.SpanStart,"{"+newLine+body+newLine+"}");
        }
        throw new ManagementException("Unsupported","このビューのQueryには本体がありません。C#で編集してください。");
    }
}
