# ストアの初期設定と拡張

`Seamless.Execution.Store` の `StoreOptions` でプロファイルと拡張方式を分けて指定する。

```csharp
using Seamless.Execution;
using Seamless.Execution.Growth;
using Seamless.Collections.Tsavorite;

using var store=Store.CreateMemory(options:new(){
    Profile=Profile.Minimum,
    Growth=new Policy<Fixed>()
});
using var data=store.CreateTsavorite<int,int>("items");
using var items=data.Create();
```

既定は `Minimum` と `Doubling`。Minimumは予想件数が多くても初期サイズを大きくしない。Tsavoriteの索引は64バイト、ページとセグメントは4096バイト、ログメモリは8192バイトで開始する。Persistedの初期件数は16、初期データ領域は4096バイトである。資源上限は `StoreLimits` で指定する。

`Fixed` は索引と常駐ログの設定を固定し、データ件数を制限するものではない。ファイルの増加・ディスク退避は保存方式と資源上限に従う。`Doubling` は必要量まで段階的に広げ、`Estimated` は観測値と予想件数・推奨値を使う。並列区間中の索引変更は行わず、安全な区切りで適用する。

独自ストラテジーは `Growth.IStrategy<TSelf>` を実装するstructで作り、`Policy<TStrategy>` に渡す。`Decide` の `ref self` はストアごとのコントローラーに保持される。共有staticフィールドに可変状態を置かない。変更案はストア側で上限と単位を確認し、`GrowthDecisions` と `GrowthEvents` から採用結果・理由を取得できる。

`Optimized` は測定済み設定を条件付きで選ぶ。優先順は明示値、明示的に読み込んだ `ProfileCatalog`、同梱カタログ、Minimum。適用条件や資源予算が合わなければMinimumへ戻る。`ResolvedProfiles` に実設定・理由・根拠の実行IDを残す。無効な明示値は例外になり、別の値へ自動修正しない。

```csharp
var profiles=ProfileCatalog.Load("profiles.json");
using var store=Store.CreateMemory(options:new(){
    Profile=Profile.Optimized,Profiles=profiles,
    Workload="FilterAggregate",Environment="測定環境の識別子"
});
```

通常の利用アプリは測定DBへ接続しない。プロファイルはJSONとして持ち運ぶ。測定・独立入力による検証を終えた条件だけを採用する。

シリアライザーの役割名は `Seamless.Collections.Serializers` に統一した。実装は `Auto<T>`、`Blittable<T>`、`MemoryPack<T>`、`RawByteArray`。契約は `IStrategy<T>`。旧 `Serialization.*Serializer` の互換入口は提供しない。
