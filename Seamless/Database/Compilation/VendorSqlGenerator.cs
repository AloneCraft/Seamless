using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Compilation;

/// <summary>ネイティブの意味モデルからSQLを生成する。元SQLの定義は入力にしない。</summary>
public sealed class VendorSqlGenerator{
    public GeneratedVendorDatabase Generate(NativeDatabaseDefinition database,SqlVendor vendor){
        ArgumentNullException.ThrowIfNull(database);
        var diagnostics=ImmutableArray.CreateBuilder<NativeDiagnostic>();
        var statements=ImmutableArray.CreateBuilder<string>();
        var available=new HashSet<NativeTable>();
        var dialect=new Dialect(vendor);
        if(vendor==SqlVendor.SqlServer){
            foreach(var schema in database.Tables.Select(t=>t.Schema).Distinct(StringComparer.Ordinal).Where(s=>s!="dbo")){
                statements.Add("IF SCHEMA_ID("+dialect.Literal(schema)+") IS NULL EXEC(N'CREATE SCHEMA "+dialect.Quote(schema).Replace("'","''",StringComparison.Ordinal)+"');");
            }
        }
        foreach(var table in database.Tables){
            try{
                var primaryKey=PrimaryKey(table,dialect,vendor);
                var columns=table.Columns.Select(c=>{
                    var nullable=" NOT NULL";if(c.IsNullable){nullable=" NULL";}
                    return "    "+dialect.Quote(c.Name)+" "+dialect.Type(c.ClrType,c.LogicalMaximumLength??c.TextCapacity)+nullable;
                }).ToList();
                if(primaryKey is not null){columns.Add(primaryKey);}
                statements.Add("CREATE TABLE "+dialect.Relation(table.Schema,table.Name)+" (\n"+string.Join(",\n",columns)+"\n);");
                available.Add(table);
            }catch(NotSupportedException error){diagnostics.Add(new("SQL_UNSUPPORTED_TYPE",error.Message,NativeDiagnosticSeverity.Unsupported,table.Name,null));}
        }
        var queries=ImmutableArray.CreateBuilder<GeneratedVendorQuery>();
        foreach(var query in database.Queries){
            if(!available.Contains(query.Query.Table)){diagnostics.Add(new("SQL_DEPENDENCY_UNAVAILABLE","The source table could not be exported.",NativeDiagnosticSeverity.Unsupported,query.Name,query.Source));continue;}
            try{
                var sql=dialect.Query(query.Query);
                var routine=dialect.Routine(query,sql);
                queries.Add(new(query.Name,sql,query.Parameters));
                statements.Add(routine);
            }catch(NotSupportedException error){diagnostics.Add(new("SQL_UNSUPPORTED_QUERY",error.Message,NativeDiagnosticSeverity.Unsupported,query.Name,query.Source));}
        }
        return new(vendor,database.SourceFingerprint,string.Join("\n\n",statements),queries.ToImmutable(),diagnostics.ToImmutable()){Statements=statements.ToImmutable()};
    }

    private static string? PrimaryKey(NativeTable table,Dialect dialect,SqlVendor vendor){
        if(table.SourceFeatures is null){
            if(table.PrimaryKey.Length==0){return null;}
            return "    PRIMARY KEY ("+string.Join(",",table.PrimaryKey.Select(dialect.Quote))+")";
        }
        try{
            var options=new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
            using var payload=JsonDocument.Parse(table.SourceFeatures);
            var features=payload.RootElement;
            if(vendor!=SqlVendor.SqlServer||features.ValueKind!=JsonValueKind.Array||features.GetArrayLength()!=1){
                throw new NotSupportedException("Imported identity, defaults, computed columns or constraints require their exact SQL representation.");
            }
            var feature=features[0];
            if(feature.ValueKind!=JsonValueKind.Object||!feature.TryGetProperty("Kind",out var kind)
                ||kind.ValueKind!=JsonValueKind.String||kind.GetString()!="PrimaryKey"
                ||!feature.TryGetProperty("Definition",out var definition)||feature.EnumerateObject().Count()!=2){
                throw new NotSupportedException("The imported table contains unsupported constraint metadata.");
            }
            var key=definition.Deserialize<PrimaryKeyCatalog>(options);
            if(key is null||string.IsNullOrWhiteSpace(key.Name)||key.Columns is null||key.Columns.Count==0
                ||key.Columns.Any(static column=>column is null||string.IsNullOrWhiteSpace(column.Name))
                ||!key.Columns.Select(column=>column.Name).SequenceEqual(table.PrimaryKey,StringComparer.Ordinal)
                ||key.IndexType is not (null or 1 or 2)){
                throw new NotSupportedException("The imported primary key does not match the native table or its index type is unsupported.");
            }
            var name=string.Empty;
            if(!key.IsSystemNamed){name="CONSTRAINT "+dialect.Quote(key.Name)+" ";}
            var indexType=string.Empty;
            if(key.IndexType==1){indexType=" CLUSTERED";}
            if(key.IndexType==2){indexType=" NONCLUSTERED";}
            var columns=key.Columns.Select(column=>{
                var direction=" ASC";
                if(column.IsDescending){direction=" DESC";}
                return dialect.Quote(column.Name)+direction;
            });
            return "    "+name+"PRIMARY KEY"+indexType+" ("+string.Join(",",columns)+")";
        }catch(JsonException error){
            throw new NotSupportedException("The imported primary key metadata is invalid.",error);
        }
    }

    private sealed class Dialect(SqlVendor vendor,bool routineParameters=false){
        private bool Server=>vendor==SqlVendor.SqlServer;
        public string Quote(string name){ArgumentException.ThrowIfNullOrWhiteSpace(name);if(this.Server){return "["+name.Replace("]","]]",StringComparison.Ordinal)+"]";}return "`"+name.Replace("`","``",StringComparison.Ordinal)+"`";}
        public string Relation(string schema,string name){if(this.Server){return this.Quote(schema)+"."+this.Quote(name);}if(schema=="dbo"){return this.Quote(name);}return this.Quote(schema+"__"+name);}

        public string Type(string type,int?capacity=null){
            if(capacity is not null){if(this.Server){return "nvarchar("+capacity.Value.ToString(CultureInfo.InvariantCulture)+")";}return "varchar("+capacity.Value.ToString(CultureInfo.InvariantCulture)+") CHARACTER SET utf8mb4 COLLATE utf8mb4_bin";}
            switch(type){
                case "System.Boolean":return "bit";
                case "System.Byte":return this.VendorType("tinyint","tinyint unsigned");
                case "System.SByte":return this.VendorType("smallint","tinyint");
                case "System.Int16":return "smallint";
                case "System.UInt16":return this.VendorType("int","smallint unsigned");
                case "System.Int32":return "int";
                case "System.UInt32":return this.VendorType("bigint","int unsigned");
                case "System.Int64":return "bigint";
                case "System.UInt64":return this.VendorType("decimal(20,0)","bigint unsigned");
                case "System.String":return this.VendorType("nvarchar(max)","longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin");
                case "System.Char":return this.VendorType("nchar(1)","varchar(1) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin");
                case "System.Byte[]":return this.VendorType("varbinary(max)","longblob");
                case "System.Guid":return this.VendorType("uniqueidentifier","binary(16)");
                default:throw new NotSupportedException("An exact native SQL representation is not implemented for "+type+". Native precision is not reduced automatically.");
            }
        }

        public string Query(NativeQuery query){
            var sql="SELECT "+string.Join(",",query.Table.Columns.Select(c=>this.Quote(c.Name)))+" FROM "+this.Relation(query.Table.Schema,query.Table.Name);
            var orders=new List<NativeOrdering>();
            foreach(var stage in query.Stages){
                switch(stage){
                    case NativeFilter filter:sql="SELECT * FROM ("+sql+") AS q WHERE "+this.Expression(filter.Predicate);break;
                    case NativeSelect select:
                        if(orders.Any(o=>o.Expression is not NativeColumnExpression c||!select.Columns.Any(p=>p.Name==c.Name&&p.Expression==c))){throw new NotSupportedException("Projection removes an ordering key; order-preserving projection is not implemented.");}
                        sql="SELECT "+string.Join(",",select.Columns.Select(c=>this.Expression(c.Expression)+" AS "+this.Quote(c.Name)))+" FROM ("+sql+") AS q";break;
                    case NativeOrder order:
                        if(!order.ThenBy){orders.Clear();}else if(orders.Count==0){throw new NotSupportedException("ThenBy requires an ordering.");}
                        orders.Add(order.Ordering);break;
                    case NativeTake take:
                        if(orders.Count==0){throw new NotSupportedException("Take requires an explicit ordering for reproducible SQL selection.");}
                        if(this.Server){sql="SELECT TOP ("+take.Count.ToString(CultureInfo.InvariantCulture)+") * FROM ("+sql+") AS q"+this.Order(orders);}
                        else{sql="SELECT * FROM ("+sql+") AS q"+this.Order(orders)+" LIMIT "+take.Count.ToString(CultureInfo.InvariantCulture);}
                        sql="SELECT * FROM ("+sql+") AS limited";
                        break;
                    case NativeDistinct:
                        if(orders.Count>0){throw new NotSupportedException("Distinct over an ordered sequence requires a stable-distinct implementation.");}
                        sql="SELECT DISTINCT * FROM ("+sql+") AS q";break;
                    case NativeAggregate aggregate:
                        var operand="*";if(aggregate.Expression is not null){operand=this.Expression(aggregate.Expression);}
                        var operation=aggregate.Operator.ToUpperInvariant();
                        if(operation is "COUNT" or "LONGCOUNT"){
                            if(aggregate.Expression is not null){throw new NotSupportedException("Count predicates require a preceding filter.");}
                            operation="COUNT";if(this.Server&&aggregate.Operator=="LongCount"){operation="COUNT_BIG";}
                        }else if(operation is not "SUM"){throw new NotSupportedException("The empty-sequence and numeric semantics for "+aggregate.Operator+" are not implemented.");}
                        var value=operation+"("+operand+")";
                        if(operation=="SUM"){value="COALESCE("+value+",0)";}
                        sql="SELECT "+value+" AS "+this.Quote("Value")+" FROM ("+sql+") AS q";orders.Clear();break;
                    default:throw new NotSupportedException("Unknown native query stage.");
                }
            }
            return sql+this.Order(orders);
        }

        private string VendorType(string server,string mysql){if(this.Server){return server;}return mysql;}
        private string Order(IEnumerable<NativeOrdering>orders){
            var items=orders.Select(o=>{var direction=" ASC";if(o.Descending){direction=" DESC";}return this.Expression(o.Expression)+direction;}).ToArray();
            if(items.Length==0){return "";}return " ORDER BY "+string.Join(",",items);
        }

        public string Routine(NativeQueryDefinition definition,string sql){
            var name=this.Relation(definition.Schema,definition.Name);
            if(this.Server){
                var parameters=string.Join(",",definition.Parameters.Select(p=>"@"+p.Name+" "+this.Type(p.ClrType)));
                if(parameters.Length>0){name+=" "+parameters;}
                return "CREATE PROCEDURE "+name+" AS BEGIN SET NOCOUNT ON; "+sql+"; END;";
            }
            var inputs=string.Join(",",definition.Parameters.Select(p=>"IN "+this.Quote("p_"+p.Name)+" "+this.Type(p.ClrType)));
            sql=new Dialect(vendor,true).Query(definition.Query);
            return "CREATE PROCEDURE "+name+"("+inputs+") READS SQL DATA BEGIN "+sql+"; END";
        }

        private string Expression(NativeExpression expression){
            switch(expression){
                case NativeColumnExpression column:return this.Quote(column.Name);
                case NativeParameterExpression parameter:
                    if(routineParameters&&!this.Server){return this.Quote("p_"+parameter.Name);}return "@"+parameter.Name;
                case NativeConstantExpression constant:return this.Literal(constant.Value);
                case NativeUnaryExpression unary:
                    var operation=unary.Operator;if(operation=="!"){operation="NOT ";}
                    return "("+operation+this.Expression(unary.Operand)+")";
                case NativeBinaryExpression binary:return this.Binary(binary);
                default:throw new NotSupportedException("Unknown native expression.");
            }
        }

        private string Binary(NativeBinaryExpression binary){
            var left=this.Expression(binary.Left);var right=this.Expression(binary.Right);
            if(binary.Left is NativeConstantExpression{Value:null}||binary.Right is NativeConstantExpression{Value:null}){
                var operand=left;if(binary.Left is NativeConstantExpression{Value:null}){operand=right;}
                if(binary.Operator=="=="){return "("+operand+" IS NULL)";}if(binary.Operator=="!="){return "("+operand+" IS NOT NULL)";}
            }
            if(binary.OrdinalText){
                string same;
                if(this.Server){same="("+left+" COLLATE Latin1_General_100_BIN2 = "+right+" COLLATE Latin1_General_100_BIN2 AND DATALENGTH("+left+")=DATALENGTH("+right+"))";}
                else{same="(CAST("+left+" AS BINARY)=CAST("+right+" AS BINARY))";}
                var equal="(("+left+" IS NULL AND "+right+" IS NULL) OR ("+left+" IS NOT NULL AND "+right+" IS NOT NULL AND "+same+"))";
                if(binary.Operator=="!="){return "(NOT "+equal+")";}return equal;
            }
            if(binary.Operator is "+" or "-" or "*" or "/"){throw new NotSupportedException("Native arithmetic overflow and division semantics require a backend implementation.");}
            var operation=binary.Operator switch{"=="=>"=","!="=>"<>","&&"=>"AND","||"=>"OR",_=>binary.Operator};
            return "("+left+" "+operation+" "+right+")";
        }

        public string Literal(object?value){
            if(value is null){return "NULL";}
            if(value is string text){
                if(this.Server){return "N'"+text.Replace("'","''",StringComparison.Ordinal)+"'";}
                var bytes=new UTF8Encoding(false,true).GetBytes(text);return "CONVERT(X'"+Convert.ToHexString(bytes)+"' USING utf8mb4)";
            }
            if(value is bool boolean){if(boolean){return "1";}return "0";}
            if(value is char character){return this.Literal(character.ToString());}
            if(value is byte or sbyte or short or ushort or int or uint or long or ulong){return Convert.ToString(value,CultureInfo.InvariantCulture)!;}
            throw new NotSupportedException("Exact SQL literal conversion is not implemented for "+value.GetType());
        }
    }
}
