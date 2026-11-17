namespace Seamless.Database.Rdbms;

public sealed record CheckConstraintCatalog(
    string Name,
    string Definition,
    bool IsDisabled){
    public bool IsSystemNamed{get;init;}
    public string? ParentColumn{get;init;}
}
