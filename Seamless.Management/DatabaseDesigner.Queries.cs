using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public enum DatabaseQueryKind{View,ScalarFunction,TableFunction}
public sealed record ParameterDesign(string Name,string Type);
public sealed record QueryDesign(string Name,string Schema,DatabaseQueryKind Kind,ColumnDesign[] Columns,ParameterDesign[] Parameters,string ReturnType,string Body,string? OriginalPath=null){
    public string Path=>string.Join(".",new[]{this.Schema,this.Name}.Where(value=>value.Length>0));
}

public static partial class DatabaseDesigner{
    public static string? FunctionElementType(string type){
        var syntax=SyntaxFactory.ParseTypeName(type);
        if(syntax is QualifiedNameSyntax qualified){syntax=qualified.Right;}
        if(syntax is AliasQualifiedNameSyntax alias){syntax=alias.Name;}
        if(syntax is GenericNameSyntax{Identifier.ValueText:"IEnumerable",TypeArgumentList.Arguments.Count:1} sequence){return sequence.TypeArgumentList.Arguments[0].ToString();}
        return null;
    }
    public static CatalogObject[] Objects(Definition definition){
        var objects=ObjectsWithoutIdentities(definition);var identities=ReadStableIdentities(definition);
        return objects.Select(item=>item with{StableId=identities.Object(item.Id),StableIdUnavailableReason=identities.UnavailableReason("Object",item.Id)}).ToArray();
    }
    private static CatalogObject[] ObjectsWithoutIdentities(Definition definition){
        var compilation=Compilation(definition);var container=Container(compilation,definition);
        var objects=new Dictionary<string,CatalogObject>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"","");
        foreach(var item in SourceStructure.Read(definition.Source).Objects.Where(item=>item.Root==container.ContainingNamespace.ToDisplayString())){
            var path=string.Join(".",new[]{item.Schema,item.Name}.Where(value=>value.Length>0));
            var kind="Table";if(item.Kind=="Views"){kind="View";}
            objects.TryAdd(path,new(path,item.Name,item.Schema,kind,item.Type));
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
                    var ns=type.ContainingNamespace.ToDisplayString();
                    if(ns!="Seamless.Database"&&!(ns=="Seamless.Collections"&&type.Name=="ISet")){continue;}
                    var kind=type.Name switch{"Table"=>"Table","MaterializedView"=>"View","ScalarFunction"=>"ScalarFunction","TableFunction"=>"TableFunction",_=>null};
                    if(kind is null||type.TypeArguments.Length==0){continue;}
                    objects.Add(path,new(path,member.Name,schema,kind,TypeName(type.TypeArguments[^1])));
                }
            }finally{ancestors.Remove(owner);}
        }
    }
    public static QueryDesign ReadQuery(Definition definition,string path){
        var item=Objects(definition).SingleOrDefault(item=>item.Id==path)??throw new ManagementException("NotFound","問い合わせがありません: "+path);
        var compilation=Compilation(definition);var container=Container(compilation,definition);
        if(item.Kind=="View"){
            var source=SourceStructure.Read(definition.Source).Objects.SingleOrDefault(source=>source.Kind=="Views"&&source.Root==container.ContainingNamespace.ToDisplayString()&&source.Schema==item.Schema&&source.Name==item.Name)
                ??throw new ManagementException("Unsupported","このビューはC#で編集してください。");
            var tree=compilation.SyntaxTrees.Single(tree=>tree.FilePath==source.File);
            var declaration=tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().Single(type=>type.SpanStart==source.Start);
            var row=compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration) as INamedTypeSymbol;
            var methods=row?.GetMembers("Query").OfType<IMethodSymbol>().ToArray()??[];
            if(methods.Length!=1||!methods[0].IsStatic){throw new ManagementException("Unsupported","このビューのQueryを一意に選択できません。C#で編集してください。");}
            var query=methods[0].DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as MethodDeclarationSyntax??throw new ManagementException("Unsupported","このビューのQueryを読めません。C#で編集してください。");
            var body=ViewQueryBody(query);
            var viewColumns=InferredViewColumns(compilation,row)??source.Columns.Select(column=>new ColumnDesign(column.Name,column.Type,false)).ToArray();
            return new(item.Name,item.Schema,DatabaseQueryKind.View,viewColumns,[],"",body,path);
        }
        var method=Owner(container,item.Schema).GetMembers(item.Name).OfType<IMethodSymbol>().SingleOrDefault()
            ??throw new ManagementException("Unsupported","この関数はC#で編集してください。");
        var functionBody=ReadFunctionBody(method,container,item.Schema,out _);
        var type=((INamedTypeSymbol)method.ReturnType).TypeArguments.Single();
        var kind=DatabaseQueryKind.ScalarFunction;if(item.Kind=="TableFunction"){kind=DatabaseQueryKind.TableFunction;}
        var columns=Array.Empty<ColumnDesign>();
        if(kind==DatabaseQueryKind.TableFunction){columns=type.GetMembers().OfType<IPropertySymbol>().Where(property=>!property.IsStatic&&property.DeclaredAccessibility==Accessibility.Public).Select(property=>new ColumnDesign(property.Name,TypeName(property.Type),false)).ToArray();}
        var returnType=TypeName(type);if(kind==DatabaseQueryKind.TableFunction){returnType="global::System.Collections.Generic.IEnumerable<"+returnType+">";}
        return new(item.Name,item.Schema,kind,columns,method.Parameters.Select(parameter=>new ParameterDesign(parameter.Name,TypeName(parameter.Type))).ToArray(),returnType,functionBody,path);
    }
    private static string ReadFunctionBody(IMethodSymbol method,INamedTypeSymbol container,string schema,out bool bindContainer){
        var syntax=method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax;
        var body=QueryBody(syntax?.ExpressionBody?.Expression);
        if(SyntaxFactory.ParseStatement("{"+body+"}") is not BlockSyntax statements||statements.ContainsDiagnostics){throw new ManagementException("Unsupported","この関数はC#で編集してください。");}
        bindContainer=false;
        var receiver="this";if(schema.Length>0){receiver="("+TypeName(container)+")this.Database";}
        if(!statements.ContainsDirectives&&statements.Statements.FirstOrDefault() is LocalDeclarationStatementSyntax local&&local.Declaration.Type.IsVar&&local.Declaration.Variables.Count==1&&local.Declaration.Variables[0] is{Identifier.ValueText:"container",Initializer.Value:{} initializer}&&SyntaxFactory.AreEquivalent(initializer,SyntaxFactory.ParseExpression(receiver))){
            bindContainer=true;var text=statements.ToFullString();return text.Remove(local.SpanStart,local.Span.Length)[1..^1];
        }
        return body;
    }
    private static string QueryBody(ExpressionSyntax? expression){
        if(expression is InvocationExpressionSyntax invocation&&QueryDefinitionArgument(invocation)?.Expression is ParenthesizedLambdaExpressionSyntax lambda){
            if(lambda.Body is BlockSyntax block){return block.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(block.OpenBraceToken.Span.End,block.CloseBraceToken.SpanStart));}
            if(lambda.Body is ExpressionSyntax value){
                var prefix="return ";if(value is ThrowExpressionSyntax){prefix="";}
                var text=value.SyntaxTree.GetText().ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(lambda.ArrowToken.Span.End,value.Span.End));
                return prefix+text+";"+value.GetTrailingTrivia().ToFullString();
            }
        }
        throw new ManagementException("Unsupported","処理本体の形式を解釈できません。C#で編集してください。");
    }
    private static ArgumentSyntax? QueryDefinitionArgument(InvocationExpressionSyntax invocation)=>invocation.ArgumentList.Arguments.FirstOrDefault(argument=>argument.NameColon?.Name.Identifier.ValueText=="definition")??invocation.ArgumentList.Arguments.FirstOrDefault(argument=>argument.NameColon is null);
    public static Definition ApplyQuery(Definition definition,QueryDesign design){
        Identifier(design.Name);if(design.Schema.Length>0){Identifier(design.Schema);}
        if(!Enum.IsDefined(design.Kind)||SyntaxFactory.ParseStatement("{\n"+design.Body+"\n}").ContainsDiagnostics){throw new ManagementException("Validation","処理本体のC#構文を確認してください。");}
        if(design.Parameters.Select(parameter=>parameter.Name).Distinct(StringComparer.Ordinal).Count()!=design.Parameters.Length){throw new ManagementException("Validation","引数名が重複しています。");}
        foreach(var parameter in design.Parameters){Identifier(parameter.Name);if(parameter.Name=="container"||SyntaxFactory.ParseTypeName(parameter.Type).ContainsDiagnostics){throw new ManagementException("Validation","引数の名前または型を確認してください。");}}
        if(design.Kind==DatabaseQueryKind.View&&design.Parameters.Length>0){throw new ManagementException("Validation","ビューには引数を指定できません。");}
        if(design.Kind!=DatabaseQueryKind.ScalarFunction){
            if((design.Kind!=DatabaseQueryKind.View&&design.Columns.Length==0)||design.Columns.Select(column=>column.Name).Distinct(StringComparer.Ordinal).Count()!=design.Columns.Length){throw new ManagementException("Validation","重複しない結果列を指定してください。");}
            foreach(var column in design.Columns){Identifier(column.Name);if(column.Name=="Query"||SyntaxFactory.ParseTypeName(column.Type).ContainsDiagnostics){throw new ManagementException("Validation","結果列の名前または型を確認してください。");}}
        }else if(SyntaxFactory.ParseTypeName(design.ReturnType).ContainsDiagnostics){throw new ManagementException("Validation","戻り型を確認してください。");}
        var compilation=Compilation(definition);var container=Container(compilation,definition);var owner=Owner(container,design.Schema);var bindContainer=true;
        if(design.OriginalPath is not null){
            var previous=ReadQuery(definition,design.OriginalPath);
            if((previous.Kind==DatabaseQueryKind.View)!=(design.Kind==DatabaseQueryKind.View)){throw new ManagementException("Conflict","ビューと関数の種類は変更できません。");}
            if(previous.Kind!=DatabaseQueryKind.View){var method=Owner(container,previous.Schema).GetMembers(previous.Name).OfType<IMethodSymbol>().Single();_=ReadFunctionBody(method,container,previous.Schema,out bindContainer);}
        }
        if(design.OriginalPath!=design.Path&&(Objects(definition).Any(item=>item.Id==design.Path)||owner.GetMembers(design.Name).Length>0||design.Schema.Length==0&&Schemas(definition).Contains(design.Name,StringComparer.Ordinal))){
            throw new ManagementException("Conflict","同じ所属先に同名のスキーマまたはオブジェクトがあります。");
        }
        if(design.OriginalPath is not null&&design.OriginalPath!=design.Path){throw new ManagementException("Unsupported","問い合わせの名前・所属先の変更はC#で編集してください。");}
        if(design.OriginalPath is not null&&ReadQuery(definition,design.OriginalPath) is{} previousQuery&&previousQuery.Kind==design.Kind){return UpdateExistingQuery(definition,previousQuery,design);}
        var ns=container.ContainingNamespace.ToDisplayString();var rowNamespace=ns+".Views";
        if(design.Kind==DatabaseQueryKind.TableFunction){rowNamespace=ns+".Functions";}
        if(design.Schema.Length>0){rowNamespace+="."+design.Schema;}
        var rowType="global::"+rowNamespace+".@"+design.Name;
        var rowName=design.Name;
        if(design.Kind==DatabaseQueryKind.TableFunction){
            var element=FunctionElementType(design.ReturnType)??throw new ManagementException("Validation","列挙を返す関数の戻り型を確認してください。");
            rowName=SyntaxFactory.ParseTypeName(element).GetLastToken().ValueText;Identifier(rowName);rowType="global::"+rowNamespace+".@"+rowName;
        }
        var resultType=design.ReturnType;if(design.Kind!=DatabaseQueryKind.ScalarFunction){resultType=rowType;}
        var file="Views/"+design.Name+".cs";if(design.Schema.Length>0){file="Views/"+design.Schema+"/"+design.Name+".cs";}
        var row="public readonly record struct @"+rowName+"("+string.Join(",",design.Columns.Select(column=>column.Type+" @"+column.Name))+")";
        if(design.Kind==DatabaseQueryKind.View){
            // [ビュー/形] 本体をQueryへ直接書く。列を指定した場合は手書きの列の形で作り、本体の結果が匿名型なら列を推論する形へ切り替える。
            var viewResult=ViewObjectResult;var viewColumns="";
            if(design.Columns.Length>0){viewResult="global::System.Collections.Generic.IEnumerable<"+rowType+">";viewColumns="("+string.Join(",",design.Columns.Select(column=>column.Type+" @"+column.Name))+")";}
            // [改行] 利用者の本文はそのまま差し込み、Managerが作る部分だけ書き込み先のファイルの改行に合わせる。
            string ViewRow(string newLine)=>"public readonly partial record struct @"+rowName+viewColumns+"{public static "+viewResult+" Query("+TypeName(container)+" container){"+newLine+design.Body+newLine+"}}";
            var view=WriteQueryRecord(definition,file,rowNamespace,design.Name,ViewRow,design.OriginalPath is not null);
            return SynchronizeViewResult(view,rowNamespace+"."+design.Name,true);
        }
        var helper="ScalarFunction";if(design.Kind==DatabaseQueryKind.TableFunction){helper="TableFunction";file=file.Replace("Views/","Functions/",StringComparison.Ordinal);definition=WriteQueryRecord(definition,file,rowNamespace,rowName,_=>row+";",design.OriginalPath is not null);}
        compilation=Compilation(definition);container=Container(compilation,definition);owner=Owner(container,design.Schema);
        var receiver="this";if(design.Schema.Length>0){receiver="("+TypeName(container)+")this.Database";}
        var binding="";if(bindContainer){binding="var container="+receiver+";";}
        var files=definition.Source.Files.ToArray();var declaration=Declaration(owner);
        var newLine=SourceNewLine.Of(files.Single(item=>item.Path==declaration.SyntaxTree.FilePath).Text);
        var member="public global::Seamless.Database."+helper+"<"+resultType+"> @"+design.Name+"("+string.Join(",",design.Parameters.Select(parameter=>parameter.Type+" @"+parameter.Name))+")=>this."+helper+"<"+resultType+">(()=>{"+binding+newLine+design.Body+newLine+"});";
        var edit=AppendMember(declaration,member);
        if(design.OriginalPath is not null){var syntax=(MethodDeclarationSyntax)owner.GetMembers(design.Name).OfType<IMethodSymbol>().Single().DeclaringSyntaxReferences.Single().GetSyntax();edit=(syntax.SpanStart,syntax.Span.Length,RewriteFunctionDeclaration(syntax,(MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(member)!));}
        for(var index=0;index<files.Length;index++){
            if(files[index].Path!=declaration.SyntaxTree.FilePath){continue;}
            var text=files[index].Text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Text);
            var imports=declaration.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>().Select(item=>item.Name?.ToString()).ToHashSet(StringComparer.Ordinal);
            if(!imports.Contains("System.Linq")){text="using System.Linq;"+newLine+text;}
            if(design.Kind==DatabaseQueryKind.TableFunction&&!imports.Contains(rowNamespace)){text="using "+rowNamespace+";"+newLine+text;}
            files[index]=files[index] with{Text=text};
        }
        var result=definition with{Source=definition.Source with{Files=files}};
        return SynchronizeFunctionResult(result,design.Schema,design.Name);
    }
    private static string RewriteFunctionDeclaration(MethodDeclarationSyntax original,MethodDeclarationSyntax generated){
        if(original.ExpressionBody?.Expression is not InvocationExpressionSyntax originalCall||generated.ExpressionBody?.Expression is not InvocationExpressionSyntax generatedCall||originalCall.Expression is not MemberAccessExpressionSyntax{ Name:GenericNameSyntax originalHelper} originalAccess||generatedCall.Expression is not MemberAccessExpressionSyntax{Name:GenericNameSyntax generatedHelper}||QueryDefinitionArgument(originalCall) is not{Expression:ParenthesizedLambdaExpressionSyntax originalLambda} originalArgument||QueryDefinitionArgument(generatedCall)?.Expression is not ParenthesizedLambdaExpressionSyntax generatedLambda){throw new ManagementException("Unsupported","カスタムな関数の種類変更はC#で編集してください。");}
        var parameters=generated.ParameterList.Parameters.Select(parameter=>{
            var previous=original.ParameterList.Parameters.SingleOrDefault(item=>item.Identifier.ValueText==parameter.Identifier.ValueText);
            if(previous is not null){return previous.WithType(parameter.Type!.WithTriviaFrom(previous.Type!));}return parameter;
        }).ToArray();
        var helper=RetypeQueryGeneric(originalHelper,generatedHelper);
        var lambda=originalLambda.WithBody(generatedLambda.Body.WithTriviaFrom(originalLambda.Body));
        if(originalLambda.Body is ExpressionSyntax){lambda=lambda.WithArrowToken(originalLambda.ArrowToken.WithTrailingTrivia()).WithBody(generatedLambda.Body);}
        var call=originalCall.WithExpression(originalAccess.WithName(helper)).WithArgumentList(originalCall.ArgumentList.WithArguments(originalCall.ArgumentList.Arguments.Replace(originalArgument,originalArgument.WithExpression(lambda))));
        TypeSyntax returnType=generated.ReturnType.WithTriviaFrom(original.ReturnType);
        var originalReturn=original.ReturnType.DescendantNodesAndSelf().OfType<GenericNameSyntax>().FirstOrDefault(name=>name.Identifier.ValueText==originalHelper.Identifier.ValueText&&name.TypeArgumentList.Arguments.Count==1);
        var generatedReturn=generated.ReturnType.DescendantNodesAndSelf().OfType<GenericNameSyntax>().FirstOrDefault(name=>name.Identifier.ValueText==generatedHelper.Identifier.ValueText&&name.TypeArgumentList.Arguments.Count==1);
        if(originalReturn is not null&&generatedReturn is not null){returnType=original.ReturnType.ReplaceNode(originalReturn,RetypeQueryGeneric(originalReturn,generatedReturn));}
        var method=original.WithReturnType(returnType).WithParameterList(original.ParameterList.WithParameters(ObjectParameterList(original.ParameterList.Parameters,parameters))).WithExpressionBody(original.ExpressionBody.WithExpression(call));
        return method.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString();
    }
    private static GenericNameSyntax RetypeQueryGeneric(GenericNameSyntax original,GenericNameSyntax generated)=>original.WithIdentifier(generated.Identifier.WithTriviaFrom(original.Identifier)).WithTypeArgumentList(original.TypeArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(generated.TypeArgumentList.Arguments[0].WithTriviaFrom(original.TypeArgumentList.Arguments[0]))));
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
        var compilation=Compilation(updated);var container=Container(compilation,updated);IMethodSymbol method;
        if(design.Kind==DatabaseQueryKind.View){method=compilation.GetTypeByMetadataName(target.RecordType.Replace("global::","",StringComparison.Ordinal))!.GetMembers("Query").OfType<IMethodSymbol>().Single();}
        else{method=Owner(container,design.Schema).GetMembers(design.Name).OfType<IMethodSymbol>().Single();}
        var syntax=method.DeclaringSyntaxReferences.Single().GetSyntax() as MethodDeclarationSyntax;
        if(design.Kind==DatabaseQueryKind.View){
            var viewSyntax=syntax??throw new ManagementException("Unsupported","このビューのQueryを読めません。C#で編集してください。");
            var viewEdit=ViewQueryBodyEdit(viewSyntax,design.Body);
            updated=updated with{Source=new SourceEditor().Apply(updated.Source,new(updated.Source.Hash(),viewSyntax.SyntaxTree.FilePath,viewEdit.Start,viewEdit.Length,viewEdit.Text))};
            return SynchronizeViewResult(updated,target.RecordType,true);
        }
        if(syntax?.ExpressionBody?.Expression is not InvocationExpressionSyntax invocation||QueryDefinitionArgument(invocation)?.Expression is not ParenthesizedLambdaExpressionSyntax lambda){throw new ManagementException("Unsupported","処理本体の形式を解釈できません。C#で編集してください。");}
        var body=design.Body;
        var newLine=SourceNewLine.Of(updated.Source.Files.Single(item=>item.Path==syntax.SyntaxTree.FilePath).Text);
        if(design.Kind!=DatabaseQueryKind.View){
            _=ReadFunctionBody(method,container,design.Schema,out var bindContainer);
            if(bindContainer&&lambda.Body is BlockSyntax original&&original.Statements.FirstOrDefault() is LocalDeclarationStatementSyntax binding){body=binding.ToString()+newLine+body;}
        }
        int start,length;string replacement;
        if(lambda.Body is BlockSyntax block){start=block.OpenBraceToken.Span.End;length=block.CloseBraceToken.SpanStart-start;replacement=newLine+body+newLine;}
        else{start=lambda.ArrowToken.Span.End;length=lambda.Body.FullSpan.End-start;replacement="{"+newLine+body+newLine+"}";}
        var source=new SourceEditor().Apply(updated.Source,new(updated.Source.Hash(),syntax.SyntaxTree.FilePath,start,length,replacement));
        updated=updated with{Source=source};
        if(design.Kind==DatabaseQueryKind.View){return SynchronizeViewResult(updated,target.RecordType);}
        return SynchronizeFunctionResult(updated,design.Schema,design.Name);
    }
    private static Definition WriteQueryRecord(Definition definition,string path,string ns,string name,Func<string,string> text,bool editing){
        var files=definition.Source.Files.ToList();
        var symbol=Compilation(definition).GetTypeByMetadataName(ns+"."+name);
        if(editing&&symbol is not null){
            var syntax=Declaration(symbol);var index=files.FindIndex(file=>file.Path==syntax.SyntaxTree.FilePath);
            if(syntax is not RecordDeclarationSyntax||syntax.Members.OfType<MethodDeclarationSyntax>().Any(method=>method.Identifier.ValueText!="Query")){throw new ManagementException("Unsupported","カスタムな結果型はC#で編集してください。");}
            files[index]=files[index] with{Text=files[index].Text.Remove(syntax.SpanStart,syntax.Span.Length).Insert(syntax.SpanStart,text(SourceNewLine.Of(files[index].Text)))};
        }else{
            if(symbol is not null||files.Any(file=>file.Path==path)){throw new ManagementException("Conflict","結果型またはソースファイルが既にあります。");}
            // 新しいファイルはLFで作る。AppendLineはWindowsでCRLFとなり、同じ定義でもOSごとにソースと成果物のハッシュが変わる。
            var newLine=SourceNewLine.Default;
            var parts=ns.Split('.');var builder=new System.Text.StringBuilder("using System.Linq;"+newLine);
            foreach(var part in parts){builder.Append("namespace @").Append(part).Append('{').Append(newLine);}
            builder.Append(text(newLine)).Append(newLine);foreach(var part in parts){builder.Append('}').Append(newLine);}
            files.Add(new(path,builder.ToString()));
        }
        return definition with{Source=definition.Source with{Files=files.ToArray()}};
    }
}
