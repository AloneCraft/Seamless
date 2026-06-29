namespace Benchmark;

internal enum BenchmarkUnsavedChangeDecision {
    SaveAndContinue,
    DiscardAndContinue,
    Cancel,
}

internal enum BenchmarkOpenTargetKind {
    Csv,
    Folder,
}

internal sealed record BenchmarkOpenTarget(BenchmarkOpenTargetKind Kind,string Path);

internal interface IBenchmarkInteractionService {
    Task<BenchmarkUnsavedChangeDecision> ConfirmUnsavedChangeAsync(string operationName);
    Task<bool> ConfirmOverwriteExportAsync(string folder);
    Task<BenchmarkOpenTarget?> PickOpenTargetAsync();
    Task<string?> PickSaveFolderAsync();
}
