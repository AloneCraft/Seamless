using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class TableRestoreTests{
    [Fact]public void RestoresOneTableAndKeepsChangesInTheSameFile(){
        var live=Designer.Empty("restore","Shop");
        live=Designer.Apply(live,new("First","",[new("Id","int",true)],[]));
        live=Designer.Apply(live,new("Second","",[new("Id","int",true)],[])) with{Migration=null};
        var draft=AddColumn(live,"First","FirstValue");
        draft=AddColumn(draft,"Second","SecondValue");
        var file=draft.Source.Files.Single(file=>file.Path=="Database.cs");
        var declaration=Designer.TableSource(draft,"Second");
        draft=draft with{Source=draft.Source with{Files=draft.Source.Files.Select(item=>item.Path==file.Path?
            item with{Text=item.Text.Insert(declaration.Start,"/* second table external comment */ ")}:item).ToArray()}};
        var unchanged=Designer.TableSource(draft,"Second").Text;
        var restored=Designer.PrepareTableRestore(live,draft,"First");
        Assert.DoesNotContain(Designer.Read(restored,"First").Columns,column=>column.Name=="FirstValue");
        Assert.Contains(Designer.Read(restored,"Second").Columns,column=>column.Name=="SecondValue");
        Assert.Equal(unchanged,Designer.TableSource(restored,"Second").Text);
        Assert.Equal("Second",Assert.Single(restored.Migration!.Tables).Target);
    }

    [Fact]public void RestoresRenamedAndDeletedTablesAndRemovesNewTableTypes(){
        var live=Designer.Apply(Designer.Empty("restore","Shop"),new("Existing","Sales",[new("Id","int",true)],[])) with{Migration=null};
        var renamed=Designer.Apply(live,Designer.Read(live,"Sales.Existing") with{Name="Renamed"});
        var restored=Designer.PrepareTableRestore(live,renamed,"Sales.Renamed");
        Assert.Equal(["Sales.Existing"],Designer.Tables(restored));
        Assert.Null(restored.Migration);
        var deleted=Designer.Remove(live,"Sales.Existing");
        Assert.Equal(["Sales.Existing"],Designer.Tables(Designer.PrepareTableRestore(live,deleted,"Sales.Existing")));
        var added=Designer.Apply(live,new("New","Sales",[new("Id","int",true)],[]));
        restored=Designer.PrepareTableRestore(live,added,"Sales.New");
        Assert.Equal(["Sales.Existing"],Designer.Tables(restored));
        Assert.DoesNotContain(restored.Source.Files,file=>file.Path=="Tables/Sales_New.cs");
        Assert.Null(restored.Migration);
    }

    [Fact]public void RefusesToUndoAnotherTableToSatisfyAnExternalKey(){
        var live=Designer.Apply(Designer.Empty("restore","Shop"),new("Parent","",[new("Id","int",true)],[])) with{Migration=null};
        var draft=Designer.Apply(live,Designer.Read(live,"Parent") with{Columns=[new("Id","long",true,"Id","row.Id")]});
        draft=Designer.Apply(draft,new("Child","",[new("Id","int",true),new("ParentId","long",false)],[new("ParentLink","Parent",["ParentId"],["Id"])]));
        Assert.Equal("ForeignKeyDefinition",Assert.Throws<ManagementException>(()=>Designer.PrepareTableRestore(live,draft,"Parent")).Code);
        Assert.Contains("Child",Designer.Tables(draft));
    }

    private static Definition AddColumn(Definition definition,string table,string name){
        var design=Designer.Read(definition,table);
        return Designer.Apply(definition,design with{Columns=[..design.Columns,new(name,"int",false,null,"0")]});
    }
}
