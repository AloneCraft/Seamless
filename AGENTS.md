# AGENTS.md
# Role and Core Directives
You are a professional assistant. You must conduct all interactions, explanations, and responses in Japanese, regardless of the language used in the user's prompt or the source files.
## Shared AI Rules And Skills
- Before planning, editing, reviewing, or otherwise working in this repository, read `G:\AI\ai_rules.md`.
- Treat `G:\AI\ai_rules.md` as the shared entry point for AI rules and skill routing.
- When a task matches a skill described by `G:\AI\ai_rules.md`, read the corresponding `SKILL.md` under `G:\AI\Skills` before starting the task.
- After reading the shared entry point, also read this repository's local rules such as `AI_SYNTAX_RULES.md` when they are relevant to the task.
## Language Constraint
- **Response Language:** Always use Japanese (日本語).
- **Exceptions:** Technical terms, code snippets, or error logs may remain in their original language, but their explanations must be in Japanese.
- Never reply in English unless explicitly requested by the user.
## Database Change Policy
- When changing local database schema or persisted data such as `Work\UI\runs.db`, apply the database change directly to the target database with an explicit backup.
- Do not rely on application startup, schema version bumps, automatic deletion, or automatic migration code to perform local DB changes unless the user explicitly requests that approach.
## Shared Project Visibility Policy
- Every `.projitems` file that owns shared source files must have a corresponding `.shproj` in the same directory and that `.shproj` must be registered in `Seamless.slnx`, so all managed source files are visible in Solution Explorer.
- The `.shproj` `ProjectGuid` must exactly match the `.projitems` `SharedGUID`, and its `Label="Shared"` import must reference the exact existing `.projitems` file name.
- When adding, renaming, or moving shared source files, update the owning `.projitems`, its `.shproj`, and `Seamless.slnx` together. Do not expose shared source files only as loose solution files when they belong to a shared project.
## user prompt
この計画のあらゆる側面について共通理解が得られるまで徹底的に質問してください
質問は1つずつ順番にしてください
コードベースを調べることで回答が得られる質問であれば、コードベースを調べてください
