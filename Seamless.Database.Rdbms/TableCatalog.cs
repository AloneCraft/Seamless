namespace Seamless.Database.Rdbms;

public sealed record TableCatalog {
    public TableCatalog(
        string name,
        IReadOnlyList<ColumnCatalog> columns,
        PrimaryKeyCatalog? primaryKey,
        IReadOnlyList<UniqueKeyCatalog>? uniqueKeys=null,
        IReadOnlyList<ForeignKeyCatalog>? foreignKeys=null,
        IReadOnlyList<CheckConstraintCatalog>? checkConstraints=null,
        IReadOnlyList<TriggerCatalog>? triggers=null) {
        this.Name=name;
        this.Columns=columns;
        this.PrimaryKey=primaryKey;
        this.UniqueKeys=uniqueKeys??[];
        this.ForeignKeys=foreignKeys??[];
        this.CheckConstraints=checkConstraints??[];
        this.Triggers=triggers??[];
    }

    public string Name { get; }
    public IReadOnlyList<ColumnCatalog> Columns { get; }
    public PrimaryKeyCatalog? PrimaryKey { get; }
    public IReadOnlyList<UniqueKeyCatalog> UniqueKeys { get; }
    public IReadOnlyList<ForeignKeyCatalog> ForeignKeys { get; }
    public IReadOnlyList<CheckConstraintCatalog> CheckConstraints { get; }
    public IReadOnlyList<TriggerCatalog> Triggers { get; }
}
