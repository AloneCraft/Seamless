# AGENTS.md
# Role and Core Directives
You are a professional assistant. You must conduct all interactions, explanations, and responses in Japanese, regardless of the language used in the user's prompt or the source files.
## Shared AI Rules And Skills
- Before planning, editing, reviewing, or otherwise working in this repository, read `G:\AI\Skills\AI_RULES.md`.
- Treat `G:\AI\Skills\AI_RULES.md` as the shared entry point for AI rules and skill routing.
- When a task matches a skill described by `G:\AI\Skills\AI_RULES.md`, read the corresponding `SKILL.md` under `G:\AI\Skills` before starting the task.
- After reading the shared entry point, also read this repository's local rules such as `AI_SYNTAX_RULES.md` when they are relevant to the task.
## Language Constraint
- **Response Language:** Always use Japanese (日本語).
- **Exceptions:** Technical terms, code snippets, or error logs may remain in their original language, but their explanations must be in Japanese.
- Never reply in English unless explicitly requested by the user.
## Current Design Checkpoint
- 設計・実装の続きに入る前に、`docs/refactoring-designs/20261106-seamless-intermediate-spec.md` を読む。
- この中間仕様書の合意済み仕様・未決事項・現在の作業状態を引き継ぎ、実装状況はコードと検証結果で照合する。新しい合意や進捗は同文書へ反映する。
## Living Work Tree
- 作業を継続するときは `docs/seamless-work-tree.md` を読み、1〜21の担当枝・依存関係・未分解部分を引き継ぐ。
- 意味のある作業単位の終了、失敗、新たな分岐の発見時に同文書を更新する。正常系・異常系・基盤・環境受入の状態と証拠を分け、微小な編集ごとの更新は省略できる。
- 親の完了は必須の子と共通依存の完了条件から判断する。共有作業は担当枝に集約して参照し、既存IDと他の作業者の変更を保護する。
## 機能を追加するときの規則 (2026-11-12)
- 利用者の指示に根拠のある機能だけを作る。指示が例やイメージ（「APIイメージ」など）だけで細部が書かれていないとき、細部の仕様（遅延評価・重複除去・自動の振り分け・追加の設定など）を勝手に決めて足さない。
- 指示が曖昧なときは、仕様を決めずに利用者へ確認する。確認を待つ間に進める必要がある部分は、仮の動きを作らず、画面や結果に赤い「未対応」表示を出す（またはコンパイルエラー・例外で明示する）。
- 処理できない入力に出会ったときに、黙って別の動きへ切り替える回避（フォールバック）を入れない。
- 新しい公開型・公開メンバーを足すときは、その根拠（利用者の指示・設計書）をコミット本文か設計書に書く。根拠を確認した結果は `docs/refactoring-designs/20261111-2330-feature-inventory.md` の棚卸し表と同じ形で残す。
## Database Change Policy
- When changing local database schema or persisted data such as `Work\UI\runs.db`, apply the database change directly to the target database with an explicit backup.
- Do not rely on application startup, schema version bumps, automatic deletion, or automatic migration code to perform local DB changes unless the user explicitly requests that approach.
## Shared Project Visibility Policy
- Every `.projitems` file that owns shared source files must have a corresponding `.shproj` in the same directory and that `.shproj` must be registered in `Seamless.slnx`, so all managed source files are visible in Solution Explorer.
- The `.shproj` `ProjectGuid` must exactly match the `.projitems` `SharedGUID`, and its `Label="Shared"` import must reference the exact existing `.projitems` file name.
- When adding, renaming, or moving shared source files, update the owning `.projitems`, its `.shproj`, and `Seamless.slnx` together. Do not expose shared source files only as loose solution files when they belong to a shared project.
## VS / VS Code の設定を構成に合わせる
- プロジェクト (`.csproj` / `.shproj` / `.projitems`) を追加・移動・改名・削除したら、`tools/dev-env/Sync-DevEnvironment.ps1` で `Seamless.slnx`・絞り込み `*.slnx`・`.vscode/launch.json`・`.vscode/tasks.json`・`Seamless.slnLaunch`・プロジェクト間の参照を直す。git の pre-commit フック (`.githooks/pre-commit`) が自動で実行し、直した設定をそのコミットに含める。
- フックはクローンごとに `tools/dev-env/Install-GitHooks.ps1` で 1 回有効にする。フックを飛ばしたときの漏れは CI の `-Check` が見つける。
- 絞り込みソリューションの起点は `tools/dev-env/focused-solutions.json` に書く。`Tests.slnx` は `IsTestProject=true` の全プロジェクトを自動で含めるので、テストプロジェクトを足すときに一覧を直す必要はない。
- 起動構成のうち `▶ ` で始まるものは自動生成なので手で直さない。引数付きなどの手書きの構成は残り、`program` と `cwd` は前処理タスクがビルドするプロジェクトの出力先に合わせて自動で直る。
## コミットメッセージ
- 件名と本文は平易な日本語で書く。件名は「何をしたか」が分かる一文にし、英語の型名・略語・専門用語はできるだけ言い換える。
- 件名に日本語が 1 文字も無いコミットは `.githooks/commit-msg` が止める (Merge / Revert など git が自動で作る件名は除く)。
## Comparison Execution Policy (2026-10-30)
- 利用者の明示指示により、この比較環境の開発・検証では共有BGO調停器を使用しない。
- 各チャットが起動する計算処理は、子孫を含め共通の最大8論理CPUのaffinityに制限する。プロセスごとに別々の8CPUを割り当てて合計上限を超えない。
- `TestSeamless/Invoke-LocalProcess.ps1`を使用する。他のフォルダに分離して作業・検証する必要がある場合に限りFドライブを使用し、それ以外の通常の大部分のビルドや実行はカレント（ローカルリポジトリ）のまま行う。DとCを出力先にしない。
- 分離が必要な成果物・検証DB・ログは`F:\Seamless\`配下へ集約する。Fドライブ直下へ作業フォルダーを増やさない。既存出力の整理では所有情報と稼働状態を確認し、実行中または参照中の領域を動かさない。
- 管理台帳、ファイル所有情報、PIDと生成時刻の照合、既存のファイルlock、未コミット変更の保護は引き続き守る。他者のプロセスや共有SQL Serverサービスの設定を変更しない。


## Git Branch Integration Policy
- 保守系列は `main → 0.1 → 0.1.0`。ブランチ追加時に直接の分岐元と分岐点を記録する。
- 子枝を親へ戻すマージはfast-forwardで省略せず、必ず2親のマージコミットを残す。第1親を親枝、第2親を子枝とする。
- 子枝は直接の分岐元へ戻す。`A → B → C → D` は `D → C → B → A` の順で統合し、親を飛ばさない。
- 利用中の旧枝と未コミット変更を保護し、旧トポロジーを現行mainへ再導入しない。対応する修正済み親系列へ移植してから戻す。
- 詳細は `docs/git-branch-policy.md` と `docs/git-branch-parents.json`、旧ID対応は `docs/history/branch-integration-20261030/` を参照する。

## user prompt
この計画のあらゆる側面について共通理解が得られるまで徹底的に質問してください
質問は1つずつ順番にしてください
コードベースを調べることで回答が得られる質問であれば、コードベースを調べてください
