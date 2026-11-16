using Seamless.Management;

namespace Management.Tests;

// Tables の行の型だけで宣言した表（表のプロパティを DB の生成器が作る表。SQL Server からの取込の出力もこの形）を、
// 設計器が一覧・読み取り・列の編集・削除できること。名前・所属・外部キーの変更は、表のプロパティそのものの変更なので未対応として止める。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の M2。
public sealed partial class DatabaseDesigner{
    private static Definition GeneratedTables(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("generated","Generated");
        definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"Sales");
        return definition with{Source=definition.Source with{Files=[..definition.Source.Files,
            new("Tables/Item.cs","namespace UserCode.Tables{public partial struct Item{[global::Seamless.Database.Key] public partial int Id{get;} public partial string Name{get;}}}\n"),
            new("Tables/Sales/Order.cs","namespace UserCode.Tables.Sales{public partial struct Order{[global::Seamless.Database.Key] public partial int Id{get;} public partial decimal Total{get;}}}\n"),
            new("Tables/Keyless.cs","namespace UserCode.Tables{public partial struct Keyless{public partial int Value{get;}}}\n")]}};
    }

    [Fact]public void GeneratedTablesAreListedAndReadFromTheirRowType(){
        var definition=GeneratedTables();
        var tables=Seamless.Management.DatabaseDesigner.Tables(definition);
        Assert.Contains("Item",tables);
        Assert.Contains("Sales.Order",tables);
        // 主キー([Key])の無い型は、生成器が作る表として扱わない。
        Assert.DoesNotContain("Keyless",tables);
        var order=Seamless.Management.DatabaseDesigner.Read(definition,"Sales.Order");
        Assert.Equal("Sales",order.Schema);
        Assert.Equal(["Id","Total"],order.Columns.Select(column=>column.Name).ToArray());
        Assert.True(order.Columns.Single(column=>column.Name=="Id").PrimaryKey);
        Assert.Empty(order.ForeignKeys);
        var source=Seamless.Management.DatabaseDesigner.TableSource(definition,"Sales.Order");
        Assert.Equal("Tables/Sales/Order.cs",source.Path);
        Assert.Contains("struct Order",source.Text);
    }

    [Fact]public void EditingAGeneratedTableChangesOnlyItsRowType(){
        var definition=GeneratedTables();
        var order=Seamless.Management.DatabaseDesigner.Read(definition,"Sales.Order");
        var edited=Seamless.Management.DatabaseDesigner.Apply(definition,order with{Columns=[..order.Columns,new("Note","string?",false)]});
        Assert.Contains("Note",edited.Source.Files.Single(file=>file.Path=="Tables/Sales/Order.cs").Text);
        // 表のプロパティは書かない(生成器が作る)。仮のファイルも利用者のソースに入らない。
        foreach(var file in edited.Source.Files.Where(file=>file.Path!="Tables/Sales/Order.cs")){
            Assert.Equal(definition.Source.Files.Single(original=>original.Path==file.Path).Text,file.Text);
        }
        Assert.DoesNotContain(edited.Source.Files,file=>file.Path.StartsWith("<",StringComparison.Ordinal));
        var read=Seamless.Management.DatabaseDesigner.Read(edited,"Sales.Order");
        Assert.Equal(["Id","Total","Note"],read.Columns.Select(column=>column.Name).ToArray());
    }

    [Fact]public void RenamingMovingOrRelatingAGeneratedTableIsRejectedExplicitly(){
        var definition=GeneratedTables();
        var order=Seamless.Management.DatabaseDesigner.Read(definition,"Sales.Order");
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,order with{Name="Order2"})).Code);
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,order with{Schema=""})).Code);
        var relation=new ForeignKeyDesign("FK_Order_Item","Item",["Id"],["Id"]);
        Assert.Equal("Unsupported",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,order with{ForeignKeys=[relation]})).Code);
    }

    [Fact]public void RemovingAGeneratedTableRemovesItsRowType(){
        var definition=GeneratedTables();
        var removed=Seamless.Management.DatabaseDesigner.Remove(definition,"Item");
        Assert.DoesNotContain("Item",Seamless.Management.DatabaseDesigner.Tables(removed));
        Assert.DoesNotContain(removed.Source.Files,file=>file.Path=="Tables/Item.cs"||file.Text.Contains("struct Item",StringComparison.Ordinal));
    }

    // 設計器で作った表(手書きの表のプロパティ＋Tables の行の型)を消すと、行の型も消える。残すと DB の生成器が行の型から表を作り直し、表が消えない。
    // ほかの表と共有している行の型は消さない。
    [Fact]public void RemovingADesignedTableAlsoRemovesItsTablesRowType(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("remove","Remove");
        definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"Sales");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("DraftOnly","",[new("Id","int",true)],[]));
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Customers","Sales",[new("Id","int",true),new("Name","string",false)],[]));
        var removed=Seamless.Management.DatabaseDesigner.Remove(definition,"DraftOnly");
        Assert.DoesNotContain("DraftOnly",Seamless.Management.DatabaseDesigner.Tables(removed));
        Assert.DoesNotContain(removed.Source.Files,file=>file.Text.Contains("struct DraftOnly",StringComparison.Ordinal));
        removed=Seamless.Management.DatabaseDesigner.Remove(removed,"Sales.Customers");
        Assert.Empty(Seamless.Management.DatabaseDesigner.Tables(removed));
        // 消した後も同じ名前で作り直せる(行の型が残っていない)。
        Assert.Contains("DraftOnly",Seamless.Management.DatabaseDesigner.Tables(Seamless.Management.DatabaseDesigner.Apply(removed,new("DraftOnly","",[new("Id","int",true)],[]))));
    }
}
