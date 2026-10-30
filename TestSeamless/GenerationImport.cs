using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;

namespace TestSeamless;

public static class GenerationImport{
    public static async Task RunAsync(string directory,string connectionString){
        var generation=ComparisonGeneration.Read(directory);if(generation.State!="Prepared"){throw new InvalidOperationException("Import requires a fresh Prepared generation.");}
        await File.WriteAllTextAsync(Path.Combine(directory,"import-floating-tolerance.json"),JsonSerializer.Serialize(FloatingTolerance.FromEnvironment()));
        var schema=Path.GetDirectoryName(typeof(Program).Assembly.Location)!;var deployed=Path.Combine(directory,"schema");Directory.CreateDirectory(deployed);
        foreach(var file in Directory.EnumerateFiles(schema,"*.dll")){File.Copy(file,Path.Combine(deployed,Path.GetFileName(file)),true);}
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(deployed,"TestSeamless.Schema.dll"));var checks=new List<object>();
        var progress=Path.Combine(directory,"import-checks.jsonl");
        void Record(object check){checks.Add(check);using var stream=new FileStream(progress,FileMode.Append,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);using var writer=new StreamWriter(stream);writer.WriteLine(JsonSerializer.Serialize(check));writer.Flush();stream.Flush(true);}
        foreach(var item in generation.Databases){
            var settings=new SqlConnectionStringBuilder(connectionString){InitialCatalog=item.Name};var storage=Path.Combine(directory,"data",item.StorageKey);if(Directory.Exists(storage)){throw new IOException("An imported database directory already exists.");}
            var sizing=await SqlServerImportSizer.MeasureTablesAsync(settings.ConnectionString,item.Tables);var options=DatabaseContainerOptions.OnStorage(storage) with{TableStorage=sizing.ToDictionary(p=>p.Key,p=>new DatabaseTableStorageOptions{IndexSize=p.Value.IndexSize,PageSize=p.Value.PageSize,SegmentSize=p.Value.SegmentSize,MemorySize=p.Value.MemorySize},StringComparer.Ordinal)};
            var type=assembly.GetType(item.GeneratedName+"."+item.GeneratedName,true)!;using var source=new SqlServerImportSource(settings.ConnectionString,item.Tables,true);
            using(var database=(DatabaseContainer)Activator.CreateInstance(type,[options])!){Invoke(type,database,"EnsureTablesCreated",[]);Invoke(type,database,"ImportTables",[source]);}
            await File.WriteAllTextAsync(Path.Combine(storage,"storage-options.json"),JsonSerializer.Serialize(options));
            using var reopened=(DatabaseContainer)Activator.CreateInstance(type,[options])!;var adapter=new InProcessDatabaseAdapter(reopened,item.Tables);var registry=new DatabaseRegistry();
            registry.Register(item.Name,adapter,true,item.Fingerprint,item.Catalog.Schemas.SelectMany(s=>s.Views.Select(v=>new DatabaseViewDefinition(s.Name,v.Name,v.Definition??throw new InvalidDataException("A view definition is missing."),v.Columns.Select(c=>new SqlColumn(c.Name,ComparisonHost.ToSqlType(c.Type),c.Type.IsNullable,new(item.Name,s.Name,v.Name),c.Ordinal)).ToArray(),v.Columns))).ToArray(),ComparisonHost.ResolveRoutines(item,assembly,reopened),defaultCollation:item.Catalog.DefaultCollation);
            var engine=new SqlQueryEngine(registry);
            Console.WriteLine($"Imported {item.Name}: {source.RowsRead} rows; checking tables and executed views.");
            foreach(var table in item.Tables){
                var result=engine.Prepare("SELECT * FROM "+Quote(table.SourceSchema)+"."+Quote(table.SourceTable),item.Name).Execute();
                var comparison=ResultBag.Compare(source.ReadVerificationRows(table),result.Rows,Path.Combine(directory,"verification-work"),false);
                Record(new{Database=item.Name,Object=table.SourceSchema+"."+table.SourceTable,Kind="Table",comparison.Rows,comparison.Equal,comparison.Reason});if(!comparison.Equal){throw new InvalidDataException($"Imported table differs: {item.Name}.{table.SourceSchema}.{table.SourceTable}");}
            }
            foreach(var view in item.Catalog.Schemas.SelectMany(s=>s.Views.Select(v=>(Schema:s.Name,View:v)))){
                var sql="SELECT * FROM "+Quote(view.Schema)+"."+Quote(view.View.Name);var result=engine.Prepare(sql,item.Name).Execute();var floatingColumns=result.Columns.Where(column=>column.IsComputedFloatingPoint).Select(column=>column.Ordinal).ToHashSet();var comparison=ResultBag.Compare(source.ReadVerificationSql(sql),result.Rows,Path.Combine(directory,"verification-work"),floatingColumns.Count>0,computedFloatingPointOrdinals:floatingColumns);
                Record(new{Database=item.Name,Object=view.Schema+"."+view.View.Name,Kind="View",ComputedFloatingPointOrdinals=floatingColumns.Order().ToArray(),comparison.Rows,comparison.Equal,comparison.Reason});if(!comparison.Equal){throw new InvalidDataException($"Executed view differs: {item.Name}.{view.Schema}.{view.View.Name}");}
            }
            source.Complete();
            await File.WriteAllTextAsync(Path.Combine(directory,"import-checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        }
        await ComparisonSource.VerifySourceAsync(connectionString,generation);(generation with{State="Imported"}).Write(directory);
    }
    private static string Quote(string text)=>"["+text.Replace("]","]]")+"]";
    private static void Invoke(Type type,object target,string name,object?[] args){try{(type.GetMethod(name)??throw new MissingMethodException(type.FullName,name)).Invoke(target,args);}catch(TargetInvocationException ex) when(ex.InnerException is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();}}
}
