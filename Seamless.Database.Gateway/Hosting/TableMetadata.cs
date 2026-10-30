namespace Seamless.Database.Gateway.Hosting;

public sealed class ColumnMetadata{
    public ColumnMetadata(string name,Type clrType,string sqlTypeName,bool isNullable,bool isPrimaryKey,int ordinal){
        this.Name=name;
        this.ClrType=clrType;
        this.SqlTypeName=sqlTypeName;
        this.IsNullable=isNullable;
        this.IsPrimaryKey=isPrimaryKey;
        this.Ordinal=ordinal;
    }

    public string Name{get;}
    public Type ClrType{get;}
    public string SqlTypeName{get;}
    public bool IsNullable{get;}
    public bool IsPrimaryKey{get;}
    public int Ordinal{get;}
}

public sealed class TableMetadata{
    public TableMetadata(string schemaName,string tableName,IReadOnlyList<ColumnMetadata>columns){
        this.SchemaName=schemaName;
        this.TableName=tableName;
        this.Columns=columns;
    }

    public string SchemaName{get;}
    public string TableName{get;}
    public string FullName{
        get{
            if(string.IsNullOrEmpty(this.SchemaName)){
                return this.TableName;
            }else{
                return $"{this.SchemaName}.{this.TableName}";
            }
        }
    }
    public IReadOnlyList<ColumnMetadata>Columns{get;}
}
