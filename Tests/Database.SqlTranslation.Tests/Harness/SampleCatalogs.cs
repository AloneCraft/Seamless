using System.Text.Json;
using Seamless.Database.SqlTranslation;
namespace Database.SqlTranslation.Tests;
/// <summary>本物の SQL Server から取ったサンプルDBの目録(Fixtures の catalog.json・columns.json)を、データベースごとの翻訳の目録にする。</summary>
internal static class SampleCatalogs{
    internal sealed record SampleView(string Database,string Schema,string Name,string Definition);
    internal sealed record SampleDatabase(string Name,TranslationCatalog Catalog,IReadOnlyList<CatalogRelation> Relations,IReadOnlyList<SampleView> Views);
    // AdventureWorks の別名の型。sys.columns には元の型が出ないので、ここで直す。
    private static readonly Dictionary<string,(string Name,int? Length)> aliases=new(StringComparer.OrdinalIgnoreCase){
        ["Name"]=("nvarchar",50),
        ["Flag"]=("bit",null),
        ["NameStyle"]=("bit",null),
        ["Phone"]=("nvarchar",25),
        ["AccountNumber"]=("nvarchar",15),
        ["OrderNumber"]=("nvarchar",25),
    };
    internal static IReadOnlyList<SampleDatabase> Load(){
        var definitions=Read("catalog.json");
        var views=new List<SampleView>();
        foreach(var item in definitions.RootElement.EnumerateArray()){
            if(item.GetProperty("type").GetString()!.Trim()=="V"){
                views.Add(new(item.GetProperty("database").GetString()!,item.GetProperty("schema").GetString()!,item.GetProperty("name").GetString()!,item.GetProperty("definition").GetString()!));
            }
        }
        var columns=Read("columns.json");
        var relations=new Dictionary<string,List<CatalogRelation>>(StringComparer.OrdinalIgnoreCase);
        var collations=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        // 名前は取込の生成器と同じ規則(スキーマの中で表・ビューの名前、表の中で列の名前を一意にする)で付ける。
        var relationScopes=new Dictionary<string,CSharpNameScope>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in columns.RootElement.EnumerateArray().OrderBy(static item=>item.GetProperty("name").GetString(),StringComparer.Ordinal)){
            var database=item.GetProperty("database").GetString()!;
            var schema=item.GetProperty("schema").GetString()!;
            var name=item.GetProperty("name").GetString()!;
            var isView=item.GetProperty("type").GetString()!.Trim()=="V";
            if(!relationScopes.TryGetValue(database+"."+schema,out var relationScope)){
                relationScope=new CSharpNameScope();
                relationScope.Reserve(CSharpNames.Identifier(schema));
                relationScopes.Add(database+"."+schema,relationScope);
            }
            var relationName=relationScope.Get(name);
            var columnScope=new CSharpNameScope();
            columnScope.Reserve(relationName);
            var list=new List<CatalogColumn>();
            foreach(var column in item.GetProperty("columns").EnumerateArray()){
                var columnName=column.GetProperty("name").GetString()!;
                string? collation=null;
                if(column.TryGetProperty("collation",out var value)&&value.ValueKind==JsonValueKind.String){
                    collation=value.GetString();
                    collations.TryAdd(database,collation!);
                }
                list.Add(new(columnName,columnScope.Get(columnName),Type(column,collation),column.GetProperty("nullable").GetBoolean()));
            }
            var root=CSharpNames.Identifier(database);
            var kind="Tables";
            if(isView){
                kind="Views";
            }
            var relation=new CatalogRelation(schema,name,$"Database.{CSharpNames.Identifier(schema)}.{relationName}",$"global::{root}.{kind}.{CSharpNames.Identifier(schema)}.{relationName}",list,isView);
            if(!relations.TryGetValue(database,out var all)){
                all=[];
                relations.Add(database,all);
            }
            all.Add(relation);
        }
        return relations.Select(pair=>{
            var root=CSharpNames.Identifier(pair.Key);
            collations.TryGetValue(pair.Key,out var collation);
            var catalog=new TranslationCatalog(root,$"global::{root}.Container",collation??"SQL_Latin1_General_CP1_CI_AS",pair.Value);
            return new SampleDatabase(pair.Key,catalog,pair.Value,views.Where(view=>view.Database.Equals(pair.Key,StringComparison.OrdinalIgnoreCase)).ToList());
        }).ToList();
    }
    private static SqlTypeDescriptor Type(JsonElement column,string? collation){
        var typeName=column.GetProperty("typeName").GetString()!;
        int? length=null;
        if(column.TryGetProperty("length",out var lengthValue)&&lengthValue.ValueKind==JsonValueKind.Number){
            length=lengthValue.GetInt32();
        }
        byte? precision=null;
        byte? scale=null;
        if(column.TryGetProperty("precision",out var p)&&p.ValueKind==JsonValueKind.Number){
            precision=p.GetByte();
        }
        if(column.TryGetProperty("scale",out var s)&&s.ValueKind==JsonValueKind.Number){
            scale=s.GetByte();
        }
        if(aliases.TryGetValue(typeName,out var alias)){
            return SqlTypeDescriptor.Of(alias.Name,alias.Length,null,null,collation);
        }
        var lower=typeName.ToLowerInvariant();
        if(lower is "nvarchar" or "nchar"&&length is{} bytes&&bytes>0){
            length=bytes/2;
        }
        if(lower is "decimal" or "numeric"){
            return SqlTypeDescriptor.Of(lower,null,precision,scale);
        }
        if(lower is "datetime2" or "datetimeoffset" or "time"){
            return SqlTypeDescriptor.Of(lower,null,null,scale);
        }
        if(lower is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary"){
            return SqlTypeDescriptor.Of(lower,length,null,null,collation);
        }
        return SqlTypeDescriptor.Of(lower,null,null,null,collation);
    }
    private static JsonDocument Read(string name){
        using var stream=typeof(SampleCatalogs).Assembly.GetManifestResourceStream("Fixtures."+name)!;
        return JsonDocument.Parse(stream);
    }
}
