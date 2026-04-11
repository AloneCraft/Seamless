using Seamless.Collections;
namespace Seamless.SessionFunctions;
/// <summary>
/// ManagedCollection 用セッション関数。ManagedAdd は RMW でのみ使われる。失敗したとき output=false で成否を返す。
/// </summary>
public readonly partial struct ManagedAdd:ICreate<ManagedAdd>{
    public static ManagedAdd Create()=> new();
}
