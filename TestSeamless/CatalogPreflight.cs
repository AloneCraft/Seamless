using System.Text.Json;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
namespace TestSeamless;
public static class CatalogPreflight{
    public static void Run(string directory,ComparisonGeneration generation){
        var catalog=new Catalog(generation);var engine=new SqlQueryEngine(catalog);var outcomes=new List<object>();var failed=false;
        void Check(string database,string? schema,string name,string kind,Action check){
            try{check();outcomes.Add(new{Database=database,Schema=schema,Object=name,Kind=kind,Status="Prepared"});}
            catch(Exception exception){failed=true;outcomes.Add(new{Database=database,Schema=schema,Object=name,Kind=kind,Status="Unsupported",Reason=exception.Message});}
        }
        foreach(var database in generation.Databases){
            Check(database.Name,null,database.Name,"Database",()=>Seamless.Database.Gateway.Tds.TdsPacketWriter.ValidateCollation(database.Catalog.DefaultCollation));
            foreach(var schema in database.Catalog.Schemas){
                foreach(var table in schema.Tables){
                    Check(database.Name,schema.Name,table.Name,"Table",()=>{
                        var binding=database.Tables.Single(item=>item.SourceSchema==schema.Name&&item.SourceTable==table.Name);
                        var relation=binding.SourceSchema+"."+binding.ObjectName.Split('.')[^1];var checkpoint=Path.Combine(directory,"direct","data",database.StorageKey,relation,"checkpoint","cpr-checkpoints","00000000-0000-0000-0000-000000000000","snapshot.obj.dat");
                        if(checkpoint.Length>=248){throw new InvalidDataException($"Storage path exceeds the Tsavorite checkpoint budget for '{database.Name}.{schema.Name}.{table.Name}'. Choose a shorter -ManagedRoot.");}
                        var plan=engine.Prepare("SELECT * FROM "+Quote(schema.Name)+"."+Quote(table.Name),database.Name);ValidateWire(plan.Columns,database.Name,schema.Name,table.Name);
                    });
                }
                foreach(var view in schema.Views){
                    Check(database.Name,schema.Name,view.Name,"View",()=>{
                        var plan=engine.Prepare("SELECT * FROM "+Quote(schema.Name)+"."+Quote(view.Name),database.Name);
                        if(plan.Columns.Count!=view.Columns.Count){throw new InvalidDataException("The bound view column count differs from SQL Server.");}
                        ValidateWire(plan.Columns,database.Name,schema.Name,view.Name);
                    });
                }
            }
        }
        File.WriteAllText(Path.Combine(directory,"preflight.json"),JsonSerializer.Serialize(outcomes,new JsonSerializerOptions{WriteIndented=true}));
        if(failed){throw new InvalidDataException("Source objects contain unsupported definitions, types, collations or storage paths. See preflight.json; the current generation has not been changed.");}
    }
    private static void ValidateWire(IReadOnlyList<SqlColumn> columns,string database,string schema,string name){
        var metadata=columns.Select(column=>new ColumnMetadata(column.Name,column.Type.ClrType,column.Type.Name,column.IsNullable,false,column.Ordinal,column.Type.Length,column.Type.Precision,column.Type.Scale,column.Type.Collation,sourceDatabase:database,sourceSchema:schema,sourceTable:name)).ToArray();
        Seamless.Database.Gateway.Tds.TdsPacketWriter.ValidateMetadata(metadata);
    }
    private static string Quote(string value)=>"["+value.Replace("]","]]")+"]";
    private sealed class Catalog(ComparisonGeneration generation):ISqlCatalog{
        public long Generation=>generation.CapturedUtc.UtcTicks;
        public string? GetDatabaseCollation(string database)=>generation.Databases.SingleOrDefault(item=>item.Name.Equals(database,StringComparison.OrdinalIgnoreCase))?.Catalog.DefaultCollation;
        public SqlTable? FindTable(SqlObjectName name){var database=generation.Databases.SingleOrDefault(d=>d.Name.Equals(name.Database,StringComparison.OrdinalIgnoreCase));var schema=database?.Catalog.Schemas.SingleOrDefault(s=>s.Name.Equals(name.Schema,StringComparison.OrdinalIgnoreCase));if(schema is null){return null;}var view=schema.Views.SingleOrDefault(v=>v.Name.Equals(name.Name,StringComparison.OrdinalIgnoreCase));if(view is not null){return new(name,view.Columns.Select(c=>Column(c,name)).ToArray(),null,view.Definition);}var table=schema.Tables.SingleOrDefault(t=>t.Name.Equals(name.Name,StringComparison.OrdinalIgnoreCase));if(table is null){return null;}return new(name,table.Columns.Select(c=>Column(c,name)).ToArray(),_=>[]);}
        public SqlRoutine? FindRoutine(SqlObjectName name){var routine=generation.Databases.SingleOrDefault(d=>d.Name.Equals(name.Database,StringComparison.OrdinalIgnoreCase))?.Catalog.Schemas.SingleOrDefault(s=>s.Name.Equals(name.Schema,StringComparison.OrdinalIgnoreCase))?.Routines.SingleOrDefault(r=>r.Name.Equals(name.Name,StringComparison.OrdinalIgnoreCase));if(routine?.Definition is null){return null;}return new(name,routine.Definition);}
        private static SqlColumn Column(ColumnCatalog column,SqlObjectName source)=>new(column.Name,ComparisonHost.ToSqlType(column.Type),column.Type.IsNullable,source,column.Ordinal);
    }
}
