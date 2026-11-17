using System.Runtime.Loader;
using System.Text;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;
using Seamless.Database.Transfer;
using Xunit.Abstractions;

namespace Database.Verification.Tests;

/// <summary>
/// SQL Server への書き出し。本物の SQL Server のサンプルDBを Seamless へ取り込み、別の名前の新しい DB へ書き出して、
/// 元の DB と同じになることを確かめる(表ごとの全行、列の型、主キー・一意キー・外部キー・CHECK・既定値・IDENTITY・計算列、ビューの結果)。
/// 対象は SEAMLESS_EXPORT_DATABASES(カンマ区切り、既定は Northwind と pubs)。書き出した DB は最後に消す。
/// </summary>
public sealed class SqlServerExportRoundTrip(ITestOutputHelper output){
    [SqlServerFact,Trait("Category","LiveSqlServer")]public async Task ExportedDatabaseMatchesSource(){
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")!;
        var names=(Environment.GetEnvironmentVariable("SEAMLESS_EXPORT_DATABASES")??"Northwind,pubs").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??Path.GetTempPath();
        var report=new StringBuilder();
        var failures=new List<string>();
        foreach(var name in names){
            var source=new SqlConnectionStringBuilder(connectionString){InitialCatalog=name}.ConnectionString;
            var sourceCatalog=await new SqlServerCatalogReader(source).ReadAsync();
            var generated=CSharpDatabaseGenerator.Generate(sourceCatalog,new ViewImportOptions(new SqlServerViewTranslator()));
            var directory=Path.Combine(root,"SqlServerExportRoundTrip",Guid.NewGuid().ToString("N"));
            var targetName="Seamless_Export_"+name+"_"+Guid.NewGuid().ToString("N")[..8];
            var context=new AssemblyLoadContext("SqlServerExportRoundTrip_"+name,isCollectible:true);
            try{
                var assembly=TranslatedViewImport.Compile(context,generated);
                var type=assembly.GetType($"{generated.TypeNamespace}.{generated.DatabaseName}",true)!;
                var sizing=await SqlServerImportSizer.MeasureTablesAsync(source,generated.Tables);
                var options=GeneratedDatabaseLoader.Options(directory,generated.Tables,
                    sizing.ToDictionary(static pair=>pair.Key,static pair=>new DatabaseTableStorageOptions{IndexSize=pair.Value.IndexSize,PageSize=pair.Value.PageSize,SegmentSize=pair.Value.SegmentSize,MemorySize=pair.Value.MemorySize},StringComparer.Ordinal));
                using var database=(DatabaseContainer)Activator.CreateInstance(type,[options])!;
                using(var rows=new SqlServerImportSource(source,generated.Tables)){_=GeneratedDatabaseLoader.Load(database,generated.Tables,rows);}
                // 取り込んだ DB の定義を読み直し、別の名前の新しい DB へ書き出す。
                var seamless=SeamlessCatalogReader.Read(type);
                Assert.DoesNotContain(seamless.Tables,static table=>table.Blocked);
                var result=await SqlServerDatabaseExporter.ExportAsync(database,seamless,new(connectionString,targetName){DataDirectory=await DataDirectoryAsync(connectionString,name)});
                var target=new SqlConnectionStringBuilder(connectionString){InitialCatalog=targetName}.ConnectionString;
                var targetCatalog=await new SqlServerCatalogReader(target).ReadAsync();
                await using var sourceConnection=new SqlConnection(source);await sourceConnection.OpenAsync();
                await using var targetConnection=new SqlConnection(target);await targetConnection.OpenAsync();
                var compared=0;
                // XML スキーマコレクション(型付きの xml の列が従う形の決まり)が、名前も本文も同じであること。
                static string Collections(DatabaseCatalog catalog)=>string.Join(" | ",catalog.XmlSchemaCollections.Select(static item=>item.Schema+"."+item.Name+"="+item.Definition).Order(StringComparer.Ordinal));
                if(Collections(sourceCatalog)!=Collections(targetCatalog)){failures.Add($"{name}: XML スキーマコレクションが違う(元 {sourceCatalog.XmlSchemaCollections.Count} / 書き出し先 {targetCatalog.XmlSchemaCollections.Count})");}
                foreach(var schema in sourceCatalog.Schemas){
                    foreach(var table in schema.Tables){
                        var other=targetCatalog.Schemas.SingleOrDefault(item=>item.Name==schema.Name)?.Tables.SingleOrDefault(item=>item.Name==table.Name);
                        if(other is null){failures.Add($"{name}.{schema.Name}.{table.Name}: 書き出し先に表が無い");continue;}
                        failures.AddRange(CompareTable(name+"."+schema.Name,table,other));
                        // 全行を比べる(計算列と rowversion は SQL Server が作るので除く)。
                        var columns=table.Columns.Where(static column=>column.ComputedDefinition is null&&column.Type.SqlTypeName is not ("timestamp" or "rowversion")).Select(static column=>SqlServerSchemaScript.Quote(column.Name)).ToArray();
                        var sql=$"SELECT {string.Join(",",columns)} FROM {SqlServerSchemaScript.Quote(schema.Name)}.{SqlServerSchemaScript.Quote(table.Name)}";
                        var expected=SqlRowSet.Query(sourceConnection,sql);var actual=SqlRowSet.Query(targetConnection,sql);
                        if(!expected.SetEquals(actual)){failures.Add($"{name}.{schema.Name}.{table.Name}: 行が違う(元 {expected.Count} / 書き出し先 {actual.Count})");}
                        compared++;
                    }
                    foreach(var view in schema.Views){
                        var sql=$"SELECT * FROM {SqlServerSchemaScript.Quote(schema.Name)}.{SqlServerSchemaScript.Quote(view.Name)}";
                        if(result.Issues.Any(issue=>issue.Schema==schema.Name&&issue.Name==view.Name)){continue;}
                        var expected=SqlRowSet.Query(sourceConnection,sql);var actual=SqlRowSet.Query(targetConnection,sql);
                        if(!expected.SetEquals(actual)){failures.Add($"{name}.{schema.Name}.{view.Name}: ビューの結果が違う");}
                    }
                }
                report.Append($"{name} → {targetName}: 表 {compared}、行 {result.Rows.Values.Sum()}、作れなかったビュー {result.Issues.Count}\n");
                foreach(var issue in result.Issues){report.Append($"  {issue.Schema}.{issue.Name} [{issue.Code}] {issue.Difference}\n");}
                foreach(var issue in seamless.Issues){report.Append($"  読み直しの差: {issue.Schema}.{issue.Name} [{issue.Code}]\n");}
            }finally{
                context.Unload();
                await DropAsync(connectionString,targetName);
                if(Directory.Exists(directory)){Directory.Delete(directory,true);}
            }
        }
        foreach(var failure in failures){report.Append("違い: ").Append(failure).Append('\n');}
        output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"sqlserver-export-report.txt"),report.ToString());
        Assert.True(failures.Count==0,report.ToString());
    }

    // 列の型・NULL・照合順序・IDENTITY・既定値・計算列と、主キー・一意キー・外部キー・CHECK を比べる。
    private static IEnumerable<string> CompareTable(string schema,TableCatalog expected,TableCatalog actual){
        var name=schema+"."+expected.Name;
        foreach(var column in expected.Columns){
            var other=actual.Columns.SingleOrDefault(item=>item.Name==column.Name);
            if(other is null){yield return $"{name}.{column.Name}: 列が無い";continue;}
            if(Describe(column)!=Describe(other)){yield return $"{name}.{column.Name}: 元 {Describe(column)} / 書き出し先 {Describe(other)}";}
        }
        if(expected.Columns.Count!=actual.Columns.Count){yield return $"{name}: 列の数が違う";}
        if(Key(expected.PrimaryKey)!=Key(actual.PrimaryKey)){yield return $"{name}: 主キーが違う 元 {Key(expected.PrimaryKey)} / 書き出し先 {Key(actual.PrimaryKey)}";}
        if(Set(expected.UniqueKeys.Select(static key=>key.Name+"("+string.Join(",",key.Columns.Select(static column=>column.Name))+")"))!=Set(actual.UniqueKeys.Select(static key=>key.Name+"("+string.Join(",",key.Columns.Select(static column=>column.Name))+")"))){yield return $"{name}: 一意キーが違う";}
        if(Set(expected.ForeignKeys.Select(ForeignKey))!=Set(actual.ForeignKeys.Select(ForeignKey))){yield return $"{name}: 外部キーが違う 元 {Set(expected.ForeignKeys.Select(ForeignKey))} / 書き出し先 {Set(actual.ForeignKeys.Select(ForeignKey))}";}
        if(Set(expected.CheckConstraints.Select(static check=>check.Name+":"+check.Definition+":"+check.IsDisabled))!=Set(actual.CheckConstraints.Select(static check=>check.Name+":"+check.Definition+":"+check.IsDisabled))){yield return $"{name}: CHECK が違う";}
    }

    private static string Describe(ColumnCatalog column)=>
        $"{SqlServerSchemaScript.TypeText(column.Type)} {(column.Type.IsNullable?"NULL":"NOT NULL")} {column.Type.Collation} identity={column.IsIdentity}({column.IdentitySeed},{column.IdentityIncrement}) default={column.DefaultDefinition} computed={column.ComputedDefinition}/{column.IsComputedPersisted}";
    private static string Key(PrimaryKeyCatalog? key)=>key is null?"なし":key.Name+"("+string.Join(",",key.Columns.Select(static column=>column.Name+(column.IsDescending?" DESC":"")))+")";
    private static string ForeignKey(ForeignKeyCatalog key)=>$"{key.Name}->{key.ReferencedSchema}.{key.ReferencedTable}({string.Join(",",key.Columns.Select(static pair=>pair.Column+"="+pair.ReferencedColumn))}) {key.DeleteAction}/{key.UpdateAction}";
    private static string Set(IEnumerable<string> items)=>string.Join(" | ",items.Order(StringComparer.Ordinal));

    // 書き出し先の DB ファイルは、元のサンプルDBと同じフォルダーに置く(C ドライブの既定の場所を使わない)。
    private static async Task<string> DataDirectoryAsync(string connectionString,string database){
        await using var connection=new SqlConnection(new SqlConnectionStringBuilder(connectionString){InitialCatalog="master"}.ConnectionString);
        await connection.OpenAsync();
        await using var command=new SqlCommand("SELECT TOP(1) physical_name FROM sys.master_files WHERE database_id=DB_ID(@name) AND type=0;",connection);
        command.Parameters.AddWithValue("@name",database);
        return Path.GetDirectoryName((string)(await command.ExecuteScalarAsync())!)!;
    }

    private static async Task DropAsync(string connectionString,string database){
        await using var connection=new SqlConnection(new SqlConnectionStringBuilder(connectionString){InitialCatalog="master"}.ConnectionString);
        await connection.OpenAsync();
        var quoted=SqlServerSchemaScript.Quote(database);
        await using var command=new SqlCommand($"IF DB_ID(@name) IS NOT NULL BEGIN ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quoted}; END",connection);
        command.Parameters.AddWithValue("@name",database);
        await command.ExecuteNonQueryAsync();
    }
}
