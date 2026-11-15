using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public enum DatabaseQueryKind{View,ScalarFunction,TableFunction}
public sealed record ParameterDesign(string Name,string Type);
/// <summary>ビュー・関数の設計。<see cref="Body"/> は本体の文で、受け手(入口)は <c>q</c> で読む(例: <c>return q.Orders.Where(o=&gt;o.Active).Select(o=&gt;new Views.Active(o.Id));</c>)。</summary>
public sealed record QueryDesign(string Name,string Schema,DatabaseQueryKind Kind,ColumnDesign[] Columns,ParameterDesign[] Parameters,string ReturnType,string Body,string? OriginalPath=null){
    public string Path=>string.Join(".",new[]{this.Schema,this.Name}.Where(value=>value.Length>0));
}

public static partial class DatabaseDesigner{
    private const string QueryReceiver="q";
    private const string SetTypePrefix="global::Seamless.Collections.ISet<";

    /// <summary>テーブル関数の戻り型(ISet&lt;行&gt;)の行の型。テーブル関数の戻り型でなければ null。</summary>
    public static string? FunctionElementType(string type){
        var syntax=SyntaxFactory.ParseTypeName(type);
        if(syntax is QualifiedNameSyntax qualified){syntax=qualified.Right;}
        if(syntax is AliasQualifiedNameSyntax alias){syntax=alias.Name;}
        if(syntax is GenericNameSyntax{Identifier.ValueText:"ISet",TypeArgumentList.Arguments.Count:1} set){return set.TypeArgumentList.Arguments[0].ToString();}
        return null;
    }
    public static CatalogObject[] Objects(Definition definition){
        var compilation=Compilation(definition);var container=Container(compilation,definition);
        var objects=new Dictionary<string,CatalogObject>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"","");
        foreach(var item in SourceStructure.Read(definition.Source).Objects.Where(item=>item.Root==container.ContainingNamespace.ToDisplayString()&&item.Kind!="Views")){
            var path=string.Join(".",new[]{item.Schema,item.Name}.Where(value=>value.Length>0));
            objects.TryAdd(path,new(path,item.Name,item.Schema,"Table",item.Type));
        }
        foreach(var query in QueryMembers(compilation,container,Schemas(definition))){
            objects.TryAdd(query.Path,new(query.Path,query.Member.Name,query.Schema,query.Kind,TypeName(query.Result)));
        }
        return objects.Values.OrderBy(item=>item.Schema,StringComparer.Ordinal).ThenBy(item=>item.Kind,StringComparer.Ordinal).ThenBy(item=>item.Name,StringComparer.Ordinal).ToArray();

        void Walk(INamedTypeSymbol owner,string prefix,string schema){
            if(!ancestors.Add(owner)){return;}
            try{
                foreach(var member in owner.GetMembers().Where(member=>!member.IsStatic&&!member.IsImplicitlyDeclared)){
                    ITypeSymbol? result=null;
                    if(member is IPropertySymbol property){result=property.Type;}
                    else if(member is IMethodSymbol{MethodKind:MethodKind.Ordinary} method){result=method.ReturnType;}
                    if(result is not INamedTypeSymbol type){continue;}
                    var path=prefix+member.Name;
                    if(IsSchemaType(type)){Walk(type,path+".",path);continue;}
                    if(type.ContainingNamespace.ToDisplayString()!="Seamless.Database"){continue;}
                    var kind=type.Name switch{"Table"=>"Table","MaterializedView"=>"View",_=>null};
                    if(kind is null||type.TypeArguments.Length==0){continue;}
                    objects.Add(path,new(path,member.Name,schema,kind,TypeName(type.TypeArguments[^1])));
                }
            }finally{ancestors.Remove(owner);}
        }
    }
    public static QueryDesign ReadQuery(Definition definition,string path){
        var compilation=Compilation(definition);
        var query=FindQueryMember(compilation,definition,path)??throw new ManagementException("NotFound","問い合わせがありません: "+path);
        var body=QueryBody(query);
        var kind=query.Kind switch{"View"=>DatabaseQueryKind.View,"TableFunction"=>DatabaseQueryKind.TableFunction,_=>DatabaseQueryKind.ScalarFunction};
        var columns=Array.Empty<ColumnDesign>();
        if(kind!=DatabaseQueryKind.ScalarFunction){columns=ReadEditableColumns(query.Result as INamedTypeSymbol,null,false);}
        var parameters=(query.Member as IMethodSymbol)?.Parameters.Select(parameter=>new ParameterDesign(parameter.Name,TypeName(parameter.Type))).ToArray()??[];
        var returnType=kind switch{DatabaseQueryKind.View=>"",DatabaseQueryKind.TableFunction=>SetTypePrefix+TypeName(query.Result)+">",_=>TypeName(query.Result)};
        return new(query.Member.Name,query.Schema,kind,columns,parameters,returnType,body,path);
    }
    // 本体の文。式の本体は return 文として見せる。
    private static string QueryBody(QueryMember query){
        ArrowExpressionClauseSyntax? arrow=null;BlockSyntax? block=null;
        switch(query.Syntax){
            case PropertyDeclarationSyntax property:
                arrow=property.ExpressionBody;
                var getter=property.AccessorList?.Accessors.SingleOrDefault(static accessor=>accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
                if(arrow is null){arrow=getter?.ExpressionBody;block=getter?.Body;}
                break;
            case MethodDeclarationSyntax method:arrow=method.ExpressionBody;block=method.Body;break;
        }
        if(block is not null){return block.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(block.OpenBraceToken.Span.End,block.CloseBraceToken.SpanStart));}
        if(arrow is not null){
            var prefix="return ";if(arrow.Expression is ThrowExpressionSyntax){prefix="";}
            var text=arrow.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(arrow.ArrowToken.Span.End,arrow.Expression.Span.End));
            return prefix+text+";";
        }
        throw new ManagementException("Unsupported","処理本体の形式を解釈できません。C#で編集してください。");
    }
    public static Definition ApplyQuery(Definition definition,QueryDesign design){
        Identifier(design.Name);if(design.Schema.Length>0){Identifier(design.Schema);}
        if(!Enum.IsDefined(design.Kind)||SyntaxFactory.ParseStatement("{\n"+design.Body+"\n}").ContainsDiagnostics){throw new ManagementException("Validation","処理本体のC#構文を確認してください。");}
        if(design.Parameters.Select(parameter=>parameter.Name).Distinct(StringComparer.Ordinal).Count()!=design.Parameters.Length){throw new ManagementException("Validation","引数名が重複しています。");}
        foreach(var parameter in design.Parameters){Identifier(parameter.Name);if(parameter.Name==QueryReceiver||SyntaxFactory.ParseTypeName(parameter.Type).ContainsDiagnostics){throw new ManagementException("Validation","引数の名前または型を確認してください。");}}
        if(design.Kind==DatabaseQueryKind.View&&design.Parameters.Length>0){throw new ManagementException("Validation","ビューには引数を指定できません。");}
        if(design.Kind!=DatabaseQueryKind.ScalarFunction){
            if(design.Columns.Length==0||design.Columns.Select(column=>column.Name).Distinct(StringComparer.Ordinal).Count()!=design.Columns.Length){throw new ManagementException("Validation","重複しない結果列を指定してください。");}
            foreach(var column in design.Columns){Identifier(column.Name);if(SyntaxFactory.ParseTypeName(column.Type).ContainsDiagnostics){throw new ManagementException("Validation","結果列の名前または型を確認してください。");}}
        }else if(SyntaxFactory.ParseTypeName(design.ReturnType).ContainsDiagnostics||FunctionElementType(design.ReturnType) is not null){throw new ManagementException("Validation","戻り型を確認してください。");}
        if(design.OriginalPath is not null){
            var previous=ReadQuery(definition,design.OriginalPath);
            if((previous.Kind==DatabaseQueryKind.View)!=(design.Kind==DatabaseQueryKind.View)){throw new ManagementException("Conflict","ビューと関数の種類は変更できません。");}
            if(design.OriginalPath!=design.Path){throw new ManagementException("Unsupported","問い合わせの名前・所属先の変更はC#で編集してください。");}
            if(previous.Kind!=design.Kind){throw new ManagementException("Unsupported","関数の種類の変更はC#で編集してください。");}
            return UpdateExistingQuery(definition,previous,design);
        }
        var compilation=Compilation(definition);var container=Container(compilation,definition);var owner=Owner(container,design.Schema);
        if(Objects(definition).Any(item=>item.Id==design.Path)||owner.GetMembers(design.Name).Length>0||design.Schema.Length==0&&Schemas(definition).Contains(design.Name,StringComparer.Ordinal)){
            throw new ManagementException("Conflict","同じ所属先に同名のスキーマまたはオブジェクトがあります。");
        }
        var ns=container.ContainingNamespace.ToDisplayString();
        // 結果の行型: ビューは <ルート>.Views、テーブル関数は <ルート>.Functions(スキーマはその下の名前空間)。
        var rowNamespace=ns+(design.Kind==DatabaseQueryKind.TableFunction?".Functions":".Views");
        if(design.Schema.Length>0){rowNamespace+="."+design.Schema;}
        var folder=design.Kind==DatabaseQueryKind.TableFunction?"Functions/":"Views/";if(design.Schema.Length>0){folder+=design.Schema+"/";}
        var resultType=design.ReturnType;
        // 本体と戻り型から結果の行型を名前だけで書けるよう、行型の名前空間を読み込む(型の名前の探索はプロパティ名と衝突しない)。
        string[] usings=["global::System.Linq","global::Seamless.Linq"];
        if(design.Kind!=DatabaseQueryKind.ScalarFunction){
            usings=[..usings,"global::"+rowNamespace];
            var rowName=design.Name;
            if(design.Kind==DatabaseQueryKind.TableFunction){
                var element=FunctionElementType(design.ReturnType)??throw new ManagementException("Validation","テーブル関数の戻り型は ISet<行> にしてください。");
                rowName=SyntaxFactory.ParseTypeName(element).GetLastToken().ValueText;Identifier(rowName);
            }
            var row="public readonly record struct @"+rowName+"("+string.Join(",",design.Columns.Select(column=>column.Type+" @"+column.Name))+");";
            definition=WriteNewFile(definition,folder+rowName+".cs",["System.Linq"],rowNamespace,row);
            resultType=SetTypePrefix+"global::"+rowNamespace+".@"+rowName+">";
        }
        // 本体: 生成される入口(IReader・I<スキーマ>Reader)への拡張メンバー。<コンテナ名>Queries という静的クラスへ、ビュー・関数ごとのファイルで足す。
        var receiver="global::"+container.ToDisplayString()+"."+ReaderInterfaceName(design.Schema);
        var newLine=SourceNewLine.Default;
        string member;
        if(design.Kind==DatabaseQueryKind.View){member="public "+resultType+" @"+design.Name+"{get{"+newLine+design.Body+newLine+"}}";}
        else{member="public "+resultType+" @"+design.Name+"("+string.Join(",",design.Parameters.Select(parameter=>parameter.Type+" @"+parameter.Name))+"){"+newLine+design.Body+newLine+"}";}
        // スキーマごとに静的クラスを分ける(同じクラスに同名の関数が並ぶと、生成コードの名前を区別できない)。
        var queriesClass=container.Name+(design.Schema.Length==0?"":"_"+design.Schema.Replace('.','_'))+"Queries";
        var queries="public static partial class @"+queriesClass+"{"+newLine+"extension("+receiver+" "+QueryReceiver+"){"+newLine+member+newLine+"}"+newLine+"}";
        var queryFolder="Queries/";if(design.Schema.Length>0){queryFolder+=design.Schema+"/";}
        return WriteNewFile(definition,queryFolder+design.Name+".cs",usings,ns,queries);
    }
    private static Definition UpdateExistingQuery(Definition definition,QueryDesign previous,QueryDesign design){
        var target=Objects(definition).Single(item=>item.Id==previous.Path);var baseline=ReadObjectDefinition(definition,target);
        var returnType=design.ReturnType;if(design.Kind==DatabaseQueryKind.View){returnType=baseline.ReturnType;}
        var updated=definition;
        if(!baseline.Columns.Select(column=>(column.Name,column.Type)).SequenceEqual(design.Columns.Select(column=>(column.Name,column.Type)))||!baseline.Parameters.SequenceEqual(design.Parameters)||baseline.ReturnType!=returnType){
            var edit=ApplyObjectStructure(definition,baseline,design.Columns,design.Parameters,returnType);
            if(!edit.Succeeded){throw new ManagementException("Validation",string.Join("\n",edit.Diagnostics.Select(diagnostic=>diagnostic.Message)));}
            updated=edit.Definition!;
        }
        if(previous.Body==design.Body){return updated;}
        var query=FindQueryMember(Compilation(updated),updated,design.Path)??throw new ManagementException("Conflict","問い合わせの宣言が変更されています。");
        var newLine=SourceNewLine.Of(updated.Source.Files.Single(item=>item.Path==query.Syntax.SyntaxTree.FilePath).Text);
        var block=(BlockSyntax)SyntaxFactory.ParseStatement("{"+newLine+design.Body+newLine+"}");
        MemberDeclarationSyntax replaced=query.Syntax switch{
            PropertyDeclarationSyntax property=>property.WithExpressionBody(null).WithSemicolonToken(default)
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,block)))),
            MethodDeclarationSyntax method=>method.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block),
            _=>throw new ManagementException("Unsupported","処理本体の形式を解釈できません。C#で編集してください。")
        };
        var syntax=query.Syntax;
        var source=new SourceEditor().Apply(updated.Source,new(updated.Source.Hash(),syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,replaced.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
        return updated with{Source=source};
    }
    // 新しいファイルはLFで作る。AppendLineはWindowsでCRLFとなり、同じ定義でもOSごとにソースと成果物のハッシュが変わる。
    private static Definition WriteNewFile(Definition definition,string path,string[] usings,string ns,string text){
        if(definition.Source.Files.Any(file=>file.Path==path)){throw new ManagementException("Conflict","ソースファイルが既にあります: "+path);}
        var newLine=SourceNewLine.Default;
        var builder=new System.Text.StringBuilder();
        foreach(var item in usings){builder.Append("using ").Append(item).Append(';').Append(newLine);}
        var parts=ns.Split('.',StringSplitOptions.RemoveEmptyEntries);
        foreach(var part in parts){builder.Append("namespace @").Append(part).Append('{').Append(newLine);}
        builder.Append(text).Append(newLine);foreach(var _ in parts){builder.Append('}').Append(newLine);}
        return definition with{Source=definition.Source with{Files=[..definition.Source.Files,new(path,builder.ToString())]}};
    }
}
