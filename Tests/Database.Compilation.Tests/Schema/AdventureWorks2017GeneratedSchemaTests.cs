using System.Reflection;

namespace Test.Seamless.Database.AdventureWorks2017;

public sealed class AdventureWorks2017GeneratedSchemaTests {
    [Fact,Trait("Category","Unit")]
    public void GeneratedSchema_ExposesDatabaseAndSchemaRelations() {
        var databaseType=typeof(global::AdventureWorks2017.AdventureWorks2017);

        Assert.NotNull(databaseType.GetProperty("HumanResources",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(databaseType.GetProperty("Person",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(databaseType.GetProperty("Production",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(databaseType.GetProperty("Purchasing",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(databaseType.GetProperty("Sales",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(databaseType.GetProperty("dbo",BindingFlags.Instance|BindingFlags.Public));

        // 表は Tables.<スキーマ> に宣言し、スキーマの表のプロパティとキーの型は DB の生成器が作る。
        // 取り込んだビューは、定義を LINQ へ訳せるまで取り込んだ時点の写しを表として持つ(合意8)。
        var salesType=typeof(global::AdventureWorks2017.Schemas.Sales);
        Assert.Equal(
            typeof(global::Seamless.Database.Table<global::AdventureWorks2017.Keys.Sales.Customer,global::AdventureWorks2017.Tables.Sales.Customer>),
            salesType.GetProperty("Customer",BindingFlags.Instance|BindingFlags.Public)!.PropertyType);
        Assert.Equal(
            typeof(global::Seamless.Database.Table<global::AdventureWorks2017.Keys.Sales.vIndividualCustomer,global::AdventureWorks2017.Tables.Sales.vIndividualCustomer>),
            salesType.GetProperty("vIndividualCustomer",BindingFlags.Instance|BindingFlags.Public)!.PropertyType);
        // 取込の実名は、行の型から生成されたプロパティへ写る。
        var imported=salesType.GetProperty("Customer",BindingFlags.Instance|BindingFlags.Public)!.GetCustomAttribute<global::Seamless.Database.ImportedDatabaseObjectAttribute>();
        Assert.Equal(("Sales","Customer"),(imported?.Schema,imported?.Name));
    }

    [Fact,Trait("Category","Unit")]
    public void GeneratedSchema_ContainsExpectedRecordAndKeyTypes() {
        var types=typeof(global::AdventureWorks2017.AdventureWorks2017).Assembly.GetTypes();

        // 表71とビューの写し20。旧方式の Records 名前空間は無い。
        Assert.Equal(91,types.Count(static type=>type.Namespace?.StartsWith("AdventureWorks2017.Tables.",StringComparison.Ordinal)==true&&type.IsValueType&&type.DeclaringType is null));
        Assert.Equal(91,types.Count(static type=>type.Namespace?.StartsWith("AdventureWorks2017.Keys.",StringComparison.Ordinal)==true&&type.IsValueType&&type.DeclaringType is null));
        Assert.DoesNotContain(types,static type=>type.Namespace=="AdventureWorks2017.Records");

        var customerRecordType=typeof(global::AdventureWorks2017.Tables.Sales.Customer);
        Assert.NotNull(customerRecordType.GetProperty("CustomerID",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(customerRecordType.GetProperty("AccountNumber",BindingFlags.Instance|BindingFlags.Public));

        var customerKeyType=typeof(global::AdventureWorks2017.Keys.Sales.Customer);
        Assert.NotNull(customerKeyType.GetProperty("CustomerID",BindingFlags.Instance|BindingFlags.Public));
        Assert.Single(customerKeyType.GetProperties(BindingFlags.Instance|BindingFlags.Public));
    }

    [Fact,Trait("Category","Unit")]
    public void GeneratedSchema_CatalogsImportedSqlServerRoutines() {
        using var database=new global::AdventureWorks2017.AdventureWorks2017();
        var catalog=database.DatabaseObjects;

        Assert.Equal(
            global::Seamless.Database.System.DatabaseObjectKind.ScalarFunction,
            catalog[new global::Seamless.Database.System.Keys.DatabaseObject("dbo.ufnGetStock")].Kind);
        Assert.Equal(
            typeof(int?),
            catalog[new global::Seamless.Database.System.Keys.DatabaseObject("dbo.ufnGetStock")].RecordType);
        Assert.Equal(
            global::Seamless.Database.System.DatabaseObjectKind.TableFunction,
            catalog[new global::Seamless.Database.System.Keys.DatabaseObject("dbo.ufnGetContactInformation")].Kind);
        Assert.Equal(
            global::Seamless.Database.System.DatabaseObjectKind.StoredProcedure,
            catalog[new global::Seamless.Database.System.Keys.DatabaseObject("dbo.uspLogError")].Kind);
    }
}
