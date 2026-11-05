using Seamless.Management;

namespace Management.Tests;

public sealed class RemovedTableTests{
    private static Definition Definition(){var definition=Seamless.Management.DatabaseDesigner.Empty("remove","Example");definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Customers","Sales",[new("Id","int",true),new("Name","string",false)],[]));definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Notes","Sales",[new("Id","int",true)],[]));return definition with{Migration=null};}
    [Fact]public void RestoreOnlyTheRemovedTableAndKeepOtherTableChanges(){
        var original=Definition();var removal=Seamless.Management.DatabaseDesigner.CaptureRemoval(original,"Sales.Customers");var removed=Seamless.Management.DatabaseDesigner.Remove(original,"Sales.Customers");
        var other=Seamless.Management.DatabaseDesigner.Read(removed,"Sales.Notes");var changed=Seamless.Management.DatabaseDesigner.Apply(removed,other with{Columns=[..other.Columns,new("Value","int",false,null,"0")]});
        var restored=Seamless.Management.DatabaseDesigner.RestoreRemoved(changed,removal);Assert.Contains("Sales.Customers",Seamless.Management.DatabaseDesigner.Tables(restored));Assert.Contains(Seamless.Management.DatabaseDesigner.Read(restored,"Sales.Notes").Columns,column=>column.Name=="Value");Assert.Empty(restored.Migration!.DroppedTables);
        Assert.Equal(Seamless.Management.DatabaseDesigner.TableSource(original,"Sales.Customers").Text,Seamless.Management.DatabaseDesigner.TableSource(restored,"Sales.Customers").Text);
    }
    [Fact]public void ChangedRowTypeOrDeclarationContextIsNotSilentlyOverwritten(){
        var original=Definition();var removal=Seamless.Management.DatabaseDesigner.CaptureRemoval(original,"Sales.Customers");var removed=Seamless.Management.DatabaseDesigner.Remove(original,"Sales.Customers");
        var changed=removed with{Source=removed.Source with{Files=removed.Source.Files.Select(file=>file with{Text=file.Text.Replace("partial string Name","partial long Name")}).ToArray()}};Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RestoreRemoved(changed,removal));
        Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RestoreRemoved(removed with{Id="another"},removal));Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RestoreRemoved(original,removal));
    }
    [Fact]public void NewTableRemovalRestoresOnlyItsNewTableMetadata(){
        var original=Definition();var created=Seamless.Management.DatabaseDesigner.Apply(original,new("DraftOnly","",[new("Id","int",true)],[]));var removal=Seamless.Management.DatabaseDesigner.CaptureRemoval(created,"DraftOnly");var removed=Seamless.Management.DatabaseDesigner.Remove(created,"DraftOnly");var restored=Seamless.Management.DatabaseDesigner.RestoreRemoved(removed,removal);
        Assert.Contains("DraftOnly",restored.Migration!.NewTables!);Assert.Empty(restored.Migration.DroppedTables);Assert.Contains("DraftOnly",Seamless.Management.DatabaseDesigner.Tables(restored));
    }
}
