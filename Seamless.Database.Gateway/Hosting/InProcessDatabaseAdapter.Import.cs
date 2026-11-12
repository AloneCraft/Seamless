using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class InProcessDatabaseAdapter{
    internal GatewaySchemaCatalog CaptureImportedIdentityValues(GatewaySchemaCatalog catalog){
        var schemas=catalog.Definition.Schemas.Select(owner=>new SchemaCatalog(owner.Name,owner.Tables.Select(table=>{
            var identities=table.Columns.Where(static column=>column.IsIdentity).ToArray();var versions=table.Columns.Where(static column=>column.Type.SqlTypeName is "rowversion" or "timestamp").ToArray();if(identities.Length==0&&versions.Length==0){return table;}
            var values=identities.ToDictionary(static column=>column.Name,static column=>column.InitialIdentityValue);var watermarks=versions.ToDictionary(static column=>column.Name,static column=>column.InitialRowVersionValue);var binding=this.tables[new TableMetadata(owner.Name,table.Name,[]).IdentifierKey];
            foreach(var row in binding.GetRows(this.database)){
                foreach(var column in identities){var value=Convert.ToDecimal(binding.ColumnGetters[column.Ordinal](row),global::System.Globalization.CultureInfo.InvariantCulture);if(values[column.Name] is not{} previous||column.IdentityIncrement>0&&value>previous||column.IdentityIncrement<0&&value<previous){values[column.Name]=value;}}
                foreach(var column in versions){if(binding.ColumnGetters[column.Ordinal](row) is not byte[] bytes){continue;}if(bytes.Length!=8){throw new InvalidDataException("An imported rowversion must have eight bytes.");}var value=global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes);if(watermarks[column.Name] is not{} previous||value>previous){watermarks[column.Name]=value;}}
            }
            var columns=table.Columns.Select(column=>{if(column.IsIdentity){return column with{InitialIdentityValue=values[column.Name]};}if(watermarks.TryGetValue(column.Name,out var watermark)){return column with{InitialRowVersionValue=watermark};}return column;}).ToArray();return new TableCatalog(table.Name,columns,table.PrimaryKey,table.UniqueKeys,table.ForeignKeys,table.CheckConstraints,table.Triggers){CreateDate=table.CreateDate};
        }).ToArray(),owner.Views,owner.Routines)).ToArray();return catalog with{Definition=new(catalog.Definition.Name,schemas,catalog.Definition.DefaultCollation)};
    }
}
