# Benchmark.TpcE.Compliance

TPC-E(仕様 1.14.0)の**準拠リハーサル**。TPC が配る EGen(データ生成と運転側)を無改変で使い、Tsavorite+FLinq の SUT に仕様どおりの手順で負荷をかけ、仕様の規則に照らして点検する。

> **TPC-E 由来・非監査・公式結果と比較不可(tpsE 相当)**
> TPC 認定の監査人の審査を受けていない自己計測です。ここで出る数字に「TPC-E」「tpsE」を付けて公開してはいけません(TPC の公正使用の規則)。

不具合探しの模擬 `Benchmark.TpcE`(`run`・`crash` など)とは別の exe で、互いに影響しません。

## 前提

1. EGen 1.14.0 を tpc.org から入手して、`G:\GitHub\Seamless\Work\TpcE\egen\` に展開する(使用許諾への同意は利用者が行う)。
   入手先: <https://www.tpc.org/tpc_documents_current_versions/current_specifications5.asp> の `TPC-E_Tools_v1.14.0.zip`
2. EGen をビルドする(Visual Studio の C++ ツールが要る)。

   ```bash
   powershell -File Benchmark/Benchmark.TpcE.Compliance/egen/build-egen.ps1
   ```

   出力は `G:\GitHub\Seamless\Work\TpcE\egen-build\`(`EGenLoader.exe`、`EGenDriver.exe`、`EGenHarness.dll`、`EGenValidate.exe`、`EGenCore.lib`)。`EGenHarness.dll` は EGen の EGenTxnHarness を C# から呼ぶための橋渡し(`egen/harness/`)。EGen 本体のソースは書き換えず、現行の Windows SDK と C++ 規格に合わせる回避(`/FIstdio.h`、`/utf-8`、ビルド用コピーへの空白追加)だけを入れている。詳しくはスクリプトの冒頭。
3. 作業フォルダは**速いドライブ**に置く。ストアと再実行ログは fsync が多く、HDD や遅い SSD では桁違いに遅い(この PC の G: は C: の約100倍遅かった)。

## コマンド

```bash
dotnet run -c Release --project Benchmark/Benchmark.TpcE.Compliance -- measure --work C:\tpce --customers 1000 --days 1 --measure-minutes 10
```

| コマンド | 内容 |
|---|---|
| `measure` | EGenLoader でデータを作り、SUT と運転側を別プロセスで起動し、立ち上がり → 計測 → 片付けを流して、仕様の規則で点検する。`report.md` / `report.json` を出す |
| `acid` | 原子性(7.2.2)、処理の喪失に対する耐久性(7.5.3.1)、初期投入後・復旧後の一貫性(7.3)。`acid-report.md` を出す |
| `audit-report` | 仕様 10 章の全項目を、現状に照らして「自動点検・要確認・未達・未実施・外部手続き・該当なし」に分類した表 |
| `sut` | SUT だけを起動する(ストアが空なら EGen のデータを取り込み、再実行ログで復旧して、要求を待つ) |

### `measure` の主な引数

| 引数 | 既定 | 備考 |
|---|---|---|
| `--work` | `%TEMP%\tpce-compliance` | データ・ストア・結果の置き場。速いドライブに |
| `--customers` | 1000 | 1,000 の倍数 |
| `--days` | 1 | 初期取引の営業日数。**公式は 300**(1 日だと EGen のログに `NO` が出て不合格)。**Trade-Status は 1 口座に取引が 50 件以上、Customer-Position は履歴 10 件以上が要る**ため、少なすぎると EGenTxnHarness が誤り(-911、-221)を返して失敗に数える。1,000 顧客なら 10 日程度から |
| `--users` | 8 | CE のユーザー数。1 ユーザーの取引の間隔が 1 秒以下になるように |
| `--ramp-minutes` / `--measure-minutes` / `--down-minutes` | 20 / 10 / 0.5 | **公式は計測 120 分以上**。立ち上がりが短いと待っている指値注文が溜まらず、Trade-Result の比率が約1割低く出る(2,000 顧客で、立ち上がり 1 分は 9.26%、20 分は 10.05%) |
| `--tpse` / `--unthrottled` | 公称(顧客数÷500) | 目標の tpsE / ペース調整なし |
| `--checkpoint-minutes` | 7.5 | データがコミット済みの状態より 15 分以上古くならないための間隔 |

終了コード: 0 全点検に合格、1 不合格あり、2 想定外のエラー、3 引数の誤り、4 メモリ(開始時 80% 以上、または実行中 90% 到達で止める)、5 EGen のビルドが無い、130 取り消し。

## 構成

```text
EGenLoader.exe ──flat files──▶ SUT(C#, Tsavorite+FLinq)
EGenDriver.exe(CE×N, MEE, DM)──TxnInput の構造体/TCP──▶ SUT ──EGenTxnHarness(EGenHarness.dll)──▶ 12 取引のフレーム(C#)
                                                        └──redo log(fsync)──▶ 応答
```

- **取引の手順**: フレームの呼び出し順・出力の検証・状態コードは EGen の EGenTxnHarness が持つ(5.9.1)。C# は、ハーネスが DB インターフェース(`C*DBInterface`、29 フレーム)経由で呼ぶフレームを実装する(`Shared/TpcE.Transactions/Harness/`)。フレームの入出力は EGen の構造体のバイト列で、C# 側は `gen_harness_structs.py` が MSVC で測って生成したラッパーで読み書きする。
- **要求の形式**: 取引は「`0x01` + 取引 ID(4 バイト) + EGen の `TxnInput` の構造体のバイト列」。運転側は EGen が作った構造体を変換せずに送る。管理用(`consistency`、`checkpoint`、`load_units` など)は JSON。
- **市場への注文**: ハーネスが `SendToMarket` で渡す注文(Trade-Order の成行/指値、Market-Feed が発動させた指値)を、SUT が応答の `trade_requests` に載せる。運転側が MEE へ送り返す。

- **SUT**: 要求を 1 本の作業スレッドで直列に実行し、書き込みを再実行ログに追記して**まとめて fsync してから応答**する。チェックポイントは 7.5 分ごと(`Store.Save` とログの位置を同時に確定)。起動時はチェックポイントに再実行ログを当てて復旧する。
- **運転側**: EGen の CE・MEE・DM をそのまま使う。応答時間は運転側で計る(sT=送る直前、eT=応答を受けた直後)。EGen の `DataFileManager` と各オブジェクトのコンストラクターはスレッド安全ではないので、入力は即時読み込み、オブジェクトは main で順に作る。
- **点検**: 取引比率(6.3)、応答時間(6.5.1)、失敗(ハーネスの status が負の取引も失敗に数える)、計測区間(6.6.5.1)、性能が公称の 80〜102%(6.7.1.2)、取引の間隔(6.5.2.2)、チェックポイント間隔(6.6.5.3)、EGen のログに `NO` が無い(10.6.2.2)、一貫性の 3 条件(7.3.2)、負荷単位ごとの Trade-Result の件数(EGenValidate、6.7.4。顧客 2,000 人以上で検定できる)。
- **EGen のスレッド安全でない部分**: `DataFileManager` と CCE/CMEE/CDM のコンストラクターは main で順に作る。MEE は `GenerateTradeResult` の最中に `SubmitTradeRequest` を呼ぶと内部の待ち行列が壊れるので、Market-Feed が発動させた注文は、戻ってから MEE のスレッドが渡す。

## 既知の隔たり(公式化には直す必要がある)

`audit-report` に全項目と理由がある。主なもの:

- SUT は取引を**直列**に実行する(**意図した設計判断**。同時実行の制御を持たない)。直列なので P0〜P3 は起こり得ず、7.4.1.7 の代替技法として扱うが、隔離の試験(7.4.2)の手順は実行できず、7.4.1.4『任意の取引の同時実行を許す』を満たすかは監査人との協議が要る。
- 外部キーと CHECK 制約を強制していない。
- 運転側と SUT は同じ PC の loopback TCP(公式には別マシンまたはネットワーク経由の構成が要る)。
- 初期取引は 1 日(公式は 300 日)、計測は短い(公式は 2 時間以上)。
- 電源の喪失・記憶装置の喪失・データへのアクセスの試験は未実施。プロセスの強制停止は OS のキャッシュが残るので、fsync の漏れは検出できない。
- 価格、開示報告書、監査、提出、製品としての一般提供は、技術以外の作業(外部手続き)。

## 試験

```bash
dotnet test Benchmark/Benchmark.TpcE.Compliance.Tests -c Release -m:1
```

EGen のビルド(`egen-build`)がある環境では、EGen の実データを使う試験も流れる。無い環境では、それらは何もせず抜ける。CI には入れていない。
