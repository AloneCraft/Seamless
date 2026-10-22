using System.Text;
namespace Benchmark.TpcE.Compliance;
internal enum AuditStatus{
    /// <summary>`measure` / `acid` / 試験が自動で点検する(結果は各報告に出る)。</summary>
    Automated,
    /// <summary>証拠(ログ・設定・ソース)は出るが、人が確かめる必要がある。</summary>
    Evidence,
    /// <summary>仕様に合っていない既知の隔たり。公式化するには直す必要がある。</summary>
    Gap,
    /// <summary>この自己点検の枠では実施していない。</summary>
    NotPerformed,
    /// <summary>外部の手続き(監査人、価格、開示報告書、製品の一般提供、提出)。</summary>
    External,
    /// <summary>この構成には当てはまらない。</summary>
    NotApplicable,
}
internal sealed record AuditItem(string Clause,string Requirement,AuditStatus Status,string Note);
/// <summary>仕様 1.14.0 の 10 章(監査の点検項目)を、この実装の現状に照らして 1 項目ずつ分類した自己監査の報告。</summary>
internal static class AuditReport{
    public static readonly IReadOnlyList<AuditItem> Items=[
        new("10.1","TPC 認定の独立した監査人が結果を審査し、証明書を出す",AuditStatus.External,"監査人との契約と費用が必要。このリハーサルは自己計測で、数字は公開できない"),
        new("10.2.1.1","列のデータ型が 2.2.1 を満たす",AuditStatus.Evidence,"スキーマは Benchmark.TpcE.Core/Schema/*.cs。金額は decimal、日時はミリ秒の long、文字の ID は ASCII を詰めた Code8/16/24。型の対応表の点検は未実施"),
        new("10.2.1.2","列のメタ型が 2.2.2 を満たす",AuditStatus.Evidence,"同上"),
        new("10.2.1.3〜6","顧客系 9・仲介系 9・市場系 11・次元系 4 の 33 表がある",AuditStatus.Automated,"試験 EGenFlatFileImporter.CoversAll33Tables と、EGen の実データの取り込み"),
        new("10.2.1.7","主キー・外部キー・CHECK 制約を DB が維持する",AuditStatus.Gap,"主キーの重複は拒否するが、外部キーと CHECK 制約は強制していない(コレクションを直接使うため)"),
        new("10.2.1.8","複製した表がある場合の要件",AuditStatus.NotApplicable,"表は複製していない"),
        new("10.2.1.9","列を他の表へ複製して性能を上げていない",AuditStatus.Evidence,"副索引(18 本)は別の構造で、表の列は増やしていない。人が確かめる"),
        new("10.2.1.10〜11","列は論理的に独立で、1 つの列として読める",AuditStatus.Evidence,"Code8/16/24 は内部の詰め方で、読み書きは 1 つの値。人が確かめる"),
        new("10.2.1.12","主キーが行の物理アドレスの直接の表現でない",AuditStatus.Evidence,"主キーは業務の ID で、ハッシュで引く"),
        new("10.2.1.13","BLOB 列(NEWS_ITEM.NI_ITEM)の性質",AuditStatus.Evidence,"byte[] として行に持つ。部分読み出しの要件は未点検"),
        new("10.2.1.14","整合性規則(2.4)",AuditStatus.Gap,"外部キーを強制していないため(10.2.1.7 と同じ)"),
        new("10.2.1.15","データ参照の透過性(2.5)",AuditStatus.NotApplicable,"表は分割していない"),
        new("10.2.2.1","EGenLoader の版が仕様に合う",AuditStatus.Evidence,"EGen 1.14.0 の正規版を使用。ただし現行 MSVC でビルドするため、EGenLoader.cpp と EGenVersion.cpp のコピーに空白を足している(build-egen.ps1)。9.3.5.4 に従い開示が必要"),
        new("10.2.2.2","EGenLogger の出力に NO が無い",AuditStatus.Automated,"EGenLogChecks。初期取引の日数が 300 でないと NO になる(リハーサルの縮小設定)"),
        new("10.2.2.3","DB を EGenLoader のデータで投入する",AuditStatus.Automated,"SUT は EGenLoader のフラットファイルだけから投入する(sut.log の loaded 行)"),
        new("10.2.2.4","ロード単位(1,000 顧客)の整数倍",AuditStatus.Automated,"MeasureArguments が顧客数を 1,000 の倍数に限る"),
        new("10.2.2.5","初期取引の営業日数が ITD(既定 300)",AuditStatus.Gap,"リハーサルは 1 日で縮小。公式化には 300 日が要る(10.2.2.2 に NO として出る)"),
        new("10.2.2.6","表ごとの件数が 2.6.1 の式に合う",AuditStatus.NotPerformed,"件数の式との照合は未実装(取り込んだ件数は sut.log に出る)"),
        new("10.2.2.7","成長しない表が 5% 以上増やせる",AuditStatus.NotPerformed,"ストア容量の余裕の検証は未実施"),
        new("10.3.1","12 取引の入力・出力・フレームが 3.3 に合う",AuditStatus.Evidence,"取引は仕様のフレームに沿って C# で実装(Shared/TpcE.Transactions)。単体試験はあるが、擬似コードとの同等性は人が確かめる"),
        new("10.3.2","フレームが静的データへの参照を省略しない",AuditStatus.Evidence,"人が確かめる"),
        new("10.3.3","フレームが入出力以外でデータを渡さない",AuditStatus.Evidence,"人が確かめる"),
        new("10.4.1","EGenDriver Connector へ渡すデータの形式を変えない",AuditStatus.Gap,"運転側は入力の構造体を JSON に変換して SUT へ送る。4.1.3.14 が許す範囲か、監査人との確認が要る"),
        new("10.4.7","運転側と SUT の間にネットワークがある",AuditStatus.Gap,"同じ PC の loopback TCP。公式には別マシンまたはネットワーク経由の構成が要る(4.2.2)"),
        new("10.4.8","運転中のオペレーターの介入が制限内",AuditStatus.Automated,"measure は無人で流す"),
        new("10.5.CE","CE は EGenDriverCE で実装",AuditStatus.Automated,"EGen の CCE を無改変で使用(EGenDriver.cpp)"),
        new("10.5.MEE","MEE は EGenDriverMEE で実装",AuditStatus.Automated,"EGen の CMEE を無改変で使用"),
        new("10.5.DM","Data-Maintenance は EGenDriverDM で実装",AuditStatus.Automated,"EGen の CDM を無改変で使用。生成器は 1 つ"),
        new("10.5.Harness","EGenTxnHarness を使う(5.9.1)",AuditStatus.Gap,"取引の手順(フレームの呼び出し順)は EGenTxnHarness を通らず、C# で独自に実装している。公式化には EGenTxnHarness のフレーム単位の DB インターフェースを実装して通す必要がある"),
        new("10.5.InputFiles","EGenInputFiles が正しい版で未改変、全コピーが同一",AuditStatus.Evidence,"ローダーと運転側が同じ flat_in フォルダを使う(measure の引数)。内容のハッシュの記録は未実装"),
        new("10.6.1.1","DB の内容が 6.6.2.1 / 6.6.2.3 を満たす",AuditStatus.Evidence,"人が確かめる"),
        new("10.6.1.2","Test Run の前に Trade-Cleanup を実行した",AuditStatus.Automated,"運転側が開始時に実行する(driver.log)"),
        new("10.6.1.3","Test Run 中に Trade-Cleanup を実行しない",AuditStatus.Automated,"運転側は開始時の 1 回だけ"),
        new("10.6.1.4","時計の同期(4.4.4)",AuditStatus.NotApplicable,"同じ PC の 1 つの時計"),
        new("10.6.2.1","CE・DM・MEE がローダーと同じ全体入力を使う",AuditStatus.Automated,"measure が SUT と運転側へ同じ顧客数・日数・スケール係数を渡す"),
        new("10.6.2.2","EGenLogger の出力に NO が無い",AuditStatus.Automated,"EGenLogChecks(10.2.2.2 と同じ)"),
        new("10.6.3.1〜4","取引比率が 6.3 の許容範囲で、精度と丸めが正しい",AuditStatus.Automated,"ComplianceChecks(許容範囲は ComplianceSpec)。有効な取引だけを数え、Data-Maintenance を除く"),
        new("10.6.3.5","CE の入力のばらつきが範囲内",AuditStatus.NotApplicable,"EGen の CE を無改変で使うため"),
        new("10.6.3.6","ロード単位の数が実行中に使われた数と同じ",AuditStatus.Automated,"同じ顧客数を渡す"),
        new("10.6.4.1","応答時間が 6.5.1.2 を満たす",AuditStatus.Automated,"ComplianceChecks"),
        new("10.6.4.2","平均が 90 パーセンタイル以下",AuditStatus.Automated,"ComplianceChecks"),
        new("10.6.4.3","取引の間隔(Dispatch Time)が 6.5.2 を満たす",AuditStatus.Automated,"設定からの上限の点検のみ。実測の間隔の分布は未記録"),
        new("10.6.5","C_ID の分割",AuditStatus.NotApplicable,"使っていない(CE は顧客の全範囲から選ぶ)"),
        new("10.6.6.1〜2","性能が公称の 80〜102% で、報告値が公称以下",AuditStatus.Automated,"ComplianceChecks.ReportedThroughput"),
        new("10.6.7.1","Data-Maintenance の生成器が 1 つ",AuditStatus.Automated,"運転側は DM スレッドを 1 本だけ起こす"),
        new("10.6.7.2","60 秒ごとに起動し、55 秒以内に終わる",AuditStatus.Automated,"応答時間(最大 55 秒)は点検。起動間隔は --dm-seconds(既定 60)で、実測の間隔の記録は未実装"),
        new("10.6.7.3","Data-Maintenance が 3.3.11 の行を変更する",AuditStatus.Evidence,"人が確かめる(単体試験あり)"),
        new("10.6.8.1〜2","定常状態(6.6.3、6.6.4)の要件",AuditStatus.NotPerformed,"Trade-Result の毎秒件数の推移は記録するが、持続的な性能の判定は未実装"),
        new("10.6.8.3","計測区間が 6.6.5 を満たす",AuditStatus.Automated,"2 時間以上(6.6.5.1)とチェックポイント間隔(6.6.5.3)を点検。リハーサルの短い実行では未達"),
        new("10.6.9.1","EGenValidate が Passed! を出す",AuditStatus.NotPerformed,"EGenValidate(6.7.4)の入力に、計測区間中のロード単位(顧客 1,000 人の区間)ごとの Trade-Result 件数の標準偏差が要る。その集計(SUT 側の単位別カウンター)と EGenValidate のビルド・実行が未実装。顧客 1,000 人だとロード単位が 1 つで標準偏差は意味を持たない"),
        new("10.6.10.1","データの増加(60 日分)の計算と領域",AuditStatus.NotPerformed,"未実施"),
        new("10.7.2","原子性の試験(7.2.2)",AuditStatus.Automated,"acid: 確定する/巻き戻す Trade-Order"),
        new("10.7.3","一貫性の試験(7.3.3)",AuditStatus.Automated,"初期投入後・復旧後・計測後に 3 条件を全行から点検"),
        new("10.7.4","隔離の試験(7.4.2)",AuditStatus.Gap,"SUT は取引を直列に実行するため、取引どうしが重ならず試験の手順(S3 がフレームの間で止まり S4 が割り込む)を実行できない。7.4.1.4『任意の取引の同時実行を許す』も満たさない可能性がある"),
        new("10.7.5","データへのアクセス(7.6)",AuditStatus.NotPerformed,"ドライブ 1 台の永久故障を作る構成が無い"),
        new("10.7.6.1〜2","耐久性(7.5): 単一故障からの復旧",AuditStatus.Evidence,"処理の喪失(プロセスの強制停止)のみ実施。記憶装置の喪失と電源の喪失は未実施。強制停止は OS のキャッシュが残るので fsync の漏れを検出できない"),
        new("10.7.6.3〜4","Business Recovery Time とグラフ",AuditStatus.NotPerformed,"復旧時間の測定とグラフは未実装"),
        new("10.8","価格(8 章): 3 年の総費用、60 日分の領域、部品の代替",AuditStatus.External,"製品として一般提供(文書・保守・価格)されていることが前提。Seamless は未提供"),
        new("10.9","開示報告書(9 章)と補足ファイル",AuditStatus.External,"report.md / report.json は点検結果で、開示報告書ではない"),
    ];
    public static string Markdown(){
        var builder=new StringBuilder();
        builder.AppendLine("# TPC-E 準拠リハーサルの自己監査の報告");
        builder.AppendLine();
        builder.AppendLine($"**{Program.Notice}**");
        builder.AppendLine();
        builder.AppendLine("仕様 1.14.0 の 10 章(監査の点検項目)を、この実装の現状に照らして 1 項目ずつ分類した表です。監査人の審査の代わりにはなりません。");
        builder.AppendLine();
        builder.AppendLine("## 集計");
        builder.AppendLine();
        builder.AppendLine("| 区分 | 件数 | 意味 |");
        builder.AppendLine("|---|---:|---|");
        foreach(var status in Enum.GetValues<AuditStatus>()){
            builder.AppendLine($"| {Label(status)} | {Items.Count(item=>item.Status==status)} | {Meaning(status)} |");
        }
        builder.AppendLine();
        builder.AppendLine("## 公式化の前に直す必要がある既知の隔たり");
        builder.AppendLine();
        foreach(var item in Items.Where(static item=>item.Status==AuditStatus.Gap)){
            builder.AppendLine($"- **{item.Clause}** {item.Requirement}: {item.Note}");
        }
        builder.AppendLine();
        builder.AppendLine("## 全項目");
        builder.AppendLine();
        builder.AppendLine("| 条項 | 要件 | 区分 | 現状・証拠 |");
        builder.AppendLine("|---|---|---|---|");
        foreach(var item in Items){
            builder.AppendLine($"| {item.Clause} | {item.Requirement} | {Label(item.Status)} | {item.Note} |");
        }
        return builder.ToString();
    }
    public static string Label(AuditStatus status){
        switch(status){
            case AuditStatus.Automated:
                return "自動点検";
            case AuditStatus.Evidence:
                return "要確認";
            case AuditStatus.Gap:
                return "**未達**";
            case AuditStatus.NotPerformed:
                return "未実施";
            case AuditStatus.External:
                return "外部手続き";
            default:
                return "該当なし";
        }
    }
    private static string Meaning(AuditStatus status){
        switch(status){
            case AuditStatus.Automated:
                return "measure / acid / 試験が自動で点検する";
            case AuditStatus.Evidence:
                return "証拠は出るが、人が確かめる";
            case AuditStatus.Gap:
                return "仕様に合っていない既知の隔たり";
            case AuditStatus.NotPerformed:
                return "この自己点検では実施していない";
            case AuditStatus.External:
                return "監査人・価格・開示報告書・製品化・提出";
            default:
                return "この構成には当てはまらない";
        }
    }
}
