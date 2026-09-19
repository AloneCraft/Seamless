# AI Syntax Rules

このファイルは、GPT/Codex、Claude、DeepSeek などの AI エージェントが Seamless ソリューション全体を編集するときに参照する共通の構文ルールです。

## Scope

- 対象はソリューション全体です。
- より近い階層に `.editorconfig` がある場合は、その設定も併用してください。
- 既存ファイルの局所的な配置規則がある場合は、既存コードの形を優先してください。

## C# Formatting

- インデントはスペース 4 個にしてください。
- タブは使わないでください。
- 行末スペースは削除してください。
- ファイル末尾には改行を入れてください。
- 波括弧は K&R スタイルで、開始波括弧を同じ行に置いてください。
- `if`、`else`、`for`、`foreach`、`while`、`switch` などの制御文では、複数行ブロックに必ず `{}` を付けてください。
- 片側に処理がない `if`/`else` でも `{}` を省略しないでください。
- ただし、既存コードと一致する単行制御文では、既存の省略スタイルを崩さないでください。
- 同一型内のインスタンスメンバーへアクセスするときは、フィールド、プロパティ、メソッド、イベントのいずれも `this.Member` または `this.Member()` の形で `this.` を付けてください。
- コンストラクタ、通常メソッド、プロパティ、イベント処理など、インスタンス文脈では一貫して `this.` を付けてください。
- static メンバー、ローカル変数、引数、型名アクセス、外部インスタンス経由アクセスには `this.` を付けないでください。
全ての識別子と記号間、記号と記号間、") {","> {"といった括弧の間のスペースは無し、カンマ前後のスペースは無し
- K&Rをsベースに

## Spacing

このソリューションでは、一般的な C# フォーマットよりも空白を詰めます。

- 識別子と記号の間の不要なスペースは入れないでください。
- 記号と記号の間の不要なスペースは入れないでください。
- 制御文キーワード直後のスペースは入れないでください。
- キャスト後のスペースは入れないでください。
- メソッド宣言、メソッド呼び出し、空引数リストの括弧内外に不要なスペースを入れないでください。
- カンマの前後にスペースを入れないでください。
- 継承句のコロン前後にスペースを入れないでください。
- 二項演算子の前後にスペースを入れないでください。
- `){`、`>{` のように、閉じ括弧やジェネリック閉じ括弧と開始波括弧の間にスペースを入れないでください。
- 三項演算子
    三項演算子は片方しか通過しなくてもカバレッジできないのでif{}else{}を必ず使うこと
- 制御文は省略して
    if(condition)
        continue;
- if elseで片方にしか処理がなくても{}は省略しない
    if(condition){
        処理
    }else{
    }
    if(condition){
    }else{
        処理
    }
- カンマの前後の間のスペースはなくして
    int a ,int b, int c
    int a,int b,int c
- 記号と識別子の間のスペースはなくして
    List<int> a
    List<int>a
- 改行の前にスペースがあったら削除して
    break;
    break;

例:

```csharp
class A:IDisposable{
    public void M(int a,int b){
        if(a>b){
            Do(a+b);
        }else{
        }
    }
}
```

## UI コードの日本語命名

- Seamless 独自の UI コードでは、型、メソッド、プロパティ、フィールド、引数、ローカル変数、列挙値、イベント、AutomationId、JSON キーを日本語で命名してください。
- 技術的な接尾語はカタカナ表記にし、非同期メソッドの接尾語 `Async` は維持してください。
- `bool` を表す名前は疑問形の `～か` とし、イベント処理メソッドは `～時` としてください。
- 名前空間、プロジェクト名、アセンブリ名、ファイル名、フォルダー名、外部 API、製品タイトルは日本語化しません。
- MAUI の起動規約に属する `App`、`MainPage`、`MauiProgram`、`Program`、`AppDelegate` は維持してください。
- `UI`、`DB`、`CPU`、`JSON`、`ID`、`FLinq` などの略語、Benchmark 引数名の `Count`、`Index`、`Page`、`Segment`、`Memory`、`Fraction`、`Initial`、`Data`、Worker の CLI 引数名と環境変数名は維持してください。
- `Index` などの英語維持は実際の Benchmark 引数名・DB列名を表す場合だけに限定し、一般的な技術接尾語やローカル変数では `インデックス` のようにカタカナ表記にしてください。
- 表示文言とコード上の概念名を一致させ、画面上で日本語にした独自概念には英語名の互換ラッパーを残さないでください。

## Collection And LINQ Constraints

- 大量データを扱う箇所では、全件を `List<T>` などのメモリ上コレクションへ蓄積しないでください。
- `Tsavorite` を使う理由は、メモリに収まらないサイズのコレクションを扱うためです。
- `Iterator`/`Enumerator` では、必要に応じて固定長配列などのチャンク単位でバッファリングし、少しずつ返してください。

## Namespace And File Layout

### 名前空間

- 原則として、プロジェクトのルート名前空間は `<AssemblyName>` としてください。
- csproj の `<RootNamespace>` は `<AssemblyName>` と同じ値にしてください。
- 公開APIや安定した論理階層を表す場合は、フォルダー階層と一致するサブ名前空間を使用できます。
- 分類専用のサブフォルダーは名前空間セグメントを増やす必要はありません(`.editorconfig` で `IDE0130` を無効化しています)。
- この規約変更だけを理由に既存コードを一括移行しないでください。
- プロジェクト名の末尾セグメントを第一階層フォルダーとして繰り返さないでください。
  `Seamless.Collections.Tsavorite/Tsavorite/Stores/` ではなく `Seamless.Collections.Tsavorite/Stores/` です。
- 出荷パッケージの `Seamless.` 接頭辞は、非出荷アセンブリ (テスト・ベンチマーク・共有) では使わないでください。

例外は次だけです。

- BCL polyfill の `System*` 名前空間。
- ソースジェネレータが出力する固定名前空間 (`Intercept`)。
- MAUI の起動規約 (`Platforms/Windows/App.xaml.cs` の `.WinUI` など、`x:Class` と一致させる必要があるもの)。
- 共有プロジェクト (`.shproj` / `.projitems`)。複数アセンブリへ取り込まれるため 1 対 1 にできません。
  共有プロジェクト名と同じ単一の名前空間 (`Shared.` 始まり) を持たせ、
  `#if` による名前空間の切り替えはしないでください。
- テストプロジェクト。同じテスト対象名のテストクラスをスイートごとに置けるよう、
  フォルダー階層を名前空間へ反映してかまいません
  (`Test/System/Linq/Intercept/Chain1/` → `Test.System.Linq.Intercept.Chain1`)。
  これは「テストクラスはテスト対象クラスと同じ名前にする」という下の Tests 規則を成り立たせるためです。

### ファイル名

- ファイル名は、そのファイルの主要な public 型名と一致させてください。ジェネリックのアリティは付けません。
- 対等な小さい型をまとめるファイルは、`DatabaseSystemTypes.cs` のようなグループ名にしてかまいません。
- partial 分割は `Type.Aspect.cs` にしてください
  (`FLinqIncrementalGenerator.ProviderFacts.cs`、`PipelineBuilder.Stages.cs`)。
- ただし演算子ごとに partial 分割する LINQ 実装は、演算子名をファイル名にしてください
  (`Seamless.Linq/Where.cs` が `partial class Enumerable` の `Where` を持つ形)。

## Project And Solution Registration

- 新しい `.csproj`/`.shproj` を作成または追加するときは、同じ変更の中で必ずソリューションファイル(`Seamless.slnx`)にも登録してください。登録を後回しにしないでください。
- `.slnx` へは、既存の `<Folder>` 分類規約(例: テストプロジェクトは `/Tests/` フォルダ配下)に従って `<Project Path="..."/>` を追加してください。
- プロジェクト追加後は、リポジトリ全体の `*.csproj`/`*.shproj` を列挙し、`Seamless.slnx` 内の `<Project Path>` と突合して差分ゼロであることを確認してください。
- ルート直下の `Benchmark.UI.slnx` などの軽量ソリューションは `Update-FocusedSolutions.ps1` が `Seamless.slnx` から生成します。手で編集しないでください。`ProjectReference`・csproj 内の `<MSBuild Projects>`・`.projitems` の `Import` を変更したとき、または `Seamless.slnx` を変更したときは、スクリプトを再実行し、生成された `*.slnx` も同じ変更に含めてください。新しい exe 用の軽量ソリューションは、スクリプト先頭の `$Solutions` に起点を追加して作ってください。
- 新規プロジェクトの `.csproj`/`.shproj` とその配下のソースファイルは、作業の完了条件として `git add` で必ずステージしてください。`git status` で未追跡(`??`)のまま放置しないでください。
- Visual Studio でプロジェクトが読み込まれない、または Solution Explorer 上でフォルダの中身が空に見える場合、多くは (a) `Seamless.slnx` への未登録、(b) `.gitignore` パターンが新規フォルダ名へ意図せず一致、のいずれかが原因です。プロジェクト作成後はこの両方を確認してください。

## File Layout Rules

- `Seamless.FLinq.Generator/SourceGenerator/Linq/Fragments` 配下の配置は、役割で分けてください。
- `Bases` には、`abstract` または `virtual` を持つ共通基底クラスだけを置いてください。
- `Helpers` には、複数クラスから使う純粋な共通ヘルパーだけを置いてください。
- 1 つのクラスからしか使われない共通処理は、可能ならそのクラスのファイル内に file-scoped でまとめてください。
- `Bases` に置くものは、`JoinBase`、`MinMaxByBase` のような複数クラスで共有する基底に限定してください。
- `Helpers` に置くものは、`InlinedScalarFunc` のような状態を持たない共通補助処理に限定してください。
- `DistinctBase` や `ToLookupBase` のように実質 1 系列専用の基底は、対応する本体ファイルへ集約してください。
- `GroupBySupport` のような補助ロジックは、関連する本体クラスのファイルへ移してください。
- アクセス修飾は最小にしてください。
- 外部公開 API である必要がない型は `internal` を優先してください。
- ファイル内限定で足りる補助関数は `private` にしてください。
- 新しい共通基底やヘルパーを追加するときは、まずこの分類に当てはめてから配置を決めてください。


## Tests

- テストクラスはテスト対象クラスと同じ名前にしてください。
- テストメソッドの順序はテスト対象の public メンバー順に合わせてください。
- 1 つの対象メソッドに関する複数ケースは、同じテストメソッド内で `{}` スコープを分け、コメントでケースを明示してください。
- `#if`/`#else` で `1.1.1` と `2.0beta3` を分岐しないでください。
- 版ごとにテストしたい場合は、対応するテストプロジェクト双方へ同じテストコードを置いてください。

例:

```csharp
[Fact,Trait("Category","Unit")]public void TryGetValue(){
    {
        //キーが一致するときのテスト
    }
    {
        //キーが一致しないときのテスト
    }
}
```
