namespace Seamless.SessionFunctions;
/// <summary>
/// ManagedCollection 用セッション関数。UnmanagedAdd は RMW でのみ使われる。失敗したとき output=false で成否を返す。
/// </summary>
public readonly partial struct UnmanagedAdd:ICreate<UnmanagedAdd>{
    public static UnmanagedAdd Create()=>new();
}
