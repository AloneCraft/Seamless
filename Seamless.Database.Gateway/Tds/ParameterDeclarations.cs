using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tds;

internal static class ParameterDeclarations{
    public static IReadOnlyList<SqlParameter> Parse(string declarations){
        if(string.IsNullOrWhiteSpace(declarations)){return [];}
        var parser=new TSql160Parser(true);var script=parser.Parse(new StringReader("DECLARE "+declarations+";"),out var errors) as TSqlScript;
        if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102);}
        var statement=script?.Batches[0].Statements[0] as DeclareVariableStatement??throw new SqlQueryException("Invalid parameter declarations.");
        return statement.Declarations.Select(v=>new SqlParameter(v.VariableName.Value,Type(v.DataType))).ToArray();
    }
    private static SqlType Type(DataTypeReference reference){
        if(reference is not SqlDataTypeReference sql){throw new NotSupportedException("User defined RPC parameter types are not supported.");}
        var name=sql.SqlDataTypeOption.ToString().ToLowerInvariant();
        var clr=name switch{"bigint"=>typeof(long),"int"=>typeof(int),"smallint"=>typeof(short),"tinyint"=>typeof(byte),"bit"=>typeof(bool),"decimal" or "numeric" or "money" or "smallmoney"=>typeof(decimal),"real"=>typeof(float),"float"=>typeof(double),"uniqueidentifier"=>typeof(Guid),"date"=>typeof(DateOnly),"time"=>typeof(TimeOnly),"datetimeoffset"=>typeof(DateTimeOffset),"datetime" or "datetime2" or "smalldatetime"=>typeof(DateTime),"binary" or "varbinary" or "image"=>typeof(byte[]),_=>typeof(string)};
        int? length=null;byte? precision=null;byte? scale=null;
        if(sql.Parameters.Count>0){
            if(name is "decimal" or "numeric"){precision=byte.Parse(sql.Parameters[0].Value);scale=0;if(sql.Parameters.Count>1){scale=byte.Parse(sql.Parameters[1].Value);}}
            else if(name is "time" or "datetime2" or "datetimeoffset"){scale=byte.Parse(sql.Parameters[0].Value);}
            else if(string.Equals(sql.Parameters[0].Value,"max",StringComparison.OrdinalIgnoreCase)){length=-1;}else{length=int.Parse(sql.Parameters[0].Value);}
        }
        return new(name,clr,length,precision,scale);
    }
}
