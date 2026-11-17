using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Seamless.Management;

public sealed record TablePublication(Definition Candidate,Definition RemainingDraft,TableSelection[] Tables);

public static partial class DatabaseDesigner{
    public static TablePublication PrepareTablePublication(Definition published,Definition draft,string path){
        var selection=SelectTables(published,draft,path);
        var oldPaths=selection.Select(item=>item.PublishedPath).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var newPaths=selection.Select(item=>item.DraftPath).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var migration=draft.Migration?.Freeze();
        foreach(var item in selection){
            if(item.PublishedPath is{} dropped&&item.DraftPath is null&&migration?.DroppedTables.Contains(dropped)!=true){
                throw new ManagementException("Validation","表の削除を移行定義で明示してください: "+dropped);
            }
        }
        var source=ProjectTables(published,draft,selection,false);
        return new(published with{Source=source,Migration=FilterMigration(migration,oldPaths,newPaths,true)},
            draft with{Migration=FilterMigration(migration,oldPaths,newPaths,false)},selection);
    }

    private static MigrationDefinition? FilterMigration(MigrationDefinition? migration,HashSet<string> oldPaths,HashSet<string> newPaths,bool included){
        if(migration is null){return null;}
        var mappings=migration.Tables.Where(item=>(oldPaths.Contains(item.Source)&&newPaths.Contains(item.Target))==included).ToArray();
        var drops=migration.DroppedTables.Where(item=>oldPaths.Contains(item)==included).ToArray();
        var additions=(migration.NewTables??[]).Where(item=>newPaths.Contains(item)==included).ToArray();
        if(mappings.Length+drops.Length+additions.Length==0){return null;}
        return new(migration.Version,mappings,drops,additions);
    }

    private static SourceSnapshot ProjectTables(Definition published,Definition draft,TableSelection[] selection,bool restoring){
        var before=Compilation(published);
        var after=Compilation(draft);
        var oldContainer=Container(before,published);
        var newContainer=Container(after,draft);
        var oldTables=TableProperties(oldContainer);
        var newTables=TableProperties(newContainer);
        var oldPaths=selection.Select(item=>item.PublishedPath).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var newPaths=selection.Select(item=>item.DraftPath).OfType<string>().ToHashSet(StringComparer.Ordinal);
        if(!SameSourceSettings(published,draft)||!GlobalUsings(before).SequenceEqual(GlobalUsings(after),StringComparer.Ordinal)){
            throw CannotSeparate("DBの入口、参照、コンパイル条件、またはglobal using");
        }

        var oldNodes=new DeclarationSelection();
        var newNodes=new DeclarationSelection();
        foreach(var name in oldPaths){oldNodes.Add(oldTables[name].DeclaringSyntaxReferences);}
        if(restoring){
            foreach(var type in oldPaths.SelectMany(name=>RowTypes(oldTables[name]))){
                if(after.GetTypeByMetadataName(MetadataName(type)) is null){
                    oldNodes.Add(type.DeclaringSyntaxReferences);
                }
            }
        }
        foreach(var name in newPaths){
            var table=newTables[name];
            newNodes.Add(table.DeclaringSyntaxReferences);
            foreach(var type in RowTypes(table)){
                newNodes.Add(type.DeclaringSyntaxReferences);
                var previous=before.GetTypeByMetadataName(MetadataName(type));
                if(previous is not null){oldNodes.Add(previous.DeclaringSyntaxReferences);}
            }
            IncludeSchema(name);
        }
        var candidateSource=published.Source with{Revision=draft.Source.Revision,Files=ProjectFiles(before,after,published.Source.Files,oldNodes,newNodes,restoring)};
        var candidate=published with{Source=candidateSource};
        var resultTables=TableProperties(Container(Compilation(candidate),candidate));
        var expected=oldTables.Where(item=>!oldPaths.Contains(item.Key)).Concat(newTables.Where(item=>newPaths.Contains(item.Key)))
            .ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal);
        if(!resultTables.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys)){throw CannotSeparate("表またはスキーマの宣言");}
        foreach(var (name,table) in expected){
            if(!SameTable(table,resultTables[name],restoring&&!newPaths.Contains(name))){throw CannotSeparate("表「"+name+"」の共有宣言または型");}
        }
        return candidateSource;

        void IncludeSchema(string tablePath){
            var path="";
            foreach(var part in tablePath.Split('.')[..^1]){
                var next=part;
                if(path.Length>0){next=path+"."+part;}
                var property=Property(newContainer,next);
                IPropertySymbol? old=null;
                try{old=Property(oldContainer,next);}catch(ManagementException error)when(error.Code is "NotFound" or "Unsupported"){}
                if(old is null){newNodes.Add(property.DeclaringSyntaxReferences);}
                else if(MetadataName((INamedTypeSymbol)property.Type)!=MetadataName((INamedTypeSymbol)old.Type)||
                    !SameDeclarations(old.DeclaringSyntaxReferences,property.DeclaringSyntaxReferences)){
                    throw CannotSeparate("スキーマ「"+next+"」の共有宣言");
                }
                path=next;
            }
        }
    }

    private static bool SameSourceSettings(Definition published,Definition draft){
        return published.EntryType==draft.EntryType&&published.EntryMember==draft.EntryMember&&published.WritesData==draft.WritesData&&
            published.Source.TargetFramework==draft.Source.TargetFramework&&published.Source.LanguageVersion==draft.Source.LanguageVersion&&
            published.Source.References.SequenceEqual(draft.Source.References)&&(published.Source.Defines??[]).SequenceEqual(draft.Source.Defines??[],StringComparer.Ordinal);
    }

    private static IEnumerable<string> GlobalUsings(CSharpCompilation compilation)=>compilation.SyntaxTrees
        .SelectMany(tree=>((CompilationUnitSyntax)tree.GetRoot()).Usings)
        .Where(item=>item.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)).Select(item=>item.WithoutTrivia().ToString()).Order(StringComparer.Ordinal);

    private static string MetadataName(INamedTypeSymbol type){
        if(type.ContainingType is{} parent){return MetadataName(parent)+"+"+type.MetadataName;}
        if(type.ContainingNamespace.IsGlobalNamespace){return type.MetadataName;}
        return type.ContainingNamespace.ToDisplayString()+"."+type.MetadataName;
    }

    private static bool SameTable(IPropertySymbol expected,IPropertySymbol actual,bool exact){
        if(!SameDeclarations(expected.DeclaringSyntaxReferences,actual.DeclaringSyntaxReferences,exact)){return false;}
        var expectedTypes=RowTypes(expected);
        var actualTypes=RowTypes(actual);
        return expectedTypes.Zip(actualTypes).All(pair=>TypeName(pair.First)==TypeName(pair.Second)&&
            SameDeclarations(pair.First.DeclaringSyntaxReferences,pair.Second.DeclaringSyntaxReferences,exact));
    }

    private static bool SameDeclarations(IEnumerable<SyntaxReference> first,IEnumerable<SyntaxReference> second,bool exact=false){
        var left=first.OrderBy(item=>item.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(item=>item.Span.Start).ToArray();
        var right=second.OrderBy(item=>item.SyntaxTree.FilePath,StringComparer.Ordinal).ThenBy(item=>item.Span.Start).ToArray();
        return left.Length==right.Length&&left.Zip(right).All(pair=>exact?
            pair.First.GetSyntax().ToFullString()==pair.Second.GetSyntax().ToFullString():
            SyntaxFactory.AreEquivalent(pair.First.GetSyntax(),pair.Second.GetSyntax()));
    }

    private static ManagementException CannotSeparate(string subject)=>new("SharedDeclaration",subject+"を単独保存の範囲へ分けられません。差分を確認してください。");

    private sealed class DeclarationSelection{
        private readonly Dictionary<SyntaxTree,HashSet<TextSpan>> spans=[];
        internal void Add(IEnumerable<SyntaxReference> declarations){
            foreach(var declaration in declarations){
                // 生成される表のプロパティは仮のファイルにだけあり、利用者のソースではない。表は行の型の宣言で選ぶ。
                if(declaration.SyntaxTree.FilePath==GeneratedTablesPath){continue;}
                if(!this.spans.TryGetValue(declaration.SyntaxTree,out var selected)){this.spans.Add(declaration.SyntaxTree,selected=[]);}
                selected.Add(declaration.Span);
            }
        }
        internal bool Has(SyntaxNode node)=>this.spans.TryGetValue(node.SyntaxTree,out var selected)&&selected.Contains(node.Span);
        internal bool Contains(SyntaxNode node)=>this.spans.TryGetValue(node.SyntaxTree,out var selected)&&selected.Any(span=>node.Span.Contains(span));
    }

    private static SourceFile[] ProjectFiles(CSharpCompilation before,CSharpCompilation after,SourceFile[] originals,DeclarationSelection oldNodes,DeclarationSelection newNodes,bool restoring){
        var previous=before.SyntaxTrees.ToDictionary(tree=>tree.FilePath,tree=>(CompilationUnitSyntax)tree.GetRoot(),StringComparer.OrdinalIgnoreCase);
        var draft=after.SyntaxTrees.ToDictionary(tree=>tree.FilePath,tree=>(CompilationUnitSyntax)tree.GetRoot(),StringComparer.OrdinalIgnoreCase);
        var files=originals.ToDictionary(file=>file.Path,StringComparer.OrdinalIgnoreCase);
        foreach(var path in previous.Keys.Concat(draft.Keys).Distinct(StringComparer.OrdinalIgnoreCase)){
            previous.TryGetValue(path,out var old);
            draft.TryGetValue(path,out var next);
            var oldSelected=old is not null&&oldNodes.Contains(old);
            var newSelected=next is not null&&newNodes.Contains(next);
            if(!oldSelected&&!newSelected){continue;}
            if(old?.ContainsDirectives==true||next?.ContainsDirectives==true){throw CannotSeparate(path+"のプリプロセッサ指示");}
            CompilationUnitSyntax root;
            if(old is null){
                if(next!.AttributeLists.Count!=0){throw CannotSeparate(path+"のアセンブリ属性");}
                root=next.WithMembers(ProjectMembers(default,next.Members,oldNodes,newNodes));
            }else{
                if(newSelected&&!SyntaxFactory.AreEquivalent(old.WithMembers(default),next!.WithMembers(default))){
                    throw CannotSeparate(path+"の共有usingまたはアセンブリ属性");
                }
                root=old.WithMembers(ProjectMembers(old.Members,next?.Members??default,oldNodes,newNodes));
            }
            if(restoring&&next is null&&root.AttributeLists.Count==0&&root.Externs.Count==0&&
                root.DescendantNodes().OfType<MemberDeclarationSyntax>().All(node=>node is BaseNamespaceDeclarationSyntax)&&
                !root.DescendantTrivia().Any(trivia=>trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)||
                    trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)||trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))){
                files.Remove(path);
            }else{files[path]=new(path,root.ToFullString());}
        }
        return files.Values.ToArray();
    }

    private static SyntaxList<MemberDeclarationSyntax> ProjectMembers(SyntaxList<MemberDeclarationSyntax> previous,SyntaxList<MemberDeclarationSyntax> draft,DeclarationSelection oldNodes,DeclarationSelection newNodes){
        var additions=draft.Where(newNodes.Contains).ToArray();
        var used=new HashSet<MemberDeclarationSyntax>();
        var result=new List<MemberDeclarationSyntax>();
        foreach(var old in previous){
            var key=MemberKey(old);
            var matches=additions.Where(item=>key is not null&&MemberKey(item)==key).ToArray();
            if(matches.Length>1||matches.Length==1&&previous.Count(item=>MemberKey(item)==key)!=1){throw CannotSeparate(key!);}
            var next=matches.SingleOrDefault();
            if(oldNodes.Has(old)){
                if(next is not null&&newNodes.Has(next)){result.Add(next);used.Add(next);}
                continue;
            }
            if(!oldNodes.Contains(old)&&next is null){result.Add(old);continue;}
            if(next is not null&&newNodes.Has(next)){throw CannotSeparate(key!);}
            var oldHeader=ScopeHeader(old);
            if(next is not null&&!SyntaxFactory.AreEquivalent(oldHeader,ScopeHeader(next))){throw CannotSeparate(key!);}
            SyntaxList<MemberDeclarationSyntax> nextMembers=default;
            if(next is not null){nextMembers=Members(next);}
            var members=ProjectMembers(Members(old),nextMembers,oldNodes,newNodes);
            result.Add(WithMembers(old,members));
            if(next is not null){used.Add(next);}
        }
        foreach(var next in additions.Where(item=>!used.Contains(item))){
            if(newNodes.Has(next)){result.Add(next);}
            else{result.Add(WithMembers(next,ProjectMembers(default,Members(next),oldNodes,newNodes)));}
        }
        return SyntaxFactory.List(result);
    }

    private static string? MemberKey(MemberDeclarationSyntax node)=>node switch{
        BaseNamespaceDeclarationSyntax space=>space.Kind()+":"+space.Name,
        TypeDeclarationSyntax type=>type.Kind()+":"+type.Identifier.ValueText+"`"+type.Arity,
        PropertyDeclarationSyntax property=>"property:"+property.Identifier.ValueText,
        _=>null
    };

    private static SyntaxList<MemberDeclarationSyntax> Members(MemberDeclarationSyntax node)=>node switch{
        BaseNamespaceDeclarationSyntax space=>space.Members,
        TypeDeclarationSyntax type=>type.Members,
        _=>throw CannotSeparate(node.Kind().ToString())
    };

    private static MemberDeclarationSyntax ScopeHeader(MemberDeclarationSyntax node)=>node switch{
        BaseNamespaceDeclarationSyntax space=>space.WithMembers(default),
        TypeDeclarationSyntax type=>type.WithMembers(default).WithSemicolonToken(default).WithOpenBraceToken(default).WithCloseBraceToken(default),
        _=>throw CannotSeparate(node.Kind().ToString())
    };

    private static MemberDeclarationSyntax WithMembers(MemberDeclarationSyntax node,SyntaxList<MemberDeclarationSyntax> members){
        if(node is BaseNamespaceDeclarationSyntax space){return space.WithMembers(members);}
        if(node is TypeDeclarationSyntax type){
            if(members.Count>0&&type.SemicolonToken.IsKind(SyntaxKind.SemicolonToken)){
                type=type.WithSemicolonToken(default).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithTrailingTrivia(type.SemicolonToken.TrailingTrivia));
            }
            return type.WithMembers(members);
        }
        throw CannotSeparate(node.Kind().ToString());
    }
}
