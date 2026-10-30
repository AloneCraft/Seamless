namespace Seamless.Database.SqlServer;

public static class SqlServerTypeMapper {
    public static DatabaseType Map(
        string systemTypeName,
        bool isNullable,
        short maximumLength,
        byte precision,
        byte scale,
        bool isEncrypted=false,string? collation=null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemTypeName);
        var normalized=systemTypeName.ToLowerInvariant();
        var kind=isEncrypted?DatabaseTypeKind.Binary:normalized switch {
            "bit"=>DatabaseTypeKind.Byte,
            "tinyint"=>DatabaseTypeKind.Byte,
            "smallint"=>DatabaseTypeKind.Int16,
            "int"=>DatabaseTypeKind.Int32,
            "bigint"=>DatabaseTypeKind.Int64,
            "decimal" or "numeric" or "money" or "smallmoney"=>DatabaseTypeKind.Decimal,
            "real"=>DatabaseTypeKind.Single,
            "float"=>DatabaseTypeKind.Double,
            "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "sysname"=>DatabaseTypeKind.String,
            "binary" or "varbinary" or "image" or "timestamp" or "rowversion" or "geometry" or "geography" or "hierarchyid"=>DatabaseTypeKind.Binary,
            "uniqueidentifier"=>DatabaseTypeKind.Guid,
            "date"=>DatabaseTypeKind.Date,
            "time"=>DatabaseTypeKind.Time,
            "datetime" or "smalldatetime" or "datetime2"=>DatabaseTypeKind.DateTime,
            "datetimeoffset"=>DatabaseTypeKind.DateTimeOffset,
            "xml"=>DatabaseTypeKind.Xml,
            _=>throw new NotSupportedException($"SQL Server type '{systemTypeName}' is not supported."),
        };
        int? length=isEncrypted||maximumLength<0||normalized is "text" or "ntext" or "image"?null:maximumLength;
        if(kind==DatabaseTypeKind.String&&normalized.StartsWith('n')&&length is not null) {
            length/=2;
        }
        var runtimeType=!isEncrypted&&normalized=="bit"
            ?typeof(global::Seamless.Database.SqlServer.Types.Bit)
            :null;
        return new(
            kind,
            isNullable,
            MaximumLength:length,
            Precision:kind==DatabaseTypeKind.Decimal?precision:null,
            Scale:kind is DatabaseTypeKind.Decimal or DatabaseTypeKind.Time or DatabaseTypeKind.DateTime or DatabaseTypeKind.DateTimeOffset?scale:null,
            RuntimeType:runtimeType,
            SqlTypeName:normalized,
            Collation:collation,
            SourceMaximumLength:maximumLength,
            SourcePrecision:precision,
            SourceScale:scale);
    }
}
