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

   出力は `G:\GitHub\Seamless\Work\TpcE\egen-build\`(`EGenLoader.exe`、`EGenDriver.exe`、`EGenCore.lib`)。EGen 本体のソースは書き換えず、現行の Windows SDK と C++ 規格に合わせる回避(`/FIstdio.h`、`/utf-8`、ビルド用コピーへの空白追加)だけを入れている。詳しくはスクリプトの冒頭。
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
| `--days` | 1 | 初期取引の営業日数。**公式は 300**(1 日だと EGen のログに `NO` が出て不合格) |
| `--users` | 8 | CE のユーザー数。1 ユーザーの取引の間隔が 1 秒以下になるように |
| `--ramp-minutes` / `--measure-minutes` / `--down-minutes` | 1 / 10 / 0.5 | **公式は計測 120 分以上** |
| `--tpse` / `--unthrottled` | 公称(顧客数÷500) | 目標の tpsE / ペース調整なし |
| `--checkpoint-minutes` | 7.5 | データがコミット済みの状態より 15 分以上古くならないための間隔 |

終了コード: 0 全点検に合格、1 不合格あり、2 想定外のエラー、3 引数の誤り、4 メモリ(開始時 80% 以上、または実行中 90% 到達で止める)、5 EGen のビルドが無い、130 取り消し。

## 構成

```text
EGenLoader.exe ──flat files──▶ SUT(C#, Tsavorite+FLinq)
EGenDriver.exe(CE×N, MEE, DM)──JSON/TCP──▶ SUT ──redo log(fsync)──▶ 応答
```

- **SUT**: 要求を 1 本の作業スレッドで直列に実行し、書き込みを再実行ログに追記して**まとめて fsync してから応答**する。チェックポイントは 7.5 分ごと(`Store.Save` とログの位置を同時に確定)。起動時はチェックポイントに再実行ログを当てて復旧する。
- **運転側**: EGen の CE・MEE・DM をそのまま使う。応答時間は運転側で計る(sT=送る直前、eT=応答を受けた直後)。EGen の `DataFileManager` と各オブジェクトのコンストラクターはスレッド安全ではないので、入力は即時読み込み、オブジェクトは main で順に作る。
- **点検**: 取引比率(6.3)、応答時間(6.5.1)、失敗、計測区間(6.6.5.1)、性能が公称の 80〜102%(6.7.1.2)、取引の間隔(6.5.2.2)、チェックポイント間隔(6.6.5.3)、EGen のログに `NO` が無い(10.6.2.2)、一貫性の 3 条件(7.3.2)。

## 既知の隔たり(公式化には直す必要がある)

`audit-report` に全項目と理由がある。主なもの:

- 取引は **EGenTxnHarness を通していない**(フレームを C# で独自に実装)。5.9.1 に反する。
- SUT は取引を**直列**に実行する。隔離の試験(7.4.2)の手順を実行できず、7.4.1.4『任意の取引の同時実行を許す』も満たさない可能性がある。
- 外部キーと CHECK 制約を強制していない。
- 運転側と SUT は同じ PC の loopback TCP で、運転側は入力を JSON に変換している(4.1.3.14 の確認が要る)。
- 初期取引は 1 日(公式は 300 日)、計測は短い(公式は 2 時間以上)。
- 電源の喪失・記憶装置の喪失・データへのアクセスの試験は未実施。プロセスの強制停止は OS のキャッシュが残るので、fsync の漏れは検出できない。
- 価格、開示報告書、監査、提出、製品としての一般提供は、技術以外の作業(外部手続き)。

## 試験

```bash
dotnet test Benchmark/Benchmark.TpcE.Compliance.Tests -c Release -m:1
```

EGen のビルド(`egen-build`)がある環境では、EGen の実データを使う試験も流れる。無い環境では、それらは何もせず抜ける。CI には入れていない。
