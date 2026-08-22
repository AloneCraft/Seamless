namespace UI;

public interface ICPUトポロジーサービス {
    IReadOnlyList<CPU選択項目> 読み込む();
}

public sealed class CPUトポロジーサービス:ICPUトポロジーサービス {
    public IReadOnlyList<CPU選択項目> 読み込む() {
        return ベンチマークCPUトポロジープロバイダー.読み込む()
            .OrderBy(static CPU => CPU.論理プロセッサインデックス)
            .Select((CPU,番号) => new CPU選択項目(
                CPU.論理プロセッサインデックス,
                CPU.コア種別,
                CPU.物理コアインデックス,
                CPU.種別内コアインデックス,
                番号==0))
            .ToArray();
    }
}
