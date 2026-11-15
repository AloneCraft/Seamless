namespace Seamless.Database.SqlServer.Functions;
/// <summary>接続者と権限の関数。値は <see cref="SqlSession"/> が持つ(Seamless では Gateway の利用者の情報から作る)。</summary>
public static class SecurityFunctions{
    /// <summary>USER_NAME()。引数つき(利用者の番号)は扱わない。</summary>
    [SqlFunction("USER_NAME")][SqlFunction("CURRENT_USER")][SqlFunction("SESSION_USER")][SqlFunction("USER")]
    public static string UserName(SqlSession session)=>session.UserName;
    /// <summary>ORIGINAL_LOGIN。</summary>
    [SqlFunction("ORIGINAL_LOGIN")][SqlFunction("SUSER_SNAME")][SqlFunction("SYSTEM_USER")]
    public static string OriginalLogin(SqlSession session)=>session.OriginalLogin;
    /// <summary>IS_MEMBER。属していれば 1、いなければ 0。</summary>
    [SqlFunction("IS_MEMBER")]
    public static int? IsMember(SqlSession session,string? groupOrRole){
        if(groupOrRole is null){
            return null;
        }
        if(session.Roles.Contains(groupOrRole)){
            return 1;
        }
        return 0;
    }
    /// <summary>IS_ROLEMEMBER。member を省くと今の利用者。他の利用者を聞かれたら、分からないので NULL。</summary>
    [SqlFunction("IS_ROLEMEMBER")]
    public static int? IsRoleMember(SqlSession session,string? role,string? member=null){
        if(role is null){
            return null;
        }
        if(member is not null&&!member.Equals(session.UserName,StringComparison.OrdinalIgnoreCase)){
            return null;
        }
        if(session.Roles.Contains(role)){
            return 1;
        }
        return 0;
    }
    /// <summary>SESSION_CONTEXT。置かれていなければ NULL。</summary>
    [SqlFunction("SESSION_CONTEXT")]
    public static object? SessionContext(SqlSession session,string? key){
        if(key is null){
            return null;
        }
        session.SessionContext.TryGetValue(key,out var value);
        return value;
    }
}
