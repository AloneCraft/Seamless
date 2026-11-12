using System.IO;
using Seamless.Database.SqlServer;

namespace Seamless.Database.Gateway.Tests;

public sealed class SqlServerSystemCatalogTests {
    [Fact]
    public void DefaultCatalogCoversWholeSystemSurface() {
        var catalog=SqlServerSystemCatalog.Default;
        Assert.Equal("17.0.1000.7",catalog.ProductVersion);
        int Count(SqlServerSystemScope scope,string type)=>catalog.Objects.Count(o=>o.Scope==scope&&o.Type==type);
        Assert.Equal(643,Count(SqlServerSystemScope.System,"V"));
        Assert.Equal(1497,Count(SqlServerSystemScope.System,"P"));
        Assert.Equal(359,Count(SqlServerSystemScope.System,"X"));
        Assert.Equal(145,Count(SqlServerSystemScope.Msdb,"U"));
        foreach(var o in catalog.Objects.Where(static o=>o.Type is "V" or "S" or "U" or "IF" or "TF")) {
            Assert.True(o.Columns.Count>0,$"{o.Scope}.{o.Schema}.{o.Name} has no columns");
        }
    }

    [Fact]
    public void ResolvesColumnsSsmsAsksFor() {
        // SSMS 22 の「上位1000行の選択」が送った問い合わせが参照した列。手書きの sys.all_columns には無かった。
        var columns=SqlServerSystemCatalog.Default.Resolve("AdventureWorks2016","SYS","All_Columns")!.Columns.Select(static c=>c.Name).ToArray();
        Assert.Contains("graph_type",columns);
        Assert.Contains("generated_always_type",columns);
        Assert.Contains("is_dropped_ledger_column",columns);
        Assert.Equal("object_id",columns[0]);
    }

    [Theory]
    [InlineData("master","sys","master_files","V")]
    [InlineData("AdventureWorks2016","INFORMATION_SCHEMA","COLUMNS","V")]
    [InlineData("msdb","dbo","syspolicy_configuration","V")]
    [InlineData("master","dbo","spt_values","V")]
    [InlineData("AdventureWorks2016","sys","sp_executesql","X")]
    public void ResolvesBySqlServerLookupRules(string database,string schema,string name,string type) {
        Assert.Equal(type,SqlServerSystemCatalog.Default.Resolve(database,schema,name)?.Type);
    }

    [Theory]
    [InlineData("AdventureWorks2016","dbo","syspolicy_configuration")]
    [InlineData("msdb","dbo","spt_values")]
    [InlineData("master","sys","no_such_view")]
    public void DoesNotResolveOutsideItsScope(string database,string schema,string name) {
        Assert.Null(SqlServerSystemCatalog.Default.Resolve(database,schema,name));
    }

    [Fact]
    public void KeepsParametersAndResultColumns() {
        var catalog=SqlServerSystemCatalog.Default;
        var parameters=catalog.Resolve("master","sys","sp_columns")!.Parameters;
        Assert.Equal("@table_name",parameters[0].Name);
        var function=catalog.Resolve("master","sys","fn_varbintohexstr")!;
        Assert.Equal(0,function.Parameters[0].ParameterId);
        Assert.Contains(catalog.Objects,static o=>o.ResultSet==SqlServerSystemResultSet.Columns&&o.ResultColumns.Count>0);
        Assert.Contains(catalog.Objects,static o=>o.ResultSet==SqlServerSystemResultSet.Undescribable&&o.UndescribableReason=="TEMPORARY_TABLE");
    }

    [Theory]
    [InlineData("columns.tsv","scope\tschema\tobject\tcolumn_id\tname\ttype\tmax_length\tprecision\tscale\tis_nullable\tcollation\nsystem\tsys\tv\t2\tb\tint\t4\t10\t0\t0\t\n","column_id 2 is out of order")]
    [InlineData("columns.tsv","scope\tschema\tobject\tcolumn_id\tname\ttype\tmax_length\tprecision\tscale\tis_nullable\tcollation\nsystem\tsys\tother\t1\tb\tint\t4\t10\t0\t0\t\n","unknown object")]
    [InlineData("columns.tsv","scope\tschema\tobject\n","unexpected header")]
    [InlineData("objects.tsv","scope\tschema\tname\ttype\tobject_id\tresult_set\nsystem\tsys\tv\tV\t-1\t\nsystem\tsys\tV\tV\t-2\t\n","duplicate object")]
    [InlineData("objects.tsv","scope\tschema\tname\ttype\tobject_id\tresult_set\ntempdb\tsys\tv\tV\t-1\t\n","unknown scope")]
    [InlineData("result_columns.tsv","scope\tschema\tobject\tordinal\tname\ttype\tis_nullable\nsystem\tsys\tv\t1\ta\tint\t1\n","without a described result set")]
    public void RejectsBrokenDefinitionFiles(string file,string content,string message) {
        var files=new Dictionary<string,string> {
            ["objects.tsv"]="scope\tschema\tname\ttype\tobject_id\tresult_set\nsystem\tsys\tv\tV\t-1\t\n",
            ["columns.tsv"]="scope\tschema\tobject\tcolumn_id\tname\ttype\tmax_length\tprecision\tscale\tis_nullable\tcollation\nsystem\tsys\tv\t1\ta\tint\t4\t10\t0\t0\t\n",
            ["typical_values.tsv"]="scope\tschema\tobject\tcolumn\tvalue\n",
            ["parameters.tsv"]="scope\tschema\tobject\tparameter_id\tname\ttype\tmax_length\tprecision\tscale\tis_output\thas_default_value\n",
            ["result_columns.tsv"]="scope\tschema\tobject\tordinal\tname\ttype\tis_nullable\n",
            ["source.txt"]="product_version\t17.0.1000.7\n",
        };
        Assert.Equal("v",SqlServerSystemCatalog.Load(name=>new StringReader(files[name])).Objects.Single().Name);
        files[file]=content;
        var error=Assert.Throws<InvalidDataException>(()=>SqlServerSystemCatalog.Load(name=>new StringReader(files[name])));
        Assert.Contains(message,error.Message);
    }
}
