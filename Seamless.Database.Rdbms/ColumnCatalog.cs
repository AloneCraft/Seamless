namespace Seamless.Database.Rdbms;

public sealed record ColumnCatalog(
    int Ordinal,
    string Name,
    DatabaseType Type,
    bool IsIdentity,
    string? DefaultDefinition=null,
    string? ComputedDefinition=null,
    bool IsSynthetic=false){
    public string? DefaultConstraintName{get;init;}
    public bool IsDefaultSystemNamed{get;init;}
    public decimal IdentitySeed{get;init;}=1;
    public decimal IdentityIncrement{get;init;}=1;
    public bool IsComputedPersisted{get;init;}
    public decimal? InitialIdentityValue{get;init;}
    public ulong? InitialRowVersionValue{get;init;}
}
