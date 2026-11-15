using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public sealed record ColumnDesign(string Name,string Type,bool PrimaryKey,string? OriginalName=null,string? Expression=null);
public sealed record ForeignKeyDesign(string Name,string ReferencedTable,string[] Columns,string[] ReferencedColumns);
public sealed record TableDesign(string Name,string Schema,ColumnDesign[] Columns,ForeignKeyDesign[] ForeignKeys,string? OriginalPath=null){public string Path=>string.Join(".",new[]{this.Schema,this.Name}.Where(value=>value.Length>0));}
/// <summary>ソースの対象宣言だけを変更する。GUIで解釈できない宣言は拒否し、C#編集へ渡す。</summary>
public static partial class DatabaseDesigner{
    public static Definition Empty(string id,string name)=>new(id,name,DefinitionKind.Database,"UserCode.ManagedDb","",new("1",[new("Database.cs","using Seamless.Database;\nnamespace UserCode;\ninternal static partial class DatabaseGenerator;\npublic sealed partial class ManagedDb(DatabaseContainerOptions options):DatabaseContainer(options){\n}\n")],[]));
    private static CSharpCompilation Compilation(Definition definition){
        var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:definition.Source.Defines??[]);
        var compilation=CSharpCompilation.Create("Design",definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,options,path:file.Path)),SourceEditor.PlatformReferences());
        var generated=GeneratedTableProperties(compilation,definition);
        if(generated.Length==0){return compilation;}
        return compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(generated,options,path:GeneratedTablesPath));
    }
    // [生成される表] Tables(・Work)の行の型だけで宣言した表は、表のプロパティを DB の生成器が作る(Shared/Database.Generation の CollectDeclaredObjects)。
    // 設計器は生成器を通さずに読むので、同じ規則で表のプロパティだけを利用者のソースではない仮のファイルへ合成し、手書きの表と同じく列挙・読み取り・列の編集をできるようにする。
    // キーの型は生成器が作る型のまま(設計器からは解決しない)にし、主キーは行の型の[Key]で表す。仮のファイルは書き換えない。
    internal const string GeneratedTablesPath="<seamless-generated>/Tables.g.cs";
    private static bool IsGeneratedTable(IPropertySymbol property)=>property.DeclaringSyntaxReferences.Any(reference=>reference.SyntaxTree.FilePath==GeneratedTablesPath);
    private static string GeneratedTableProperties(CSharpCompilation compilation,Definition definition){
        if(compilation.GetTypeByMetadataName(definition.EntryType) is not{} container){return "";}
        var root=container.ContainingNamespace.IsGlobalNamespace?"":container.ContainingNamespace.ToDisplayString();
        var handWritten=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var owners=new Dictionary<string,INamedTypeSymbol>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"");
        var builder=new System.Text.StringBuilder();
        foreach(var row in SourceTypes(compilation.Assembly.GlobalNamespace).Where(type=>type.TypeKind==TypeKind.Struct&&type.ContainingType is null)){
            var ns=row.ContainingNamespace.IsGlobalNamespace?"":row.ContainingNamespace.ToDisplayString();
            string? schema=null;
            foreach(var kind in new[]{"Tables","Work"}){
                var prefix=root.Length==0?kind:root+"."+kind;
                if(ns==prefix){schema="";}else if(ns.StartsWith(prefix+".",StringComparison.Ordinal)){schema=ns[(prefix.Length+1)..];}
                if(schema is not null){break;}
            }
            // 手書きの表のプロパティが使う行の型と、主キー([Key])の無い型は、生成器が作る表として扱わない。所属のスキーマが宣言されていなければ合成しない。
            if(schema is null||handWritten.Contains(row)||KeyColumns(row).Length==0||!owners.TryGetValue(schema,out var owner)||owner.GetMembers(row.Name).Length>0){continue;}
            var keyType="global::"+(root.Length==0?"Keys":root+".Keys")+(schema.Length==0?"":"."+schema)+".@"+row.Name;
            var property="public global::Seamless.Database.Table<"+keyType+","+TypeName(row)+"> @"+row.Name+"=>throw null!;";
            builder.Append(Wrap(owner,property)).Append('\n');
        }
        return builder.ToString();

        void Walk(INamedTypeSymbol owner,string prefix){
            if(!ancestors.Add(owner)){return;}
            try{
                owners.TryAdd(prefix,owner);
                foreach(var property in owner.GetMembers().OfType<IPropertySymbol>()){
                    if(property.Type is not INamedTypeSymbol named){continue;}
                    if(named.Name=="Table"&&named.TypeArguments.Length==2&&named.TypeArguments[1] is INamedTypeSymbol record){handWritten.Add(record);}
                    else if(named.BaseType?.Name=="DatabaseSchema"){Walk(named,prefix.Length==0?property.Name:prefix+"."+property.Name);}
                }
            }finally{ancestors.Remove(owner);}
        }
    }
    private static IEnumerable<INamedTypeSymbol> SourceTypes(INamespaceSymbol ns){
        foreach(var type in ns.GetTypeMembers()){if(type.DeclaringSyntaxReferences.Length>0){yield return type;}}
        foreach(var child in ns.GetNamespaceMembers()){foreach(var type in SourceTypes(child)){yield return type;}}
    }
    // 合成するメンバーを、所属の型(入れ子なら外側の型も)の partial 宣言で包む。
    private static string Wrap(INamedTypeSymbol owner,string member){
        var text="partial class @"+owner.Name+"{"+member+"}";
        for(var outer=owner.ContainingType;outer is not null;outer=outer.ContainingType){text="partial class @"+outer.Name+"{"+text+"}";}
        if(!owner.ContainingNamespace.IsGlobalNamespace){text="namespace "+owner.ContainingNamespace.ToDisplayString()+"{"+text+"}";}
        return text;
    }
    private static INamedTypeSymbol Container(CSharpCompilation compilation,Definition definition)=>compilation.GetTypeByMetadataName(definition.EntryType)??throw new ManagementException("Unsupported","DBの宣言を特定できません。C#で編集してください。");
    private static INamedTypeSymbol Owner(INamedTypeSymbol container,string schema){var owner=container;foreach(var part in schema.Split('.',StringSplitOptions.RemoveEmptyEntries)){owner=(owner.GetMembers(part).OfType<IPropertySymbol>().SingleOrDefault()?.Type as INamedTypeSymbol)??throw new ManagementException("Unsupported","スキーマの宣言を特定できません: "+schema);}return owner;}
    private static IPropertySymbol Property(INamedTypeSymbol container,string path){var split=path.LastIndexOf('.');var schema="";var name=path;if(split>=0){schema=path[..split];name=path[(split+1)..];}return Owner(container,schema).GetMembers(name).OfType<IPropertySymbol>().SingleOrDefault()??throw new ManagementException("NotFound","表の宣言がありません: "+path);}
    public static (string Path,int Start,int Length,string Text) TableSource(Definition definition,string path){
        var compilation=Compilation(definition);var property=Property(Container(compilation,definition),path);
        if(IsGeneratedTable(property)){var row=Declaration(RowTypes(property)[1]);return(row.SyntaxTree.FilePath,row.SpanStart,row.Span.Length,row.ToFullString());}
        var syntax=property.DeclaringSyntaxReferences.Single().GetSyntax();
        var text=syntax.ToFullString()+string.Concat(RowTypes(property).SelectMany(type=>type.DeclaringSyntaxReferences).Select(reference=>reference.GetSyntax().ToFullString()));
        return(syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,text);
    }
    private static INamedTypeSymbol[] RowTypes(IPropertySymbol property){if(property.Type is not INamedTypeSymbol{TypeArguments.Length:2} table||table.Name!="Table"||table.TypeArguments.Any(type=>type is not INamedTypeSymbol)){throw new ManagementException("Unsupported","この表はC#で編集してください。");}return table.TypeArguments.Cast<INamedTypeSymbol>().ToArray();}
    private static string TypeName(ITypeSymbol type)=>type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions|SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
    private static TypeDeclarationSyntax Declaration(INamedTypeSymbol type)=>type.DeclaringSyntaxReferences.SingleOrDefault()?.GetSyntax() as TypeDeclarationSyntax??throw new ManagementException("Unsupported","複数の宣言や外部型はC#で編集してください: "+type.Name);
    public static TableDesign Read(Definition definition,string path){
        var compilation=Compilation(definition);var property=Property(Container(compilation,definition),path);var types=RowTypes(property);var keyNames=KeyColumns(types[1]);var keys=keyNames.ToHashSet(StringComparer.Ordinal);
        if(keyNames.Length==0){throw new ManagementException("Unsupported","主キー([Key])の無い表はC#で編集してください。");}
        if(!IsGeneratedTable(property)){
            var syntax=(PropertyDeclarationSyntax)property.DeclaringSyntaxReferences.Single().GetSyntax();
            if(syntax.ExpressionBody?.Expression is not InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax{Expression:ThisExpressionSyntax,Name:GenericNameSyntax{Identifier.ValueText:"Table"}},ArgumentList.Arguments.Count:1} call||call.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax lambda){throw new ManagementException("Unsupported","カスタム実装の表はC#で編集してください。");}
            if(!IsKeySelector(lambda,keyNames)){throw new ManagementException("Unsupported","計算を含むキーの定義はC#で編集してください。");}
        }
        var existing=definition.Migration?.Tables.SingleOrDefault(table=>table.Target==path);
        var columns=Columns(types[1]).Select(item=>new ColumnDesign(item.Name,TypeName(item.Type),keys.Contains(item.Name),item.Name,existing?.Columns.SingleOrDefault(column=>column.Target==item.Name)?.Expression)).ToArray();
        var relations=property.GetAttributes().Where(attribute=>attribute.AttributeClass?.Name=="ForeignKeyAttribute").Select(attribute=>{var args=attribute.ConstructorArguments;return new ForeignKeyDesign((string)args[0].Value!,(string)args[1].Value!,args[2].Values.Select(value=>(string)value.Value!).ToArray(),args[3].Values.Select(value=>(string)value.Value!).ToArray());}).ToArray();
        var split=path.LastIndexOf('.');if(split<0){return new(path,"",columns,relations,path);}return new(path[(split+1)..],path[..split],columns,relations,path);
    }
    // [予約名] すべての入口がルートの入口を返す Database を持つので、表・スキーマ・ビュー・関数の名前に使えない(DB生成器の SDB002 と同じ)。
    private const string RootAccessName="Database";
    private static void ObjectName(string value){Identifier(value);if(value==RootAccessName){throw new ManagementException("Validation","名前 Database は、入口からルートへ戻る Database と重なるので使えません。");}}
    private static void Identifier(string value){if(!SyntaxFacts.IsValidIdentifier(value)){throw new ManagementException("Validation","有効なC#識別子を入力してください: "+value);}}
    private static string Literal(string value)=>SyntaxFactory.Literal(value).Text;
    // [改行] Managerが作る断片は書き込み先のファイルの改行に合わせる。利用者の本文は書き換えない。
    private static string FileNewLine(SyntaxNode node)=>SourceNewLine.Of(node.SyntaxTree.GetText().ToString());
    /// <summary>Managerが作った断片(利用者の本文を含まないもの)の改行を、書き込み先のファイルの改行へそろえる。</summary>
    private static string GeneratedText(string generated,SyntaxNode target){var newLine=FileNewLine(target);return newLine==SourceNewLine.Default?generated:generated.Replace(SourceNewLine.Default,newLine,StringComparison.Ordinal);}
    private static (int Start,int Length,string Text) AppendMember(TypeDeclarationSyntax owner,string member){
        var newLine=FileNewLine(owner);
        if(!owner.OpenBraceToken.IsMissing&&owner.OpenBraceToken.IsKind(SyntaxKind.OpenBraceToken)){return(owner.CloseBraceToken.SpanStart,0,newLine+member+newLine);}
        var updated=owner.WithSemicolonToken(default).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)).WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)).AddMembers(SyntaxFactory.ParseMemberDeclaration(member)!);return(owner.SpanStart,owner.Span.Length,updated.WithoutLeadingTrivia().WithoutTrailingTrivia().NormalizeWhitespace(eol:newLine).ToFullString());
    }
    public static string[] Tables(Definition definition)=>TableProperties(Container(Compilation(definition),definition)).Keys.ToArray();
    private static Dictionary<string,IPropertySymbol> TableProperties(INamedTypeSymbol container){
        var result=new Dictionary<string,IPropertySymbol>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"");
        return result;
        void Walk(INamedTypeSymbol type,string prefix){
            if(!ancestors.Add(type)){return;}
            try{
                foreach(var property in type.GetMembers().OfType<IPropertySymbol>()){
                    if(property.Type is not INamedTypeSymbol named){continue;}
                    if(named.Name=="Table"&&named.TypeArguments.Length==2){
                        if(!result.TryAdd(prefix+property.Name,property)){throw new ManagementException("Conflict","同名の表があります: "+prefix+property.Name);}
                    }else if(named.BaseType?.Name=="DatabaseSchema"){
                        Walk(named,prefix+property.Name+".");
                    }
                }
            }finally{ancestors.Remove(type);}
        }
    }
    private static Definition EnsureSchema(Definition definition,string schema){
        var parts=schema.Split('.',StringSplitOptions.RemoveEmptyEntries);var path="";foreach(var part in parts){ObjectName(part);var compilation=Compilation(definition);var container=Container(compilation,definition);var owner=Owner(container,path);if(owner.GetMembers(part).Length>0){
                if(owner.GetMembers(part).OfType<IPropertySymbol>().SingleOrDefault()?.Type is not INamedTypeSymbol type||!IsSchemaType(type)){throw new ManagementException("Conflict","スキーマ名が既存オブジェクトと競合しています: "+part);}
                path=string.Join(".",new[]{path,part}.Where(value=>value.Length>0));continue;
            }
            var full=string.Join(".",new[]{path,part}.Where(value=>value.Length>0));var ns=container.ContainingNamespace.ToDisplayString();var typeName="Schema_"+full.Replace('.','_');if(compilation.GetTypeByMetadataName(ns+"."+typeName) is not null){throw new ManagementException("Conflict","スキーマ型名が競合しています。");}
            var syntax=Declaration(owner);var receiver="this.Database";if(SymbolEqualityComparer.Default.Equals(owner,container)){receiver="this";}
            var text=GeneratedText("\npublic global::"+ns+"."+typeName+" "+part+"=>new("+receiver+");\n",syntax);var edit=AppendMember(syntax,text);var files=definition.Source.Files.Select(file=>{if(file.Path==syntax.SyntaxTree.FilePath){return file with{Text=file.Text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Text)};}return file;}).ToList();
            var fileName="Schemas/"+typeName+".cs";if(files.Any(file=>file.Path==fileName)){throw new ManagementException("Conflict","スキーマのソースファイルが既に存在します。");}
            files.Add(new(fileName,Format("namespace "+ns+";public sealed partial class "+typeName+"(global::Seamless.Database.DatabaseContainer db):global::Seamless.Database.DatabaseSchema(db,"+Literal(full)+"){ }")));definition=definition with{Source=definition.Source with{Files=files.ToArray()}};path=full;
        }return definition;
    }
    public static Definition Apply(Definition definition,TableDesign design){
        definition=EnsureSchema(definition,design.Schema);
        ObjectName(design.Name);foreach(var part in design.Schema.Split('.',StringSplitOptions.RemoveEmptyEntries)){ObjectName(part);}if(design.Columns.Length==0||!design.Columns.Any(column=>column.PrimaryKey)||design.Columns.Select(column=>column.Name).Distinct().Count()!=design.Columns.Length){throw new ManagementException("Validation","列と主キーを指定し、列名の重複を解消してください。");}
        foreach(var column in design.Columns){Identifier(column.Name);if(SyntaxFactory.ParseTypeName(column.Type).ContainsDiagnostics){throw new ManagementException("Validation","列の型が不正です: "+column.Name);}if(column.PrimaryKey&&column.Type.EndsWith('?')){throw new ManagementException("Validation","主キーをnull許容にはできません。");}}
        var compilation=Compilation(definition);var container=Container(compilation,definition);var edits=new List<(string File,int Start,int Length,string Text)>();var files=definition.Source.Files.ToList();var ns=container.ContainingNamespace.ToDisplayString();var owner=container;
        if(design.Schema.Length>0){owner=Owner(container,design.Schema);}
        IPropertySymbol? property=null;if(design.OriginalPath is not null){property=Property(container,design.OriginalPath);}if(owner.GetMembers(design.Name).Any(member=>!SymbolEqualityComparer.Default.Equals(member,property))){throw new ManagementException("Conflict","同名のオブジェクトがあります。");}
        string keyType,recordType;var shapeChanged=true;
        var generatedTable=property is not null&&IsGeneratedTable(property);
        if(generatedTable&&(design.OriginalPath!=design.Path||design.ForeignKeys.Length>0)){
            throw new ManagementException("Unsupported","Tablesの行の型だけで宣言した表の名前・所属・外部キーは、C#で編集してください。");
        }
        if(property is not null){var types=RowTypes(property);keyType=KeyTypeSyntax(property);recordType=TypeName(types[1]);var original=Read(definition,design.OriginalPath!);shapeChanged=!original.Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)).SequenceEqual(design.Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)));
            if(shapeChanged){
                if(Tables(definition).Where(path=>path!=design.OriginalPath).Select(path=>RowTypes(Property(container,path))[1]).Any(other=>SymbolEqualityComparer.Default.Equals(other,types[1]))){throw new ManagementException("Unsupported","行型を他の表と共有しています。影響を確認してC#で編集してください。");}
                ReplaceFields(types[1],design.Columns);
            }
        }
        else{
            var prefix=design.Schema.Replace('.','_');if(prefix.Length>0){prefix+="_";}prefix+=design.Name;
            keyType="global::"+ns+".Keys."+prefix;recordType="global::"+ns+".Tables."+prefix;
            if(compilation.GetTypeByMetadataName(ns+".Keys."+prefix) is not null||compilation.GetTypeByMetadataName(ns+".Tables."+prefix) is not null){throw new ManagementException("Conflict","行・キー型が既に存在します。C#で型を指定してください。");}
            // 行の型だけを Tables に書き、主キーの列に [Key] を付ける。キーの型 Keys.<名前> は DB の生成器が作る。
            var text="namespace "+ns+".Tables{public partial struct "+prefix+"{"+Fields(design.Columns)+"}}\n";
            var fileName="Tables/"+prefix+".cs";if(files.Any(file=>file.Path==fileName)){throw new ManagementException("Conflict","表のソースファイルが既に存在します。");}files.Add(new(fileName,Format("using System;\n"+text)));
        }
        var attributes=string.Join("\n",design.ForeignKeys.Select(relation=>"[global::Seamless.Database.ForeignKey("+Literal(relation.Name)+","+Literal(relation.ReferencedTable)+",new string[]{"+string.Join(",",relation.Columns.Select(Literal))+"},new string[]{"+string.Join(",",relation.ReferencedColumns.Select(Literal))+"})]"));
        var declaration=attributes+"\npublic global::Seamless.Database.Table<"+keyType+","+recordType+"> "+design.Name+"=>this.Table<"+keyType+","+recordType+">(row=>row.Key);\n";
        if(property is null){var type=Declaration(owner);var edit=AppendMember(type,GeneratedText(declaration,type));edits.Add((type.SyntaxTree.FilePath,edit.Start,edit.Length,edit.Text));}
        else if(!generatedTable){var syntax=(PropertyDeclarationSyntax)property.DeclaringSyntaxReferences.Single().GetSyntax();
            if(shapeChanged&&syntax.ExpressionBody?.Expression is not InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax{Expression:ThisExpressionSyntax,Name:GenericNameSyntax{Identifier.ValueText:"Table"}}}){throw new ManagementException("Unsupported","カスタム実装の表はC#で編集してください。");}
            var generated=(PropertyDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(declaration)!;
            if(!shapeChanged){generated=generated.WithExpressionBody(syntax.ExpressionBody).WithAccessorList(syntax.AccessorList).WithSemicolonToken(syntax.SemicolonToken);}
            var kept=syntax.AttributeLists.Select(list=>list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute=>!attribute.Name.ToString().EndsWith("ForeignKey",StringComparison.Ordinal)&&!attribute.Name.ToString().EndsWith("ForeignKeyAttribute",StringComparison.Ordinal))))).Where(list=>list.Attributes.Count>0);
            generated=generated.WithAttributeLists(SyntaxFactory.List(kept.Concat(generated.AttributeLists)));
            var original=Read(definition,design.OriginalPath!);if(original.Schema!=design.Schema){var targetOwner=Declaration(owner);edits.Add((syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,""));edits.Add((targetOwner.SyntaxTree.FilePath,targetOwner.CloseBraceToken.SpanStart,0,GeneratedText("\n"+generated.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()+"\n",targetOwner)));}
            else{edits.Add((syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,generated.WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));}
        }
        foreach(var group in edits.GroupBy(edit=>edit.File)){var index=files.FindIndex(file=>file.Path==group.Key);var text=files[index].Text;foreach(var edit in group.OrderByDescending(edit=>edit.Start)){text=text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Text);}files[index]=files[index] with{Text=text};}
        if(design.OriginalPath is not null&&design.OriginalPath!=design.Path){for(var index=0;index<files.Count;index++){
            var root=SyntaxFactory.ParseCompilationUnit(files[index].Text);var referenceAttributes=root.DescendantNodes().OfType<AttributeSyntax>().Where(attribute=>(attribute.Name.ToString().EndsWith("ForeignKey",StringComparison.Ordinal)||attribute.Name.ToString().EndsWith("ForeignKeyAttribute",StringComparison.Ordinal))&&attribute.ArgumentList?.Arguments.Count>=2&&attribute.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax literal&&literal.Token.ValueText==design.OriginalPath).ToArray();
            var updated=root.ReplaceNodes(referenceAttributes,(node,_)=>node.WithArgumentList(node.ArgumentList!.WithArguments(node.ArgumentList.Arguments.Replace(node.ArgumentList.Arguments[1],node.ArgumentList.Arguments[1].WithExpression(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,SyntaxFactory.Literal(design.Path)).WithTriviaFrom(node.ArgumentList.Arguments[1].Expression))))));files[index]=files[index] with{Text=updated.ToFullString()};
        }}
        var migration=definition.Migration??new(1,[],[]);var mappings=migration.Tables.ToList();
        var newTables=(migration.NewTables??[]).ToList();if(design.OriginalPath is null){newTables.Add(design.Path);}else if(newTables.Remove(design.OriginalPath)){newTables.Add(design.Path);}
        if(design.OriginalPath is not null&&!(migration.NewTables??[]).Contains(design.OriginalPath)){
            var original=Read(definition,design.OriginalPath);var previous=mappings.SingleOrDefault(table=>table.Target==design.OriginalPath);mappings.RemoveAll(table=>table.Target==design.OriginalPath);
            var columns=design.Columns.Select(column=>{var old=previous?.Columns.SingleOrDefault(item=>item.Target==column.OriginalName);string? source=column.OriginalName;if(old is not null){source=old.Source;}return new ColumnMigration(column.Name,source,column.Expression);}).ToArray();
            var removed=(previous?.DroppedColumns??[]).Concat(original.Columns.Where(column=>!design.Columns.Any(next=>next.OriginalName==column.Name)).Select(column=>{var old=previous?.Columns.SingleOrDefault(item=>item.Target==column.Name);if(old is not null){return old.Source;}return column.Name;}).OfType<string>()).Distinct().ToArray();
            mappings.Add(new(design.Path,previous?.Source??design.OriginalPath,columns,removed));
        }
        return definition with{Source=definition.Source with{Files=files.ToArray(),Revision=Guid.NewGuid().ToString("N")},Migration=migration with{Tables=mappings.ToArray(),NewTables=newTables.ToArray()}};
        void ReplaceFields(INamedTypeSymbol type,ColumnDesign[] columns){
            var syntax=Declaration(type);var properties=syntax.Members.OfType<PropertyDeclarationSyntax>().ToArray();if(properties.Any(item=>item.ExpressionBody is not null||item.Initializer is not null)){throw new ManagementException("Unsupported","カスタムプロパティを持つ型はC#で編集してください。");}
            var members=syntax.Members.Where(member=>member is not PropertyDeclarationSyntax).ToList();foreach(var column in columns){var original=properties.SingleOrDefault(item=>item.Identifier.ValueText==column.OriginalName);if(original is null){members.Add(SyntaxFactory.ParseMemberDeclaration(Field(column))!);}else{members.Add(WithKeyAttribute(original.WithType(SyntaxFactory.ParseTypeName(column.Type).WithTriviaFrom(original.Type)).WithIdentifier(SyntaxFactory.Identifier(column.Name).WithTriviaFrom(original.Identifier)),column.PrimaryKey));}}
            edits.Add((syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,syntax.WithMembers(SyntaxFactory.List(members)).WithoutLeadingTrivia().WithoutTrailingTrivia().ToFullString()));
        }
    }
    private static string Fields(IEnumerable<ColumnDesign> columns)=>string.Join("\n",columns.Select(Field));
    private static string Field(ColumnDesign column)=>(column.PrimaryKey?"[global::Seamless.Database.Key] ":"")+"public partial "+column.Type+" "+column.Name+"{get;}";
    // [主キー] 設計器は DB の生成器を通さずに読むので、キーの型(生成される Keys.<名前>)ではなく行の型の [Key] から主キーの列を読む。
    private static bool IsKeyAttribute(AttributeSyntax attribute)=>attribute.Name.ToString().Split('.').Last() is "Key" or "KeyAttribute";
    private static IPropertySymbol[] Columns(INamedTypeSymbol row)=>row.GetMembers().OfType<IPropertySymbol>().Where(item=>!item.IsStatic&&!item.IsIndexer&&item.DeclaredAccessibility==Accessibility.Public&&item.Name!="Key").ToArray();
    private static string[] KeyColumns(INamedTypeSymbol row)=>Columns(row).Where(property=>property.DeclaringSyntaxReferences.Select(reference=>reference.GetSyntax()).OfType<PropertyDeclarationSyntax>().Any(declaration=>declaration.AttributeLists.SelectMany(list=>list.Attributes).Any(IsKeyAttribute))).Select(property=>property.Name).ToArray();
    private static PropertyDeclarationSyntax WithKeyAttribute(PropertyDeclarationSyntax property,bool key){
        var lists=property.AttributeLists.Select(list=>list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute=>!IsKeyAttribute(attribute))))).Where(list=>list.Attributes.Count>0).ToList();
        if(key){lists.Insert(0,SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::Seamless.Database.Key")))).WithTrailingTrivia(SyntaxFactory.Space));}
        if(lists.Count==0&&property.AttributeLists.Count==0){return property;}
        var trivia=property.GetLeadingTrivia();
        return property.WithoutLeadingTrivia().WithAttributeLists(SyntaxFactory.List(lists)).WithLeadingTrivia(trivia);
    }
    // キーの選び方：row=>row.Key、または主キーの列を宣言の順に並べた row=>new(row.A,…)。
    private static bool IsKeySelector(SimpleLambdaExpressionSyntax lambda,string[] keyNames){
        var parameter=lambda.Parameter.Identifier.ValueText;
        if(lambda.Body is MemberAccessExpressionSyntax{Expression:IdentifierNameSyntax receiver,Name.Identifier.ValueText:"Key"}&&receiver.Identifier.ValueText==parameter){return true;}
        ArgumentListSyntax? arguments=null;if(lambda.Body is ImplicitObjectCreationExpressionSyntax implicitKey){arguments=implicitKey.ArgumentList;}else if(lambda.Body is ObjectCreationExpressionSyntax explicitKey){arguments=explicitKey.ArgumentList;}
        return arguments is not null&&arguments.Arguments.Count==keyNames.Length&&arguments.Arguments.Select((argument,index)=>argument.Expression is MemberAccessExpressionSyntax{Expression:IdentifierNameSyntax owner} member&&owner.Identifier.ValueText==parameter&&member.Name.Identifier.ValueText==keyNames[index]).All(value=>value);
    }
    // 表のプロパティに書かれたキーの型(生成される型なので設計器からは解決できない)。
    private static string KeyTypeSyntax(IPropertySymbol property){
        var type=((PropertyDeclarationSyntax)property.DeclaringSyntaxReferences.Single().GetSyntax()).Type;
        var generic=type switch{GenericNameSyntax name=>name,QualifiedNameSyntax{Right:GenericNameSyntax name}=>name,AliasQualifiedNameSyntax{Name:GenericNameSyntax name}=>name,_=>null};
        if(generic is not{TypeArgumentList.Arguments.Count:2}){throw new ManagementException("Unsupported","この表はC#で編集してください。");}
        return generic.TypeArgumentList.Arguments[0].ToString();
    }
    private static string Format(string text)=>SyntaxFactory.ParseCompilationUnit(text).NormalizeWhitespace(eol:"\n").ToFullString()+"\n";
    public static Definition Remove(Definition definition,string path){
        var property=Property(Container(Compilation(definition),definition),path);var syntax=TableDeclaration(property);var source=new SourceEditor().Apply(definition.Source,new(definition.Source.Hash(),syntax.SyntaxTree.FilePath,syntax.SpanStart,syntax.Span.Length,""));
        var migration=definition.Migration??new(1,[],[]);var prior=migration.Tables.SingleOrDefault(table=>table.Target==path);var dropped=migration.DroppedTables;
        if(!(migration.NewTables??[]).Contains(path)){dropped=dropped.Append(prior?.Source??path).Distinct().ToArray();}
        return definition with{Source=source,Migration=migration with{Tables=migration.Tables.Where(table=>table.Target!=path).ToArray(),DroppedTables=dropped,NewTables=(migration.NewTables??[]).Where(table=>table!=path).ToArray()}};
    }
    public static RemovedTableDefinition CaptureRemoval(Definition definition,string path){
        SourceEditor.Validate(definition.Source);var compilation=Compilation(definition);var property=Property(Container(compilation,definition),path);var syntax=TableDeclaration(property);var text=definition.Source.Files.Single(file=>file.Path==syntax.SyntaxTree.FilePath).Text;
        var start=syntax.SpanStart;var end=syntax.Span.End;var prefixStart=Math.Max(0,start-128);var mapping=definition.Migration?.Tables.SingleOrDefault(table=>table.Target==path);var wasNew=definition.Migration?.NewTables?.Contains(path)==true;string? drop=null;
        if(!wasNew){var key=path;if(mapping is not null){key=mapping.Source;}if(definition.Migration?.DroppedTables.Contains(key)!=true){drop=key;}}
        return new(1,definition.Id,path,syntax.SyntaxTree.FilePath,text[start..end],text[prefixStart..start],text[end..Math.Min(text.Length,end+128)],RowFingerprint(property),mapping,wasNew,drop);
    }
    public static Definition RestoreRemoved(Definition definition,RemovedTableDefinition removed){
        if(removed.Version!=1||definition.Id!=removed.DefinitionId){throw new ManagementException("Conflict","削除記録の対象・版が一致しません。");}if(Tables(definition).Contains(removed.Path)){throw new ManagementException("Conflict","同名のテーブルが既に存在します。");}
        var file=definition.Source.Files.SingleOrDefault(file=>file.Path==removed.File)??throw new ManagementException("Conflict","元のソースファイルが変更されています。");var anchor=removed.Prefix+removed.Suffix;var offset=file.Text.IndexOf(anchor,StringComparison.Ordinal);
        if(anchor.Length==0||offset<0||file.Text.IndexOf(anchor,offset+1,StringComparison.Ordinal)>=0){throw new ManagementException("Conflict","削除位置の周辺が変更されています。入力と削除記録を保持して差分を確認してください。");}
        var source=new SourceEditor().Apply(definition.Source,new(definition.Source.Hash(),removed.File,offset+removed.Prefix.Length,0,removed.Declaration));var candidate=definition with{Source=source};var compilation=Compilation(candidate);
        if(RowFingerprint(Property(Container(compilation,candidate),removed.Path))!=removed.RowFingerprint){throw new ManagementException("Conflict","行型・キー型が変更されています。削除の取消を自動適用できません。");}
        var migration=definition.Migration??new(1,[],[]);var mappings=migration.Tables.ToList();if(removed.Mapping is{} mapping){if(mappings.Any(table=>table.Source==mapping.Source||table.Target==mapping.Target)){throw new ManagementException("Conflict","同じテーブルの移行定義が変更されています。");}mappings.Add(mapping);}
        var added=migration.NewTables??[];if(removed.WasNew){added=added.Append(removed.Path).Distinct().ToArray();}var dropped=migration.DroppedTables;if(removed.AddedDrop is{} drop){dropped=dropped.Where(value=>value!=drop).ToArray();}
        return candidate with{Migration=migration with{Tables=mappings.ToArray(),DroppedTables=dropped,NewTables=added}};
    }
    // 表を表す宣言：手書きの表は表のプロパティ、生成される表は行の型の宣言。
    private static SyntaxNode TableDeclaration(IPropertySymbol property)=>IsGeneratedTable(property)?Declaration(RowTypes(property)[1]):property.DeclaringSyntaxReferences.Single().GetSyntax();
    // 行の型の指紋。仮のファイルの宣言(合成した表のプロパティ)は含めない。
    private static string RowFingerprint(IPropertySymbol property)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",RowTypes(property).Select(type=>type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)+"\n"+string.Join("\n",type.DeclaringSyntaxReferences.OrderBy(reference=>reference.SyntaxTree.FilePath,StringComparer.Ordinal).Select(reference=>reference.GetSyntax().ToFullString())))))));
}
