using RdbmsApi=global::Seamless.Database.Rdbms;

namespace Test.Seamless.Database.Rdbms;

public sealed class GeneratedRuntimeProfileTests{
    [Fact,Trait("Category","Unit")]
    public void RuntimeProfileKeepsLegacyOutputAndSeparatesIdentity(){
        var integer=new RdbmsApi.DatabaseType(RdbmsApi.DatabaseTypeKind.Int32,false);
        var column=new RdbmsApi.ColumnCatalog(0,"id",integer,false);
        var catalog=new RdbmsApi.DatabaseCatalog("class",[new RdbmsApi.SchemaCatalog("dbo",[new RdbmsApi.TableCatalog("Item",[column],new RdbmsApi.PrimaryKeyCatalog("pk",[new RdbmsApi.KeyColumnCatalog("id",false)]))],routines:[new("dbo","read",RdbmsApi.RoutineKind.ScalarFunction,integer,[],[],"SELECT 1",false,0,1)])]);
        var legacy=RdbmsApi.CSharpDatabaseGenerator.Generate(catalog);
        var same=RdbmsApi.CSharpDatabaseGenerator.Generate(catalog,false,false,false);
        Assert.Equal(legacy.Files,same.Files);Assert.Equal(legacy.SchemaFingerprint,same.SchemaFingerprint);Assert.Equal(legacy.DatabaseName,legacy.TypeNamespace);Assert.Null(legacy.RootNamespace);
        var runtime=RdbmsApi.CSharpDatabaseGenerator.Generate(catalog,false,false,true);
        Assert.EndsWith("_RuntimeSqlV1",runtime.DatabaseName);Assert.Equal("Seamless.Generated."+runtime.DatabaseName,runtime.TypeNamespace);Assert.NotEqual(legacy.SchemaFingerprint,runtime.SchemaFingerprint);
        var root=Assert.Single(runtime.Files,file=>file.RelativePath==runtime.DatabaseName+".cs").Content;
        Assert.Contains("RuntimeSqlDatabaseContainer",root);Assert.Contains(runtime.SchemaFingerprint,root);Assert.Contains("this.EnsureTablesCreated()",root);Assert.Contains("this.Dispose()",root);
        var binding=Assert.Single(runtime.Routines);Assert.Equal(runtime.TypeNamespace+".Schemas.dbo",binding.DeclaringTypeName);Assert.Contains(binding.CSharpSource,root);
        Assert.All(runtime.Files,file=>Assert.DoesNotContain("global::class.",file.Content));
        var oldManifest=new RdbmsApi.ComparisonDatabase(catalog.Name,legacy.DatabaseName,legacy.SchemaFingerprint,false,catalog,legacy.Tables);
        Assert.Equal(legacy.DatabaseName+"."+legacy.DatabaseName,oldManifest.TypeFullName);Assert.False(oldManifest.RuntimeSqlEnabled);
        var manifest=oldManifest with{GeneratedName=runtime.DatabaseName,GeneratedNamespace=runtime.TypeNamespace,RuntimeSqlEnabled=true};
        Assert.Equal(runtime.TypeNamespace+"."+runtime.DatabaseName,manifest.TypeFullName);
        var json=global::System.Text.Json.JsonSerializer.Serialize(manifest,RdbmsApi.ComparisonGeneration.JsonOptions);var restored=global::System.Text.Json.JsonSerializer.Deserialize<RdbmsApi.ComparisonDatabase>(json,RdbmsApi.ComparisonGeneration.JsonOptions)!;Assert.Equal(manifest.TypeFullName,restored.TypeFullName);Assert.True(restored.RuntimeSqlEnabled);
    }
}
