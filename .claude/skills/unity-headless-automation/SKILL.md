---
name: unity-headless-automation
description: Automate Unity Editor/batchmode development loops for Replay Syndrome. Use when Codex must generate scenes/assets, run Unity validation, capture screenshots, inspect results, and iterate without asking the user to click Unity menus.
---

# Unity Headless Automation

Use this skill when the user wants AI-driven Unity work without manual Editor operation. Prefer automation over asking the user to open menus.

## Core Rule

Do not ask the user to operate Unity unless the automation bridge is unavailable or Unity reports a blocking compile/runtime error that needs a screenshot or log from the open Editor.

## Project Paths

- Unity project: `g:\Unity\6.4\AIDevelop`
- Unity executable: `C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe`
- Main automation script: `tools/unity-automation/run-chapter1-automation.ps1`
- Automation request script: `tools/unity-automation/request-chapter1-full-pass.ps1`
- Choice preview request script: `tools/unity-automation/request-chapter1-play-choice-check.ps1`
- Live choice request script: `tools/unity-automation/request-chapter1-live-choice-check.ps1`
- Critical path request script: `tools/unity-automation/request-chapter1-critical-path-check.ps1`
- Live critical path request script: `tools/unity-automation/request-chapter1-live-critical-path-check.ps1`
- Live manual C -> C path request script: `tools/unity-automation/request-chapter1-live-manual-path-check.ps1`
- Verification suite script: `tools/unity-automation/request-chapter1-verification-suite.ps1`
- Unity bridge request file: `Temp/ReplaySyndromeAutomation/request.txt`
- Unity bridge result file: `Logs/ReplaySyndromeAutomation/last-result.txt`
- Verification suite result file: `Logs/ReplaySyndromeAutomation/chapter1-verification-suite.txt`
- Screenshot output: `Logs/ReplaySyndromeAutomation/Screenshots/`

## Decision Tree

1. If Unity Editor is already open on this project:
   - First use `tools/unity-automation/run-chapter1-automation.ps1`.
   - By default this runs the verification suite through the open-editor bridge.
   - Read `Logs/ReplaySyndromeAutomation/chapter1-verification-suite.txt`.
   - Inspect the latest PNG under `Logs/ReplaySyndromeAutomation/Screenshots/`.
   - If the bridge does not complete and the user has allowed autonomous Unity closing, rerun with `-AllowCloseUnity`.

2. If Unity Editor is closed:
   - Use `tools/unity-automation/run-chapter1-automation.ps1`.
   - It runs Unity batchmode with `ReplaySyndrome.EditorTools.UnityAutomationBridge.RunChapter1FullPass`.
   - Read the result file and screenshot.

3. If either path reports compile errors:
   - Read the Unity log.
   - Patch the smallest relevant C# or Editor file.
   - Run the same automation again.

4. If the screenshot is visually broken:
   - Patch blockout/UI/camera code.
   - Re-run automation.
   - Repeat until the screenshot is readable.

## Open-Editor Bridge Workflow

Use this when Unity is open and the project is compiled.

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/run-chapter1-automation.ps1
```

Expected behavior:

- The open Unity Editor polls `Temp/ReplaySyndromeAutomation/request.txt`.
- It runs the Chapter 1 verification suite by default.
- It rebuilds `Chapter1_NewFlowBlockout.unity` as needed.
- It captures screenshots for each check.
- It writes `Logs/ReplaySyndromeAutomation/chapter1-verification-suite.txt`.

If the script times out, assume Unity is not open, not compiled, or the bridge failed to load. Read Unity logs or use batchmode if Unity is closed.

If autonomous fallback is acceptable:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/run-chapter1-automation.ps1 -AllowCloseUnity
```

This sends Unity a graceful close request. If Unity shows an unsaved changes dialog and does not close, the script must stop instead of killing Unity.

To specifically verify the Chapter 1 bridge choice moment, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-play-choice-check.ps1
```

This generates the blockout scene, forces the Route 3/C choice preview state in the Editor, captures a screenshot, and writes the same result file. It avoids entering Play Mode so Unity domain reloads cannot erase the automation state.

To verify that the real runtime flow reaches the Chapter 1 bridge choice in Play Mode, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-live-choice-check.ps1
```

This builds the blockout scene, enters Play Mode, waits until the real `ChoicePanel` and `OptionCButton` are visible, captures a screenshot, writes the result file, and stops Play Mode automatically. Use this after the preview check when validating the actual data-driven event runner.

To verify that Chapter 1 can complete through the current MVP critical path, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-critical-path-check.ps1
```

This builds the blockout scene, simulates the ScriptableObject event graph through Option C at the first bridge choice and the mutated replay choice, verifies the completion flags, prepares the completion overlay, captures a screenshot, and writes the result file. Use `live-choice` separately when validating real Play Mode UI.

To verify the same route in Play Mode, including Chapter 1 completion save state and the next-chapter hook, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-live-critical-path-check.ps1
```

This builds the blockout scene, enters Play Mode, resolves the real scene `ChapterEventRunner` through the C -> C critical path, verifies `Chapter1Saved: True`, verifies `NextChapterHook: Chapter2`, captures a screenshot, writes the result file, and stops Play Mode automatically.

To verify the same route through the visible runtime UI buttons, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-live-manual-path-check.ps1
```

This builds the blockout scene, enters Play Mode, starts at the bridge route, clicks the real `OptionCButton` twice, verifies `Chapter1Saved: True`, verifies `NextChapterHook: Chapter2`, captures a screenshot, writes the result file, and stops Play Mode automatically.

For the standard no-manual-Unity regression loop, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools/unity-automation/request-chapter1-verification-suite.ps1
```

This runs the data critical path check, the Play Mode live choice check, the Play Mode visible-button C -> C route check, and the Play Mode live critical path/save check in sequence. It writes a combined summary to `Logs/ReplaySyndromeAutomation/chapter1-verification-suite.txt`. Prefer this suite after code or scene-builder changes because it avoids request-file races from parallel execution.

## Batchmode Workflow

Use only when Unity is closed.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe' `
  -batchmode `
  -quit `
  -projectPath 'g:\Unity\6.4\AIDevelop' `
  -executeMethod ReplaySyndrome.EditorTools.UnityAutomationBridge.RunChapter1FullPass `
  -logFile 'g:\Unity\6.4\AIDevelop\Logs\ReplaySyndromeAutomation\unity-batch.log'
```

If batchmode says another Unity is open, switch to the open-editor bridge instead of asking the user to click menus.

## Screenshot Review Loop

After each automation run:

1. Read `last-result.txt`.
2. Open the latest screenshot with `view_image`.
3. Check:
   - `ROOM`, `BRIDGE`, `CHILD` are visible and not overlapping.
   - Route text is horizontal and readable.
   - MIG/Cue/Focus text is readable.
   - Evidence list is readable.
   - Choice buttons are visible when the flow reaches a choice event.
   - For live choice checks, the result mentions `Runtime ChoicePanel and OptionCButton became visible`.
   - For critical path checks, the result mentions `Chapter 1 critical path completed`.
   - For live critical path checks, the result mentions `Chapter1Saved: True` and `NextChapterHook: Chapter2`.
   - For live manual path checks, the result mentions `Manual C-clicks: 2`, `Chapter1Saved: True`, and `NextChapterHook: Chapter2`.
   - For the verification suite, the result mentions `suite=True`.
4. Patch the relevant file.
5. Re-run automation.

## Files Usually Patched

- `Assets/_Project/Editor/Chapter1NewFlowBlockoutBuilder.cs`: blockout layout, UI placement, camera setup.
- `Assets/_Project/Scripts/HospitalRoom/ChapterEventRunner.cs`: event flow, prompt timing, state progression.
- `Assets/_Project/Scripts/HospitalRoom/ChapterBlockoutCameraDirector.cs`: camera movement.
- `Assets/_Project/Scripts/HospitalRoom/ChapterBlockoutEvidencePresenter.cs`: evidence visibility and UI list.
- `Assets/_Project/Scripts/ChoiceUI/ChoiceUI.cs`: choice button behavior.
- `Assets/_Project/Scripts/ChoiceUI/ChoiceManager.cs`: input handling.

## Completion Criteria

The automation loop is successful when:

- `last-result.txt` has `success=True`.
- A fresh screenshot exists.
- The screenshot is readable without manual Unity interaction.
- The generated scene is `Assets/_Project/Scenes/Chapter1_NewFlowBlockout.unity`.
- The flow can be tested using `1/A`, `2/B`, `3/C` or UI buttons.
- The live critical path result confirms Chapter 1 is saved and `Chapter2` is available as the next-chapter hook.

## Safety

- Do not manually edit `.meta` files.
- Do not delete user-authored scenes.
- Do not use `git reset` or destructive cleanup.
- Do not ask the user to click Unity menus unless automation is genuinely unavailable.
