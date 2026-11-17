namespace Management.Tests;

// 成果物の検査・読み出しの予算(SourceBuildArtifactInspector.SharedBudget)はプロセスで1つ。
// 予算が0に戻ることを確かめる試験があるので、SDK のビルドの成果物を開く試験は、ほかの試験と同時に流さない。
[CollectionDefinition(Name,DisableParallelization=true)]
public sealed class SourceBuildBudgetCollection{internal const string Name="SourceBuildBudget";}
