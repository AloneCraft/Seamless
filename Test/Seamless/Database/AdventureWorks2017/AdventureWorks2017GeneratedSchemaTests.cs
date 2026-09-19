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

        var salesType=typeof(global::AdventureWorks2017.Schemas.Sales);
        Assert.Equal(
            typeof(global::Seamless.Database.Table<global::AdventureWorks2017.Keys.Customer,global::AdventureWorks2017.Records.Customer>),
            salesType.GetProperty("Customer",BindingFlags.Instance|BindingFlags.Public)!.PropertyType);
        Assert.Equal(
            typeof(global::Seamless.Database.MaterializedView<global::AdventureWorks2017.Keys.vIndividualCustomer,global::AdventureWorks2017.Records.vIndividualCustomer>),
            salesType.GetProperty("vIndividualCustomer",BindingFlags.Instance|BindingFlags.Public)!.PropertyType);
    }

    [Fact,Trait("Category","Unit")]
    public void GeneratedSchema_ContainsExpectedRecordAndKeyTypes() {
        var types=typeof(global::AdventureWorks2017.AdventureWorks2017).Assembly.GetTypes();

        Assert.Equal(92,types.Count(static type=>type.Namespace=="AdventureWorks2017.Records"&&type.IsValueType&&type.DeclaringType is null));
        Assert.Equal(91,types.Count(static type=>type.Namespace=="AdventureWorks2017.Keys"&&type.IsValueType&&type.DeclaringType is null));

        var customerRecordType=typeof(global::AdventureWorks2017.Records.Customer);
        Assert.NotNull(customerRecordType.GetProperty("CustomerID",BindingFlags.Instance|BindingFlags.Public));
        Assert.NotNull(customerRecordType.GetProperty("AccountNumber",BindingFlags.Instance|BindingFlags.Public));

        var customerKeyType=typeof(global::AdventureWorks2017.Keys.Customer);
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
