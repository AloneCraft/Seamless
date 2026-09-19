using Microsoft.CodeAnalysis;

namespace Test.SourceGenerator;

public sealed class DatabaseMaterializedViewGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void MaterializedView_IsCatalogedAsViewAndIncludedInImportWiring() {
        const string source="""
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.MaterializedView<int,int> Totals=>
                    this.MaterializedView<int,int>(static row=>row);
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGenerators(compilation);
        var result=driver.GetRunResult().Results[0];
        var generated=Assert.Single(
            result.GeneratedSources,
            static value=>value.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();

        Assert.Contains("DatabaseObjectKind.View",generated,StringComparison.Ordinal);
        Assert.Contains("source.ReadTable<int>(new global::Seamless.Database.System.Keys.DatabaseObject(\"Totals\"))",generated,StringComparison.Ordinal);
        Assert.Contains("AddMaterializedViewRow(this.Totals,row)",generated,StringComparison.Ordinal);
    }
}
