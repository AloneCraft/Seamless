namespace Seamless.Database.Rdbms;

public sealed record GeneratedDatabase(
    string SourceDatabaseName,
    string DatabaseName,
    string SchemaFingerprint,
    IReadOnlyList<GeneratedSourceFile> Files,
    IReadOnlyList<GeneratedTableBinding> Tables){
    public IReadOnlyList<GeneratedRoutineBinding> Routines{get;init;}=[];
    public string? RootNamespace{get;init;}
    public string TypeNamespace=>this.RootNamespace??this.DatabaseName;
}
