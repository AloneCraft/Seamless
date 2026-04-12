namespace Seamless.SessionFunctions;
/// <summary>
/// ManagedCollection 用セッション関数。ManagedRead は Read でのみ使われる。
/// </summary>
public readonly partial struct ManagedRead:ICreate<ManagedRead>{
    public static ManagedRead Create()=>new();
}