using Seamless.Management;

namespace Management.Tests;

// 空のスキーマだけを消せる(所属先のプロパティとスキーマの型の宣言を消す)。中身のあるスキーマは中の定義や行のデータまで消すので消さない。
public sealed partial class DatabaseDesigner{
    [Fact]public void EmptySchemaIsRemovedWithItsDeclarations(){
        var definition=Seamless.Management.DatabaseDesigner.AddSchema(Seamless.Management.DatabaseDesigner.Empty("schemas","Schemas"),"v1");
        Assert.Contains("v1",Seamless.Management.DatabaseDesigner.Schemas(definition));
        var removed=Seamless.Management.DatabaseDesigner.RemoveSchema(definition,"v1");
        Assert.DoesNotContain("v1",Seamless.Management.DatabaseDesigner.Schemas(removed));
        Assert.DoesNotContain(removed.Source.Files,file=>file.Path.Contains("Schema_v1",StringComparison.Ordinal)||file.Text.Contains("Schema_v1",StringComparison.Ordinal));
        // 消した後も同じ名前で作り直せる(残りが無い)。
        Assert.Contains("v1",Seamless.Management.DatabaseDesigner.Schemas(Seamless.Management.DatabaseDesigner.AddSchema(removed,"v1")));
    }

    [Fact]public void SchemaWithObjectsIsNotRemoved(){
        var definition=Seamless.Management.DatabaseDesigner.AddSchema(Seamless.Management.DatabaseDesigner.Empty("schemas","Schemas"),"Sales");
        var withTable=Seamless.Management.DatabaseDesigner.Apply(definition,new TableDesign("Orders","Sales",[new("Id","int",true)],[]));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RemoveSchema(withTable,"Sales")).Code);
        var withView=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Recent","Sales",DatabaseQueryKind.View,[new("Id","int",false)],[],"","throw new System.NotImplementedException();"));
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RemoveSchema(withView,"Sales")).Code);
        Assert.Equal("NotFound",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.RemoveSchema(definition,"Missing")).Code);
    }
}
