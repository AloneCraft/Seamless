using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;



public sealed class ColumnMetadata{
    public ColumnMetadata(string name,Type clrType,string sqlTypeName,bool isNullable,bool isPrimaryKey,int ordinal,int? maximumLength=null,byte? precision=null,byte? scale=null,string? collation=null,short? sourceMaximumLength=null,byte? sourcePrecision=null,byte? sourceScale=null,string? sourceDatabase=null,string? sourceSchema=null,string? sourceTable=null,string? sourceSqlTypeName=null){
        this.Name=name;this.ClrType=clrType;this.SqlTypeName=sqlTypeName;this.IsNullable=isNullable;this.IsPrimaryKey=isPrimaryKey;this.Ordinal=ordinal;this.MaximumLength=maximumLength;this.Precision=precision;this.Scale=scale;this.Collation=collation;this.SourceMaximumLength=sourceMaximumLength;this.SourcePrecision=sourcePrecision;this.SourceScale=sourceScale;this.SourceDatabase=sourceDatabase;this.SourceSchema=sourceSchema;this.SourceTable=sourceTable;this.SourceSqlTypeName=sourceSqlTypeName;
    }
    public string Name{get;}
    public Type ClrType{get;}
    public string SqlTypeName{get;}
    public bool IsNullable{get;}
    public bool IsPrimaryKey{get;}
    public int Ordinal{get;}
    public int? MaximumLength{get;}
    public byte? Precision{get;}
    public byte? Scale{get;}
    public string? Collation{get;}
    public short? SourceMaximumLength{get;}
    public byte? SourcePrecision{get;}
    public byte? SourceScale{get;}
    public string? SourceDatabase{get;}
    public string? SourceSchema{get;}
    public string? SourceTable{get;}
    public string? SourceSqlTypeName{get;}
}

public sealed class TableMetadata{
    public TableMetadata(string schemaName,string tableName,IReadOnlyList<ColumnMetadata>columns,PrimaryKeyCatalog? primaryKey=null){
        this.PrimaryKey=primaryKey;
        this.SchemaName=schemaName;
        this.TableName=tableName;
        this.Columns=columns;
    }

    public TableMetadata(string schemaName,string tableName,IReadOnlyList<ColumnMetadata>columns,PrimaryKeyCatalog? primaryKey,int? stableObjectId):this(schemaName,tableName,columns,primaryKey){this.StableObjectId=stableObjectId;}
    public int? StableObjectId{get;}
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
    public PrimaryKeyCatalog? PrimaryKey{get;}
    public string IdentifierKey=>"["+this.SchemaName.Replace("]","]]",StringComparison.Ordinal)+"].["+this.TableName.Replace("]","]]",StringComparison.Ordinal)+"]";
}
