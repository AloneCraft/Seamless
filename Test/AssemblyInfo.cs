// 統合後は旧テストプロジェクト間の共有パスや子プロセス実行が同一 assembly 内で干渉するため、逐次実行する。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
