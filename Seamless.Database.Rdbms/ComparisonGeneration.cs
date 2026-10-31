using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seamless.Database.Rdbms;

public sealed record ComparisonDatabase(string Name,string GeneratedName,string Fingerprint,bool ReadOnly,DatabaseCatalog Catalog,IReadOnlyList<GeneratedTableBinding> Tables,string? StorageDirectoryName=null){
    public IReadOnlyList<GeneratedRoutineBinding> Routines{get;init;}=[];
    public string StorageKey=>this.StorageDirectoryName??this.GeneratedName;
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public string? GeneratedNamespace{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]public bool RuntimeSqlEnabled{get;init;}
    [JsonIgnore]public string TypeNamespace=>this.GeneratedNamespace??this.GeneratedName;
    [JsonIgnore]public string TypeFullName=>this.TypeNamespace+"."+this.GeneratedName;
}
public sealed record ComparisonGeneration(string Id,string ProductVersion,DateTimeOffset CapturedUtc,IReadOnlyList<ComparisonDatabase> Databases,string SchemaAssembly="schema/TestSeamless.Schema.dll",string? ModuleManifest=null,string State="Prepared",string SourceServer="localhost",bool SourceIntegratedSecurity=true){
    public IReadOnlyList<string> SourceDatabaseNames{get;init;}=[];
    public const string FileName="generation.json";
    public static JsonSerializerOptions JsonOptions{get;}=CreateOptions();
    private static JsonSerializerOptions CreateOptions(){var options=new JsonSerializerOptions{WriteIndented=true};options.Converters.Add(new RuntimeTypeConverter());return options;}
    public static ComparisonGeneration Read(string directory)=>JsonSerializer.Deserialize<ComparisonGeneration>(File.ReadAllText(Path.Combine(directory,FileName)),JsonOptions)??throw new InvalidDataException("Generation manifest is empty.");
    public void Write(string directory){
        Directory.CreateDirectory(directory);var target=Path.Combine(directory,FileName);var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){JsonSerializer.Serialize(stream,this,JsonOptions);stream.Flush(true);}
        File.Move(temporary,target,true);
    }
    public static string ResolveManagedPath(string root,string relative){
        if(Path.IsPathRooted(relative)){throw new InvalidDataException("A generation path must be relative.");}
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var path=Path.GetFullPath(Path.Combine(root,relative));if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){throw new InvalidDataException("A generation path escapes its root.");}return path;
    }
    private sealed class RuntimeTypeConverter:JsonConverter<Type>{
        public override Type? Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options){var name=reader.GetString();if(name is null){return null;}return Type.GetType(name,true);}
        public override void Write(Utf8JsonWriter writer,Type value,JsonSerializerOptions options)=>writer.WriteStringValue(value.AssemblyQualifiedName);
    }
}
