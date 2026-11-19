using System.Text.Json;
using Seamless.Database;

namespace Seamless.Management.Server;

internal sealed record TableStorageLocation(string Table,string Root,Guid Allocation);

internal sealed record TableStorageLayout(Guid Id,bool Bundled,TableStorageLocation[] Locations);

internal sealed class TableStorage{
    private const string LayoutFile="table-storage-layout.json";
    private const string BundleDirectory="external-tables";
    private const string OwnerFile="owner.txt";
    private static readonly StringComparer PathComparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
    private readonly Dictionary<string,Dictionary<string,string>> configured=new(StringComparer.Ordinal);

    internal TableStorage(string managementRoot){
        var path=Path.Combine(managementRoot,"table-storage.json");
        if(!File.Exists(path)){return;}
        SourceWorkspace.CheckLinks(path);
        using var file=File.OpenRead(path);
        CheckSize(file);
        using var json=JsonDocument.Parse(file);
        if(json.RootElement.ValueKind!=JsonValueKind.Object){throw Invalid(path,"DB名をキーにしたJSONオブジェクトを指定してください。");}
        foreach(var database in json.RootElement.EnumerateObject()){
            if(database.Value.ValueKind!=JsonValueKind.Object){throw Invalid(path,"テーブル名をキーにしたJSONオブジェクトを指定してください。"+database.Name);}
            var tables=new Dictionary<string,string>(StringComparer.Ordinal);
            if(!this.configured.TryAdd(database.Name,tables)){throw Invalid(path,"DB名が重複しています。"+database.Name);}
            foreach(var table in database.Value.EnumerateObject()){
                var root=table.Value.ValueKind==JsonValueKind.String?table.Value.GetString():null;
                if(string.IsNullOrWhiteSpace(table.Name)||string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root)){
                    throw Invalid(path,"DB・テーブルと絶対パスを指定してください。"+database.Name+"."+table.Name);
                }
                root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                if(Overlaps(root,managementRoot)||this.configured.Values.SelectMany(set=>set.Values).Any(other=>Overlaps(root,other))){
                    throw Invalid(path,"テーブルの保存先は管理領域および他のテーブルから分けてください。"+database.Name+"."+table.Name+" "+root);
                }
                if(!tables.TryAdd(table.Name,root)){throw Invalid(path,"テーブル名が重複しています。"+database.Name+"."+table.Name);}
            }
        }
    }

    internal Dictionary<string,string> Plan(string database,Definition definition,Type type,Type? previousType,TableStorageLayout? previous){
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        this.configured.TryGetValue(database,out var settings);
        if(settings is null&&previous is null){return result;}
        var existing=previousType is null?new HashSet<string>(StringComparer.Ordinal):
            DatabaseTableDeclaration.Read(previousType).Select(table=>table.Path).ToHashSet(StringComparer.Ordinal);
        foreach(var table in DatabaseTableDeclaration.Read(type)){
            var name=table.Path;
            var oldName=definition.Migration?.Tables.FirstOrDefault(mapping=>mapping.Target==name)?.Source??name;
            var prior=previous?.Locations.FirstOrDefault(location=>location.Table==oldName)?.Root;
            var configuredRoot=settings?.GetValueOrDefault(name);
            var added=definition.Migration?.NewTables?.Contains(name,StringComparer.Ordinal)==true||!existing.Contains(oldName);
            if(!added&&configuredRoot is not null&&!PathComparer.Equals(prior,configuredRoot)){
                throw new ManagementException("TableStorage","公開済みテーブルの保存先は変更できません。"+database+"."+name+" "+configuredRoot);
            }
            var root=added?configuredRoot:prior;
            if(root is not null){result.Add(name,root);}
        }
        return result;
    }

    internal bool HasConfiguration(string database)=>this.configured.TryGetValue(database,out var tables)&&tables.Count>0;

    internal static Guid? Allocate(string generation,IReadOnlyDictionary<string,string> placements){
        if(placements.Count==0){return null;}
        var id=Guid.NewGuid();
        var locations=new List<TableStorageLocation>();
        foreach(var (table,root) in placements.OrderBy(item=>item.Key,StringComparer.Ordinal)){
            var location=new TableStorageLocation(table,root,Guid.NewGuid());
            var allocation=AllocationPath(location);
            try{
                SourceWorkspace.CheckLinks(allocation);
                Directory.CreateDirectory(allocation);
                using(var owner=new FileStream(Path.Combine(allocation,OwnerFile),FileMode.CreateNew,FileAccess.Write,FileShare.Read)){
                    using var writer=new StreamWriter(owner,leaveOpen:true);writer.Write(Owner(id,location));writer.Flush();owner.Flush(true);
                }
                Directory.CreateDirectory(Path.Combine(allocation,"data"));
                locations.Add(location);
            }catch(Exception error) when(error is IOException or UnauthorizedAccessException){
                throw Invalid(root,"テーブル「"+table+"」の保存先を準備できません。"+error.Message);
            }
        }
        SourceWorkspace.Atomic(Path.Combine(generation,LayoutFile),JsonSerializer.Serialize(new TableStorageLayout(id,false,locations.ToArray()),ManagementJson.Options));
        return id;
    }

    internal static TableStorageLayout? Read(string generation){
        var path=Path.Combine(generation,LayoutFile);
        if(!File.Exists(path)){return null;}
        SourceWorkspace.CheckLinks(path);
        using var file=File.OpenRead(path);
        CheckSize(file);
        var layout=JsonSerializer.Deserialize<TableStorageLayout>(file,ManagementJson.Options);
        if(layout is null||layout.Id==Guid.Empty||layout.Locations is not {Length:>0} entries||entries.Any(entry=>entry is null||string.IsNullOrWhiteSpace(entry.Table)||
            string.IsNullOrWhiteSpace(entry.Root)||!Path.IsPathFullyQualified(entry.Root)||entry.Allocation==Guid.Empty)||
            entries.Select(entry=>entry.Table).Distinct(StringComparer.Ordinal).Count()!=entries.Length||
            entries.Select(entry=>entry.Allocation).Distinct().Count()!=entries.Length){
            throw Invalid(path,"公開世代のテーブル別保存先が不正です。");
        }
        return layout;
    }

    internal static Dictionary<string,string>? OpenDirectories(string generation,Guid? expected){
        var layout=Read(generation);
        CheckIdentity(generation,layout,expected);
        return layout?.Locations.ToDictionary(location=>location.Table,location=>DataPath(generation,layout,location),StringComparer.Ordinal);
    }

    internal static void Bundle(string source,string destination,CancellationToken cancellation){
        var layout=Read(source);
        var manifestPath=Path.Combine(source,"manifest.json");
        if(File.Exists(manifestPath)){
            SourceWorkspace.CheckLinks(manifestPath);
            using var file=File.OpenRead(manifestPath);
            if(file.Length>64L<<20){throw Invalid(manifestPath,"公開世代の記録が大きすぎます。");}
            using var manifest=JsonDocument.Parse(file);
            var expected=manifest.RootElement.TryGetProperty("tableStorageId",out var value)?value.GetGuid():(Guid?)null;
            CheckIdentity(source,layout,expected);
        }
        if(layout is null){return;}
        if(layout.Bundled){foreach(var location in layout.Locations){_=DataPath(source,layout,location);}return;}
        foreach(var location in layout.Locations){
            var from=DataPath(source,layout,location);
            var to=BundlePath(destination,location);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            NativeRuntime.CopyGenerationDirectory(from,to,cancellation,bundleTables:false);
        }
        SourceWorkspace.Atomic(Path.Combine(destination,LayoutFile),JsonSerializer.Serialize(layout with{Bundled=true},ManagementJson.Options));
    }

    private static string DataPath(string generation,TableStorageLayout layout,TableStorageLocation location){
        var path=layout.Bundled?BundlePath(generation,location):Path.Combine(AllocationPath(location),"data");
        try{
            SourceWorkspace.CheckLinks(path);
            if(!Directory.Exists(path)){throw new DirectoryNotFoundException(path);}
            if(!layout.Bundled){
                var owner=Path.Combine(AllocationPath(location),OwnerFile);
                SourceWorkspace.CheckLinks(owner);
                if(new FileInfo(owner).Length>4096||File.ReadAllText(owner)!=Owner(layout.Id,location)){throw new InvalidDataException("所有記録が一致しません。");}
            }
            return path;
        }catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException){
            throw Invalid(path,"テーブル「"+location.Table+"」の保存先を開けません。"+error.Message);
        }
    }

    private static string AllocationPath(TableStorageLocation location)=>Path.Combine(location.Root,"seamless-"+location.Allocation.ToString("N"));
    private static string BundlePath(string generation,TableStorageLocation location)=>Path.Combine(generation,BundleDirectory,location.Allocation.ToString("N"));
    private static string Owner(Guid id,TableStorageLocation location)=>id.ToString("N")+"\n"+location.Allocation.ToString("N")+"\n"+location.Table;
    private static void CheckIdentity(string generation,TableStorageLayout? layout,Guid? expected){
        if(layout?.Id!=expected){throw Invalid(generation,"公開世代のテーブル別保存先の記録が一致しません。");}
    }
    private static bool Overlaps(string first,string second)=>Contains(first,second)||Contains(second,first);
    private static bool Contains(string root,string path){
        var relative=Path.GetRelativePath(root,path);
        return !Path.IsPathRooted(relative)&&relative!=".."&&!relative.StartsWith(".."+Path.DirectorySeparatorChar,StringComparison.Ordinal);
    }
    private static void CheckSize(FileStream file){if(file.Length>4L<<20){throw Invalid(file.Name,"テーブル別保存先の記録が大きすぎます。");}}
    private static ManagementException Invalid(string path,string message)=>new("TableStorage",message+" ["+path+"]");
}
