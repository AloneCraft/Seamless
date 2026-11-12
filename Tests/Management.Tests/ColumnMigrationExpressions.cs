using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class ColumnMigrationExpressions{
    [Theory]
    [InlineData(null,"row.Value + 1")]
    [InlineData("",null)]
    [InlineData(" \t",null)]
    [InlineData("row.Value + 2","row.Value + 2")]
    public void StructureEditingDistinguishesRetainedClearedAndReplacedExpressions(string? input,string? expected){
        var definition=ExistingTable("row.Value + 1");
        var target=Assert.Single(Designer.Objects(definition));
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var columns=baseline.Columns.Select(column=>column.Name=="Value"?column with{Expression=input}:column).ToArray();
        var result=Designer.ApplyObjectStructure(definition,baseline,columns,baseline.Parameters,baseline.ReturnType);
        Assert.True(result.Succeeded);
        var mapping=Assert.Single(Assert.Single(result.Definition!.Migration!.Tables).Columns,column=>column.Target=="Value");
        Assert.Equal("Value",mapping.Source);
        Assert.Equal(expected,mapping.Expression);
        var read=Designer.ReadObjectDefinition(result.Definition,target);
        Assert.Equal(expected,Assert.Single(read.Columns,column=>column.Name=="Value").Expression);
        Assert.Equal(baseline.Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)),read.Columns.Select(column=>(column.Name,column.Type,column.PrimaryKey)));
    }

    [Fact]
    public void UnspecifiedExpressionRetainsTheExistingDraftTextWithoutNormalizingIt(){
        var definition=ExistingTable(" \t");
        var target=Assert.Single(Designer.Objects(definition));
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var columns=baseline.Columns.Select(column=>column with{Expression=null}).ToArray();
        var result=Designer.ApplyObjectStructure(definition,baseline,columns,baseline.Parameters,baseline.ReturnType);
        Assert.True(result.Succeeded);
        Assert.Equal(" \t",Assert.Single(Assert.Single(result.Definition!.Migration!.Tables).Columns,column=>column.Target=="Value").Expression);
    }

    private static Definition ExistingTable(string expression){
        var definition=Designer.Apply(Designer.Empty("migration-expressions","Expressions"),new("Items","",[new("Id","int",true),new("Value","int",false)],[]));
        return definition with{Migration=new(1,[new("Items","Items",[new("Id","Id"),new("Value","Value",expression)],[])],[])};
    }
}
