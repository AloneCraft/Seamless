using RdbmsApi=global::Seamless.Database.Rdbms;

namespace Test.Seamless.Database.Rdbms;

public sealed class GeneratedRoutineBindingTests{
    [Fact,Trait("Category","Unit")]
    public void Generate_MapsOriginalNamesToActualGeneratedMethods(){
        var integer=new RdbmsApi.DatabaseType(RdbmsApi.DatabaseTypeKind.Int32,false);
        var column=new RdbmsApi.ColumnCatalog(0,"value",integer,false);
        var catalog=new RdbmsApi.DatabaseCatalog("db name",[
            new RdbmsApi.SchemaCatalog("class",[new RdbmsApi.TableCatalog("A_B",[column],null)],routines:[
                new("class","A B",RdbmsApi.RoutineKind.ScalarFunction,integer,[],[],"SELECT 1",false,0,1),
                new("class","A-B",RdbmsApi.RoutineKind.StoredProcedure,null,[new(0,"@answer",integer,true)],[],"SELECT 2",false,0,0),
                new("class","for",RdbmsApi.RoutineKind.TableFunction,null,[],[column],"SELECT 3",false,1,0),
            ])]);
        foreach(var preserve in new[]{false,true}){
            var generated=RdbmsApi.CSharpDatabaseGenerator.Generate(catalog,preserveSqlRows:preserve,executableViews:true);
            Assert.Equal(3,generated.Routines.Count);
            var scalar=Assert.Single(generated.Routines,static item=>item.SourceName=="A B");
            var procedure=Assert.Single(generated.Routines,static item=>item.SourceName=="A-B");
            var function=Assert.Single(generated.Routines,static item=>item.SourceName=="for");
            Assert.Equal(RdbmsApi.RoutineKind.ScalarFunction,scalar.Kind);
            Assert.Equal(RdbmsApi.RoutineKind.StoredProcedure,procedure.Kind);
            Assert.Equal(RdbmsApi.RoutineKind.TableFunction,function.Kind);
            Assert.NotEqual(scalar.MethodName,procedure.MethodName);
            Assert.NotEqual("A_B",scalar.MethodName);
            Assert.NotEqual("A_B",procedure.MethodName);
            Assert.Equal("for",function.MethodName);
            Assert.All(generated.Routines,item=>{
                Assert.Equal("db name",item.SourceDatabaseName);
                Assert.Equal("class",item.SourceSchemaName);
                Assert.Equal(generated.DatabaseName+".Schemas.class",item.DeclaringTypeName);
                Assert.Equal(generated.DatabaseName+".cs",item.SourceFile);
                Assert.True(item.IsStub);
                Assert.Contains("requires relational-algebra execution",item.CSharpSource,StringComparison.Ordinal);
                Assert.Contains(item.CSharpSource,Assert.Single(generated.Files,file=>file.RelativePath==item.SourceFile).Content,StringComparison.Ordinal);
            });
            Assert.Contains(" "+scalar.MethodName+"(",scalar.CSharpSource,StringComparison.Ordinal);
            Assert.Contains(" "+procedure.MethodName+"(",procedure.CSharpSource,StringComparison.Ordinal);
            Assert.Contains(" @for(",function.CSharpSource,StringComparison.Ordinal);
            Assert.Contains("answer=default!",procedure.CSharpSource,StringComparison.Ordinal);
        }
        var empty=RdbmsApi.CSharpDatabaseGenerator.Generate(new RdbmsApi.DatabaseCatalog("empty",[]));
        Assert.Empty(empty.Routines);
    }
}
