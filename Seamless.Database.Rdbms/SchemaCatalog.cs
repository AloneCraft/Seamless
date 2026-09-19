namespace Seamless.Database.Rdbms;

public sealed record SchemaCatalog {
    public SchemaCatalog(
        string name,
        IReadOnlyList<TableCatalog> tables,
        IReadOnlyList<ViewCatalog>? views=null,
        IReadOnlyList<RoutineCatalog>? routines=null) {
        this.Name=name;
        this.Tables=tables;
        this.Views=views??[];
        this.Routines=routines??[];
    }

    public string Name { get; }
    public IReadOnlyList<TableCatalog> Tables { get; }
    public IReadOnlyList<ViewCatalog> Views { get; }
    public IReadOnlyList<RoutineCatalog> Routines { get; }
}
