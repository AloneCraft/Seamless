// ManagementExecutionContext は、登録クエリを SDK でビルドするときの参照を DB の部品だけにするため Seamless.Database へ移した(2026-11-16)。
// 公開済みの登録クエリはこの部品の型として参照しているので、転送の印を残す。
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(Seamless.Management.Server.ManagementExecutionContext))]
