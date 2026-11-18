using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    internal static ObjectDefinitionEdit ChangeFunctionResultShape(Definition definition,ObjectDefinitionDesign baseline,ColumnDesign[] columns){
        var current=CheckObjectBaseline(definition,baseline);
        var compilation=Compilation(definition);
        var query=FindQueryMember(compilation,definition,current.Object.Id);
        if(query?.Syntax is not MethodDeclarationSyntax method||!current.StructureAvailable||query.Kind is not ("ScalarFunction" or "TableFunction")){
            throw new ManagementException("Validation","結果列を切り替えられるDB内の関数を指定してください。");
        }
        if(columns.Select(column=>column.Name).Distinct(StringComparer.Ordinal).Count()!=columns.Length){
            throw new ManagementException("Validation","結果列の名前が重複しています。");
        }
        foreach(var column in columns){Identifier(column.Name);ValidateObjectType(column.Type);}
        var returnType="int";
        ExpressionSyntax? replacement=null;
        var previousResult=query.Kind=="TableFunction"?query.Result as INamedTypeSymbol:null;
        var expression=method.ExpressionBody?.Expression??(method.Body is{Statements:[ReturnStatementSyntax statement]}?statement.Expression:null);
        if(columns.Length>0){
            var container=Container(compilation,definition);
            var ns=container.ContainingNamespace.ToDisplayString()+".Functions";
            var folder="Functions/";
            if(query.Schema.Length>0){ns+="."+query.Schema;folder+=query.Schema.Replace('.','/')+"/";}
            var prefix=query.Member.Name+"Result";
            var rowName=prefix;
            for(var suffix=2;definition.Source.Files.Any(file=>file.Path.Equals(folder+rowName+".cs",StringComparison.OrdinalIgnoreCase))||compilation.GetTypeByMetadataName(ns+"."+rowName) is not null;suffix++){rowName=prefix+suffix;}
            var row="public readonly record struct @"+rowName+"("+string.Join(", ",columns.Select(column=>column.Type+" @"+column.Name))+");";
            definition=WriteNewFile(definition,folder+rowName+".cs",[],ns,row);
            var fullName="global::"+ns+".@"+rowName;
            returnType=SetTypePrefix+fullName+">";
            if(expression?.IsKind(SyntaxKind.DefaultLiteralExpression)==true){replacement=SyntaxFactory.ParseExpression(EmptyResult(fullName));}
        }else if(previousResult is not null&&expression is not null){
            var fullName=previousResult.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var expected=fullName[..(fullName.LastIndexOf('.')+1)]+"@"+previousResult.Name;
            if(SyntaxFactory.AreEquivalent(expression,SyntaxFactory.ParseExpression(EmptyResult(expected)))){
                replacement=SyntaxFactory.ParseExpression("default");
            }
        }
        var changed=method;
        if(expression is not null&&replacement is not null){changed=changed.ReplaceNode(expression,replacement.WithTriviaFrom(expression));}
        changed=changed.WithReturnType(SyntaxFactory.ParseTypeName(returnType).WithTriviaFrom(method.ReturnType));
        var source=new SourceEditor().Apply(definition.Source,new(definition.Source.Hash(),method.SyntaxTree.FilePath,method.FullSpan.Start,method.FullSpan.Length,changed.ToFullString()));
        if(previousResult?.DeclaringSyntaxReferences is [var result]&&result.SyntaxTree.FilePath.StartsWith("Functions/",StringComparison.Ordinal)
            &&result.SyntaxTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>().All(member=>member is BaseNamespaceDeclarationSyntax||member.Span==result.Span)
            &&!source.Files.Where(file=>file.Path!=result.SyntaxTree.FilePath).Any(file=>SyntaxFactory.ParseCompilationUnit(file.Text).DescendantNodes().OfType<SimpleNameSyntax>().Any(name=>name.Identifier.ValueText==previousResult.Name))){
            source=source with{Files=source.Files.Where(file=>file.Path!=result.SyntaxTree.FilePath).ToArray()};
        }
        return new(definition with{Source=source with{Revision=Guid.NewGuid().ToString("N")}},[]);

        static string EmptyResult(string row)=>"global::Seamless.Linq.Set.Empty<"+row+">()";
    }

    internal static Definition RenameQuery(Definition definition,string path,string name,CSharpCompilation compilation){
        ObjectName(name);
        var schemas=Schemas(definition);
        var selected=QueryMembers(compilation,Container(compilation,definition),schemas).Where(item=>item.Path==path).ToArray();
        if(selected.Length!=1){throw new ManagementException("Validation","名前を変更するビュー・関数を一意に特定できません。");}
        var query=selected[0];
        if(query.Member.Name==name){return definition;}
        var nextPath=query.Schema.Length==0?name:query.Schema+"."+name;
        if(Objects(definition).Any(item=>item.Id==nextPath)||schemas.Contains(nextPath,StringComparer.Ordinal)){
            throw new ManagementException("Conflict","同じ所属先に同名のスキーマまたはオブジェクトがあります。");
        }
        var queryFile="Queries/"+path.Replace('.','/')+".cs";
        var resultFolder=query.Kind=="View"?"Views/":"Functions/";
        var rowFile=resultFolder+path.Replace('.','/')+".cs";
        var row=query.Result as INamedTypeSymbol;
        var ownRow=query.Kind!="ScalarFunction"&&row is not null&&row.Name==query.Member.Name
            &&row.DeclaringSyntaxReferences is [var declaration]&&declaration.SyntaxTree.FilePath==rowFile
            &&declaration.SyntaxTree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Count()==1;
        var rowSymbol=ownRow?row:null;
        if(rowSymbol?.ContainingNamespace.GetTypeMembers(name).Length>0){
            throw new ManagementException("Conflict","結果の行型と同名のC#型があります。");
        }
        var trees=compilation.SyntaxTrees.ToDictionary(tree=>tree.FilePath,StringComparer.Ordinal);
        var files=definition.Source.Files.Select(file=>{
            var tree=trees[file.Path];
            var root=tree.GetRoot();
            var model=compilation.GetSemanticModel(tree);
            var identifiers=new HashSet<SyntaxToken>();
            if(tree==query.Syntax.SyntaxTree){
                identifiers.Add(query.Syntax is PropertyDeclarationSyntax property?property.Identifier:((MethodDeclarationSyntax)query.Syntax).Identifier);
            }
            foreach(var node in root.DescendantNodes()){
                if(rowSymbol is not null&&node is RecordDeclarationSyntax record&&IsSame(model.GetDeclaredSymbol(record),rowSymbol)){
                    identifiers.Add(record.Identifier);
                }
                if(node is not SimpleNameSyntax simple||simple.Identifier.ValueText!=query.Member.Name){continue;}
                var binding=model.GetSymbolInfo(simple);
                if(IsSame(binding.Symbol,query.Member)||IsSame(binding.Symbol,rowSymbol)){identifiers.Add(simple.Identifier);}
                else if(binding.Symbol is null&&binding.CandidateSymbols.Any(symbol=>IsSame(symbol,query.Member)||IsSame(symbol,rowSymbol))){
                    throw new ManagementException("Validation","参照先が確定していない名前があります。C#のエラーを確認してください。");
                }
            }
            var updated=root.ReplaceTokens(identifiers,(old,_)=>SyntaxFactory.Identifier(old.LeadingTrivia,"@"+name,old.TrailingTrivia));
            var fileName=file.Path;
            if(file.Path==queryFile&&root.DescendantNodes().OfType<ExtensionBlockDeclarationSyntax>().Count()==1
                &&root.DescendantNodes().OfType<ExtensionBlockDeclarationSyntax>().Single().Members.Count==1){
                fileName="Queries/"+nextPath.Replace('.','/')+".cs";
            }else if(file.Path==rowFile&&ownRow){
                fileName=resultFolder+nextPath.Replace('.','/')+".cs";
            }
            var text=updated.ToFullString();
            if(ownRow&&query.Kind=="View"&&tree==query.Syntax.SyntaxTree){
                text=text.Replace("// return q.スキーマ名.テーブル名.Select(row => new @"+query.Member.Name+"(row.Id));",
                    "// return q.スキーマ名.テーブル名.Select(row => new @"+name+"(row.Id));",StringComparison.Ordinal)
                    .Replace("//        select new @"+query.Member.Name+"(a.Id);","//        select new @"+name+"(a.Id);",StringComparison.Ordinal);
            }
            return file with{Path=fileName,Text=text};
        }).ToArray();
        if(files.Select(file=>file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=files.Length){
            throw new ManagementException("Conflict","変更先と同名のソースファイルがあります。");
        }
        var updated=definition with{Source=definition.Source with{Files=files,Revision=Guid.NewGuid().ToString("N")}};
        if(!Objects(updated).Any(item=>item.Id==nextPath&&item.Kind==query.Kind)){
            throw new ManagementException("Validation","名前を変更した定義を読み取れません。");
        }
        return updated;

        static bool IsSame(ISymbol? actual,ISymbol? expected){
            if(actual is null||expected is null){return false;}
            if(actual is IMethodSymbol{ReducedFrom:{} original}){actual=original;}
            if(SymbolEqualityComparer.Default.Equals(actual.OriginalDefinition,expected.OriginalDefinition)){return true;}
            var spans=expected.DeclaringSyntaxReferences;
            return actual.DeclaringSyntaxReferences.Any(reference=>spans.Any(item=>item.SyntaxTree==reference.SyntaxTree&&item.Span==reference.Span));
        }
    }
}
