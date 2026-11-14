using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Database.SqlTranslation;
namespace Database.SqlTranslation.Tests;
/// <summary>試験の中で、表の中身を名前で渡す置き場。コンパイルした仮のコンテナがここから読む。</summary>
public static class StubData{
    private static Dictionary<string,object>? sets;
    private static Func<string,object>? loader;
    private static readonly object gate=new();
    public static void Set(Dictionary<string,object> values){
        lock(gate){
            sets=values;
            loader=null;
        }
    }
    /// <summary>表を初めて読むときに loader で作り、以後は使い回す(本物の DB から読む試験用)。</summary>
    public static void SetLoader(Func<string,object> load){
        lock(gate){
            sets=[];
            loader=load;
        }
    }
    public static Seamless.Collections.ISet<T> Get<T>(string key){
        lock(gate){
            if(sets is{} values){
                if(values.TryGetValue(key,out var set)){
                    return (Seamless.Collections.ISet<T>)set;
                }
                if(loader is{} load){
                    var loaded=load(key);
                    values[key]=loaded;
                    return (Seamless.Collections.ISet<T>)loaded;
                }
            }
            return new ListSet<T>([]);
        }
    }
}
/// <summary>試験用の ISet。重複を除いた要素の並び。</summary>
public sealed class ListSet<T>(IEnumerable<T> items):Seamless.Collections.ISet<T>{
    private readonly HashSet<T> items=[..items];
    public int Count=>this.items.Count;
    public bool Contains(T item)=>this.items.Contains(item);
    public IEnumerator<T> GetEnumerator()=>this.items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
/// <summary>
/// 訳した C# を、目録から作った仮のコンテナ(読み取りの入口と表の行の型)と一緒にコンパイルする。
/// 仮のコンテナの表は <see cref="StubData"/> から読む。
/// </summary>
internal static class TranslationCompiler{
    internal sealed record Result(Assembly? Assembly,IReadOnlyList<string> Errors,string Source);
    /// <summary>目録の表(と訳せなかったビュー)の行の型と、読み取りの入口を作る。</summary>
    internal static string StubSource(TranslationCatalog catalog,IReadOnlyList<CatalogRelation> relations,IReadOnlyCollection<ViewTranslation> translated){
        var done=translated.Where(static view=>view.Succeeded).Select(static view=>(view.Schema.ToUpperInvariant(),view.Name.ToUpperInvariant())).ToHashSet();
        var root=catalog.RootNamespace;
        var builder=new StringBuilder("#nullable enable\n#pragma warning disable CS8981\n");
        var stored=relations.Where(relation=>!relation.IsView||!done.Contains((relation.Schema.ToUpperInvariant(),relation.Name.ToUpperInvariant()))).ToList();
        foreach(var relation in stored){
            var kind="Tables";
            if(relation.IsView){
                kind="Views";
            }
            builder.Append($"namespace {root}.{kind}.{CSharpNames.Identifier(relation.Schema)}{{public readonly record struct {Member(relation)}(");
            builder.Append(string.Join(",",relation.Columns.Select(static column=>$"{Storage(column)} {column.CSharpName}")));
            builder.Append(");}\n");
        }
        var schemas=relations.Select(static relation=>relation.Schema).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        builder.Append($"namespace {root}{{\npublic static class Container{{\n");
        builder.Append("public interface IReader{IReader Database{get;}");
        foreach(var schema in schemas){
            builder.Append($"I{CSharpNames.Identifier(schema)}Reader {CSharpNames.Identifier(schema)}{{get;}}");
        }
        builder.Append("}\n");
        foreach(var schema in schemas){
            builder.Append($"public interface I{CSharpNames.Identifier(schema)}Reader{{IReader Database{{get;}}");
            foreach(var relation in stored.Where(relation=>relation.Schema.Equals(schema,StringComparison.OrdinalIgnoreCase)&&!relation.IsView)){
                builder.Append($"global::Seamless.Collections.ISet<{relation.RowType}> {Member(relation)}{{get;}}");
            }
            builder.Append("}\n");
        }
        builder.Append("}\n");
        builder.Append("public sealed class StubReader:Container.IReader");
        foreach(var schema in schemas){
            builder.Append($",Container.I{CSharpNames.Identifier(schema)}Reader");
        }
        builder.Append("{\nContainer.IReader Container.IReader.Database=>this;\n");
        foreach(var schema in schemas){
            var s=CSharpNames.Identifier(schema);
            builder.Append($"Container.I{s}Reader Container.IReader.{s}=>this;\nContainer.IReader Container.I{s}Reader.Database=>this;\n");
            foreach(var relation in stored.Where(relation=>relation.Schema.Equals(schema,StringComparison.OrdinalIgnoreCase)&&!relation.IsView)){
                builder.Append($"global::Seamless.Collections.ISet<{relation.RowType}> Container.I{s}Reader.{Member(relation)}=>global::Database.SqlTranslation.Tests.StubData.Get<{relation.RowType}>(\"{relation.Schema}.{relation.Name}\");\n");
            }
        }
        builder.Append("}\n");
        // 訳せなかったビューは、写しの表と同じく、行の集合を返すだけの仮のプロパティにする。
        foreach(var schema in stored.Where(static relation=>relation.IsView).GroupBy(static relation=>relation.Schema,StringComparer.OrdinalIgnoreCase)){
            var s=CSharpNames.Identifier(schema.Key);
            builder.Append($"public static class {s}ViewStubs{{extension(Container.I{s}Reader q){{");
            foreach(var relation in schema){
                builder.Append($"public global::Seamless.Collections.ISet<{relation.RowType}> {Member(relation)}=>global::Database.SqlTranslation.Tests.StubData.Get<{relation.RowType}>(\"{relation.Schema}.{relation.Name}\");");
            }
            builder.Append("}}\n");
        }
        builder.Append("}\n");
        return builder.ToString();
    }
    internal static Result Compile(string assemblyName,params string[] sources){
        var trees=sources.Select(static source=>CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.Preview))).ToArray();
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(static path=>(MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
        foreach(var type in new[]{typeof(Seamless.Collections.ISet<>),typeof(Seamless.Linq.Set),typeof(Seamless.Database.SqlServer.Functions.SqlCompare),typeof(StubData)}){
            references.Add(MetadataReference.CreateFromFile(type.Assembly.Location));
        }
        var compilation=CSharpCompilation.Create(assemblyName,trees,references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        using var stream=new MemoryStream();
        var emitted=compilation.Emit(stream);
        var errors=emitted.Diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Select(static diagnostic=>diagnostic.ToString()).ToList();
        if(!emitted.Success){
            return new(null,errors,string.Join("\n",sources));
        }
        stream.Position=0;
        var context=new AssemblyLoadContext(assemblyName,true);
        return new(context.LoadFromStream(stream),errors,string.Join("\n",sources));
    }
    private static string Member(CatalogRelation relation)=>relation.Access[(relation.Access.LastIndexOf('.')+1)..];
    private static string Storage(CatalogColumn column){
        try{
            return column.Type.Name switch{
                "bit"=>Nullable("bool",column),
                "tinyint"=>Nullable("byte",column),
                "smallint"=>Nullable("short",column),
                "int"=>Nullable("int",column),
                "bigint"=>Nullable("long",column),
                "decimal" or "money" or "smallmoney"=>Nullable("decimal",column),
                "float"=>Nullable("double",column),
                "real"=>Nullable("float",column),
                "uniqueidentifier"=>Nullable("global::System.Guid",column),
                "date"=>Nullable("global::System.DateOnly",column),
                "datetime" or "datetime2" or "smalldatetime"=>Nullable("global::System.DateTime",column),
                "datetimeoffset"=>Nullable("global::System.DateTimeOffset",column),
                "time"=>Nullable("global::System.TimeOnly",column),
                "binary" or "varbinary" or "image" or "timestamp" or "rowversion"=>"byte[]?",
                "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml"=>"string?",
                _=>"object?"
            };
        }catch(Exception){
            return "object?";
        }
    }
    private static string Nullable(string type,CatalogColumn column){
        if(column.IsNullable){
            return type+"?";
        }
        return type;
    }
}
