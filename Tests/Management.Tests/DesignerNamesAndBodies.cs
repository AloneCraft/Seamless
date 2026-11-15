using Seamless.Management;

namespace Management.Tests;

// 設計器の入力の検査：名前 Database は入口の Database と重なるので使えない(DB生成器の SDB002 と同じ)。
// ビュー・関数の本体は return 文1つ(式1つ)。新しく作るときの仮の本体(throw 文1つ)は認める。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の M5・M6。
public sealed partial class DatabaseDesigner{
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
