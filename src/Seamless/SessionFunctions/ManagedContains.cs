namespace Seamless.SessionFunctions;
/// <summary>
/// ManagedCollection 用 Contains セッション関数。Read でキーの存在のみ確認し、値のデシリアライズを行わない。
/// </summary>
public readonly partial struct ManagedContains:ICreate<ManagedContains>{
    public static ManagedContains Create()=>new();
}
