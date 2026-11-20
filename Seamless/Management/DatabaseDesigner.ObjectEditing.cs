using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public sealed record ObjectDefinitionPart(string Id,string Role,string Path,int Start,int Length,string Text);
public sealed record ObjectDefinitionDesign(string DefinitionId,CatalogObject Object,string Fingerprint,ObjectDefinitionPart[] Parts,ColumnDesign[] Columns,ParameterDesign[] Parameters,string ReturnType,bool StructureAvailable,string? StructureUnavailableReason=null){
}
public sealed record ObjectDefinitionEdit(Definition? Definition,DiagnosticInfo[] Diagnostics){public bool Succeeded=>this.Definition is not null&&this.Diagnostics.Length==0;}

public static partial class DatabaseDesigner{
    private sealed record EditableObject(INamedTypeSymbol? Row,INamedTypeSymbol? Key,ISymbol? Member);

    public static ObjectDefinitionDesign ReadObjectDefinition(Definition definition,CatalogObject target){
        SourceEditor.Validate(definition.Source);
        var compilation=Compilation(definition);
        var selected=ResolveEditableObject(definition,target,compilation);
        var parts=new List<ObjectDefinitionPart>();
        AddType(selected.Row,"Record");if(!SymbolEqualityComparer.Default.Equals(selected.Key,selected.Row)){AddType(selected.Key,"Key");}
        if(selected.Member is{} member){
            foreach(var reference in member.DeclaringSyntaxReferences){
                var syntax=reference.GetSyntax();
                if(syntax.SyntaxTree.FilePath==GeneratedTablesPath){continue;}
                if(parts.Any(part=>part.Path==syntax.SyntaxTree.FilePath&&part.Start<=syntax.FullSpan.Start&&part.Start+part.Length>=syntax.FullSpan.End)){continue;}
                Add(syntax,"Member",member.ToDisplayString());
            }
        }
        if(parts.Count==0){throw new ManagementException("Unsupported","対象の編集可能な元宣言がありません。");}
        var columns=ReadEditableColumns(selected.Row,selected.Key,target.Kind=="Table").Select(column=>column with{Expression=definition.Migration?.Tables.SingleOrDefault(table=>table.Target==target.Id)?.Columns.SingleOrDefault(item=>item.Target==column.Name)?.Expression}).ToArray();
        var parameters=(selected.Member as IMethodSymbol)?.Parameters.Where(parameter=>!parameter.IsThis&&!InjectedObjectParameter(parameter)).Select(parameter=>new ParameterDesign(parameter.Name,TypeName(parameter.Type))).ToArray()??[];
        var returnType="";
        if(selected.Member is IMethodSymbol method){returnType=TypeName(method.ReturnType);}
        else if(target.Kind=="Property"&&selected.Member is IPropertySymbol property){returnType=TypeName(property.Type);}
        else if(selected.Row is{} row){returnType=TypeName(row);}
        var available=true;string? reason=null;
        if(selected.Key is not null&&SymbolEqualityComparer.Default.Equals(selected.Key,selected.Row)){available=false;reason="キー型と行型を共有する定義はC#で編集してください。";}
        if(selected.Row is null&&selected.Member is not IMethodSymbol&&target.Kind!="Property"){available=false;reason="構造を編集できる列または引数がありません。C#で編集してください。";}
        if(selected.Row is{} type&&type.DeclaringSyntaxReferences.Any(reference=>reference.GetSyntax() is TypeDeclarationSyntax syntax&&syntax.Members.OfType<PropertyDeclarationSyntax>().Any(property=>property.ExpressionBody is not null||property.AccessorList?.Accessors.Any(accessor=>accessor.Body is not null||accessor.ExpressionBody is not null)==true))){available=false;reason="カスタムプロパティの処理はC#で編集してください。";}
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",parts.OrderBy(part=>part.Id,StringComparer.Ordinal).Select(part=>part.Id+"\n"+part.Text)))));
        return new(definition.Id,target,fingerprint,parts.ToArray(),columns,parameters,returnType,available,reason);

        void AddType(INamedTypeSymbol? type,string role){
            if(type is null){return;}
            var ordinal=0;
            foreach(var reference in type.DeclaringSyntaxReferences.OrderBy(reference=>reference.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(reference=>reference.Span.Start)){
                Add(reference.GetSyntax(),role,type.ToDisplayString()+"#"+ordinal++);
            }
        }
        void Add(SyntaxNode syntax,string role,string identity){
            var path=syntax.SyntaxTree.FilePath;
            var source=definition.Source.Files.Single(file=>file.Path==path).Text;
            parts.Add(new(role+":"+identity+":"+path,role,path,syntax.FullSpan.Start,syntax.FullSpan.Length,source.Substring(syntax.FullSpan.Start,syntax.FullSpan.Length)));
        }
    }

    public static ObjectDefinitionEdit ApplyObjectSource(Definition latest,ObjectDefinitionDesign baseline,string partId,string replacement){
        var current=CheckObjectBaseline(latest,baseline);
        var part=current.Parts.SingleOrDefault(part=>part.Id==partId)??throw new ManagementException("Conflict","対象の宣言が変更されています。入力を保持して再読み込みしてください。");
        var source=new SourceEditor().Apply(latest.Source,new(latest.Source.Hash(),part.Path,part.Start,part.Length,replacement));
        var candidate=latest with{Source=source with{Revision=Guid.NewGuid().ToString("N")}};
        var diagnostics=ObjectSyntaxDiagnostics(candidate);
        if(diagnostics.Length>0){return new(null,diagnostics);}
        try{_=ReadObjectDefinition(candidate,baseline.Object);}
        catch(ManagementException exception){return new(null,[new("ObjectBinding",exception.Message,part.Path,part.Start,replacement.Length,"Error")]);}
        return new(candidate,[]);
    }

    /// <summary>既存宣言を差分編集する。列の移行式はnullで保持し、空白文字列で明示解除する。</summary>
    public static ObjectDefinitionEdit ApplyObjectStructure(Definition latest,ObjectDefinitionDesign baseline,ColumnDesign[] columns,ParameterDesign[] parameters,string returnType){
        var current=CheckObjectBaseline(latest,baseline);
        if(!current.StructureAvailable){throw new ManagementException("Unsupported",current.StructureUnavailableReason!);}
        if(baseline.Object.Kind=="Table"&&ManagedFilePath(latest) is not null){
            columns=columns.Select(column=>column with{Type=ManagedColumnType(column.Type)}).ToArray();
        }
        if(columns.Select(column=>column.Name).Distinct(StringComparer.Ordinal).Count()!=columns.Length||parameters.Select(parameter=>parameter.Name).Distinct(StringComparer.Ordinal).Count()!=parameters.Length){throw new ManagementException("Validation","列名・引数名の重複を解消してください。");}
        foreach(var column in columns){Identifier(column.Name);ValidateObjectType(column.Type);}
        foreach(var parameter in parameters){Identifier(parameter.Name);ValidateObjectType(parameter.Type);}
        var compilation=Compilation(latest);var selected=ResolveEditableObject(latest,baseline.Object,compilation);
        var edits=new List<(string Path,int Start,int Length,string Text)>();
        var function=baseline.Object.Kind is "ScalarFunction" or "TableFunction";
        if(selected.Row is{} row&&(!function||!current.Columns.SequenceEqual(columns))){EditType(row,columns,baseline.Object.Kind=="Table"&&selected.Key is null);}
        if(selected.Key is{} key){
            var keyColumns=columns.Where(column=>column.PrimaryKey).ToArray();
            if(keyColumns.Length==0){throw new ManagementException("Validation","主キーを指定してください。");}
            EditType(key,keyColumns,false);
        }
        if(selected.Member is IMethodSymbol method){
            var syntax=method.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as MethodDeclarationSyntax??throw new ManagementException("Unsupported","このメソッドはC#で編集してください。");
            if(!function||!current.Parameters.SequenceEqual(parameters)){
                var old=syntax.ParameterList.Parameters;
                var changed=parameters.Select(parameter=>{
                    var existing=old.SingleOrDefault(item=>item.Identifier.ValueText==parameter.Name);
                    if(existing is not null){return existing.WithType(SyntaxFactory.ParseTypeName(parameter.Type).WithTriviaFrom(existing.Type!));}
                    return SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter.Name)).WithType(SyntaxFactory.ParseTypeName(parameter.Type+" "));
                }).ToArray();
                var queue=new Queue<ParameterSyntax>(changed);var combined=new List<ParameterSyntax>();
                foreach(var parameter in method.Parameters){
                    if(InjectedObjectParameter(parameter)){combined.Add(old.Single(item=>item.Identifier.ValueText==parameter.Name));}
                    else if(queue.TryDequeue(out var next)){combined.Add(next);}
                }
                while(queue.TryDequeue(out var next)){combined.Add(next);}
                var parameterList=syntax.ParameterList.WithParameters(ObjectParameterList(old,combined));
                edits.Add((syntax.SyntaxTree.FilePath,syntax.ParameterList.SpanStart,syntax.ParameterList.Span.Length,parameterList.ToString()));
            }
            if(returnType!=current.ReturnType){
                ValidateObjectType(returnType);
                edits.Add((syntax.SyntaxTree.FilePath,syntax.ReturnType.SpanStart,syntax.ReturnType.Span.Length,returnType));
            }
        }
        // 明示Tableのキー抽出式も、既存の受け手・属性・本文の外側を保持して列だけ更新する。
        if(selected.Member is IPropertySymbol scalar&&baseline.Object.Kind=="Property"&&returnType!=current.ReturnType){
            ValidateObjectType(returnType);
            if(FunctionElementType(returnType) is not null){throw new ManagementException("Validation","プロパティの戻り型には1件の値の型を指定してください。");}
            var declaration=(PropertyDeclarationSyntax)scalar.DeclaringSyntaxReferences.Single().GetSyntax();
            edits.Add((declaration.SyntaxTree.FilePath,declaration.Type.SpanStart,declaration.Type.Span.Length,returnType));
        }
        if(selected.Member is IPropertySymbol property&&selected.Key is not null){
            var syntax=property.DeclaringSyntaxReferences.Single().GetSyntax();
            var lambda=syntax.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>().SingleOrDefault();
            if(lambda?.Body is not (ImplicitObjectCreationExpressionSyntax or ObjectCreationExpressionSyntax)){throw new ManagementException("Unsupported","カスタムなキー抽出はC#で編集してください。");}
            var arguments=SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(columns.Where(column=>column.PrimaryKey).Select(column=>SyntaxFactory.Argument(SyntaxFactory.ParseExpression(lambda.Parameter.Identifier.Text+".@"+column.Name)))));
            ArgumentListSyntax original;
            if(lambda.Body is ImplicitObjectCreationExpressionSyntax implicitCreation){original=implicitCreation.ArgumentList;}else{original=((ObjectCreationExpressionSyntax)lambda.Body).ArgumentList!;}
            edits.Add((syntax.SyntaxTree.FilePath,original.SpanStart,original.Span.Length,arguments.ToString()));
        }
        // キーの型を DB の生成器が作る表は、キーの選び方を row=>row.Key にそろえる(主キーの列は行の型の [Key] が決める)。
        if(selected.Member is IPropertySymbol generatedKeyTable&&selected.Key is null&&baseline.Object.Kind=="Table"){
            var syntax=generatedKeyTable.DeclaringSyntaxReferences.Single().GetSyntax();
            if(syntax.DescendantNodes().OfType<SimpleLambdaExpressionSyntax>().SingleOrDefault() is{Body:ImplicitObjectCreationExpressionSyntax or ObjectCreationExpressionSyntax} lambda){
                edits.Add((syntax.SyntaxTree.FilePath,lambda.Body.SpanStart,lambda.Body.Span.Length,lambda.Parameter.Identifier.Text+".Key"));
            }
        }
        var files=latest.Source.Files.Select(file=>{
            var text=file.Text;
            foreach(var edit in edits.Where(edit=>edit.Path==file.Path).OrderByDescending(edit=>edit.Start)){text=text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Text);}
            return file with{Text=text};
        }).ToArray();
        var candidate=latest with{Source=latest.Source with{Files=files,Revision=Guid.NewGuid().ToString("N")}};
        var diagnostics=ObjectSyntaxDiagnostics(candidate);if(diagnostics.Length>0){return new(null,diagnostics);}
        if(baseline.Object.Kind=="Table"){
            if(ManagedFilePath(candidate) is{} path){
                candidate=MergeManagedTableNamespaces(UpgradeManagedTableSetters(candidate),path,candidate.EntryType.Split('.')[0],null);
            }
            var migration=latest.Migration??new(1,[],[]);
            if(!(migration.NewTables??[]).Contains(baseline.Object.Id,StringComparer.Ordinal)){
                var previous=migration.Tables.SingleOrDefault(table=>table.Target==baseline.Object.Id);
                var mappings=columns.Select(column=>{
                    var old=previous?.Columns.SingleOrDefault(item=>item.Target==column.OriginalName);var source=column.OriginalName;var expression=column.Expression;
                    if(old is not null){source=old.Source;}
                    if(expression is null){expression=old?.Expression;}
                    else if(string.IsNullOrWhiteSpace(expression)){expression=null;}
                    return new ColumnMigration(column.Name,source,expression);
                }).ToArray();
                var dropped=(previous?.DroppedColumns??[]).Concat(current.Columns.Where(column=>!columns.Any(next=>(next.OriginalName??next.Name)==column.Name)).Select(column=>previous?.Columns.SingleOrDefault(item=>item.Target==column.Name)?.Source??column.Name)).Distinct(StringComparer.Ordinal).ToArray();
                var table=new TableMigration(baseline.Object.Id,previous?.Source??baseline.Object.Id,mappings,dropped);
                candidate=candidate with{Migration=migration with{Tables=[..migration.Tables.Where(item=>item.Target!=baseline.Object.Id),table]}};
            }
        }
        _=ReadObjectDefinition(candidate,baseline.Object);return new(candidate,[]);

        void EditType(INamedTypeSymbol type,ColumnDesign[] desired,bool keyAttributes){
            var declarations=type.DeclaringSyntaxReferences.Select(reference=>reference.GetSyntax()).OfType<TypeDeclarationSyntax>().OrderBy(syntax=>syntax.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(syntax=>syntax.SpanStart).ToArray();
            if(declarations.Length==0){throw new ManagementException("Unsupported","外部の型は構造画面で変更できません。");}
            if(keyAttributes&&desired.All(column=>column.PrimaryKey)&&!declarations.Any(declaration=>declaration.DescendantNodes().OfType<AttributeSyntax>().Any(ObjectKeyAttribute))){keyAttributes=false;}
            var properties=declarations.SelectMany(declaration=>declaration.Members.OfType<PropertyDeclarationSyntax>()).Where(property=>property.Modifiers.Any(SyntaxKind.PublicKeyword)&&!property.Modifiers.Any(SyntaxKind.StaticKeyword)).ToArray();
            var positional=declarations.OfType<RecordDeclarationSyntax>().SingleOrDefault(record=>record.ParameterList is not null);
            if(positional is not null){
                var original=positional.ParameterList!;
                var newParameters=desired.Where(column=>original.Parameters.Any(parameter=>parameter.Identifier.ValueText==(column.OriginalName??column.Name))||!properties.Any(property=>property.Identifier.ValueText==(column.OriginalName??column.Name))).Select(column=>{
                    var old=original.Parameters.SingleOrDefault(parameter=>parameter.Identifier.ValueText==(column.OriginalName??column.Name));
                    var next=SyntaxFactory.Parameter(SyntaxFactory.Identifier(column.Name)).WithType(SyntaxFactory.ParseTypeName(column.Type+" "));
                    if(old is not null){next=old.WithType(SyntaxFactory.ParseTypeName(column.Type).WithTriviaFrom(old.Type!)).WithIdentifier(SyntaxFactory.Identifier(column.Name).WithTriviaFrom(old.Identifier));}
                    if(keyAttributes){
                        var leading=next.GetLeadingTrivia();next=next.WithoutLeadingTrivia();
                        var lists=next.AttributeLists.Select(list=>list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute=>!ObjectKeyAttribute(attribute))))).Where(list=>list.Attributes.Count>0).ToList();
                        if(column.PrimaryKey){lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::Seamless.Database.Key")))).WithTarget(SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.PropertyKeyword))));}
                        next=next.WithAttributeLists(SyntaxFactory.List(lists)).WithLeadingTrivia(leading);
                    }
                    return next;
                });
                edits.Add((positional.SyntaxTree.FilePath,original.SpanStart,original.Span.Length,original.WithParameters(ObjectParameterList(original.Parameters,newParameters.ToArray())).ToFullString()));
            }
            foreach(var property in properties){
                var column=desired.SingleOrDefault(column=>(column.OriginalName??column.Name)==property.Identifier.ValueText);
                if(column is null){edits.Add((property.SyntaxTree.FilePath,property.FullSpan.Start,property.FullSpan.Length,""));continue;}
                var next=property.WithType(SyntaxFactory.ParseTypeName(column.Type).WithTriviaFrom(property.Type)).WithIdentifier(SyntaxFactory.Identifier(column.Name).WithTriviaFrom(property.Identifier));
                if(keyAttributes){
                    var lists=next.AttributeLists.Select(list=>list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute=>!ObjectKeyAttribute(attribute))))).Where(list=>list.Attributes.Count>0).ToList();
                    if(column.PrimaryKey){lists.Add(SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::Seamless.Database.Key")))));}
                    next=next.WithAttributeLists(SyntaxFactory.List(lists));
                }
                edits.Add((property.SyntaxTree.FilePath,property.SpanStart,property.Span.Length,next.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
            }
            var added=desired.Where(column=>positional is null&&!properties.Any(property=>property.Identifier.ValueText==(column.OriginalName??column.Name))).ToArray();
            if(added.Length==0){return;}
            var owner=declarations[0];
            if(owner.CloseBraceToken.IsMissing||!owner.CloseBraceToken.IsKind(SyntaxKind.CloseBraceToken)){throw new ManagementException("Unsupported","この型への列追加はC#で編集してください。");}
            var text=string.Join("\n",added.Select(column=>{
                var attribute="";if(keyAttributes&&column.PrimaryKey){attribute="[global::Seamless.Database.Key] ";}
                var partial="";if(properties.Any(property=>property.Modifiers.Any(SyntaxKind.PartialKeyword))){partial="partial ";}
                return attribute+"public "+partial+column.Type+" @"+column.Name+"{get;init;}";
            }));
            edits.Add((owner.SyntaxTree.FilePath,owner.CloseBraceToken.SpanStart,0,GeneratedText("\n"+text+"\n",owner)));
        }
    }

    private static ObjectDefinitionDesign CheckObjectBaseline(Definition latest,ObjectDefinitionDesign baseline){
        if(latest.Id!=baseline.DefinitionId){throw new ManagementException("Conflict","編集対象の定義が異なります。");}
        var current=ReadObjectDefinition(latest,baseline.Object);
        if(current.Fingerprint!=baseline.Fingerprint){throw new ManagementException("Conflict","同じ対象が別の操作で変更されています。入力を保持して差分を確認してください。");}
        return current;
    }
    internal static ObjectDefinitionDesign RebaseFunctionStructure(Definition latest,CatalogObject target,ObjectDefinitionDesign baseline,bool editsParameters,bool editsResult){
        var current=ReadObjectDefinition(latest,target);
        if(editsParameters&&!baseline.Parameters.SequenceEqual(current.Parameters)
            ||editsResult&&(baseline.Object.Kind!=current.Object.Kind||baseline.ReturnType!=current.ReturnType||!baseline.Columns.SequenceEqual(current.Columns))){throw Conflict();}
        if(current.Fingerprint==baseline.Fingerprint){return current;}
        var previousMethod=Method(baseline);
        var currentMethod=Method(current);
        if(previousMethod is null||currentMethod is null
            ||editsParameters&&!SyntaxFactory.AreEquivalent(previousMethod.ParameterList,currentMethod.ParameterList)
            ||editsResult&&(!SyntaxFactory.AreEquivalent(previousMethod.ReturnType,currentMethod.ReturnType)||!ResultDeclarations(baseline).SequenceEqual(ResultDeclarations(current)))){
            throw Conflict();
        }
        return current;

        static ManagementException Conflict()=>new("Conflict","同じ対象が別の操作で変更されています。入力を保持して差分を確認してください。");
        static MethodDeclarationSyntax? Method(ObjectDefinitionDesign design)=>design.Parts.SingleOrDefault(part=>part.Role=="Member") is{} part?SyntaxFactory.ParseMemberDeclaration(part.Text) as MethodDeclarationSyntax:null;
        static IEnumerable<(string Id,string Text)> ResultDeclarations(ObjectDefinitionDesign design)=>design.Parts.Where(part=>part.Role=="Record").Select(part=>(part.Id,part.Text));
    }
    private static DiagnosticInfo[] ObjectSyntaxDiagnostics(Definition definition)=>Compilation(definition).SyntaxTrees.SelectMany(tree=>tree.GetDiagnostics()).Where(diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(SourceEditor.ToDiagnostic).ToArray();
    private static bool InjectedObjectParameter(IParameterSymbol parameter)=>parameter.Type.ToDisplayString()=="Seamless.Management.Server.ManagementExecutionContext";
    private static SeparatedSyntaxList<ParameterSyntax> ObjectParameterList(SeparatedSyntaxList<ParameterSyntax> original,IReadOnlyList<ParameterSyntax> values){
        var separators=original.GetSeparators().ToArray();var tokens=new List<SyntaxNodeOrToken>();
        for(var index=0;index<values.Count;index++){
            if(index>0){var separator=SyntaxFactory.Token(SyntaxKind.CommaToken);if(index-1<separators.Length){separator=separators[index-1];}tokens.Add(separator);}
            tokens.Add(values[index]);
        }
        return SyntaxFactory.SeparatedList<ParameterSyntax>(tokens);
    }
    private static void ValidateObjectType(string type){if(string.IsNullOrWhiteSpace(type)||SyntaxFactory.ParseTypeName(type).ContainsDiagnostics){throw new ManagementException("Validation","C#型を確認してください: "+type);}}
    private static bool ObjectKeyAttribute(AttributeSyntax attribute)=>attribute.Name.ToString().Split('.').Last() is "Key" or "KeyAttribute" or "PrimaryKey" or "PrimaryKeyAttribute";
    private static ColumnDesign[] ReadEditableColumns(INamedTypeSymbol? row,INamedTypeSymbol? key,bool table){
        if(row is null){return [];}
        // キーの型が生成される型(設計器から解決できない)なら、行の型の [Key] から主キーを読む。
        var keyNames=key is null||key.TypeKind==TypeKind.Error?null:key.GetMembers().OfType<IPropertySymbol>().Select(property=>property.Name).ToHashSet(StringComparer.Ordinal);
        var properties=row.GetMembers().OfType<IPropertySymbol>().Where(property=>!property.IsStatic&&!property.IsIndexer&&property.DeclaredAccessibility==Accessibility.Public&&property.Name!="Key").ToArray();
        var attributed=properties.Where(property=>property.DeclaringSyntaxReferences.Any(reference=>{
            var syntax=reference.GetSyntax();if(syntax is PropertyDeclarationSyntax declaration){return declaration.AttributeLists.SelectMany(list=>list.Attributes).Any(ObjectKeyAttribute);}if(syntax is ParameterSyntax parameter){return parameter.AttributeLists.SelectMany(list=>list.Attributes).Any(ObjectKeyAttribute);}return false;
        })).Select(property=>property.Name).ToHashSet(StringComparer.Ordinal);
        return properties.Select(property=>{
            var syntax=property.DeclaringSyntaxReferences.Select(reference=>reference.GetSyntax()).FirstOrDefault();
            var type=TypeName(property.Type);if(syntax is PropertyDeclarationSyntax declaration){type=declaration.Type.ToString();}else if(syntax is ParameterSyntax parameter&&parameter.Type is{} parameterType){type=parameterType.ToString();}
            var primary=false;if(table){primary=keyNames?.Contains(property.Name)??(attributed.Count==0||attributed.Contains(property.Name));}
            return new ColumnDesign(property.Name,type,primary,property.Name);
        }).ToArray();
    }
    private static EditableObject ResolveEditableObject(Definition definition,CatalogObject target,CSharpCompilation compilation){
        ISymbol? member=null;INamedTypeSymbol? row=null;INamedTypeSymbol? key=null;
        if(definition.Kind==DefinitionKind.Query){
            member=PlanTargetMethod(compilation.GetTypeByMetadataName(definition.EntryType)?.GetMembers(definition.EntryMember).OfType<IMethodSymbol>()??[]);
        }else if(target.Kind is "TableFunction" or "ScalarFunction" or "Property"||target.Kind=="View"&&FindQueryMember(compilation,definition,target.Id) is not null){
            // ビュー・関数は IReader への拡張メンバー。行型は ISet<行> の行(スカラー関数は行型を持たない)。
            var query=FindQueryMember(compilation,definition,target.Id)??throw new ManagementException("NotFound","対象の元宣言がありません。");
            member=query.Member;
            if(query.Kind!="ScalarFunction"){row=query.Result as INamedTypeSymbol;}
            if(row?.DeclaringSyntaxReferences.Length==0){row=null;}
            return new(row,null,member);
        }else{
            var container=compilation.GetTypeByMetadataName(definition.EntryType);
            ISymbol[] members=[];if(container is not null){members=Owner(container,target.Schema).GetMembers(target.Name).Where(symbol=>!symbol.IsImplicitlyDeclared).ToArray();}
            if(members.Length>1){throw new ManagementException("Unsupported","対象のオーバーロードを一意に選択できません。");}
            member=members.SingleOrDefault();
        }
        if(member is IPropertySymbol property&&property.Type is INamedTypeSymbol table&&table.Name=="Table"&&table.TypeArguments.Length==2){
            // キーの型が DB の生成器が作る型(設計器から解決できない)なら、主キーは行の型の [Key] で表すので、キーの型は編集の対象にしない。
            key=table.TypeArguments[0] is INamedTypeSymbol{TypeKind:not TypeKind.Error} declaredKey?declaredKey:null;row=table.TypeArguments[1] as INamedTypeSymbol;}
        else{
            ITypeSymbol? result=null;if(member is IMethodSymbol method){result=method.ReturnType;}else if(member is IPropertySymbol view){result=view.Type;}
            if(result is INamedTypeSymbol named){row=named;if(named.IsGenericType&&named.TypeArguments.Length>0){row=named.TypeArguments[^1] as INamedTypeSymbol;}}
        }
        var declared=compilation.GetTypeByMetadataName(target.RecordType.Replace("global::","",StringComparison.Ordinal));
        if(member is null&&declared?.DeclaringSyntaxReferences.Length>0){row=declared;}
        if(row?.DeclaringSyntaxReferences.Length==0){row=null;}
        if(row is null&&member is null){throw new ManagementException("NotFound","対象の元宣言がありません。");}
        return new(row,key,member);
    }
}
