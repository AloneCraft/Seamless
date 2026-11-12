namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerSampleCatalogTests {
    [Fact,Trait("Category","Unit")]
    public void All_ContainsEveryRequestedOfficialSample() {
        var samples=global::Seamless.Database.SqlServer.SqlServerSampleCatalog.All;

        Assert.Equal(23,samples.Count);
        Assert.Equal(samples.Count,samples.Select(static sample=>sample.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(samples,static sample=>sample.Name=="WideWorldImporters");
        Assert.Contains(samples,static sample=>sample.Name=="WideWorldImportersDW");
        Assert.Contains(samples,static sample=>sample.Name=="Northwind");
        Assert.Contains(samples,static sample=>sample.Name=="pubs");
        Assert.Contains(samples,static sample=>sample.Name=="ContosoRetailDW");
        Assert.Contains(samples,static sample=>sample.Name=="AdventureWorks2025");
        Assert.Contains(samples,static sample=>sample.Name=="AdventureWorksDW2008R2");
        Assert.Equal("WideWorldImporters-Standard.bak",samples.Single(static sample=>sample.Name=="WideWorldImporters").PackageFileName);
        Assert.Equal("WideWorldImportersDW-Standard.bak",samples.Single(static sample=>sample.Name=="WideWorldImportersDW").PackageFileName);
    }

    [Fact,Trait("Category","Unit")]
    public void Get_IsCaseInsensitiveAndRejectsUnknownName() {
        var sample=global::Seamless.Database.SqlServer.SqlServerSampleCatalog.Get("northwind");

        Assert.Equal("Northwind",sample.Name);
        Assert.Throws<KeyNotFoundException>(()=>
            global::Seamless.Database.SqlServer.SqlServerSampleCatalog.Get("missing"));
    }
}
