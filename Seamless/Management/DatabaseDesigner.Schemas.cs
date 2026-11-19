using Microsoft.CodeAnalysis;

namespace Seamless.Management;

public static partial class DatabaseDesigner{
    public static string[] Schemas(Definition definition){
        var container=Container(Compilation(definition),definition);
        var names=new HashSet<string>(StringComparer.Ordinal);
        var ancestors=new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        Walk(container,"");
        // Views の型はビューの結果の行型(ビューそのものは IReader への拡張メンバー)なので、スキーマは表の宣言から読む。
        foreach(var item in SourceStructure.Read(definition.Source).Objects.Where(item=>item.Root==container.ContainingNamespace.ToDisplayString()&&item.Kind!="Views"&&item.Schema.Length>0)){
            names.Add(item.Schema);
        }
        return names.Order(StringComparer.Ordinal).ToArray();

        void Walk(INamedTypeSymbol owner,string prefix){
            if(!ancestors.Add(owner)){return;}
            try{
                foreach(var property in owner.GetMembers().OfType<IPropertySymbol>()){
                    if(property.Type is not INamedTypeSymbol type||!IsSchemaType(type)){continue;}
                    var path=prefix+property.Name;names.Add(path);Walk(type,path+".");
                }
            }finally{ancestors.Remove(owner);}
        }
    }
    public static Definition AddSchema(Definition definition,string name){
        ObjectName(name);
        var owner=Container(Compilation(definition),definition);
        if(owner.GetMembers(name).Length>0||Schemas(definition).Contains(name,StringComparer.Ordinal)||Objects(definition).Any(item=>item.Schema.Length==0&&item.Name==name)){
            throw new ManagementException("Conflict","同じ名前のスキーマまたはオブジェクトがあります: "+name);
        }
        return EnsureSchema(definition,name);
    }
    /// <summary>
    /// 空のスキーマ(表・ビュー・関数・下位のスキーマが無い)を消す。所属先のスキーマのプロパティと、スキーマの型の宣言を消す。
    /// 中身のあるスキーマは、行のデータや中の定義を一緒に消してしまうので消さない(SQL Server の DROP SCHEMA と同じく空だけ)。
    /// </summary>
    public static Definition RemoveSchema(Definition definition,string schema){
        SourceEditor.Validate(definition.Source);
        var schemas=Schemas(definition);
        if(!schemas.Contains(schema,StringComparer.Ordinal)){throw new ManagementException("NotFound","スキーマがありません: "+schema);}
        if(schemas.Any(item=>item.StartsWith(schema+".",StringComparison.Ordinal))||Objects(definition).Any(item=>item.Schema==schema||item.Schema.StartsWith(schema+".",StringComparison.Ordinal))){
            throw new ManagementException("Conflict","中身のあるスキーマは削除できません。中の表・ビュー・関数と下位のスキーマを先に削除してください: "+schema);
        }
        var compilation=Compilation(definition);var container=Container(compilation,definition);
        var parts=schema.Split('.');var parent=string.Join(".",parts[..^1]);var owner=parent.Length==0?container:Owner(container,parent);
        if(owner.GetMembers(parts[^1]).OfType<IPropertySymbol>().SingleOrDefault() is not{Type:INamedTypeSymbol type} property||!IsSchemaType(type)||property.DeclaringSyntaxReferences.Length!=1||type.DeclaringSyntaxReferences.Length==0){
            throw new ManagementException("Unsupported","このスキーマはC#で編集してください: "+schema);
        }
        var propertySyntax=property.DeclaringSyntaxReferences[0].GetSyntax();
        var typeSyntaxes=type.DeclaringSyntaxReferences.Select(reference=>reference.GetSyntax()).ToArray();
        // スキーマの型をほかの宣言から使っていれば、消すとコンパイルできなくなるので止める。
        foreach(var tree in compilation.SyntaxTrees.Where(tree=>definition.Source.Files.Any(file=>file.Path==tree.FilePath))){
            var model=compilation.GetSemanticModel(tree);
            foreach(var name in tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.SimpleNameSyntax>().Where(name=>name.Identifier.ValueText==type.Name)){
                if(propertySyntax.Span.Contains(name.Span)&&propertySyntax.SyntaxTree==tree||typeSyntaxes.Any(syntax=>syntax.SyntaxTree==tree&&syntax.Span.Contains(name.Span))){continue;}
                if(SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol?.OriginalDefinition,type)){throw new ManagementException("Unsupported","スキーマの型をほかの宣言から使っています。C#で編集してください: "+schema);}
            }
        }
        // 型の宣言だけのファイル(Schemas/Schema_<名前>.cs など)はファイルごと消し、ほかの宣言と同じファイルなら宣言だけを消す。
        var removedFiles=new HashSet<string>(StringComparer.Ordinal);
        var edits=new List<(string Path,int Start,int Length)>{(propertySyntax.SyntaxTree.FilePath,propertySyntax.FullSpan.Start,propertySyntax.FullSpan.Length)};
        foreach(var syntax in typeSyntaxes){
            var root=syntax.SyntaxTree.GetRoot();
            var others=root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax>().Where(item=>item!=syntax&&!syntax.Span.Contains(item.Span));
            if(!others.Any()&&syntax.SyntaxTree.FilePath!=propertySyntax.SyntaxTree.FilePath){removedFiles.Add(syntax.SyntaxTree.FilePath);continue;}
            edits.Add((syntax.SyntaxTree.FilePath,syntax.FullSpan.Start,syntax.FullSpan.Length));
        }
        var files=definition.Source.Files.Where(file=>!removedFiles.Contains(file.Path)).Select(file=>{
            var text=file.Text;
            foreach(var edit in edits.Where(edit=>edit.Path==file.Path).OrderByDescending(edit=>edit.Start)){text=text.Remove(edit.Start,edit.Length);}
            return file with{Text=text};
        }).ToArray();
        var candidate=definition with{Source=definition.Source with{Files=files,Revision=Guid.NewGuid().ToString("N")}};
        if(Schemas(candidate).Contains(schema,StringComparer.Ordinal)){throw new ManagementException("Unsupported","スキーマの宣言が別の場所にも残っています。C#で編集してください: "+schema);}
        return candidate;
    }
    // 公開定義から対象だけを消した差分を逆向きに当てる。共有ファイルの他の記述は残す。
    internal static Definition RestoreRemoved(Definition draft,Definition published,string id,string kind){
        if(kind=="Schema"?Schemas(draft).Contains(id,StringComparer.Ordinal):Objects(draft).Any(item=>item.Id==id)){
            throw new ManagementException("Conflict","同じ名前の定義があるため復元できません: "+id);
        }
        var removed=kind switch{"Schema"=>RemoveSchema(published,id),"Table"=>Remove(published,id),_=>RemoveQuery(published,id)};
        var remaining=removed.Source.Files.ToDictionary(file=>file.Path,StringComparer.Ordinal);
        var files=draft.Source.Files.ToList();
        foreach(var original in published.Source.Files){
            remaining.TryGetValue(original.Path,out var after);
            if(after==original){continue;}
            var index=files.FindIndex(file=>file.Path.Equals(original.Path,StringComparison.OrdinalIgnoreCase));
            if(index<0&&after is null){files.Add(original);continue;}
            if(index<0||after is null){throw Conflict(original.Path);}
            var current=files[index];
            if(current.Text==after.Text){files[index]=current with{Text=original.Text};continue;}

            var prefix=0;
            while(prefix<after.Text.Length&&prefix<original.Text.Length&&after.Text[prefix]==original.Text[prefix]){prefix++;}
            var suffix=0;
            while(suffix<after.Text.Length-prefix&&suffix<original.Text.Length-prefix&&
                after.Text[after.Text.Length-suffix-1]==original.Text[original.Text.Length-suffix-1]){suffix++;}
            var start=Math.Max(0,prefix-48);
            var end=Math.Min(after.Text.Length,after.Text.Length-suffix+48);
            var anchor=after.Text[start..end];
            var position=current.Text.IndexOf(anchor,StringComparison.Ordinal);
            if(anchor.Length==0||position<0||current.Text.IndexOf(anchor,position+1,StringComparison.Ordinal)>=0){throw Conflict(original.Path);}
            var edit=position+prefix-start;
            files[index]=current with{Text=current.Text.Remove(edit,after.Text.Length-prefix-suffix)
                .Insert(edit,original.Text[prefix..(original.Text.Length-suffix)])};
        }
        var candidate=draft with{Source=draft.Source with{Files=files.ToArray(),Revision=Guid.NewGuid().ToString("N")}};
        if(kind=="Table"&&candidate.Migration is{} migration){
            candidate=candidate with{Migration=migration with{DroppedTables=migration.DroppedTables.Where(path=>path!=id).ToArray()}};
        }
        if(kind=="Schema"? !Schemas(candidate).Contains(id,StringComparer.Ordinal): !Objects(candidate).Any(item=>item.Id==id&&item.Kind==kind)){
            throw Conflict(id);
        }
        return candidate;

        static ManagementException Conflict(string path)=>new("Conflict","別の変更と重なるため、入力を保持したまま復元を中止しました: "+path);
    }
    private static bool IsSchemaType(INamedTypeSymbol type){
        for(var current=type;current is not null;current=current.BaseType){
            if(current.ToDisplayString()=="Seamless.Database.DatabaseSchema"){return true;}
        }
        return false;
    }
}
