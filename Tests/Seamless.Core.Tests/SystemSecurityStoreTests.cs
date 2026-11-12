using System.Security.Cryptography;
using Seamless.Security;
using Xunit;

namespace Seamless.Core.Tests;

public sealed class SystemSecurityStoreTests {
    private static readonly Guid ServerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DatabaseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly SecuritySubjectId AdminSubject = new(Guid.Parse("44444444-4444-4444-4444-444444444444"), "admin");

    private static AuthenticatedIdentity CreateIdentity(SecuritySubjectId subject) =>
        new(subject, subject.Value, [], DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow);

    private static AuthorizationPolicySnapshot CreateInitialPolicy(long version = 1) {
        var resources = new SecurityResource[] {
            new(ServerId, null, SecurityResourceKind.Server, "server"),
            new(DatabaseId, ServerId, SecurityResourceKind.Database, "testdb")
        };
        var roles = new SecurityRole[] {
            new(RoleId, "admin", [new(ServerId, SecurityPermission.All)])
        };
        var bindings = new RoleBinding[] {
            new(AdminSubject, SecurityPrincipalKind.User, RoleId)
        };
        return new AuthorizationPolicySnapshot(version, resources, roles, bindings);
    }

    [Fact]
    public void FailSecure_WhenTriggered_BlocksNewSessionsAndCancelsActiveLeases() {
        var policy = CreateInitialPolicy();
        using var manager = new AuthorizationManager(policy);
        var identity = CreateIdentity(AdminSubject);

        // 正常時はセッション開設および操作リースが取得可能
        using var session1 = manager.CreateSession(identity);
        using var lease1 = session1.BeginOperation();
        Assert.False(lease1.CancellationToken.IsCancellationRequested);

        // 整合性破損を検知し Fail-Secure をトリガー
        manager.TriggerFailSecure("Simulated WAL sync corruption");

        Assert.True(manager.IsFailSecure);
        Assert.Contains("Simulated WAL sync corruption", manager.FailSecureReason);

        // 1. 既存の操作リースの CancellationToken が即座にキャンセルされることを検証
        Assert.True(lease1.CancellationToken.IsCancellationRequested);

        // 2. 既存セッションでの新規操作開始が Fail-Secure 例外で即座遮断されることを検証
        var exOp = Assert.Throws<AuthorizationException>(() => session1.BeginOperation());
        Assert.Equal("FailSecureLocked", exOp.Code);

        // 3. 旧版への暗黙フォールバックを排除し、新規セッション開設が即座遮断されることを検証
        var exSession = Assert.Throws<AuthorizationException>(() => manager.CreateSession(identity));
        Assert.Equal("FailSecureLocked", exSession.Code);
    }

    [Fact]
    public void MockSystemSecurityStore_SimulatesWalTransactionSyncAndFailSecure() {
        var store = new MockSystemSecurityStore(ServerId);
        var initialPolicy = CreateInitialPolicy(1);
        using var manager = new AuthorizationManager(initialPolicy);

        // 1. 正常系: WAL トランザクション一括書き込み
        var committed = false;
        store.WriteTransaction(ctx => {
            ctx.Put("authorization", "active", initialPolicy);
            ctx.Put("audit", Guid.NewGuid().ToString("N"), "audit-log");
            committed = true;
        });

        Assert.True(committed);
        Assert.Equal(1, store.AuthorizationEpoch);
        Assert.False(store.IsCorrupted);

        // 2. WAL 同期失敗（ディスク満杯・I/O 障害）のシミュレーション
        Assert.Throws<IOException>(() => {
            store.WriteTransaction(ctx => {
                ctx.Put("authorization", "active", CreateInitialPolicy(2));
                throw new IOException("Disk full during WAL flush");
            });
        });

        // 障害発生時、アクティブ版は更新されず未コミットデータはロールバック
        var activeInStore = store.Read<AuthorizationPolicySnapshot>("authorization", "active");
        Assert.NotNull(activeInStore);
        Assert.Equal(1, activeInStore.Version);

        // 3. WAL 破損検出時に Fail-Secure をトリガーし、暗黙フォールバックを完全遮断
        store.MarkCorrupted("WAL checksum mismatch");
        manager.TriggerFailSecure("WAL checksum mismatch");

        Assert.True(store.IsCorrupted);
        Assert.True(manager.IsFailSecure);

        var identity = CreateIdentity(AdminSubject);
        var ex = Assert.Throws<AuthorizationException>(() => manager.CreateSession(identity));
        Assert.Equal("FailSecureLocked", ex.Code);
    }

    private sealed class MockSystemSecurityStore : ISystemSecurityStore {
        private readonly Dictionary<(string Collection, string Key), object> state = new();
        public Guid InstanceId { get; }
        public long AuthorizationEpoch { get; private set; }
        public bool HasAudit => true;
        public bool IsCorrupted { get; private set; }
        public string? CorruptionReason { get; private set; }

        public MockSystemSecurityStore(Guid instanceId) {
            this.InstanceId = instanceId;
        }

        public T? Read<T>(string collection, string key) {
            if (this.state.TryGetValue((collection, key), out var value) && value is T typed) {
                return typed;
            }
            return default;
        }

        public void WriteTransaction(Action<ISystemSecurityTransactionContext> action) {
            if (this.IsCorrupted) {
                throw new InvalidOperationException("Store is corrupted.");
            }
            var temp = new Dictionary<(string Collection, string Key), object>(this.state);
            var ctx = new MockTransactionContext(temp);
            action(ctx);
            // コミット成功時に一括反映
            this.state.Clear();
            foreach (var kvp in temp) {
                this.state[kvp.Key] = kvp.Value;
            }
            this.AuthorizationEpoch++;
        }

        public void MarkCorrupted(string reason) {
            this.IsCorrupted = true;
            this.CorruptionReason = reason;
        }

        private sealed class MockTransactionContext : ISystemSecurityTransactionContext {
            private readonly Dictionary<(string Collection, string Key), object> storage;
            public MockTransactionContext(Dictionary<(string Collection, string Key), object> storage) {
                this.storage = storage;
            }
            public T? Read<T>(string collection, string key) {
                if (this.storage.TryGetValue((collection, key), out var value) && value is T typed) {
                    return typed;
                }
                return default;
            }
            public void Put<T>(string collection, string key, T value) {
                this.storage[(collection, key)] = value!;
            }
            public void Remove(string collection, string key) {
                this.storage.Remove((collection, key));
            }
        }
    }
}
