using Seamless.Management;

namespace Management.Tests;

// 設計器の入力の検査：名前 Database は入口の Database と重なるので使えない(DB生成器の SDB002 と同じ)。
// ビュー・関数の本体は return 文1つ(式1つ)。新しく作るときの仮の本体(throw 文1つ)は認める。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の M5・M6。
public sealed partial class DatabaseDesigner{
    [Fact]
    public void ManagedDatabasesKeepTheirDeclarationsInOneNamedFile(){
        foreach(var name in new[]{"db1","db2"}){
            var definition=Seamless.Management.DatabaseDesigner.EmptyManagedDatabase(name,name);
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","Catalog",[new("Id","int",true)],[]));
            definition=Seamless.Management.DatabaseDesigner.CreateStarterView(definition,"ItemsView","",row=>"return global::Seamless.Linq.Set.Empty<"+row+">();");
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Find","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"));
            var file=Assert.Single(definition.Source.Files);
            Assert.Equal(name+".cs",file.Path);
            Assert.Equal(name+".Database",definition.EntryType);
            Assert.Contains("Catalog.Items",Seamless.Management.DatabaseDesigner.Tables(definition));
            using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(definition.Source);
            Assert.DoesNotContain(compiled.Result.Diagnostics,diagnostic=>diagnostic.Severity=="Error");
            var renamed=Seamless.Management.DatabaseDesigner.PrepareManagedDatabase(definition,"Renamed");
            Assert.Equal("Renamed.cs",Assert.Single(renamed.Source.Files).Path);
            using var renamedBuild=new Seamless.Management.Server.RuntimeCompiler().Compile(renamed.Source);
            Assert.DoesNotContain(renamedBuild.Result.Diagnostics,diagnostic=>diagnostic.Severity=="Error");
            var recreated=Seamless.Management.DatabaseDesigner.RemoveQuery(definition,"ItemsView");
            recreated=Seamless.Management.DatabaseDesigner.CreateStarterView(recreated,"ItemsView","",row=>"return global::Seamless.Linq.Set.Empty<"+row+">();");
            Assert.Equal(name+".cs",Assert.Single(recreated.Source.Files).Path);
        }
        var legacy=Seamless.Management.DatabaseDesigner.Empty("legacy","222");
        legacy=Seamless.Management.DatabaseDesigner.Apply(legacy,new("Rows","",[new("Id","int",true)],[]));
        var converted=Seamless.Management.DatabaseDesigner.PrepareManagedDatabase(legacy,"Migrated");
        Assert.Equal("Migrated.cs",Assert.Single(converted.Source.Files).Path);
        Assert.Equal(new[]{"Rows"},Seamless.Management.DatabaseDesigner.Tables(converted));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("222"));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("CON"));
        Assert.False(Seamless.Management.DatabaseDesigner.IsManagedDatabaseName("class"));
    }
    [Fact]public void TheNameDatabaseIsRejectedForTablesSchemasViewsAndFunctions(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("names","Names");
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,new("Database","",[new("Id","int",true)],[]))).Code);
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","Database",[new("Id","int",true)],[]))).Code);
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.AddSchema(definition,"Database")).Code);
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items","",[new("Id","int",true)],[]));
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.ApplyQuery(definition,
            new("Database","",DatabaseQueryKind.ScalarFunction,[],[],"int","return 1;"))).Code);
    }

    [Fact]public void ViewAndFunctionBodiesMustBeASingleReturn(){
        var definition=Seamless.Management.DatabaseDesigner.Apply(Seamless.Management.DatabaseDesigner.Empty("bodies","Bodies"),new("Items","",[new("Id","int",true)],[]));
        var twoStatements=new QueryDesign("Count","",DatabaseQueryKind.ScalarFunction,[],[],"int","var items=q.Database.Items;\nreturn items.Count;");
        Assert.Equal("Validation",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements)).Code);
        // return 文1つは、ルートからたどる書き方でも短い書き方でも受け付ける。
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Body="return q.Database.Items.Count;"});
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Name="Count2",Body="return q.Items.Count;"});
        // 新しく作るときの仮の本体。
        Seamless.Management.DatabaseDesigner.ApplyQuery(definition,twoStatements with{Name="Draft",Body="throw new global::System.NotSupportedException();"});
    }
}
