namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerRoutineAnalyzerTests {
    [Fact,Trait("Category","Unit")]
    public void Analyze_UsesScriptDomForScalarFunction() {
        var analysis=global::Seamless.Database.SqlServer.SqlServerRoutineAnalyzer.Analyze("""
            CREATE FUNCTION dbo.Increment(@value int)
            RETURNS int
            AS
            BEGIN
                RETURN @value + 1;
            END;
            """);

        Assert.False(analysis.HasMutation);
        Assert.Equal(0,analysis.ResultSetCount);
        Assert.Equal(1,analysis.ReturnCount);
        Assert.Contains(analysis.RelationalAlgebra.Operations,static operation=>
            operation.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Return);
    }

    [Fact,Trait("Category","Unit")]
    public void Analyze_ClassifiesStoredProcedureBehavior() {
        var analysis=global::Seamless.Database.SqlServer.SqlServerRoutineAnalyzer.Analyze("""
            CREATE PROCEDURE dbo.RenameProduct
                @id int,
                @name nvarchar(50)
            AS
            BEGIN
                UPDATE dbo.Product SET Name=@name WHERE Id=@id;
                SELECT Id,Name FROM dbo.Product WHERE Id=@id;
            END;
            """);

        Assert.True(analysis.HasMutation);
        Assert.Equal(1,analysis.ResultSetCount);
        Assert.Equal(0,analysis.ReturnCount);
        Assert.Contains(analysis.RelationalAlgebra.Operations,static operation=>
            operation.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Update);
        Assert.Contains(analysis.RelationalAlgebra.Operations,static operation=>
            operation.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Select);
        var update=Assert.Single(analysis.RelationalAlgebra.Operations,static operation=>
            operation.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Update);
        Assert.Equal("dbo.Product",update.RelationName);
        Assert.Contains(update.Inputs,static input=>
            input.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Scan
            &&input.RelationName=="dbo.Product");
    }

    [Fact,Trait("Category","Unit")]
    public void Analyze_RejectsInvalidTransactSql() {
        Assert.Throws<FormatException>(()=>
            global::Seamless.Database.SqlServer.SqlServerRoutineAnalyzer.Analyze(
                "CREATE PROCEDURE dbo.Broken AS SELECT FROM;"));
    }

    [Fact,Trait("Category","Unit")]
    public void Analyze_MapsStoredProcedureCall() {
        var analysis=global::Seamless.Database.SqlServer.SqlServerRoutineAnalyzer.Analyze(
            "CREATE PROCEDURE dbo.Wrapper AS EXEC dbo.Other;" );

        var call=Assert.Single(analysis.RelationalAlgebra.Operations,static operation=>
            operation.Operator==global::Seamless.Database.Rdbms.RelationalAlgebraOperator.Call);
        Assert.Equal("dbo.Other",call.RelationName);
    }

    [Fact,Trait("Category","Unit")]
    public void Analyze_RejectsDynamicExecuteStatement() {
        Assert.Throws<NotSupportedException>(()=>
            global::Seamless.Database.SqlServer.SqlServerRoutineAnalyzer.Analyze(
                "CREATE PROCEDURE dbo.Unsupported @sql nvarchar(max) AS EXEC(@sql);"));
    }
}
