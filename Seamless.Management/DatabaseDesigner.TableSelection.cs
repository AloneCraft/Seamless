using Microsoft.CodeAnalysis;

namespace Seamless.Management;

public sealed record TableSelection(string? PublishedPath,string? DraftPath);

public static partial class DatabaseDesigner{
    private sealed record RelationTable(Dictionary<string,string> Keys,Dictionary<string,string> Columns,ForeignKeyDesign[] Relations,bool InvalidRelation);

    /// <summary>稼働版へ選択した表の変更を重ねるために必要な外部キーの依存範囲を求める。</summary>
    public static TableSelection[] SelectTables(Definition published,Definition draft,string path){
        if(published.Id!=draft.Id||published.Database!=draft.Database||published.Kind!=DefinitionKind.Database||draft.Kind!=DefinitionKind.Database){
            throw new ManagementException("Conflict","同じDBの稼働版と下書きを指定してください。");
        }
        var before=RelationTables(published);
        var after=RelationTables(draft);
        var byPublished=new Dictionary<string,TableSelection>(StringComparer.Ordinal);
        var byDraft=new Dictionary<string,TableSelection>(StringComparer.Ordinal);
        foreach(var mapping in draft.Migration?.Tables??[]){
            if(!before.ContainsKey(mapping.Source)||!after.ContainsKey(mapping.Target)){
                throw new ManagementException("Validation","表の移行定義と宣言が一致しません: "+mapping.Source+" → "+mapping.Target);
            }
            Register(new(mapping.Source,mapping.Target));
        }
        foreach(var name in before.Keys.Where(name=>!byPublished.ContainsKey(name))){
            if(after.ContainsKey(name)&&!byDraft.ContainsKey(name)){
                Register(new(name,name));
            }else{
                Register(new(name,null));
            }
        }
        foreach(var name in after.Keys.Where(name=>!byDraft.ContainsKey(name))){
            Register(new(null,name));
        }

        if(!byDraft.TryGetValue(path,out var first)&&!byPublished.TryGetValue(path,out first)){
            throw new ManagementException("NotFound","表の宣言がありません: "+path);
        }
        var selected=new HashSet<TableSelection>{first};
        var order=new List<TableSelection>{first};
        while(true){
            var candidate=new Dictionary<string,RelationTable>(before,StringComparer.Ordinal);
            var changed=new HashSet<string>(StringComparer.Ordinal);
            TableSelection? collision=null;
            foreach(var item in order){
                if(item.PublishedPath is{} old){candidate.Remove(old);changed.Add(old);}
            }
            foreach(var item in order){
                if(item.DraftPath is not{} next){continue;}
                if(!candidate.TryAdd(next,after[next])){
                    if(byPublished.TryGetValue(next,out collision)&&!selected.Contains(collision)){
                        break;
                    }
                    throw new ManagementException("Conflict","同名の表があります: "+next);
                }
                changed.Add(next);
            }
            if(collision is not null){Include(collision);continue;}
            var added=false;
            foreach(var (childPath,child) in candidate){
                if(changed.Contains(childPath)&&child.InvalidRelation){
                    throw new ManagementException("ForeignKeyDefinition","外部キーの宣言を確認してください: "+childPath);
                }
                foreach(var relation in child.Relations){
                    if(!changed.Contains(childPath)&&!changed.Contains(relation.ReferencedTable)){continue;}
                    if(candidate.TryGetValue(relation.ReferencedTable,out var parent)&&Compatible(child,parent,relation)){continue;}
                    TableSelection? dependency=null;
                    if(byDraft.TryGetValue(childPath,out var changedChild)&&selected.Contains(changedChild)){
                        if(byDraft.TryGetValue(relation.ReferencedTable,out var target)&&target.DraftPath is{} next&&Compatible(child,after[next],relation)){
                            dependency=target;
                        }
                    }else{
                        byPublished.TryGetValue(childPath,out dependency);
                    }
                    if(dependency is null||selected.Contains(dependency)){
                        throw new ManagementException("ForeignKeyDefinition","表 "+childPath+" の外部キー「"+relation.Name+"」を成立させる下書きの変更がありません。");
                    }
                    Include(dependency);
                    added=true;
                    break;
                }
                if(added){break;}
            }
            if(!added){return order.ToArray();}
        }

        void Register(TableSelection item){
            if(item.PublishedPath is{} old&&!byPublished.TryAdd(old,item)||item.DraftPath is{} next&&!byDraft.TryAdd(next,item)){
                throw new ManagementException("Conflict","表の移行定義が重複しています。");
            }
        }
        void Include(TableSelection item){selected.Add(item);order.Add(item);}
    }

    private static Dictionary<string,RelationTable> RelationTables(Definition definition){
        var container=Container(Compilation(definition),definition);
        return TableProperties(container).ToDictionary(item=>item.Key,item=>{
            var types=RowTypes(item.Value);
            var attributes=item.Value.GetAttributes().Where(attribute=>attribute.AttributeClass?.ToDisplayString()=="Seamless.Database.ForeignKeyAttribute");
            var invalid=false;
            var relations=new List<ForeignKeyDesign>();
            foreach(var attribute in attributes){
                var args=attribute.ConstructorArguments;
                if(args.Length!=4||args[0].Value is not string name||args[1].Value is not string target||
                    args[2].Kind!=TypedConstantKind.Array||args[2].IsNull||args[3].Kind!=TypedConstantKind.Array||args[3].IsNull||
                    args[2].Values.Any(value=>value.Value is not string)||args[3].Values.Any(value=>value.Value is not string)){
                    invalid=true;
                    continue;
                }
                relations.Add(new(name,target,args[2].Values.Select(value=>(string)value.Value!).ToArray(),args[3].Values.Select(value=>(string)value.Value!).ToArray()));
            }
            return new RelationTable(Fields(types[0],false),Fields(types[1],true),relations.ToArray(),invalid);
        },StringComparer.Ordinal);

        static Dictionary<string,string> Fields(INamedTypeSymbol type,bool unwrapNullable){
            return type.GetMembers().OfType<IPropertySymbol>().Where(property=>!property.IsStatic&&!property.IsIndexer&&property.DeclaredAccessibility==Accessibility.Public)
                .ToDictionary(property=>property.Name,property=>{
                    if(unwrapNullable&&property.Type is INamedTypeSymbol named&&named.OriginalDefinition.SpecialType==SpecialType.System_Nullable_T){
                        return TypeName(named.TypeArguments[0]);
                    }
                    return TypeName(property.Type);
                },StringComparer.Ordinal);
        }
    }

    private static bool Compatible(RelationTable child,RelationTable parent,ForeignKeyDesign relation){
        if(relation.Columns.Length==0||relation.Columns.Length!=relation.ReferencedColumns.Length||
            relation.ReferencedColumns.Length!=parent.Keys.Count||relation.Columns.Distinct(StringComparer.Ordinal).Count()!=relation.Columns.Length||
            relation.ReferencedColumns.Distinct(StringComparer.Ordinal).Count()!=parent.Keys.Count){return false;}
        for(var i=0;i<relation.Columns.Length;i++){
            if(!child.Columns.TryGetValue(relation.Columns[i],out var local)||!parent.Keys.TryGetValue(relation.ReferencedColumns[i],out var remote)||local!=remote){return false;}
        }
        return true;
    }
}
