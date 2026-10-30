// エンジンは 1 プロセスで TsavoriteKV を多く使い、ストアのフォルダも排他で開くので、テストは 1 つずつ流す。
[assembly:CollectionBehavior(DisableTestParallelization=true)]
