using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;

namespace TestSeamless;

internal static class Diagnostics{
    public static Task RunAsync(string directory,string databaseName,string objectName,string connectionString){
        var generation=ComparisonGeneration.Read(directory);var item=generation.Databases.Single(x=>x.Name==databaseName);var parts=objectName.Split('.');if(parts.Length!=2){throw new ArgumentException("Expected schema.view.");}
        var storage=Path.Combine(directory,"data",item.StorageKey);var options=JsonSerializer.Deserialize<DatabaseContainerOptions>(File.ReadAllText(Path.Combine(storage,"storage-options.json")))??throw new InvalidDataException("Missing storage options.");
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory,"schema","TestSeamless.Schema.dll"));var type=assembly.GetType(item.TypeFullName,true)!;ComparisonHost.ValidateRuntimeProfile(item,type,options);
        using var reopened=(DatabaseContainer)Activator.CreateInstance(type,[options])!;var adapter=new InProcessDatabaseAdapter(reopened,item.Tables);var registry=new DatabaseRegistry();
        registry.Register(item.Name,adapter,true,item.Fingerprint,item.Catalog.Schemas.SelectMany(s=>s.Views.Select(v=>new DatabaseViewDefinition(s.Name,v.Name,v.Definition??throw new InvalidDataException("Missing view definition."),v.Columns.Select(c=>new SqlColumn(c.Name,ComparisonHost.ToSqlType(c.Type),c.Type.IsNullable,new(item.Name,s.Name,v.Name),c.Ordinal)).ToArray()))).ToArray(),ComparisonHost.ResolveRoutines(item,assembly,reopened));
        var sql="SELECT * FROM "+Quote(parts[0])+"."+Quote(parts[1]);var engine=new SqlQueryEngine(registry);var plan=engine.Prepare(sql,item.Name);var settings=new SqlConnectionStringBuilder(connectionString){InitialCatalog=item.Name};using var source=new SqlServerImportSource(settings.ConnectionString,item.Tables,true);
        var expected=source.ReadVerificationSql(sql).Select(Format).Order(StringComparer.Ordinal).ToArray();var actual=plan.Execute().Rows.Select(Format).Order(StringComparer.Ordinal).ToArray();
        Console.WriteLine("Columns: "+string.Join(", ",plan.Columns.Select(x=>x.Name)));Console.WriteLine($"Rows: source={expected.Length}, query={actual.Length}");
        var i=0;var j=0;while(i<expected.Length&&j<actual.Length&&expected[i]==actual[j]){i++;j++;}if(i<expected.Length){Console.WriteLine("Source first unmatched: "+expected[i]);}if(j<actual.Length){Console.WriteLine("Query first unmatched: "+actual[j]);}return Task.CompletedTask;
    }
    private static string Quote(string text)=>"["+text.Replace("]","]]")+"]";
    private static string Format(object?[] row)=>JsonSerializer.Serialize(row.Select(value=>value is decimal number?number.ToString("G29",System.Globalization.CultureInfo.InvariantCulture):value).ToArray());
}
