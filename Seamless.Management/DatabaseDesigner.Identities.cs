using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Management;

public sealed record CatalogIdentityEntry(string Kind,string Address,string Member,string StableId);

/// <summary>名前は現版の参照先だけに使い、永続IDは元宣言の明示マーカーから取得する。</summary>
public sealed class CatalogIdentities{
    private readonly Dictionary<(string Kind,string Address,string Member),string> entries;
    public IReadOnlyList<CatalogIdentityEntry> Entries{get;}
    public IReadOnlyList<DiagnosticInfo> Diagnostics{get;}
    internal CatalogIdentities(IEnumerable<CatalogIdentityEntry> entries,IEnumerable<DiagnosticInfo>? diagnostics=null){
        var values=entries.ToArray();this.Entries=Array.AsReadOnly(values);
        this.Diagnostics=Array.AsReadOnly((diagnostics??[]).ToArray());
        this.entries=values.ToDictionary(item=>(item.Kind,item.Address,item.Member),item=>item.StableId);
    }
    public string? Schema(string address)=>this.entries.GetValueOrDefault(("Schema",address,""));
    public string? Object(string address)=>this.entries.GetValueOrDefault(("Object",address,""));
    public string? Column(string address,string name)=>this.entries.GetValueOrDefault(("Column",address,name));
    public string? UnavailableReason(string kind,string address,string member="")=>this.Diagnostics.Any(item=>item.Message==kind+":"+address+":"+member)?"AmbiguousDeclaration":null;
}

public static partial class DatabaseDesigner{
    public static CatalogIdentities ReadStableIdentities(Definition definition)=>StableIdentities(definition,false).Identities;

    /// <summary>表示用IDをソースへ明示付与した下書きを返す。保存・公開・データ移行は実行しない。</summary>
    public static Definition AssignStableIdentities(Definition definition)=>StableIdentities(definition,true).Definition;

    private static (Definition Definition,CatalogIdentities Identities) StableIdentities(Definition definition,bool assign){
        SourceEditor.Validate(definition.Source);
        if(definition.Kind!=DefinitionKind.Database){throw new ManagementException("Unsupported","表示用IDの付与はDB定義を指定してください。");}
        if(!assign&&!definition.Source.Files.Any(file=>file.Text.Contains(SourceIdentityMarkers.Prefix,StringComparison.Ordinal))){return(definition,new([]));}
        var compilation=Compilation(definition);var container=Container(compilation,definition);
        var entries=new List<CatalogIdentityEntry>();
        var additions=new Dictionary<(string Path,int Start),string>();
        foreach(var schema in Schemas(definition)){
            var split=schema.LastIndexOf('.');var parent="";var name=schema;if(split>=0){parent=schema[..split];name=schema[(split+1)..];}
            var symbols=new List<ISymbol>();
            if(IdentityOwner(container,parent) is{} owner){symbols.AddRange(owner.GetMembers(name).OfType<IPropertySymbol>().Where(property=>property.Type is INamedTypeSymbol type&&IsSchemaType(type)));}
            foreach(var category in new[]{"Tables","Views","Work"}){
                var ns=FindNamespace(compilation.GlobalNamespace,string.Join(".",new[]{container.ContainingNamespace.ToDisplayString(),category,schema}.Where(value=>value.Length>0)));
                if(ns is not null){symbols.Add(ns);}
            }
            Add("Schema",schema,"",symbols);
        }
        foreach(var target in ObjectsWithoutIdentities(definition)){
            EditableObject selected;
            if(IdentityOwner(container,target.Schema) is null){
                var declaredRow=compilation.GetTypeByMetadataName(target.RecordType.Replace("global::","",StringComparison.Ordinal));
                if(declaredRow?.DeclaringSyntaxReferences.Length is not >0){throw new ManagementException("Unsupported","対象の元宣言を特定できません: "+target.Id);}
                selected=new(declaredRow,null,null);
            }else{selected=ResolveEditableObject(definition,target,compilation);}
            ISymbol? anchor=selected.Member;
            if(anchor?.DeclaringSyntaxReferences.Length is not >0){anchor=selected.Row;}
            if(anchor is not null){Add("Object",target.Id,"",[anchor]);}
            if(selected.Row is not{} row){continue;}
            foreach(var column in row.GetMembers().OfType<IPropertySymbol>().Where(property=>!property.IsStatic&&!property.IsIndexer&&property.DeclaredAccessibility==Accessibility.Public&&property.Name!="Key")){
                Add("Column",target.Id,column.Name,[column]);
            }
        }
        var ambiguous=entries.GroupBy(item=>item.StableId,StringComparer.Ordinal).Where(group=>group.Count()>1).SelectMany(group=>group).ToArray();
        if(ambiguous.Length>0&&assign){throw new ManagementException("Unsupported","同じ元宣言を複数のスキーマ・表で共有しているため、表示用IDを一意に付与できません。共有する宣言を分けてください。");}
        var ambiguousObjects=ambiguous.Where(item=>item.Kind=="Object").Select(item=>item.Address).ToHashSet(StringComparer.Ordinal);
        var excluded=ambiguous.Concat(entries.Where(item=>item.Kind=="Column"&&ambiguousObjects.Contains(item.Address))).Distinct().ToArray();
        var diagnostics=excluded.Select(item=>new DiagnosticInfo("IdentityAmbiguous",item.Kind+":"+item.Address+":"+item.Member,Severity:"Warning")).ToList();
        if(entries.Count>0){diagnostics.Add(new("IdentityDeclaration","表示用IDは元宣言の明示マーカーです。利用者によるIDの転用や、別版での同一性・同じ保存データを証明するものではありません。",Severity:"Info"));}
        var declaredIds=compilation.SyntaxTrees.SelectMany(tree=>tree.GetRoot().DescendantNodes().Where(SourceIdentityMarkers.Supported)).Select(SourceIdentityMarkers.Read).OfType<string>().ToHashSet(StringComparer.Ordinal);
        declaredIds.ExceptWith(entries.Select(item=>item.StableId));
        if(declaredIds.Count>0){diagnostics.Add(new("IdentityUnbound","管理対象へ一意に対応しない表示用IDマーカーがあります。名前から補完せず、対象のIDとして提供しません。",Severity:"Warning"));}
        var identities=new CatalogIdentities(entries.Except(excluded),diagnostics);
        if(additions.Count==0){return(definition,identities);}
        var files=definition.Source.Files.Select(file=>{
            var text=file.Text;
            foreach(var item in additions.Where(item=>item.Key.Path==file.Path).OrderByDescending(item=>item.Key.Start)){
                // 改行で直前の区切り記号のtrailing triviaから分離し、引数も宣言へ結び付ける。
                text=text.Insert(item.Key.Start,"\n/* "+SourceIdentityMarkers.Prefix+item.Value+" */ ");
            }
            return file with{Text=text};
        }).ToArray();
        var updated=definition with{Source=definition.Source with{Files=files,Revision=Guid.NewGuid().ToString("N")}};
        SourceEditor.Validate(updated.Source);return(updated,identities);

        void Add(string kind,string address,string member,IEnumerable<ISymbol> symbols){
            var declarations=symbols.SelectMany(symbol=>symbol.DeclaringSyntaxReferences).Select(reference=>reference.GetSyntax())
                .Where(SourceIdentityMarkers.Supported).DistinctBy(node=>(node.SyntaxTree.FilePath,node.SpanStart,node.Span.Length))
                .OrderBy(node=>node.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(node=>node.SpanStart).ToArray();
            var ids=declarations.Select(SourceIdentityMarkers.Read).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
            if(ids.Length>1){throw new ManagementException("Conflict","同じ対象に複数の表示用IDがあります。C#のマーカーを確認してください。");}
            var id=ids.SingleOrDefault();
            if(id is null&&assign){
                var declaration=declarations.FirstOrDefault()??throw new ManagementException("Unsupported","元宣言のない対象には表示用IDを付与できません: "+address);
                var position=(declaration.SyntaxTree.FilePath,declaration.SpanStart);
                if(!additions.TryGetValue(position,out id)){id=Guid.NewGuid().ToString("N");additions.Add(position,id);}
            }
            if(id is not null){entries.Add(new(kind,address,member,id));}
        }
    }
    private static INamespaceSymbol? FindNamespace(INamespaceSymbol root,string path){
        INamespaceSymbol? result=root;
        foreach(var name in path.Split('.')){result=result?.GetNamespaceMembers().SingleOrDefault(item=>item.Name==name);}
        return result;
    }
    private static INamedTypeSymbol? IdentityOwner(INamedTypeSymbol root,string schema){
        INamedTypeSymbol? owner=root;
        foreach(var part in schema.Split('.',StringSplitOptions.RemoveEmptyEntries)){
            owner=owner?.GetMembers(part).OfType<IPropertySymbol>().SingleOrDefault()?.Type as INamedTypeSymbol;
        }
        return owner;
    }
}

internal static class SourceIdentityMarkers{
    internal const string Prefix="seamless-id:";
    internal static bool Supported(SyntaxNode node)=>node is BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax or PropertyDeclarationSyntax or ParameterSyntax or MethodDeclarationSyntax;
    internal static string? Read(SyntaxNode node){
        var ids=node.GetLeadingTrivia().Select(Parse).OfType<string>().ToArray();
        if(ids.Length>1){throw Invalid();}return ids.SingleOrDefault();
    }
    private static string? Parse(SyntaxTrivia trivia){
        if(!trivia.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)){return null;}
        var content=trivia.ToString().Trim();
        if(!content.StartsWith("/* "+Prefix,StringComparison.Ordinal)){return null;}
        if(!content.EndsWith(" */",StringComparison.Ordinal)){throw Invalid();}
        var value=content[(3+Prefix.Length)..^3];
        if(!Guid.TryParseExact(value,"N",out var id)||id==Guid.Empty||value!=id.ToString("N")){throw Invalid();}
        return value;
    }
    internal static void Validate(SourceSnapshot source){
        var ids=new HashSet<string>(StringComparer.Ordinal);var count=0;
        foreach(var file in source.Files.Where(file=>file.Text.Contains(Prefix,StringComparison.Ordinal))){
            var root=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(file.Text,new(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14,preprocessorSymbols:source.Defines??[]),file.Path).GetRoot();
            var positions=root.DescendantNodes().Where(Supported).SelectMany(node=>node.GetLeadingTrivia()).Select(trivia=>trivia.Span).ToHashSet();
            foreach(var trivia in root.DescendantTrivia(descendIntoTrivia:false)){
                var id=Parse(trivia);if(id is null){continue;}
                if(++count>8192||!ids.Add(id)){throw new ManagementException("Conflict","表示用IDの重複または件数超過があります。コピーした宣言には新しいIDを付与してください。");}
                if(!positions.Contains(trivia.Span)){throw Invalid();}
            }
            foreach(var node in root.DescendantNodes().Where(Supported)){_=Read(node);}
        }
    }
    private static ManagementException Invalid()=>new("Validation","表示用IDマーカーの形式または配置が不正です。宣言直前のC#コメントを確認してください。");
}
