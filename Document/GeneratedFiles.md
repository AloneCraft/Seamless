---
phase: code-reverse-summary
reverse_document: true
canonical_target: Document/GeneratedFiles.md
evidence_sources:
  - Reverse/Code/
confidence: medium
review_status: draft
---

# Generated Files

## 生成済み

### Code/Internal
- `Reverse/Code/Internal/FLinqSourceGeneratorInternalDesign.md`
- `Reverse/Code/Internal/TsavoriteCollectionsInternalDesign.md`
- `Reverse/Code/Internal/TsavoriteDBSourceGeneratorInternalDesign.md`
- `Reverse/Code/Internal/BenchmarkRunnerUnoInternalDesign.md`
- `Reverse/Code/Internal/StructuralDiagrams/ClassDiagram.md`
- `Reverse/Code/Internal/StructuralDiagrams/ComponentDiagram.md`
- `Reverse/Code/Internal/BehavioralDiagrams/InteractionDiagrams.md`
- `Reverse/Code/Internal/BehavioralDiagrams/StateMachineDiagram.md`

### Code/External
- `Reverse/Code/External/CodeModuleInterface.md`
- `Reverse/Code/External/CodeDataContract.md`
- `Reverse/Code/External/CodeLifecycleContract.md`

### Code/Basic
- `Reverse/Code/Basic/ModuleBreakdown.md`
- `Reverse/Code/Basic/DataFlowOverview.md`
- `Reverse/Code/Basic/LayerMapping.md`

### Code/Concept
- `Reverse/Code/Concept/TechStackSelection.md`
- `Reverse/Code/Concept/CoreArchitecturePattern.md`
- `Reverse/Code/Concept/TechDataConstraint.md`

## 未生成

- Game 系仕様書は、ユーザー指定により生成対象外。
- `Document/` 配下への反映は未実施。
- 全クラス単位の内部設計書は未生成。今回は主要コンポーネント単位で復元した。

## Review Gate: Code Summary

### Confirmed
- Code 系の Internal / External / Basic / Concept 文書を生成した。

### Inferred
- confidence: medium。現時点の非ゲーム仕様としては、主要設計判断を追跡するのに足る粒度である。

### Unknown
- 全クラス網羅版を必要とするか。
