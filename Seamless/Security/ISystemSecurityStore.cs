namespace Seamless.Security;

/// <summary>
/// 認可・監査ポリシーの system DB 永続化および WAL トランザクション同期を司る抽象化ストア境界。
/// </summary>
public interface ISystemSecurityStore {
    Guid InstanceId { get; }
    long AuthorizationEpoch { get; }
    bool HasAudit { get; }
    bool IsCorrupted { get; }

    T? Read<T>(string collection, string key);

    void WriteTransaction(Action<ISystemSecurityTransactionContext> action);

    /// <summary>
    /// WAL 同期失敗や整合性破損検出時に呼び出され、旧版フォールバックを排除して新規セッションを即座遮断（Fail-Secure）する。
    /// </summary>
    void MarkCorrupted(string reason);
}

/// <summary>
/// 一括書き込みトランザクションのコンテキスト。
/// </summary>
public interface ISystemSecurityTransactionContext {
    T? Read<T>(string collection, string key);
    void Put<T>(string collection, string key, T value);
    void Remove(string collection, string key);
}
