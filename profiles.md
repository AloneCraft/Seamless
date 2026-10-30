---
layout: default
title: 初期設定と拡張
---

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
    Workload="FilterAggregate"
});
```

通常の利用アプリは測定DBへ接続しない。プロファイルはJSONとして持ち運ぶ。測定・独立入力による検証を終えた条件だけを採用する。

同梱の `tsavorite-file-int32-filter-4096-doubling-v1` は旧測定の証拠として保持する。新方式に必要な証拠版がないため、現行Resolverはこの候補を採用せずMinimumへ戻す。旧測定の数値は新方式での性能向上の証拠には使わない。

環境は既定で `ProfileEnvironment.Current` を使う。旧版1の候補が記録している範囲はTsavorite/Temporary、intのキーと値、4096件、NativeのFilterAggregate、当時の測定環境である。索引64→4096バイトの変更を2種類の入力で独立に検証したもので、他の件数・処理・環境まで最適だとするものではない。条件外ではMinimumを採用し、`ResolvedProfiles` から理由を取得できる。プロファイルが推奨する拡張方式より利用者の明示的な `Growth` を優先する。

メモリストアでは常駐ログの論理容量を最小から増やす。Tsavorite内部のページリングは資源上限内で確保可能な上限を持ち、初期のページ本体は最小で開始する。`Fixed` のメモリストアは設定した常駐容量を超えると理由付きで失敗する。ファイルストアの通常の退避とは区別する。`GetCurrentSettings` は初期設定へ成功した拡張を反映した現在値を返す。

## アルゴリズムの公開契約

| 役割 | 静的契約・実装 | 実行時の入口 |
|---|---|---|
| 拡張 | `Seamless.Execution.Growth.IStrategy<TSelf>`、Fixed/Doubling/Estimated | `Policy<TStrategy>`でストアごとに状態を保持 |
| 要素走査 | `Seamless.Collections.Iteration.IStrategy<TSelf,T>` | コレクションの型付きIterate |
| バイト符号化 | `Seamless.Execution.Codecs.IStrategy<TSelf,T>`、Serializer | `IAdapter<T>`と`Adapter<T,TStrategy>`をCodecRegistryへ登録 |
| 等価判定 | `Seamless.Execution.Equalities.IStrategy<TSelf,T>`、Default/Binary | BCLのIEqualityComparerへAdapterで接続 |

外部TsavoriteのISessionFunctions/IScanIteratorFunctionsとBCLの契約は、外部から呼ばれる境界として維持する。外部資源を所有するストアや型レジストリ全体をstructへ置き換えない。旧IBinaryCodec、BinaryEqualityComparer、バックエンド別の要素走査用インターフェースは新契約へ移し、互換入口を残さない。

シリアライザーの役割名は `Seamless.Collections.Serializers` に統一した。実装は `Auto<T>`、`Blittable<T>`、`MemoryPack<T>`、`RawByteArray`。契約は `IStrategy<T>`。旧 `Serialization.*Serializer` の互換入口は提供しない。

## 実行前の説明と証拠版

`ProfileResolver.Explain` は設定だけを解決し、ストア作成、入力列挙、測定DBへの接続を行わない。要求・採用プロファイル、ID/版、実設定、拡張方式、根拠Run ID、採用理由、不足条件、実行時まで確定しない条件を不変データとして返す。明示した設定とGrowthを優先する。

`MeasuredProfile.Evidence` はライブラリ、Codec、測定方式、比較ポリシーの互換条件を持つ。現行は `ProfileEvidence.Current` と一致する証拠だけを選定する。ライブラリ識別にはExecution DLLのMVIDを含めるため、コードを変更したビルドの候補を黙って流用しない。Codec/測定の契約を変えるときも版を更新する。未知の旧証拠はJSONから読めるが、`Find` / Resolverの候補選定・新規発行に使わない。

Scalableでは `ScalableRequest.Explain()`、UIの「設定と実行経路を説明」、CLIの `explain --request FILE` を使う。Native/Scoped/Systemの指定は確定情報、拡張・退避の詳細は実行時判断として表示する。
