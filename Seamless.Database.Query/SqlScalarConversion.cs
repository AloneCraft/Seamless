namespace Seamless.Database.Query;

/// <summary>SQL の代入変換。数値の scale は丸め、文字列・binary の代入切詰めは拒否する。</summary>
public static class SqlScalarConversion{
    public static bool MatchesPattern(string value,string pattern,string? collation=null)=>SqlValues.Like(value,pattern,null,false,collation)==true;
    public static object? Assign(object? value,SqlType type){
        ArgumentNullException.ThrowIfNull(type);
        try{return SqlValues.ConvertTo(value,type,type.ClrType!=typeof(string)&&type.ClrType!=typeof(byte[]));}
        catch(OverflowException error){throw new SqlQueryException("Arithmetic overflow: "+error.Message,8115);}
        catch(FormatException error){throw new SqlQueryException("Conversion failed: "+error.Message,245);}
    }
}
