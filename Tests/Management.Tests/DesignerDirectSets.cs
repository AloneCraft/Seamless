namespace Management.Tests;

// 取引を通さない表(db.X、Set<T>)は、設計器の一覧に種類 Set として出る(行のデータを開くため)。構造の編集は C# で行う。
// 設計：Document/refactoring-designs/20261115-取引を通さない表の作り直し.md
public sealed partial class DatabaseDesigner{
    [Fact]public void DirectSetsAreListedAsSets(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("direct","Direct");
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,
            new("Sets.cs","namespace UserCode{public sealed partial class ManagedDb{public global::Seamless.Database.Set<string> Tags=>this.Set<string>();}}\n")]}};
        var item=Assert.Single(Seamless.Management.DatabaseDesigner.Objects(definition),item=>item.Name=="Tags");
        Assert.Equal("Set",item.Kind);
        Assert.Empty(Seamless.Management.DatabaseDesigner.Tables(definition));
    }
}
