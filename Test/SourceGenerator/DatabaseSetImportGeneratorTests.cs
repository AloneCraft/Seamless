using Microsoft.CodeAnalysis;

namespace Test.SourceGenerator;

public sealed class DatabaseSetImportGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void ImportTables_TreatsDuplicateTuplesAsOneRelationMember() {
        const string source="""
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.Table<int> Items=>this.Table<int>();
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGenerators(compilation);
        var result=driver.GetRunResult().Results[0];
        var generated=Assert.Single(
            result.GeneratedSources,
            static value=>value.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();

        Assert.Contains("_=this.Items.Add(row);",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("duplicate tuple",generated,StringComparison.Ordinal);
    }
}
