namespace Seamless.Database;

/// <summary>同一DB内の主キーを参照する。最外取引の最終状態で検査する。</summary>
[AttributeUsage(AttributeTargets.Property,AllowMultiple=true)]
public sealed class ForeignKeyAttribute(string name,string referencedTable,string[] columns,string[] referencedColumns):Attribute{
    public string Name{get;}=name;
    public string ReferencedTable{get;}=referencedTable;
    public string[] Columns{get;}=columns;
    public string[] ReferencedColumns{get;}=referencedColumns;
}
public sealed class ForeignKeyViolationException(string constraint,string message):InvalidOperationException(message){public string Constraint{get;}=constraint;}
