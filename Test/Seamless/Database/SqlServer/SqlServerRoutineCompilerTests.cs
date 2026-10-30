using RdbmsApi=global::Seamless.Database.Rdbms;
using Compiler=global::Seamless.Database.SqlServer.SqlServerRoutineCompiler;

namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerRoutineCompilerTests{
    [Fact,Trait("Category","Unit")]
    public void TryCompile_OnlyAcceptsValidatedCaseSubset(){
        const string sql="""
            CREATE FUNCTION [dbo].[StatusText](@Status [tinyint])
            RETURNS [nvarchar](16)
            AS
            BEGIN
                DECLARE @ret [nvarchar](16);
                SET @ret = CASE @Status WHEN 1 THEN N'Approved' WHEN 2 THEN 'It''s ok' ELSE N'Invalid' END;
                RETURN @ret;
            END;
            """;
        var number=new RdbmsApi.DatabaseType(RdbmsApi.DatabaseTypeKind.Byte,true,SqlTypeName:"tinyint");
        var result=new RdbmsApi.DatabaseType(RdbmsApi.DatabaseTypeKind.String,true,16,SqlTypeName:"nvarchar");
        var routine=new RdbmsApi.RoutineCatalog("dbo","StatusText",RdbmsApi.RoutineKind.ScalarFunction,result,[new(1,"@Status",number,false)],[],sql,false,0,1);
        Assert.True(Compiler.TryCompile(routine,out var implementation,out var reason),reason);
        Assert.NotNull(implementation);
        Assert.Equal(Compiler.CompilerProfile,implementation.CompilerProfile);
        Assert.Contains("return @Status switch",implementation.CSharpBody,StringComparison.Ordinal);
        var catalog=new RdbmsApi.DatabaseCatalog("Demo",[new RdbmsApi.SchemaCatalog("dbo",[],routines:[routine with{Implementation=implementation}])]);
        var generated=RdbmsApi.CSharpDatabaseGenerator.Generate(catalog);
        var binding=Assert.Single(generated.Routines);
        Assert.False(binding.IsStub);
        Assert.Contains(implementation.CSharpBody,binding.CSharpSource,StringComparison.Ordinal);
        var stale=routine with{Definition=sql+" ",Implementation=implementation};
        Assert.True(Assert.Single(RdbmsApi.CSharpDatabaseGenerator.Generate(new("Demo",[new("dbo",[],routines:[stale])])).Routines).IsStub);
        foreach(var invalid in new[]{
            sql.Replace("RETURN @ret;","RETURN @ret; SELECT 1;",StringComparison.Ordinal),
            sql.Replace("WHEN 2","WHEN 1",StringComparison.Ordinal),
            sql.Replace("nvarchar](16)","nvarchar](2)",StringComparison.Ordinal),
            sql.Replace("ELSE N'Invalid'", "ELSE @Status",StringComparison.Ordinal),
            sql.Replace("@Status [tinyint]","@Other [tinyint]",StringComparison.Ordinal),
            sql+" /* unterminated"
        }){
            Assert.False(Compiler.TryCompile(routine with{Definition=invalid},out var rejected,out var failure));
            Assert.Null(rejected);Assert.False(string.IsNullOrWhiteSpace(failure));
        }
    }
}
