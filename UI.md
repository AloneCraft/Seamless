# UI 仕様書統合

# Source: Document\UI\UiStorageSpecification.md

# UI Storage Specification

Benchmark繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ縺ｫ菫晏ｭ倥☆繧句ｷｦ蜿ｳ蛻･LoopCount縲∵怙邨・繝舌ャ繝√・蜷郁ｨ亥ｮ滓ｸｬ譎る俣縲√♀繧医・蠕ｩ蜈・ｾ後・螳悟・螳御ｺ・愛螳壹・ [Benchmark Measurement Specification](BenchmarkMeasurementSpecification.md) 繧呈ｭ｣縺ｨ縺吶ｋ縲・
## 逶ｮ逧・

Benchmark騾壼ｸｸUI縲ヾcalable UI縲￣arameterSweep UI縺ｮRun螻･豁ｴ縲∝ｮ溯｡檎憾諷九∫ｵ先棡縲∵ｯ碑ｼ・ゞI驕ｸ謚樒憾諷九ｒ蜈ｱ騾售QLite DB縺ｸ髮・ｴ・☆繧九ら樟陦袈I縺ｧ縺ｯCSV縲～progress.json`縲～latest-run.json`縲～benchmark-results.json`縲～benchmark-ui-state.json`縲ヾave/Save As/Open縲∝､夜ΚCSV隱ｭ霎ｼ繧剃ｿ晏ｭ倅ｻ墓ｧ倥→縺励※菴ｿ繧上↑縺・・

譌｢蟄倥・ `Work` 驟堺ｸ九↓谿九▲縺ｦ縺・ｋ譌ｧCSV/JSON繝輔ぃ繧､繝ｫ縺ｯ螳牙・縺ｮ縺溘ａ蜑企勁縺励↑縺・ゅ◆縺縺励∫樟陦袈I縺ｯ譌ｧ繝輔ぃ繧､繝ｫ縺九ｉ縺ｮ遘ｻ陦悟・逅・ｄ莠呈鋤隱ｭ霎ｼ繧呈署萓帙＠縺ｪ縺・・

## DB驟咲ｽｮ

- 騾壼ｸｸ: `Work/UI/runs.db`
- fallback: `%LocalApplicationData%/Seamless/Work/UI/runs.db`
- 隗｣豎ｺ蜿｣: `Seamless.UI.Storage.UiRunDatabasePathResolver`

## 蟇ｾ雎｡繧｢繝励Μ

`Runs` 縺ｯ `Apps` 繧貞盾辣ｧ縺励∵ｬ｡縺ｮ繧｢繝励Μ遞ｮ蛻･繧剃ｿ晄戟縺吶ｋ縲・

- `Benchmark`
- `ScalableBenchmark`
- `ParameterSweep`

## Run繝ｩ繧､繝輔し繧､繧ｯ繝ｫ

蜈ｱ騾啌un迥ｶ諷九・谺｡縺ｮ騾壹ｊ縲・

- `Queued`
- `Running`
- `Paused`
- `Stopped`
- `Completed`
- `Failed`

迥ｶ諷矩・遘ｻ螻･豁ｴ縺ｯ `RunStates` 縺ｫ霑ｽ險倥☆繧九ＡRuns.CurrentStatus` 縺ｯ譛譁ｰ迥ｶ諷九ｒ菫晄戟縺吶ｋ縲・enchmark騾壼ｸｸUI/Scalable UI縺ｯRun髢句ｧ九・ｲ陦後∝●豁｢縲∽ｸ譎ょ●豁｢縲∝ｮ御ｺ・∝､ｱ謨励ｒDB縺ｸ菫晏ｭ倥☆繧九１arameterSweep UI繧３un螻･豁ｴ縺ｨ貂ｬ螳夂ｵ先棡繧貞酔縺魯B縺ｸ菫晏ｭ倥☆繧九・

Benchmark騾壼ｸｸUI/Scalable UI縺ｧ縺ｯ縲∬ｵｷ蜍墓凾縺ｫ縲檎樟蝨ｨ縺ｮSourceTab縺ｨ迴ｾ蝨ｨ縺ｮ螳溯｡後・繝ｭ繝輔ぃ繧､繝ｫ縲阪↓荳閾ｴ縺吶ｋ譛譁ｰRun縺縺代ｒ閾ｪ蜍募・髢句呵｣懊→縺励※遒ｺ隱阪☆繧九よ怙譁ｰRun縺・`Running` / `Queued` / `Paused` 縺ｧ縲・∈謚樊ｸ医∩BenchmarkCell縺ｫ譛ｪ螳御ｺ・ob縺梧ｮ九ｋ蝣ｴ蜷医・DB荳翫・迥ｶ諷九ｒ `Paused` 縺ｸ陬懈ｭ｣縺励ヽun荳隕ｧ縺ｧ縺昴・Run繧帝∈謚槭☆繧九ＡStopped`縲～Completed`縲～Failed` 縺ｯ譏守､ｺ逧・↑邨ゆｺ・憾諷九→縺励※謇ｱ縺・∬・蜍募ｾｩ蜈・＠縺ｪ縺・・

Run髢句ｧ区凾縺ｫ縺ｯ縲ヽunRoot縲・∈謚昿lementCount荳隕ｧ縲、daptive髢ｾ蛟､縲・∈謚櫁ｫ也炊CPU荳隕ｧ縲・∈謚曖enchmarkCell縲∵､懷・貂医∩Benchmark鬆・岼繧奪B縺ｸ菫晏ｭ倥☆繧九ょ・髢区凾縺ｮ豁｣譛ｬ縺ｯSQLite DB縺ｧ縺ゅｊ縲～ProgressState` JSON縲．B蜀・`BenchmarkProgressStateJson`縲｝rogress邉ｻJSON繝輔ぃ繧､繝ｫ縺ｯ菴ｿ繧上↑縺・・

## 繧ｹ繧ｭ繝ｼ繝樊ｭ｣譛ｬ

荳ｻ縺ｪ繝・・繝悶Ν:

- `SchemaMigrations`: schema version
- `Apps`: UI繧｢繝励Μ遞ｮ蛻･
- `Runs`: Run蜊倅ｽ阪・螻･豁ｴ
- `RunStates`: Run迥ｶ諷句ｱ･豁ｴ
- `Cases`: Benchmark/ParameterSweep縺ｮ螳溯｡悟ｯｾ雎｡
- `CaseArguments`: 螳溯｡悟ｯｾ雎｡縺ｮ蠑墓焚縺ｨ險ｭ螳壼､
- `Jobs`: Case縺ｫ蟇ｾ縺吶ｋ蛟句挨螳溯｡・
- `Measurements`: 貂ｬ螳夂匱逕滓凾蛻ｻ
- `MetricValues`: 貂ｬ螳壼､
- `Comparisons`: 豈碑ｼ・ｵ先棡繧ｭ繝｣繝・す繝･
- `UiSelections`: 繧ｻ繝ｫ縲∬｡後，PU縺ｪ縺ｩ縺ｮ驕ｸ謚樒憾諷・
- `UiViewStates`: 驕ｸ謚槭ち繝悶・∈謚朦縺ｪ縺ｩ縺ｮ霆ｽ驥剰｡ｨ遉ｺ迥ｶ諷・

陦ｨ遉ｺ縺ｨ豈碑ｼ・・豁｣譛ｬ縺ｯ `Runs`縲～Cases`縲～CaseArguments`縲～Jobs`縲～Measurements`縲～MetricValues` 縺ｨ縺吶ｋ縲ＡComparisons` 縺ｯDB蜀・・鬮倬溷ｾｩ蜈・畑繧ｭ繝｣繝・す繝･縺ｧ縺ゅｊ縲∵ｯ碑ｼ・ｯ皮紫縺ｯ `MetricValues` 縺九ｉ蜀肴ｧ狗ｯ牙庄閭ｽ縺ｧ縺ゅｋ縺薙→繧貞燕謠舌↓縺吶ｋ縲・

Benchmark騾壼ｸｸUI/Scalable UI縺ｮ菫晏ｭ伜腰菴阪・縲∬｡ｨ遉ｺ荳翫・繝壹い陦後ｒ `SourceTab + 陦後く繝ｼ + 霆ｸ(ElementCount/LoopCount)` 縺ｨ縺励※謇ｱ縺・ょｮ溯｣・ｸ翫・ `Cases` / `Jobs` 縺ｮ豁｣隕丞喧繧堤ｶｭ謖√＠縲～RowKey` 縺ｨ蟾ｦ蜿ｳ縺ｮ `Side`縲～Project`縲～TypeFullName`縲～ArgumentKey` / `Arguments` 縺ｧ蜷後§陦ｨ遉ｺ陦後・蟾ｦ蜿ｳ繧ｻ繝ｫ繧貞ｯｾ蠢應ｻ倥￠繧九ょ推Job縺ｫ縺ｯ `Status`縲～MeanNanoseconds`縲～AllocatedBytes`縲・幕蟋・邨ゆｺ・律譎ゅ∝､ｱ謨礼炊逕ｱ縲∬ｫ也炊CPU縲ヽandomSeed縲ヽesultDigest縲ヽesultCheckText 繧剃ｿ晏ｭ倥＠縲∝ｷｦ縺縺大ｮ御ｺ・∝承縺縺大ｮ御ｺ・∫援蛛ｴ螟ｱ謨励ｂ蜷後§陦ｨ遉ｺ陦後く繝ｼ縺ｸ驛ｨ蛻・峩譁ｰ縺吶ｋ縲・

Schema version 荳堺ｸ閾ｴ譎ゅ・譌ｧRun莠呈鋤繧定｡後ｏ縺壹～runs.db`縲～runs.db-wal`縲～runs.db-shm` 繧貞炎髯､縺励※迴ｾ陦茎chema縺ｧ蜀堺ｽ懈・縺吶ｋ縲よ立螻･豁ｴ莠呈鋤縺ｯ謖√◆縺ｪ縺・・

## 陦悟腰菴垢PU蝗ｺ螳・

Adaptive螳溯｡後〒縺ｯ縲ゞI縺ｮ陦後げ繝ｫ繝ｼ繝斐Φ繧ｰ縺ｨ蜷後§繧ｭ繝ｼ縺ｧ螳溯｡瑚｡後ｒ菴懊ｊ縲・縺､縺ｮ陦ｨ遉ｺ陦後↓1縺､縺ｮ隲也炊CPU繧貞牡繧雁ｽ薙※繧九ょ酔縺倩｡後・蟾ｦ繝励Ο繧ｸ繧ｧ繧ｯ繝・蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝医・蜷後§隲也炊CPU縺ｧ鬆・分縺ｫ螳溯｡後☆繧九ょｮ溯｡碁・・縲悟ｷｦ繝励Ο繧ｸ繧ｧ繧ｯ繝亥・霆ｸ 竊・蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝亥・霆ｸ縲阪→縺励∫援蛛ｴ縺悟､ｱ謨励＠縺ｦ繧ょ渚蟇ｾ蛛ｴ縺ｯ蜷後§CPU縺ｧ邯夊｡後☆繧九ら援蛛ｴ縺縺鷹∈謚槭＆繧後※縺・ｋ陦後ｂ陦悟腰菴垢PU繧呈戟縺､縲・

Adaptive髢ｾ蛟､雜・℃縺ｫ繧医ｋ謇薙■蛻・ｊ縺ｯ縲∵悴險域ｸｬ縺ｧ縺ｯ縺ｪ縺丞ｮ御ｺ・桶縺・・繧ｹ繧ｭ繝・・陦ｨ遉ｺ縺ｨ縺励※謇ｱ縺・ょｷｦ蜿ｳ縺ｮ謇薙■蛻・ｊ蛻､螳壹・迢ｬ遶九・縺ｾ縺ｾ邯ｭ謖√☆繧九・

UI荳翫・CPU陦ｨ遉ｺ縺ｯ `蝙義縲～繝吶Φ繝√・繝ｼ繧ｯ` 縺ｮ逶ｴ蠕後↓縺ゅｋ `CPU` 蛻励∈髮・ｴ・☆繧九ょ推繝励Ο繧ｸ繧ｧ繧ｯ繝亥・蛛ｴ縺ｫ縺ｯ繝√ぉ繝・け縲∫憾諷九∵律譎ゅｒ谿九＠縲，PU繧｢繧､繧ｳ繝ｳ縺ｨCPU逡ｪ蜿ｷ縺ｯ陦ｨ遉ｺ縺励↑縺・・PU繧ｻ繝ｫ縺ｯ繧ｳ繧｢遞ｮ蛻･繧｢繧､繧ｳ繝ｳ縺ｨ隲也炊CPU逡ｪ蜿ｷ繧定｡ｨ遉ｺ縺励∵悴髢句ｧ玖｡後・遨ｺ谺・↓縺吶ｋ縲５ooltip縺ｯ `P core / CPU 8` 縺ｮ繧医≧縺ｪ隧ｳ邏ｰ蜷阪↓縺吶ｋ縲・PU蛻励・繧ｽ繝ｼ繝亥ｯｾ雎｡縺ｧ縲∫ｩｺ谺・・蟶ｸ縺ｫ譛ｫ蟆ｾ縺ｫ荳ｦ縺ｹ繧九ゅけ繝ｪ繝・け譎ゅ・陦碁∈謚槭→縺励※謇ｱ縺・３un荳隕ｧ繧・ち繝夜ｲ謐励・ `螳御ｺ・驕ｸ謚杼 繧ｫ繧ｦ繝ｳ繝医・蠕捺擂騾壹ｊ蟾ｦ蜿ｳ繧ｻ繝ｫ蜊倅ｽ阪〒謨ｰ縺医ｋ縲・

## 蠖ｹ蜑ｲ蛻・球

- `Shared/UI.Storage`: SQLite縺ｮ菴弱Ξ繝吶Ν菫晏ｭ・隱ｭ霎ｼ蝓ｺ逶､縲・B繝代せ隗｣豎ｺ縲《chema菴懈・縲ヽun/Case/Job/Metric/Comparison/UI迥ｶ諷九・CRUD繧呈球蠖薙☆繧九・enchmark蝗ｺ譛峨・陦ｨ遉ｺ繝｢繝・Ν繧ФI蛻､譁ｭ縺ｯ謖√◆縺ｪ縺・・
- `UiRunStore`: `Shared/UI.Storage` 縺ｮ荳ｭ蠢アPI縲よｸｬ螳壼､菫晏ｭ倥・蜷御ｸJob縺ｮ譌｢蟄弄easurement繧堤ｽｮ謠帙＠縲・㍾隍・｡ｨ遉ｺ繧帝∩縺代ｋ縲・
- `BenchmarkUiRunRepository`: Benchmark騾壼ｸｸUI/Scalable UI蝗ｺ譛峨・DB螟画鋤螻､縲ＡUiRunStore` 縺ｨ `BenchmarkResultItem` / `BenchmarkRunSession` 縺ｮ螟画鋤縲ヾQLite譖ｸ縺崎ｾｼ縺ｿ繧ｭ繝･繝ｼ縲ヽun蠕ｩ蜈・∝燕蝗樒ｵ先棡讀懃ｴ｢繧呈球蠖薙☆繧九・
- `BenchmarkRunSession`: Benchmark UI蜀・・蜚ｯ荳縺ｮ繝ｩ繧､繝也ｵ先棡豁｣譛ｬ縲ゅく繝ｼ縺ｯ `SourceTab + Project + TypeFullName + Method + ArgumentKey + ElementCount`縲・
- `BenchmarkRunProjectionService`: `BenchmarkRunSession` 縺ｾ縺溘・DB蠕ｩ蜈・ｵ先棡縺九ｉ `ResultItems`縲～VisibleResultItems`縲∵ｯ碑ｼ・｡後∬｡ｨ繧ｻ繝ｫ驕ｩ逕ｨ逕ｨ繝・・繧ｿ繧呈ｴｾ逕溘☆繧九・
- `BenchmarkRunnerViewModel`: 繧ｳ繝槭Φ繝峨∫判髱｢驕ｸ謚樒憾諷九・夂衍縲√し繝ｼ繝薙せ蜻ｼ縺ｳ蜃ｺ縺励ｒ諡・ｽ薙☆繧九ＡResultItems` / `VisibleResultItems` 縺ｯBinding逕ｨ蜃ｺ蜉帙ヰ繝・ヵ繧｡縺ｧ縺ゅｊ縲∵ｭ｣譛ｬ縺ｧ縺ｯ縺ｪ縺・・

## UI蠕ｩ蜈・Ν繝ｼ繝ｫ

- Run荳隕ｧ縺ｯ `UiRunStore.ListRuns(Benchmark/ScalableBenchmark/ParameterSweep)` 縺九ｉ讒狗ｯ峨☆繧九・
- Benchmark騾壼ｸｸUI/Scalable UI縺ｮ邨先棡陦ｨ遉ｺ縲∝ｱ･豁ｴ蠕ｩ蜈・∵ｯ碑ｼ・｡後∝・髢狗畑縺ｮProgressState縺ｯDB縺九ｉ蠕ｩ蜈・☆繧九・
- 繧ｿ繝門・譖ｿ譎ゅ・DB隱ｭ霎ｼ繧医ｊ繝｡繝｢繝ｪ荳翫・ `BenchmarkRunSession` 繧貞━蜈医＠縲∫峩霑代Λ繧､繝也ｵ先棡繧貞､ｱ繧上↑縺・・
- 險域ｸｬ貂医∩繧ｻ繝ｫ縺ｯ `Completed` 陦ｨ遉ｺ繧貞━蜈医＠縲√ち繝也ｧｻ蜍輔□縺代〒 `Waiting` 陦ｨ遉ｺ縺ｸ謌ｻ縺輔↑縺・・
- 驕ｸ謚樒憾諷九く繝ｼ縺ｯSourceTab繧貞性繧√∝挨繧ｿ繝悶・蜷悟錐陦・蜷悟錐Project縺ｸ貍上ｌ縺ｪ縺・ｈ縺・↓縺吶ｋ縲・
- 邨ゆｺ・｢ｺ隱阪・譌ｧSave蟆守ｷ壹ｒ蜃ｺ縺輔★縲√檎ｴ譽・＠縺ｦ髢峨§繧九阪→縲後く繝｣繝ｳ繧ｻ繝ｫ縲阪ｒ荳ｭ蠢・↓謇ｱ縺・・

## 譌ｧ菫晏ｭ伜ｽ｢蠑上・謇ｱ縺・

譌ｧRun莠呈鋤縺ｯ荳崎ｦ√→縺吶ｋ縲・SV縲～progress.json`縲∵立UI迥ｶ諷九ヵ繧｡繧､繝ｫ縲￣arameterSweep縺ｮ `results.csv` 縺ｯ迴ｾ陦御ｿ晏ｭ倅ｻ墓ｧ倥・蜈･蜉帙↓繧ょ・蜉帙↓繧ゆｽｿ繧上↑縺・よ立繝輔ぃ繧､繝ｫ縺後Ο繝ｼ繧ｫ繝ｫ縺ｫ蟄伜惠縺励※繧ょ炎髯､縺帙★縲．B豁｣譛ｬ縺ｮRun螻･豁ｴ縺ｨ縺ｯ蛻･迚ｩ縺ｨ縺励※謇ｱ縺・１arameterSweep縺ｯ譌ｧ螻･豁ｴ莠呈鋤繧呈戟縺溘↑縺・′縲’resh DB縺ｧRun菴懈・縲∫ｵ先棡菫晏ｭ倥∝ｱ･豁ｴ隱ｭ霎ｼ縲｜aseline豈碑ｼ・ｾｩ蜈・′縺ｧ縺阪ｋ迥ｶ諷九ｒ邯ｭ謖√☆繧九・
# Source: Document\UI\RebuildImplementationStatus.md

# UI Rebuild Implementation Status

## 螳御ｺ・

- 蜈ｱ騾售QLite菫晏ｭ榔roject `Shared/UI.Storage` 繧定ｿｽ蜉縺励。enchmark騾壼ｸｸUI縲ヾcalable UI縲￣arameterSweep UI縲ゝest project縺九ｉ蜿ら・縺励◆縲・
- DB schema繧・`Run`縲～Case`縲～Job`縲～Measurement`縲～Metric`縲～Comparison`縲～UiSelection`縲～UiViewState` 縺ｸ豁｣隕丞喧縺励◆縲・
- Benchmark騾壼ｸｸUI/Scalable UI縺ｮRun荳隕ｧ縲∫ｵ先棡蠕ｩ蜈・∵ｯ碑ｼ・｡後∝・髢狗畑snapshot繧奪B豁｣譛ｬ縺ｸ蛻・ｊ譖ｿ縺医◆縲・
- Benchmark騾壼ｸｸUI/Scalable UI縺ｮToolbar縺九ｉ `Save`縲～Save As`縲～Open`縲∝､夜ΚCSV隱ｭ霎ｼ蟆守ｷ壹ｒ蜑企勁縺励◆縲・
- `BenchmarkCsvDiscoveryService`縲～BenchmarkRunPersistenceService`縲～BenchmarkResultWriter`縲～CsvStringBuilderExtensions`縲～IBenchmarkCsvService`縲～BenchmarkCsvService`縲∵立UI interaction菫晏ｭ倡ｵ瑚ｷｯ繧貞炎髯､縺励◆縲・
- Core縺ｮ `BenchmarkOrchestrator` 縺ｯCSV謌先棡迚ｩ螂醍ｴ・ｒ謖√◆縺壹（n-memory `ProgressState` 縺ｨprogress update繧定ｿ斐☆險ｭ險医∈螟画峩縺励◆縲・
- ParameterSweep UI縺ｮ譌ｧCSV菫晏ｭ倥ｒ蜑企勁縺励ヽun螻･豁ｴ縲∫ｵ先棡菫晏ｭ倥｜aseline豈碑ｼ・ｾｩ蜈・ｒDB菫晏ｭ倥∈邨ｱ荳縺励◆縲・
- Benchmark髢句ｧ区凾縺ｮUI繝輔Μ繝ｼ繧ｺ繧帝∩縺代ｋ縺溘ａ縲ヾQLite譖ｸ縺崎ｾｼ縺ｿ繧坦epository蛛ｴ縺ｮ逶ｴ蛻励ヰ繝・け繧ｰ繝ｩ繧ｦ繝ｳ繝峨く繝･繝ｼ縺ｸ髢峨§霎ｼ繧√◆縲・
- 繧ｿ繝門ｾｩ蟶ｰ譎ゅ・邨先棡豸亥､ｱ縺ｨ蠕・ｩ溯｡ｨ遉ｺ豺ｷ蜈･繧剃ｿｮ豁｣縺励√Λ繧､繝也ｵ先棡縺ｯ `BenchmarkRunSession` 縺九ｉ蠕ｩ蜈・☆繧九ｈ縺・↓縺励◆縲・
- 邨ゆｺ・｢ｺ隱阪〒縲後く繝｣繝ｳ繧ｻ繝ｫ縲阪□縺代′蜃ｺ繧狗憾諷九ｒ菫ｮ豁｣縺励∵悴菫晏ｭ倡ｵ先棡縺後≠繧句ｴ蜷医ｂ髢峨§繧矩∈謚櫁い繧定｡ｨ遉ｺ縺吶ｋ繧医≧縺ｫ縺励◆縲・
- `BenchmarkRunSession`縲～BenchmarkRunProjectionService`縲～BenchmarkUiRunRepository` 繧辰ore蛛ｴ縺ｮ繝・せ繝亥庄閭ｽ縺ｪ蠅・阜縺ｸ謨ｴ逅・＠縺溘・
- `MemoryOnlyDevice` 縺ｨ `TsavoriteCheckpointCapacity` 縺ｮ譌｢蟄倥ユ繧ｹ繝亥､ｱ謨励ｒ縲∝・菴薙ユ繧ｹ繝医げ繝ｪ繝ｼ繝ｳ蛹悶・莉倬囂菫ｮ豁｣縺ｨ縺励※蜿肴丐縺励◆縲・
- Adaptive螳溯｡後・CPU蜑ｲ蠖薙ｒ陦ｨ遉ｺ陦悟腰菴阪∈螟画峩縺励∝酔縺倩｡後・蟾ｦ繝励Ο繧ｸ繧ｧ繧ｯ繝・蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝医′蜷後§隲也炊CPU縺ｧ鬆・分縺ｫ襍ｰ繧九ｈ縺・↓縺励◆縲・
- Benchmark陦ｨ縺ｮCPU陦ｨ遉ｺ繧偵・繝ｭ繧ｸ繧ｧ繧ｯ繝亥・縺九ｉ螟悶＠縲～蝙義縲～繝吶Φ繝√・繝ｼ繧ｯ` 縺ｮ蜿ｳ縺ｫ陦悟腰菴阪・ `CPU` 蛻励ｒ霑ｽ蜉縺励◆縲・PU蛻励・繧ｳ繧｢遞ｮ蛻･繧｢繧､繧ｳ繝ｳ縲∬ｫ也炊CPU逡ｪ蜿ｷ縲ゝooltip縲√た繝ｼ繝医∬｡碁∈謚槭け繝ｪ繝・け繧呈戟縺､縲・
- Run髢句ｧ区凾縺ｫRunRoot縲・∈謚昿lementCount縲、daptive髢ｾ蛟､縲・∈謚曚PU縲・∈謚曖enchmarkCell縲∵､懷・貂医∩Benchmark鬆・岼繧奪B縺ｸ菫晏ｭ倥☆繧九ｈ縺・↓縺励◆縲・
- `BenchmarkProgressStateJson` / progress邉ｻJSON繧貞・髢区ｭ｣譛ｬ縺ｨ縺励※菴ｿ縺・ｵ瑚ｷｯ繧貞炎髯､縺励．B縺ｮRun險ｭ螳壹，ases縲゛obs縲｀etrics縲ゞiSelections縺九ｉ蜀埼幕逕ｨ `ProgressState` 繧貞・讒狗ｯ峨☆繧九ｈ縺・↓縺励◆縲・
- 襍ｷ蜍墓凾Run荳隕ｧ譖ｴ譁ｰ縺ｧ縲∫樟蝨ｨ縺ｮSourceTab/螳溯｡後・繝ｭ繝輔ぃ繧､繝ｫ縺ｫ荳閾ｴ縺吶ｋ譛譁ｰ縺ｮ `Running` / `Queued` / `Paused` Run縺ｫ譛ｪ螳御ｺ・∈謚槭そ繝ｫ縺梧ｮ九ｋ蝣ｴ蜷医∬・蜍輔〒 `Paused` 縺ｨ縺励※驕ｸ謚槭☆繧九ｈ縺・↓縺励◆縲ＡStopped`縲～Completed`縲～Failed` 縺ｯ閾ｪ蜍募ｾｩ蜈・ｯｾ雎｡螟悶・
- UI DB schema version繧剃ｸ翫￡縲∵立 `runs.db*` 莠呈鋤繧堤ｴ譽・＠縺ｦ迴ｾ陦茎chema縺ｧ蜀堺ｽ懈・縺吶ｋ譁ｹ驥昴∈譖ｴ譁ｰ縺励◆縲・

## 讀懆ｨｼ貂医∩

- `dotnet build Shared\UI.Storage\UI.Storage.csproj -v:minimal`
- `dotnet build Benchmark\UI\Benchmark.UI.csproj -v:minimal`
- `dotnet build Benchmark\Scalable.UI\Scalable.UI.csproj -v:minimal`
- `dotnet build Benchmark\Tsavorite.ParameterSweep.UI\Tsavorite.ParameterSweep.UI.csproj -v:minimal`
- `dotnet test Test\Test.csproj -v:minimal`
- `dotnet test Test\Test.csproj --filter "FullyQualifiedName~BenchmarkOrchestrator" -v:minimal`
- `dotnet test Test.Tsavorite.ParameterSweep\Test.Tsavorite.ParameterSweep.csproj -v:minimal`
- 3 UI EXE縺ｮ遏ｭ譎る俣襍ｷ蜍輔せ繝｢繝ｼ繧ｯ

## 霑ｽ蜉貂医∩蝗槫ｸｰ繝・せ繝・

- `BenchmarkRunSession`: 蜷御ｸ繧ｭ繝ｼUpsert鄂ｮ謠帙ヾourceTab蛻･蜿門ｾ励・lementCount蠕ｩ蜈・￣rogressState逕滓・縲・
- `BenchmarkRunProjectionService`: 驕ｸ謚朦謚募ｽｱ縲∝ｮ御ｺ・ｵ先棡縺縺代・豈碑ｼ・｡檎函謌舌∵怙譁ｰ邨先棡蠕ｩ蜈・・
- `BenchmarkUiRunRepository`: temp SQLite DB縺ｧRun蠕ｩ蜈・｀easurement鄂ｮ謠帙ヾcalable縺ｮ `TypeFullName` / `ArgumentKey` 菫晄戟縲・
- `BenchmarkOrchestrator`: 陦ｨ遉ｺ陦悟腰菴阪〒蟾ｦ蜿ｳ縺悟酔縺呂PU縺ｫ縺ｪ繧翫∬｡後＃縺ｨ縺ｫCPU縺悟・謨｣縺輔ｌ縲∬｡悟・鬆・ｺ上′蟾ｦ蜈ｨ霆ｸ竊貞承蜈ｨ霆ｸ縺ｫ縺ｪ繧九％縺ｨ縲・
- `UiRunStore` / Run蠕ｩ蜈・ fresh schema菴懈・縲￣rogressState JSON繧剃ｿ晏ｭ倥＠縺ｪ縺・％縺ｨ縲．B縺九ｉRun險ｭ螳壹→邨先棡繧貞ｾｩ蜈・☆繧九％縺ｨ縲・
- Benchmark UI陦ｨ遉ｺ: 陦靴PU陦ｨ遉ｺ繧呈戟縺､縺薙→縲√・繝ｭ繧ｸ繧ｧ繧ｯ繝亥・蛛ｴ縺ｮCPU陦ｨ遉ｺ縺ｫ萓晏ｭ倥＠縺ｪ縺・％縺ｨ縲・
- Close logic: 譛ｪ菫晏ｭ倡憾諷九〒髢峨§繧矩∈謚櫁い縺ｨ繧ｭ繝｣繝ｳ繧ｻ繝ｫ縺悟・繧九％縺ｨ縲・

## 谿倶ｻｶ

- 蟆・擂縺ｮUI閾ｪ蜍墓桃菴懊ユ繧ｹ繝亥ｼｷ蛹悶ょ・菴鍋噪縺ｫ縺ｯ縲。enchmark髢句ｧ九√ち繝門・譖ｿ縲∝ｱ･豁ｴ蠕ｩ蜈・，PU蛻励た繝ｼ繝医∵ｯ碑ｼ・｡ｨ遉ｺ縲∫ｵゆｺ・｢ｺ隱阪ｒ螳溽判髱｢謫堺ｽ懊〒讀懆ｨｼ縺吶ｋ縲・
- DB迚ｩ逅・ｨｭ險医・譌｢蟄倥・豁｣隕丞喧繧堤ｶｭ謖√＠縲～RowKey` 縺ｨ蟾ｦ蜿ｳ `Side` 縺ｧ繝壹い陦後ｒ讒区・縺励※縺・ｋ縲ょｮ悟・縺ｪ縲悟ｷｦ蜿ｳ蛻励ｒ1迚ｩ逅・Ξ繧ｳ繝ｼ繝峨↓謖√▽縲榊ｽ｢縺悟ｿ・ｦ√↓縺ｪ縺｣縺溷ｴ蜷医・縲∬ｿｽ蜉繝槭う繧ｰ繝ｬ繝ｼ繧ｷ繝ｧ繝ｳ縺ｧ縺ｯ縺ｪ縺叔resh schema蜑肴署縺ｧ蜀崎ｨｭ險医☆繧九・

## 蟾ｮ蛻・紛逅・Γ繝｢

- `Artifacts/UiSmoke` 縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ縺ｯ繝ｭ繝ｼ繧ｫ繝ｫ讀懆ｨｼ險ｼ霍｡縺ｧ縺ゅｊ縲∵怙邨ょｷｮ蛻・↓縺ｯ蜷ｫ繧√↑縺・・
- 譌｢蟄・`Work` 驟堺ｸ九・譌ｧCSV/JSON縺ｯ蜑企勁縺励↑縺・・
- 譌ｧschema縺ｮ `Work/UI/runs.db*` 縺ｯ莠呈鋤蟇ｾ雎｡螟悶〒縺ゅｊ縲∫樟陦茎chema縺ｸ蛻・ｊ譖ｿ縺医ｋ繧ｿ繧､繝溘Φ繧ｰ縺ｧ蜑企勁/蜀堺ｽ懈・縺吶ｋ縲・
- `csv` 譁・ｭ怜・縺ｮ谿句ｭ倥・縲靴SV縺檎函謌舌＆繧後↑縺・％縺ｨ縲阪ｒ讀懆ｨｼ縺吶ｋ繝・せ繝医√∪縺溘・Reverse驟堺ｸ九・豁ｴ蜿ｲ逧・ｨｼ霍｡縺縺題ｨｱ螳ｹ縺吶ｋ縲・
# Source: Document\UI\BenchmarkMeasurementSpecification.md

# Benchmark Measurement Specification

## 逶ｮ逧・
騾壼ｸｸBenchmark UI縺ｮLoopCount邉ｻ繧ｿ繝悶↓縺翫＞縺ｦ縲：Linq縺ｨZLinq繧貞・蟷ｳ縺ｪ譚｡莉ｶ縺ｧ貂ｬ螳壹＠縲∵欠螳壽ｸｬ螳壽凾髢薙∈蛻ｰ驕斐☆繧句燕縺ｫ縲悟ｮ悟・螳御ｺ・阪→陦ｨ遉ｺ縺輔ｌ繧九％縺ｨ繧帝亟豁｢縺吶ｋ縲・
譛ｬ莉墓ｧ倥・ `BenchmarkWorkloadAxis.UsesLoopCount` 縺檎悄縺ｨ縺ｪ繧区ｯ碑ｼ・｡後↓驕ｩ逕ｨ縺吶ｋ縲４calable邉ｻ繧ｿ繝悶・隕∫ｴ謨ｰ謇薙■蛻・ｊ隕丞援縺ｯ螟画峩縺励↑縺・・
## 逕ｨ隱・
- 貂ｬ螳壽凾髢・ Worker縺瑚ｿ斐☆譛邨・繝舌ャ繝∝・菴薙・蜷郁ｨ亥ｮ滓ｸｬ譎る俣縲ょ腰菴阪・ns縺ｧ菫晏ｭ倥☆繧九・- 陦ｨ遉ｺ譎る俣: 蜷郁ｨ亥ｮ滓ｸｬ譎る俣繧鱈oopCount縺ｧ髯､縺励◆1蝗槭≠縺溘ｊ縺ｮ譎る俣縲り｡ｨ縺ｧ縺ｯns縲「s縲［s遲峨↓謨ｴ蠖｢縺励※陦ｨ遉ｺ縺吶ｋ縲・- 謖・ｮ壽ｸｬ螳壽凾髢・ UI縺ｮ縲梧ｸｬ螳壽凾髢薙阪〒驕ｸ謚槭＆繧後◆蛟､縲・024 ms繧帝∈謚槭＠縺溷ｴ蜷医・ `1,024,000,000 ns` 縺ｨ縺吶ｋ縲・- 螳悟・螳御ｺ・ 蜷後§豈碑ｼ・｡後・FLinq縺ｨZLinq縺後∝酔荳LoopCount縺ｮ譛邨ゅヰ繝・メ縺ｧ縲√→繧ゅ↓謖・ｮ壽ｸｬ螳壽凾髢謎ｻ･荳翫→縺ｪ縺｣縺溽憾諷九・
陦ｨ遉ｺ譎る俣縺ｨ蛻､螳夂畑縺ｮ蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺ｯ蛻･縺ｮ蛟､縺ｧ縺ゅｋ縲り｡ｨ遉ｺ縺梧焚蜊］s縺ｧ縺ゅ▲縺ｦ繧ゅ～陦ｨ遉ｺ譎る俣 ﾃ・LoopCount` 縺ｫ逶ｸ蠖薙☆繧倶ｿ晏ｭ俶ｸ医∩蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺ｧ蛻ｰ驕泌愛螳壹☆繧九・
## 貂ｬ螳夐・ｺ・
1. 螳溯｡悟腰菴阪ｒ縲梧ｯ碑ｼ・｡・竊・LoopCount 竊・蟾ｦ蜿ｳ繧ｱ繝ｼ繧ｹ縲阪→縺吶ｋ縲・2. 蜷後§豈碑ｼ・｡後・FLinq縺ｨZLinq縺ｯ縲∝推谿ｵ髫弱〒蟶ｸ縺ｫ蜷後§LoopCount繧剃ｽｿ逕ｨ縺吶ｋ縲・3. 蟾ｦ蜿ｳ荳｡譁ｹ縺ｮ謌仙粥邨先棡縺梧純縺｣縺ｦ縺九ｉ縲∝推蜷郁ｨ亥ｮ滓ｸｬ譎る俣繧呈欠螳壽ｸｬ螳壽凾髢薙→豈碑ｼ・☆繧九・4. 迚・・縺ｧ繧よ欠螳壽ｸｬ螳壽凾髢捺悴貅縺ｪ繧鵜oopCount繧・蛟阪↓縺励∵ｬ｡縺ｮLoopCount縺ｧFLinq縺ｨZLinq縺ｮ荳｡譁ｹ繧貞・貂ｬ螳壹☆繧九・5. 蜷御ｸLoopCount縺ｧ蟾ｦ蜿ｳ縺ｨ繧よ欠螳壽ｸｬ螳壽凾髢謎ｻ･荳翫↓縺ｪ縺｣縺溷ｴ蜷医□縺代√◎縺ｮ豈碑ｼ・｡後ｒ螳悟・螳御ｺ・↓縺吶ｋ縲・6. `int.MaxValue` 縺ｮLoopCount縺ｧ繧ら援蛛ｴ縺梧欠螳壽ｸｬ螳壽凾髢薙∈螻翫°縺ｪ縺・ｴ蜷医・縲∫炊逕ｱ繧定ｨ倬鹸縺励※螟ｱ謨励→縺吶ｋ縲・
迚・・縺悟・縺ｫ髢ｾ蛟､縺ｸ蛻ｰ驕斐＠縺ｦ繧ゅ√◎縺ｮ蛛ｴ縺縺代ｒ莉･蜑阪・LoopCount縺ｮ邨先棡縺ｧ遒ｺ螳壹＠縺ｦ縺ｯ縺ｪ繧峨↑縺・よｬ｡谿ｵ髫弱〒縺ｯ蛻ｰ驕疲ｸ医∩蛛ｴ繧貞性繧√※蟾ｦ蜿ｳ縺ｨ繧ょ・貂ｬ螳壹☆繧九◆繧√∝ｮ悟・螳御ｺ・凾縺ｮ譛邨・oopCount縺ｯ蠢・★荳閾ｴ縺吶ｋ縲・
## 迥ｶ諷狗ｮ｡逅・→蜀埼幕

- 蜍慕噪LoopCount縺ｯ縲ヾourceTab縲ゝype縲｀ethod縲、rgumentKey縲∵ｯ碑ｼ・・繧｢縺ｧ隴伜挨縺輔ｌ繧区ｯ碑ｼ・｡悟腰菴阪〒邂｡逅・☆繧九・- 縺ゅｋ豈碑ｼ・｡後〒蠅怜刈縺励◆LoopCount繧偵∝挨繝｡繧ｽ繝・ラ縲∝挨蠑墓焚縲∝挨豈碑ｼ・｡後・髢句ｧ句､縺ｨ縺励※蜈ｱ譛峨＠縺ｪ縺・・- `ProgressState.ElementCounts` 縺ｫ縺ｯ繝ｦ繝ｼ繧ｶ繝ｼ縺碁∈謚槭＠縺溷・譛溯ｻｸ縺縺代ｒ菫晄戟縺励∝ｮ溯｡御ｸｭ縺ｫ逕滓・縺励◆蜍慕噪LoopCount繧定ｿｽ蜉縺励↑縺・・- 荳ｭ譁ｭ繝ｻ蜀埼幕譎ゅ・縲∬｡悟崋譛峨・螻･豁ｴ縺九ｉ蟾ｦ蜿ｳ縺ｨ繧よ・蜉溘＠縺ｦ縺・ｋ譛螟ｧ縺ｮ蜈ｱ騾哭oopCount繧堤｢ｺ隱阪＠縲√◎縺ｮ谿ｵ髫弱・蛻､螳壼ｾ後∵悴蛻ｰ驕斐↑繧画ｬ｡縺ｮLoopCount縺ｸ騾ｲ繧縲・- 迚・・縺縺台ｿ晏ｭ倥＆繧後◆谿ｵ髫弱・螳悟・螳御ｺ・〒縺ｯ縺ｪ縺・ょ・髢句ｾ後↓蜷御ｸLoopCount縺ｮ荳崎ｶｳ蛛ｴ繧呈ｸｬ螳壹＠縲∝ｷｦ蜿ｳ縺梧純縺｣縺ｦ縺九ｉ蛻､螳壹☆繧九・- 隍・焚CPU縺ｧ螳溯｡後＠縺ｦ繧ゅ∝虚逧ЛoopCount縺ｯ陦後く繝ｼ縺ｫ髢峨§縲∬｡碁俣縺ｧ蜈ｱ譛峨＠縺ｪ縺・・
## 螳悟・螳御ｺ・・蠢・ｦ∵擅莉ｶ

LoopCount邉ｻ縺ｮ豈碑ｼ・｡後・縲∵ｬ｡繧偵☆縺ｹ縺ｦ貅縺溘☆蝣ｴ蜷医□縺大ｮ悟・螳御ｺ・→縺吶ｋ縲・
- 陦後′貂ｬ螳壼ｯｾ雎｡縺ｨ縺励※驕ｸ謚槭＆繧後※縺・ｋ縲・- FLinq縺ｨZLinq縺ｮ荳｡譁ｹ縺梧・蜉溘＠縺ｦ縺・ｋ縲・- 蟾ｦ蜿ｳ縺ｮLoopCount縺悟酔荳縺ｧ縺ゅｋ縲・- 蟾ｦ蜿ｳ縺昴ｌ縺槭ｌ縺ｮ蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺御ｿ晏ｭ倥＆繧後※縺・ｋ縲・- 蟾ｦ蜿ｳ縺昴ｌ縺槭ｌ縺ｮ蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺梧欠螳壽ｸｬ螳壽凾髢謎ｻ･荳翫〒縺ゅｋ縲・
谺｡縺ｮ迥ｶ諷九°繧牙ｮ悟・螳御ｺ・∈陬懷ｮ後∪縺溘・譏・ｼ縺励※縺ｯ縺ｪ繧峨↑縺・・
- 貂ｬ螳壼､縺・莉ｶ莉･荳雁ｭ伜惠縺吶ｋ縺縺代・迥ｶ諷・- 騾壼ｸｸ騾ｲ謐励′螳御ｺ・＠縺溘□縺代・迥ｶ諷・- 迚・・縺縺代′謖・ｮ壽ｸｬ螳壽凾髢薙∈蛻ｰ驕斐＠縺溽憾諷・- 迴ｾ蝨ｨ菫晏ｭ倥＆繧後※縺・ｋ譛螟ｧ霆ｸ繧偵∬ｦ∵ｱゅ＆繧後◆譛邨りｻｸ縺ｨ縺ｿ縺ｪ縺励◆迥ｶ諷・- 螟ｱ謨励∽ｾ句､悶√ち繧､繝繧｢繧ｦ繝・- LoopCount縺ｾ縺溘・蜷郁ｨ亥ｮ滓ｸｬ譎る俣繧貞ｾｩ蜈・〒縺阪↑縺・立蠖｢蠑上せ繝翫ャ繝励す繝ｧ繝・ヨ

## 菫晏ｭ倥→蠕ｩ蜈・
- 繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ縺ｯ蟾ｦ蜿ｳ縺昴ｌ縺槭ｌ縺ｮ `LoopCount` 縺ｨ `TotalNanoseconds` 繧剃ｿ晏ｭ倥☆繧九・- 螳悟・螳御ｺ・｡後ｒ菫晏ｭ倥☆繧九→縺阪・縲∝ｷｦ蜿ｳ縺梧・蜉溘＠縲´oopCount縺御ｸ閾ｴ縺励∽ｸ｡譁ｹ縺ｮ蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺梧欠螳壽ｸｬ螳壽凾髢謎ｻ･荳翫〒縺ゅｋ縺薙→繧呈､懆ｨｼ縺吶ｋ縲ゆｸ肴紛蜷域凾縺ｯ菫晏ｭ伜・逅・ｒ螟ｱ謨励＆縺帙ｋ縲・- 蠕ｩ蜈・ｾ後・螳悟・螳御ｺ・憾諷九・縲∽ｿ晏ｭ倥＆繧後◆LoopCount縲∝粋險亥ｮ滓ｸｬ譎る俣縲√せ繝・・繧ｿ繧ｹ縺九ｉ蜀崎ｨ育ｮ励☆繧九・- 譌ｧ蠖｢蠑上〒LoopCount縺ｾ縺溘・蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺後↑縺・ｵ先棡縺ｯ貂ｬ螳壼､縺ｨ縺励※陦ｨ遉ｺ縺ｧ縺阪ｋ縺後∝ｮ悟・螳御ｺ・↓縺ｯ縺励↑縺・・- Schema version 15縺ｧ縺ｯ `BenchmarkSnapshotMeasurement` 縺ｫ蟾ｦ蜿ｳ蛻･縺ｮLoopCount縺ｨ蜷郁ｨ亥ｮ滓ｸｬ譎る俣繧剃ｿ晏ｭ倥☆繧九・
## 蝗槫ｸｰ螻･豁ｴ

### 2026-07-10: `Any` 縺・024 ms譛ｪ蛻ｰ驕斐〒螳悟・螳御ｺ・＠縺滉ｸ榊・蜷・
System.Array縺ｮ `Any` 縺ｯLoopCount 524,288縺ｧ縲：Linq縺ｮ蜷郁ｨ亥ｮ滓ｸｬ譎る俣縺・4.8 ms縲〇Linq縺・3.2 ms縺縺｣縺溘ゆｸ｡譁ｹ縺ｨ繧よ欠螳壹＆繧後◆1024 ms縺ｸ螻翫＞縺ｦ縺・↑縺・↓繧ゅ°縺九ｏ繧峨★縲∵ｸｬ螳壼､縺ｮ蟄伜惠縺ｨ騾壼ｸｸ騾ｲ謐怜ｮ御ｺ・ｒ螳悟・螳御ｺ・∈譏・ｼ縺吶ｋUI陬懷ｮ後↓繧医ｊ縲∬｡後→繧ｿ繝悶′螳悟・螳御ｺ・↓縺ｪ縺｣縺溘・
蜷梧凾縺ｫ縲∝虚逧ЛoopCount繧・`ProgressState.ElementCounts` 縺ｸ霑ｽ蜉縺励※蜈ｨ陦後〒蜈ｱ譛峨＠縺ｦ縺・◆縺溘ａ縲∝・陦瑚｡後′霑ｽ蜉縺励◆524,288繧貞ｾ檎ｶ壹・ `Any` 縺悟・譛滓怙螟ｧ霆ｸ縺ｨ縺励※蜿励￠蜿悶▲縺溘ょ虚逧・榊｢励ｒ髢句ｧ九☆繧区擅莉ｶ縺ｯ蜈・・蛻晄悄譛螟ｧ霆ｸ縺ｨ縺ｮ荳閾ｴ繧定ｦ∵ｱゅ＠縺ｦ縺・◆縺溘ａ縲～Any` 縺ｯ524,288縺ｮ1蝗槭□縺代〒蛟榊｢励ｒ邨ゆｺ・＠縺溘・
蟇ｾ遲悶→縺励※縲∝虚逧ЛoopCount繧呈ｯ碑ｼ・｡後Ο繝ｼ繧ｫ繝ｫ縺ｫ螟画峩縺励∝ｷｦ蜿ｳ蜷御ｸLoopCount縺ｧ縺ｮ蜷郁ｨ域凾髢灘愛螳壹↓邨ｱ荳縺励◆縲よｸｬ螳壼､縺ｮ蟄伜惠縲・壼ｸｸ騾ｲ謐怜ｮ御ｺ・∫援蛛ｴ髢ｾ蛟､蛻ｰ驕斐↓繧医ｋ螳悟・螳御ｺ・｣懷ｮ後・蜑企勁縺励◆縲・
## 螳溯｣・ｸ翫・蝗槫ｸｰ髦ｲ豁｢譚｡莉ｶ

- 蜈郁｡瑚｡後・蜍慕噪LoopCount縺悟ｾ檎ｶ夊｡後∈蠖ｱ髻ｿ縺励↑縺・％縺ｨ縲・- 蜷ЛoopCount縺ｧ蟾ｦ蜿ｳ縺悟酔縺廊oopCount繧剃ｽｿ逕ｨ縺吶ｋ縺薙→縲・- 迚・・縺縺大芦驕斐＠縺滓ｬ｡谿ｵ髫弱〒繧ょｷｦ蜿ｳ縺ｨ繧ょｮ溯｡後＆繧後ｋ縺薙→縲・- ns蜊倅ｽ阪・陦ｨ遉ｺ譎る俣縺ｧ縺ｯ縺ｪ縺丞粋險亥ｮ滓ｸｬ譎る俣縺ｧ蛻､螳壹☆繧九％縺ｨ縲・- 蟾ｦ蜿ｳ縺ｮ蛻ｰ驕猫oopCount縺檎焚縺ｪ繧句ｴ蜷医・螳悟・螳御ｺ・↓縺励↑縺・％縺ｨ縲・- 螟ｱ謨礼ｵ先棡繧・粋險域凾髢薙・谺關ｽ縺励◆蠕ｩ蜈・ｵ先棡繧貞ｮ悟・螳御ｺ・↓縺励↑縺・％縺ｨ縲・
# Source: Reverse\UI\UiSpecification.md

# Reverse UI Specifications

## Benchmark UI Specification

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/UiSpecification.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/Shared/UI.projitems
confidence: medium
review_status: draft
---

# Benchmark UI Specification

## Current Specification Notice

This reverse document captures the historical Benchmark UI implementation. CSV/JSON persistence, Save/Save As/Open, and external CSV import descriptions below are no longer the current forward specification. Current Benchmark and Scalable UI storage behavior is defined in `UI.md`.

## 蟇ｾ雎｡

縺薙・譁・嶌縺ｯ `Benchmark\UI` 縺ｨ `Benchmark\Scalable.UI` 縺悟・譛峨☆繧・MAUI 繝吶Φ繝√・繝ｼ繧ｯ螳溯｡袈I縺ｮ迴ｾ迥ｶ莉墓ｧ倥ｒ縲∝ｮ溯｣・°繧牙ｾｩ蜈・＠縺溘ｂ縺ｮ縺ｧ縺吶る壼ｸｸ迚医・繧｢繝励Μ蜷・`Seamless Benchmark`縲ヾcalable迚医・ `Seamless Scalable Benchmark` 縺ｨ縺励※讒区・縺輔ｌ縺ｾ縺吶ゆｸ｡譁ｹ縺ｨ繧・`Benchmark\Shared\UI.projitems` 邨檎罰縺ｧ蜷後§蜈ｱ譛蔚I繧ｳ繝ｼ繝峨ｒ蜿悶ｊ霎ｼ縺ｿ縺ｾ縺吶・

險ｼ諡:
- `Benchmark/UI/UI.csproj:3`
- `Benchmark/UI/UI.csproj:8`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:3`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:8`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## 逕ｻ髱｢讒区・

Benchmark UI 縺ｯ蜊倅ｸ縺ｮ `ContentPage` 縺ｧ縺吶ゅさ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｧ繧ｿ繧､繝医Ν縲∬レ譎ｯ濶ｲ縲～BenchmarkRunnerViewModel`縲√ヤ繝ｼ繝ｫ繝舌・縲√Γ繧､繝ｳ繧ｳ繝ｳ繝・Φ繝・ｒ菴懈・縺励・陦後・繝ｫ繝ｼ繝・`Grid` 縺ｫ驟咲ｽｮ縺励∪縺吶ゆｸ頑ｮｵ縺ｯ繝・・繝ｫ繝舌・縲∽ｸ区ｮｵ縺ｯ繝吶Φ繝√・繝ｼ繧ｯ謫堺ｽ憺伜沺縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:10`
- `Benchmark/Shared/UI/MainPage.cs:72`
- `Benchmark/Shared/UI/MainPage.cs:80`
- `Benchmark/Shared/UI/MainPage.cs:81`

荳ｻ隕・伜沺:
- 繝・・繝ｫ繝舌・: 螳溯｡後∝●豁｢縲∽ｸ譎ょ●豁｢縲∽ｿ晏ｭ倥∝錐蜑阪ｒ莉倥￠縺ｦ菫晏ｭ倥・幕縺上√＠縺阪＞蛟､縲∝ｮ溯｡後・繝ｭ繝輔ぃ繧､繝ｫ縲∝ｮ溯｡悟ｱ･豁ｴ繧定｡ｨ遉ｺ縺励∪縺吶・
- 繧ｽ繝ｼ繧ｹ繧ｿ繝・ 繝吶Φ繝√・繝ｼ繧ｯ蟇ｾ雎｡縺ｮ豈碑ｼ・き繝・ざ繝ｪ繧貞・繧頑崛縺医∪縺吶・
- Benchmark 荳矩Κ繧ｿ繝・ CPU縲´og縲！mprovement Prompt 繧貞・繧頑崛縺医∪縺吶・
- 繝吶Φ繝√・繝ｼ繧ｯ陦ｨ: 繝｡繧ｽ繝・ラ陦後∝ｷｦ蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝医・驕ｸ謚・迥ｶ諷・險域ｸｬ蛟､縲∝ｼ墓焚蛻励∵ｯ皮紫蛻励ｒ陦ｨ遉ｺ縺励∪縺吶・
- 繝√Ε繝ｼ繝・ 驕ｸ謚槭＠縺溯ｨ域ｸｬ蛟､繧貞ｷｦ蜿ｳ豈碑ｼ・∪縺溘・繧ｹ繧ｱ繝ｼ繝ｫ謗ｨ遘ｻ縺ｧ陦ｨ遉ｺ縺励∪縺吶・
- CPU 陦ｨ: 隲也炊CPU繧単 core縲・ core縲ゞnknown core蜊倅ｽ阪〒陦ｨ遉ｺ縺励∝ｮ溯｡悟ｯｾ雎｡CPU繧帝∈謚槭＠縺ｾ縺吶・
- 繝繧､繧｢繝ｭ繧ｰ: 邨ゆｺ・∵悴菫晏ｭ倥∽ｸ頑嶌縺阪∽ｸ譎ょ●豁｢荳ｭ謫堺ｽ懊・遒ｺ隱阪ｒ陦ｨ遉ｺ縺励∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:980`
- `Benchmark/Shared/UI/MainPage.cs:1124`
- `Benchmark/Shared/UI/MainPage.cs:1310`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:2474`
- `Benchmark/Shared/UI/MainPage.cs:2511`
- `Benchmark/Shared/UI/MainPage.cs:2558`

## 繝・・繝ｫ繝舌・莉墓ｧ・

繝・・繝ｫ繝舌・縺ｯ `ImageButton` 繧剃ｸｭ蠢・↓讒区・縺輔ｌ縺ｾ縺吶ゅ・繧ｿ繝ｳ縺ｯ繝ｭ繝ｼ繝ｫ縺斐→縺ｫ菴懈・縺輔ｌ縲√け繝ｪ繝・け譎ゅ↓ `HandleToolbarButtonClickAsync` 繧貞他縺ｳ縺ｾ縺吶３un 繝懊ち繝ｳ縺縺代・繝・・繝ｫ繝√ャ繝励′ `RunButtonTooltipText` 縺ｫ繝舌う繝ｳ繝峨＆繧後∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:604`
- `Benchmark/Shared/UI/MainPage.cs:618`
- `Benchmark/Shared/UI/MainPage.cs:628`
- `Benchmark/Shared/UI/MainPage.cs:672`

繝・・繝ｫ繝舌・譚｡莉ｶ:
- 縺励″縺・､Picker縺ｯ `ThresholdOptions` 縺ｨ `SelectedThreshold` 縺ｫ繝舌う繝ｳ繝峨＆繧後√・繝ｳ繝√・繝ｼ繧ｯ譚｡莉ｶ螟画峩蜿ｯ閭ｽ譎ゅ□縺第怏蜉ｹ縺ｧ縺吶・
- 螳溯｡後・繝ｭ繝輔ぃ繧､繝ｫPicker縺ｯ `ExecutionProfiles` 縺ｨ `SelectedExecutionProfile` 縺ｫ繝舌う繝ｳ繝峨＆繧後√・繝ｳ繝√・繝ｼ繧ｯ譚｡莉ｶ螟画峩蜿ｯ閭ｽ譎ゅ□縺第怏蜉ｹ縺ｧ縺吶・
- 螳溯｡悟ｱ･豁ｴPicker縺ｯ `RunOptions` 縺ｨ `SelectedRunOption` 縺ｫ繝舌う繝ｳ繝峨＆繧後∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:495`
- `Benchmark/Shared/UI/MainPage.cs:496`
- `Benchmark/Shared/UI/MainPage.cs:504`
- `Benchmark/Shared/UI/MainPage.cs:505`
- `Benchmark/Shared/UI/MainPage.cs:506`
- `Benchmark/Shared/UI/MainPage.cs:514`
- `Benchmark/Shared/UI/MainPage.cs:515`

## 繝吶Φ繝√・繝ｼ繧ｯ陦ｨ莉墓ｧ・

繝吶Φ繝√・繝ｼ繧ｯ陦ｨ縺ｯ Syncfusion `SfDataGrid` 縺ｧ讒狗ｯ峨＆繧後～BenchmarkRows` 繧・ItemsSource 縺ｫ縺励∪縺吶り｡後＃縺ｨ縺ｫ驕ｸ謚槭メ繧ｧ繝・け縲∝ｷｦ蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝医そ繝ｫ縲√せ繝・・繧ｿ繧ｹ縲，PU陦ｨ遉ｺ縲∝ｮ溯｡後ユ繧ｭ繧ｹ繝医∝､ｱ謨励ユ繧ｭ繧ｹ繝医∬ｨ域ｸｬ蛟､縲√ヤ繝ｼ繝ｫ繝√ャ繝励ｒ陦ｨ遉ｺ縺励∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1605`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:1988`
- `Benchmark/Shared/UI/MainPage.cs:2009`
- `Benchmark/Shared/UI/MainPage.cs:2019`
- `Benchmark/Shared/UI/MainPage.cs:2022`
- `Benchmark/Shared/UI/MainPage.cs:2023`
- `Benchmark/Shared/UI/MainPage.cs:2040`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2059`
- `Benchmark/Shared/UI/MainPage.cs:2180`

驕ｸ謚樊桃菴・
- 陦悟腰菴阪∝・繧ｰ繝ｫ繝ｼ繝怜腰菴阪∝ｷｦ蜿ｳ繧ｻ繝ｫ蜊倅ｽ阪・驕ｸ謚樒憾諷九ｒ謖√■縺ｾ縺吶・
- 繝倥ャ繝繝ｼ縺ｮ髮・ｴ・メ繧ｧ繝・け縺ｯ `IsAllBenchmarkSelectionChecked` 縺ｾ縺溘・ `BenchmarkColumnSelectionGroup.IsChecked` 繧・OneWay 縺ｧ陦ｨ遉ｺ縺励√ち繝・・譎ゅ↓ViewModel蛛ｴ縺ｮ驕ｸ謚樊桃菴懊ｒ蜻ｼ縺ｳ縺ｾ縺吶・
- 繝√Ε繝ｼ繝井ｸ翫・陦・繝舌・繧偵ち繝・・縺吶ｋ縺ｨ縲∝ｯｾ蠢懊☆繧九・繝ｳ繝√・繝ｼ繧ｯ陦後∪縺溘・蟾ｦ蜿ｳ繧ｻ繝ｫ繧帝∈謚槭＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:1798`
- `Benchmark/Shared/UI/MainPage.cs:1802`
- `Benchmark/Shared/UI/MainPage.cs:1816`
- `Benchmark/Shared/UI/MainPage.cs:1820`
- `Benchmark/Shared/UI/MainPage.cs:1823`
- `Benchmark/Shared/UI/MainPage.cs:1830`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3331`

## 繝√Ε繝ｼ繝井ｻ墓ｧ・

繝√Ε繝ｼ繝医・ Syncfusion `SfCartesianChart` 縺ｨ `StackingColumn100Series` 縺ｧ蟾ｦ蜿ｳ豈碑ｼ・ｒ陦ｨ遉ｺ縺励∪縺吶らｳｻ蛻励・ ItemsSource 縺ｯ `ChartPoints` 縺ｧ縺吶るｫ倥＆縺ｯ `ChartHeight` 縺ｫ繝舌う繝ｳ繝峨＆繧後∪縺吶ゅメ繝｣繝ｼ繝医↓縺ｯ迢ｬ閾ｪ縺ｮ `GraphicsView` 繧ｪ繝ｼ繝舌・繝ｬ繧､縺碁㍾縺ｭ繧峨ｌ縲・∈謚樊棧縲√Λ繝吶Ν縲√ヲ繝・ヨ繝・せ繝医ｒ諡・ｽ薙＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3138`
- `Benchmark/Shared/UI/MainPage.cs:3154`
- `Benchmark/Shared/UI/MainPage.cs:3174`
- `Benchmark/Shared/UI/MainPage.cs:3209`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3306`
- `Benchmark/Shared/UI/MainPage.cs:3331`

繝√Ε繝ｼ繝医・陦ｨ遉ｺ繝｢繝ｼ繝峨・ `BenchmarkChartMode.ElementComparison` 縺ｨ `BenchmarkChartMode.ScaleTrend` 縺ｧ縺吶よ欠讓吶・ `Time` 縺ｨ `Allocated` 縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:18`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:23`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`

## CPU驕ｸ謚樔ｻ墓ｧ・

CPU陦ｨ遉ｺ縺ｯ `CpuSections` 繧貞・縺ｫ菴懊ｉ繧後∪縺吶７iewModel縺ｯCPU繝医・繝ｭ繧ｸ繧定ｪｭ縺ｿ霎ｼ縺ｿ縲￣ core縲・ core縲ゞnknown core 縺ｮ繧ｻ繧ｯ繧ｷ繝ｧ繝ｳ縺ｸ蜀肴ｧ狗ｯ峨＠縺ｾ縺吶６I荳翫〒縺ｯ繧ｻ繧ｯ繧ｷ繝ｧ繝ｳ縲∬｡後∝・縲∝句挨CPU蜊倅ｽ阪〒驕ｸ謚槭〒縺阪∪縺吶ょｰ代↑縺上→繧・縺､縺ｮCPU縺碁∈謚槭＆繧後ｋ繧医≧縺ｫViewModel縺瑚｣懈ｭ｣縺励∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:203`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:750`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:758`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:767`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:780`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3676`

## 繝繧､繧｢繝ｭ繧ｰ/邨ゆｺ・ｻ墓ｧ・

邨ゆｺ・凾縺ｯ `RequestCloseAsync` 縺悟ｮ溯｡御ｸｭ縲∝●豁｢荳ｭ縲∵悴菫晏ｭ倡ｵ先棡縺ｮ譛臥┌繧堤｢ｺ隱阪＠縺ｾ縺吶ょｿ・ｦ√↑蝣ｴ蜷医・迢ｬ閾ｪ繧ｪ繝ｼ繝舌・繝ｬ繧､縺ｮ遒ｺ隱阪ム繧､繧｢繝ｭ繧ｰ繧定｡ｨ遉ｺ縺励・∈謚槭↓蠢懊§縺ｦ蛛懈ｭ｢縲∽ｿ晏ｭ倥∫ｴ譽・√く繝｣繝ｳ繧ｻ繝ｫ繧定｡後＞縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:122`
- `Benchmark/Shared/UI/MainPage.cs:141`
- `Benchmark/Shared/UI/MainPage.cs:145`
- `Benchmark/Shared/UI/MainPage.cs:155`
- `Benchmark/Shared/UI/MainPage.cs:255`
- `Benchmark/Shared/UI/MainPage.cs:269`

繝輔ぃ繧､繝ｫ/繝輔か繝ｫ繝謫堺ｽ・
- `Open` 縺ｯCSV縺ｾ縺溘・繝輔か繝ｫ繝繧帝∈謚槭〒縺阪∪縺吶・
- CSV縺ｯ繝励Λ繝・ヨ繝輔か繝ｼ繝蛻･縺ｮ繝輔ぃ繧､繝ｫ繧ｿ繧､繝玲欠螳壹ｒ謖√■縺ｾ縺吶・
- 菫晏ｭ伜・/繧ｨ繧ｯ繧ｹ繝昴・繝亥・縺ｯ繝輔か繝ｫ繝Picker繧剃ｽｿ縺・∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:413`
- `Benchmark/Shared/UI/MainPage.cs:421`
- `Benchmark/Shared/UI/MainPage.cs:449`

## Visual Verification

2026-07-01縺ｮ讀懆ｨｼ縺ｧ縲｀AUI繧｢繝励Μ襍ｷ蜍輔→繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ蜿門ｾ励ｒ隧ｦ陦後＠縺ｾ縺励◆縺後・壼ｸｸUI繝励Ο繧ｻ繧ｹ縺ｯ `MainWindowHandle=0` 縺ｮ縺ｾ縺ｾ繧ｦ繧｣繝ｳ繝峨え讀懷・縺ｧ縺阪★縲√せ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ縺ｯ譛ｪ蜿門ｾ励〒縺吶４calable UI縺ｯ譌｢蟄倥・繝ｭ繧ｻ繧ｹ縺悟・蜉帙ヵ繧｡繧､繝ｫ繧偵Ο繝・け縺励※縺・∪縺励◆縺後∝酔縺倥￥ `MainWindowHandle=0` 縺ｧ逕ｻ髱｢險ｼ霍｡縺ｨ縺励※蛻ｩ逕ｨ縺ｧ縺阪∪縺帙ｓ縺ｧ縺励◆縲・

## Unknown

- 螳溯｡檎判髱｢縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ辣ｧ蜷医・譛ｪ螳御ｺ・〒縺吶・AUI繧｢繝励Μ縺碁壼ｸｸ縺ｮ繝医ャ繝励Ξ繝吶Ν繧ｦ繧｣繝ｳ繝峨え縺ｨ縺励※讀懷・縺ｧ縺阪ｋ迺ｰ蠅・〒蜀榊叙蠕励′蠢・ｦ√〒縺吶・
- 譌｢蟄倥た繝ｼ繧ｹ蜀・↓譁・ｭ怜喧縺代＠縺ｦ縺・ｋ譌･譛ｬ隱櫁｡ｨ遉ｺ縺瑚､・焚縺ゅｊ縺ｾ縺吶ゅ％繧後・螳溯｣・ｺ句ｮ溘→縺励※險倬鹸縺励∵隼蝟・愛譁ｭ縺ｯ `UI.md` 縺ｫ蛻・屬縺励∪縺吶・


## Parameter Sweep UI Specification

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/UiSpecification.md
evidence_sources:
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep UI Specification

## Current Specification Notice

This reverse document captures the historical ParameterSweep UI implementation. References to `results.csv` or `ParameterSweepRunStore` are no longer the current forward specification. Current ParameterSweep storage behavior is defined in `UI.md`.

## 蟇ｾ雎｡

縺薙・譁・嶌縺ｯ `Benchmark\Tsavorite.ParameterSweep.UI` 縺ｮ迴ｾ迥ｶUI莉墓ｧ倥ｒ螳溯｣・°繧牙ｾｩ蜈・＠縺溘ｂ縺ｮ縺ｧ縺吶ゅ％縺ｮ繧｢繝励Μ縺ｯMAUI蜊倅ｸ繝壹・繧ｸ繧｢繝励Μ縺ｧ縲√ち繧､繝医Ν縺ｯ `Tsavorite Parameter Sweep` 縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`

## 逕ｻ髱｢讒区・

逕ｻ髱｢縺ｯ `ContentPage` 縺ｧ縲√さ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｧ `ParameterSweepViewModel` 繧・`BindingContext` 縺ｫ險ｭ螳壹＠縲～CreateContent` 縺ｧ菴懊▲縺溘さ繝ｳ繝・Φ繝・ｒ陦ｨ遉ｺ縺励∪縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:10`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`

逕ｻ髱｢縺ｯ3陦後・ `Grid` 縺ｧ縺吶・
- 1陦檎岼: 繝・・繝ｫ繝舌・縲・
- 2陦檎岼: 繧ｹ繝・・繧ｿ繧ｹ縲・
- 3陦檎岼: 讓ｪ繧ｹ繧ｯ繝ｭ繝ｼ繝ｫ蜿ｯ閭ｽ縺ｪ邨先棡陦ｨ縲・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:67`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:69`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`

## 繝・・繝ｫ繝舌・莉墓ｧ・

繝・・繝ｫ繝舌・縺ｫ縺ｯ莉･荳九・UI隕∫ｴ縺後≠繧翫∪縺吶・
- `Run sweep` 繝懊ち繝ｳ: `CanRun` 縺ｫ譛牙柑迥ｶ諷九ｒ繝舌う繝ｳ繝峨＠縲√け繝ｪ繝・け縺ｧ `RunFullAsync` 繧貞ｮ溯｡後＠縺ｾ縺吶・
- `Smoke` 繝懊ち繝ｳ: `CanRun` 縺ｫ譛牙柑迥ｶ諷九ｒ繝舌う繝ｳ繝峨＠縲√け繝ｪ繝・け縺ｧ `RunSmokeAsync` 繧貞ｮ溯｡後＠縺ｾ縺吶・
- `Refresh runs` 繝懊ち繝ｳ: 繧ｯ繝ｪ繝・け縺ｧ `RefreshRuns` 繧貞ｮ溯｡後＠縺ｾ縺吶・
- `MaxCount` 蜈･蜉帶ｬ・ `MaxSuccessfulCountText` 縺ｫ繝舌う繝ｳ繝峨＠縺ｾ縺吶・
- `Steps` 蜈･蜉帶ｬ・ `MaxSweepStepsText` 縺ｫ繝舌う繝ｳ繝峨＠縺ｾ縺吶・
- 螻･豁ｴPicker: `RunHistory` 繧棚temsSource縲～SelectedBaselineRun` 繧担electedItem縺ｫ繝舌う繝ｳ繝峨＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:18`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:19`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:20`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:22`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:31`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:35`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:36`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:56`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:60`

## 邨先棡陦ｨ莉墓ｧ・

邨先棡陦ｨ縺ｯ `CollectionView` 縺ｧ縲～Rows` 繧棚temsSource縺ｫ縺励∪縺吶ゅ・繝・ム繝ｼ縺ｨ蜷・｡後・蜷後§蛻怜ｹ・ｮ夂ｾｩ縺ｮ `Grid` 繧剃ｽｿ縺・∪縺吶り｡碁∈謚槭・辟｡蜉ｹ縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:73`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:129`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:153`

蛻・
- `CaseName`
- `Collection`
- `Generic`
- `Backend`
- `Sweep`
- `Index`
- `Page`
- `Segment`
- `Memory`
- `MF`
- `MaxCount`
- `Failure`
- `Add`
- `Lookup`
- `CountDelta`
- `AddRatio`
- `LookupRatio`
- `FailureChanged`

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:118`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:149`

蜷・そ繝ｫ縺ｯ `ParameterSweepComparisonViewRow` 縺ｮ蜷悟錐縺ｾ縺溘・蟇ｾ蠢懊・繝ｭ繝代ユ繧｣縺ｫ `Label.Text` 繧偵ヰ繧､繝ｳ繝峨＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:198`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:206`

## Visual Verification

2026-07-01縺ｮ讀懆ｨｼ縺ｧ縲｀AUI繧｢繝励Μ襍ｷ蜍輔→繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ蜿門ｾ励ｒ隧ｦ陦後＠縺ｾ縺励◆縺後・壼ｸｸUI縺ｮ繧ｦ繧｣繝ｳ繝峨え讀懷・螟ｱ謨玲凾轤ｹ縺ｧ蜃ｦ逅・ｒ蛛懈ｭ｢縺励◆縺溘ａ縲￣arameterSweep UI縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ縺ｯ譛ｪ蜿門ｾ励〒縺吶・

## Unknown

- 縺薙・UI縺ｫ縺ｯ譏守､ｺ逧・↑繧ｨ繝ｩ繝ｼ繝繧､繧｢繝ｭ繧ｰ繧・く繝｣繝ｳ繧ｻ繝ｫ謫堺ｽ懊・螳溯｣・ｸ顔｢ｺ隱阪〒縺阪∪縺帙ｓ縲ょ､ｱ謨励・ `StatusText` 縺ｫ陦ｨ遉ｺ縺輔ｌ縺ｾ縺吶・
- 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ辣ｧ蜷医・譛ｪ螳御ｺ・〒縺吶・AUI繧｢繝励Μ縺碁壼ｸｸ縺ｮ繝医ャ繝励Ξ繝吶Ν繧ｦ繧｣繝ｳ繝峨え縺ｨ縺励※讀懷・縺ｧ縺阪ｋ迺ｰ蠅・〒蜀榊叙蠕励′蠢・ｦ√〒縺吶・
# Source: Reverse\UI\UIDebtFindings.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/UIDebtFindings.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
confidence: medium
review_status: draft
---

# UI Debt Findings

## Current Specification Notice

This file is a historical reverse-engineering debt report. Items that mention CSV/JSON persistence or old Save/Open flows describe the implementation state at the time of reverse engineering. The current UI storage specification is maintained in `UI.md` and `UI.md`.

Current implementation note: DB history restore is the active scenario. Mentions of CSV import/read/write in the findings below are retained only as historical evidence and should not be treated as current requirements.

## 1. 蟾ｨ螟ｧ縺ｪ繧ｳ繝ｼ繝峨ン繝上う繝ｳ繝峨→ViewModel

`Benchmark\Shared\UI\MainPage.cs` 縺ｯ逕ｻ髱｢逕滓・縲√う繝吶Φ繝磯・邱壹√ム繧､繧｢繝ｭ繧ｰ縲√メ繝｣繝ｼ繝医，PU陦ｨ縲√ョ繝ｼ繧ｿ繧ｰ繝ｪ繝・ラ蛻礼函謌舌√さ繝ｳ繝舌・繧ｿ縲∵緒逕ｻ繧ｪ繝ｼ繝舌・繝ｬ繧､繧・繝輔ぃ繧､繝ｫ縺ｧ謚ｱ縺医※縺・∪縺吶ＡBenchmarkRunnerViewModel.cs` 繧ょｮ溯｡悟宛蠕｡縲∽ｿ晏ｭ・隱ｭ霎ｼ縲・∈謚樒憾諷九√メ繝｣繝ｼ繝育函謌舌，PU驕ｸ謚槭∫ｵ先棡螟画鋤繧・繧ｯ繝ｩ繧ｹ縺ｫ髮・ｴ・＠縺ｦ縺・∪縺吶６I雋蛯ｵ隗｣豸医〒縺ｯ縲∫判髱｢讒狗ｯ峨∵桃菴懊ワ繝ｳ繝峨Λ縲∵緒逕ｻ陬懷勧縲∫憾諷句､画鋤縲∵ｰｸ邯壼喧騾｣謳ｺ縺ｮ蛻・屬縺御ｸｻ蛟呵｣懊〒縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3331`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:734`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1307`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1766`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3139`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`

## 2. 陦ｨ遉ｺ譁・ｨ縺ｮ譁・ｭ怜喧縺・

Benchmark UI 縺ｮ譌･譛ｬ隱櫁｡ｨ遉ｺ譁・ｭ怜・縺ｫ譁・ｭ怜喧縺代′谿九▲縺ｦ縺・∪縺吶ゅΘ繝ｼ繧ｶ繝ｼ縺ｫ陦ｨ遉ｺ縺輔ｌ繧句庄閭ｽ諤ｧ縺後≠繧九◆繧√∽ｻ墓ｧ伜喧縺ｧ縺ｯ迴ｾ迥ｶ莠句ｮ溘→縺励※險倬鹸縺励▽縺､縲∽ｿｮ豁｣蟇ｾ雎｡縺ｨ縺励※謇ｱ縺・∋縺阪〒縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3237`
- `Benchmark/Shared/UI/MainPage.cs:215`
- `Benchmark/Shared/UI/MainPage.cs:390`

## 3. 繧ｳ繝ｼ繝臥函謌振I縺ｮ繝舌う繝ｳ繝・ぅ繝ｳ繧ｰ縺梧焔譖ｸ縺阪〒謨｣蝨ｨ

Benchmark UI 縺ｯXAML縺ｧ縺ｯ縺ｪ縺修#繧ｳ繝ｼ繝峨〒UI繧呈ｧ狗ｯ峨＠縲∝､壽焚縺ｮ `SetBinding` 縺ｨ繧､繝吶Φ繝医ワ繝ｳ繝峨Λ繧貞句挨縺ｫ險ｭ螳壹＠縺ｦ縺・∪縺吶ゅヰ繧､繝ｳ繝・ぅ繝ｳ繧ｰ邨瑚ｷｯ縺檎判髱｢讒狗ｯ峨さ繝ｼ繝峨↓謨｣蝨ｨ縺励※縺・ｋ縺溘ａ縲∝､画峩譎ゅ↓萓晏ｭ倬未菫ゅｒ隕玖誠縺ｨ縺励ｄ縺吶＞讒矩縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2784`
- `Benchmark/Shared/UI/MainPage.cs:3138`

## 4. 螳溯｡檎憾諷九→陦ｨ遉ｺ迥ｶ諷九′蟇・ｵ仙粋

Benchmark UI 縺ｯ螳溯｡檎憾諷九∽ｿ晏ｭ倡憾諷九・∈謚樒憾諷九√メ繝｣繝ｼ繝郁｡ｨ遉ｺ縲，PU陦ｨ遉ｺ繧貞酔荳ViewModel蜀・〒騾｣蜍墓峩譁ｰ縺励∪縺吶ＡIsBenchmarkRunning` 繧・`IsRefreshing` 縺ｮsetter縺悟､壽焚縺ｮ髢｢騾｣繝励Ο繝代ユ繧｣縺ｨ繧ｳ繝槭Φ繝臥憾諷九ｒ騾夂衍縺吶ｋ縺溘ａ縲∫憾諷玖ｿｽ蜉譎ゅ・蜑ｯ菴懃畑遽・峇縺悟ｺ・＞縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:313`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:316`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:317`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:345`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:346`

## 5. ParameterSweep UI 縺ｯ邁｡貎斐□縺悟ｮ溯｡悟宛蠕｡縺御ｸ崎ｶｳ

ParameterSweep UI 縺ｯ讒矩縺悟ｰ上＆縺・ｸ譁ｹ縲∝ｮ溯｡御ｸｭ縺ｫ蜈･蜉帶ｬ・ｄ螻･豁ｴPicker繧堤┌蜉ｹ蛹悶☆繧句ｮ溯｣・・遒ｺ隱阪〒縺阪∪縺帙ｓ縲ゅく繝｣繝ｳ繧ｻ繝ｫ繧・ｩｳ邏ｰ繧ｨ繝ｩ繝ｼ陦ｨ遉ｺ繧ら｢ｺ隱阪〒縺阪∪縺帙ｓ縲る聞譎る俣螳溯｡後ｄ螟ｱ謨苓ｪｿ譟ｻ縺ｮUX縺ｨ縺励※縺ｯ霑ｽ蜉險ｭ險医′蠢・ｦ√〒縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`

## 6. 螳溽判髱｢讀懆ｨｼ縺ｮ險ｼ霍｡縺梧悴謨ｴ蛯・

UI莉墓ｧ倥ｒ螳牙ｮ壼喧縺吶ｋ縺ｫ縺ｯ縲∝・譛溯｡ｨ遉ｺ縲∝ｮ溯｡御ｸｭ縲∝●豁｢荳ｭ縲∵悴菫晏ｭ倡｢ｺ隱阪，SV隱ｭ霎ｼ縲√メ繝｣繝ｼ繝磯∈謚槭，PU驕ｸ謚槭・繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ縺ｾ縺溘・UI繝・せ繝郁ｨｼ霍｡縺悟ｿ・ｦ√〒縺吶ら樟譎らせ縺ｧ縺ｯ繧ｳ繝ｼ繝芽ｨｼ諡縺御ｸｭ蠢・〒縲∝ｮ溽判髱｢縺ｮ讀懆ｨｼ險ｼ霍｡縺ｯ霑ｽ蜉蜿門ｾ励′蠢・ｦ√〒縺吶・

險ｼ諡:
- `Reverse/UI/UiSpecification.md`

## 蠕檎ｶ壽隼菫ｮ蛟呵｣・

- Benchmark UI 縺ｮ逕ｻ髱｢逕滓・繧偵ゝoolbar縲ヾourceTabs縲。enchmarkGrid縲，hartPanel縲，puPanel縲．ialogOverlay 縺ｮ蜊倅ｽ阪∈蛻・牡縺吶ｋ縲・
- `BenchmarkRunnerViewModel` 縺九ｉ縲ヽunLifecycle縲ヾelectionState縲，hartState縲，puSelection縲￣ersistenceCoordinator 繧貞・髮｢縺吶ｋ縲・
- 陦ｨ遉ｺ譁・ｨ繧貞ｮ壽焚縺ｾ縺溘・繝ｪ繧ｽ繝ｼ繧ｹ縺ｸ遘ｻ縺励∵枚蟄怜喧縺代ｒ隗｣豸医☆繧九・
- ParameterSweep UI 縺ｫ蜈･蜉帷┌蜉ｹ蛹悶√く繝｣繝ｳ繧ｻ繝ｫ縲∬ｩｳ邏ｰ繧ｨ繝ｩ繝ｼ縲∝ｮ溯｡後Ο繧ｰ繧定ｿｽ蜉縺吶ｋ縲・
- 繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ縺ｾ縺溘・UI繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ讀懆ｨｼ繧呈紛蛯吶☆繧九・
# Source: Reverse\UI\ReverseEvidenceMap.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ReverseEvidenceMap.md
evidence_sources:
  - Benchmark/Shared/UI/MainPage.cs
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Shared/UI.Core/BenchmarkRunSession.cs
  - Shared/UI.Core/BenchmarkRunProjectionService.cs
  - Shared/UI.Core/BenchmarkUiRunRepository.cs
  - Shared/UI.Storage/UiRunStore.cs
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Shared/UI.Storage/UiRunStore.cs
confidence: medium
review_status: draft
---

# UI Reverse Evidence Map

## Current Specification Notice

This file is a historical reverse-engineering evidence map. Some evidence entries below describe the pre-DB canonical implementation, including CSV/JSON persistence paths that have since been removed. The current forward specification is maintained in `UI.md` and `UI.md`.

## Current Code Boundary Notice

The current implementation uses `Shared/UI.Storage/UiRunStore.cs` as the low-level SQLite boundary. Benchmark-specific conversion is in `Shared/UI.Core/BenchmarkUiRunRepository.cs`; live UI result truth is in `Shared/UI.Core/BenchmarkRunSession.cs`; display/comparison derivation is in `Shared/UI.Core/BenchmarkRunProjectionService.cs`. References below to `BenchmarkResultStore`, `BenchmarkRunPersistenceService`, `ParameterSweepPersistence`, CSV files, or `progress.json` are historical evidence only.

## Formalized Forward Specification

謨ｴ逅・ｸ医∩縺ｮ豁｣蠑丈ｻ墓ｧ倥・ `Document/UI/` 驟堺ｸ九∈譁ｰ險ｭ縺励∪縺励◆縲・

- `UI.md`
- `UI.md`

`Reverse/UI` 縺ｯ螳溯｣・°繧牙ｾｩ蜈・＠縺溯ｨｼ諡雉・侭縺ｨ縺励※谿九＠縲∽ｻ雁ｾ後・豁｣譛ｬ縺ｯ `Document/UI` 繧貞━蜈医＠縺ｾ縺吶・

## Generated

- `Reverse/UI/UiSpecification.md`
- `UI.md`
- `UI.md`

- `UI.md`
- `UI.md`
- `UI.md`

## Code Evidence

Benchmark app:
- `Benchmark/UI/UI.csproj`: MAUI縲√い繝励Μ蜷阪∝盾辣ｧ繝励Ο繧ｸ繧ｧ繧ｯ繝医ヾyncfusion/CommunityToolkit萓晏ｭ倥∝・譛蔚I繧､繝ｳ繝昴・繝医・
- `Benchmark/Scalable.UI/Scalable.UI.csproj`: Scalable迚医・繧｢繝励Μ蜷阪～SCALABLE_BENCHMARK`縲∝盾辣ｧ繝励Ο繧ｸ繧ｧ繧ｯ繝医∝・譛蔚I繧､繝ｳ繝昴・繝医・
- `Benchmark/Shared/UI.projitems`: 蜈ｱ譛蔚I縺ｨ `Shared\UI.Core` 縺ｮ蜿悶ｊ霎ｼ縺ｿ縲・
- `Benchmark/Shared/UI/MainPage.cs`: 逕ｻ髱｢讒狗ｯ峨√ヰ繧､繝ｳ繝・ぅ繝ｳ繧ｰ縲√う繝吶Φ繝医√ム繧､繧｢繝ｭ繧ｰ縲√メ繝｣繝ｼ繝医，PU陦ｨ縲・
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs`: 逕ｻ髱｢迥ｶ諷九√さ繝槭Φ繝峨・∈謚槭∝ｮ溯｡後∽ｿ晏ｭ・隱ｭ霎ｼ縲√メ繝｣繝ｼ繝域峩譁ｰ縲，PU驕ｸ謚槭・
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs`: 陦ｨ遉ｺ繝｢繝・Ν縲√そ繝ｫ繝｢繝・Ν縲，PU繝｢繝・Ν縲√メ繝｣繝ｼ繝育せ縲・
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs`: Run菫晏ｭ倥√お繧ｯ繧ｹ繝昴・繝医｝rogress隱ｭ霎ｼ縲・
- `Shared/UI.Core/BenchmarkStateManager.cs`: `progress.json` 縺ｨRun繝ｫ繝ｼ繝育ｮ｡逅・・

ParameterSweep app:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj`: MAUI縲√い繝励Μ蜷阪￣arameterSweep蜿ら・縲・
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs`: 蜊倅ｸ繝壹・繧ｸ逕ｻ髱｢縲√ヤ繝ｼ繝ｫ繝舌・縲∫ｵ先棡陦ｨ縲√ヰ繧､繝ｳ繝・ぅ繝ｳ繧ｰ縲・
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs`: 螳溯｡檎憾諷九∝・蜉帙∝ｱ･豁ｴ縲∵ｯ碑ｼ・√せ繝・・繧ｿ繧ｹ縲・
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs`: CSV菫晏ｭ・隱ｭ霎ｼ縲ヽun螻･豁ｴ縲∵ｯ碑ｼ・・

## Visual Evidence

譛ｪ蜿門ｾ励〒縺吶・026-07-01縺ｫ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ蜿門ｾ励ｒ隧ｦ陦後＠縺ｾ縺励◆縺後・壼ｸｸUI繝励Ο繧ｻ繧ｹ縺ｯ `MainWindowHandle=0` 縺ｮ縺ｾ縺ｾ繧ｦ繧｣繝ｳ繝峨え讀懷・縺ｧ縺阪∪縺帙ｓ縺ｧ縺励◆縲４calable UI縺ｯ譌｢蟄倥・繝ｭ繧ｻ繧ｹ縺瑚ｵｷ蜍輔＠縺ｦ縺・∪縺励◆縺後√％縺｡繧峨ｂ `MainWindowHandle=0` 縺ｧ縺励◆縲１arameterSweep UI縺ｯ騾壼ｸｸUI縺ｮ讀懷・螟ｱ謨玲凾轤ｹ縺ｧ蜃ｦ逅・′蛛懈ｭ｢縺励◆縺溘ａ譛ｪ謦ｮ蠖ｱ縺ｧ縺吶・

蜀榊叙蠕怜ｯｾ雎｡:
- Benchmark騾壼ｸｸUI 蛻晄悄陦ｨ遉ｺ縲・
- Scalable UI 蛻晄悄陦ｨ遉ｺ縲・
- ParameterSweep UI 蛻晄悄陦ｨ遉ｺ縲・

蜿門ｾ励〒縺阪◆蝣ｴ蜷医・ `Reverse/UI/Screenshots/` 縺ｫ菫晏ｭ倥＠縲√％縺ｮ遽縺ｸ繝輔ぃ繧､繝ｫ繝代せ縺ｨ蜿門ｾ玲擅莉ｶ繧定ｿｽ險倥＠縺ｦ縺上□縺輔＞縲・

## Confirmed

- Benchmark騾壼ｸｸUI縺ｨScalable UI縺ｯ蜷後§蜈ｱ譛蔚I繧ｳ繝ｼ繝峨ｒ菴ｿ縺・∪縺吶・
- Benchmark騾壼ｸｸUI縺ｨScalable UI縺ｯMAUI縲ヾyncfusion縲，ommunityToolkit縲。enchmarkDotNet縺ｫ萓晏ｭ倥＠縺ｾ縺吶・
- Scalable UI縺ｯ騾壼ｸｸUI縺ｨ逡ｰ縺ｪ繧句盾辣ｧ繝励Ο繧ｸ繧ｧ繧ｯ繝医→縺励″縺・､蜊倅ｽ阪ｒ謖√■縺ｾ縺吶・
- Benchmark UI縺ｯViewModel荳ｭ蠢・・謇区嶌縺垢# UI縺ｧ縺吶・
- ParameterSweep UI縺ｯ蟆剰ｦ乗ｨ｡縺ｪ謇区嶌縺垢# UI縺ｧ縺吶・
- ParameterSweep邨先棡縺ｯ `results.csv` 縺ｫ菫晏ｭ倥＆繧後∪縺吶・

## Inferred

- Benchmark UI雋蛯ｵ縺ｮ荳ｭ蠢・・縲∫判髱｢讒狗ｯ峨さ繝ｼ繝峨→ViewModel縺ｮ雋ｬ蜍咎寔荳ｭ縺ｧ縺吶Ｄonfidence: medium
- ParameterSweep UI雋蛯ｵ縺ｮ荳ｭ蠢・・縲∝ｮ溯｡御ｸｭ縺ｮ蜈･蜉帛宛蠕｡縲√く繝｣繝ｳ繧ｻ繝ｫ縲√お繝ｩ繝ｼ隧ｳ邏ｰ陦ｨ遉ｺ縺ｮ荳崎ｶｳ縺ｧ縺吶Ｄonfidence: medium
- 陦ｨ遉ｺ譁・ｨ縺ｮ譁・ｭ怜喧縺代・繝ｦ繝ｼ繧ｶ繝ｼ蜿ｯ隕悶・蜩∬ｳｪ蝠城｡後〒縺吶Ｄonfidence: medium

## Unknown

- GUI襍ｷ蜍募ｾ後・螳滄圀縺ｮ蛻晄悄陦ｨ遉ｺ縲√え繧｣繝ｳ繝峨え繧ｵ繧､繧ｺ縲∬｡ｨ遉ｺ蟠ｩ繧後・譛臥┌縲・
- 譁・ｭ怜喧縺代＠縺ｦ縺・ｋ譁・ｨ縺ｮ譛ｬ譚･縺ｮ譌･譛ｬ隱槭・
- UI迥ｶ諷九せ繝医い縺ｮ蜈ｨ繧ｭ繝ｼ縺ｨ譛溷ｾ・ｾｩ蜈・ｻ墓ｧ倥・
- Mac Catalyst縺ｧ縺ｮ螳溽判髱｢蟾ｮ蛻・・

## Review Gate: UI Reverse

### Generated
荳願ｨ・譁・嶌繧堤函謌舌＠縺ｾ縺励◆縲・

### Confirmed
荳ｻ隕∫判髱｢讒矩縲√ヰ繧､繝ｳ繝・ぅ繝ｳ繧ｰ縲√ョ繝ｼ繧ｿ讒矩縲∽ｿ晏ｭ・隱ｭ霎ｼ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ縲ヾcalable蟾ｮ蛻・￣arameterSweep蟾ｮ蛻・ｒ螳溯｣・ｨｼ諡縺九ｉ遒ｺ隱阪＠縺ｾ縺励◆縲・

### Inferred
雋蛯ｵ蛟呵｣懊・螳溯｣・・雋ｬ蜍咎寔荳ｭ縲√ヰ繧､繝ｳ繝・ぅ繝ｳ繧ｰ謨｣蝨ｨ縲∵枚蟄怜喧縺代∝ｮ溯｡御ｸｭ蛻ｶ蠕｡荳崎ｶｳ繧剃ｸｭ蠢・↓謨ｴ逅・＠縺ｾ縺励◆縲・

### Unknown
螳溽判髱｢繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ辣ｧ蜷医・譛ｪ蜿門ｾ励〒縺吶ゆｻ雁屓縺ｮ隧ｦ陦後〒縺ｯ縲∝ｯｾ雎｡繝励Ο繧ｻ繧ｹ縺後ヨ繝・・繝ｬ繝吶Ν繧ｦ繧｣繝ｳ繝峨え繝上Φ繝峨Ν繧貞・髢九○縺壼叙蠕励〒縺阪∪縺帙ｓ縺ｧ縺励◆縲・

### Questions
縺ｪ縺励ら樟譎らせ縺ｮ霑ｽ蜉隱ｿ譟ｻ縺ｯ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ繧ｷ繝ｧ繝・ヨ蜿門ｾ励→UI迥ｶ諷九せ繝医い隧ｳ邏ｰ縺ｧ縺吶・
# Source: Reverse\UI\Benchmark\BindingAndStateSpecification.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/BindingAndStateSpecification.md
evidence_sources:
  - Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs
  - Benchmark/Shared/UI/BenchmarkRunnerModels.cs
  - Benchmark/Shared/UI/BenchmarkResultStore.cs
  - Shared/UI.Core/BenchmarkRunPersistenceService.cs
  - Shared/UI.Core/BenchmarkStateManager.cs
confidence: medium
review_status: draft
---

# Benchmark Binding And State Specification

## Current Specification Notice

This reverse document captures historical binding and state behavior. References to CSV, `progress.json`, Save/Open commands, or old persistence services are retained only as reverse evidence. Current DB-canonical behavior is defined in `UI.md`.

## ViewModel讒矩

`BenchmarkRunnerViewModel` 縺ｯ `ObservableObject` 繧堤ｶ呎価縺吶ｋ蜊倅ｸ縺ｮ逕ｻ髱｢迥ｶ諷九さ繝ｳ繝・リ縺ｧ縺吶ら判髱｢縺ｮ荳ｻ隕√ョ繝ｼ繧ｿ縺ｯ `ObservableCollection` 縺ｨ繝励Ο繝代ユ繧｣騾夂衍縺ｧ蜈ｬ髢九＆繧後√さ繝槭Φ繝峨・ CommunityToolkit 縺ｮ `IRelayCommand` / `IAsyncRelayCommand` 縺ｨ縺励※謠蝉ｾ帙＆繧後∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:7`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:226`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:227`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:228`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:234`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:237`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:238`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:254`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:265`

荳ｻ隕√さ繝ｬ繧ｯ繧ｷ繝ｧ繝ｳ:
- `BenchmarkRows`: 繝吶Φ繝√・繝ｼ繧ｯ陦ｨ縺ｮ陦後・
- `ResultItems`: 螳溯｡・隱ｭ霎ｼ邨先棡縺ｮ蜈ｨ菴薙・
- `VisibleResultItems`: 逕ｻ髱｢陦ｨ遉ｺ荳ｭ縺ｮ邨先棡縲・
- `CpuItems`: 隲也炊CPU縲・
- `CpuSections`: CPU陦ｨ縺ｮ繧ｻ繧ｯ繧ｷ繝ｧ繝ｳ縲・
- `ChartPoints`: 繝√Ε繝ｼ繝郁｡ｨ遉ｺ轤ｹ縲・
- `RunOptions`: 菫晏ｭ俶ｸ医∩/譛ｪ菫晏ｭ・螟夜Κ隱ｭ霎ｼ縺ｮ螳溯｡悟ｱ･豁ｴ縲・
- `LogEntries`: 繝ｭ繧ｰ陦ｨ遉ｺ縲・

## 繧ｳ繝槭Φ繝・

ViewModel縺ｯ繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｧ蜷ФI謫堺ｽ懊・繧ｳ繝槭Φ繝峨ｒ菴懈・縺励∪縺吶・

繧ｳ繝槭Φ繝牙ｯｾ蠢・
- `RunCommand`: `RunOrResumeAsync`
- `StopCommand`: `StopAsync`
- `PauseCommand`: `PauseAsync`
- `LoadCsvCommand`: `LoadCsvAsync`
- `SaveCommand`: `SaveCurrentBenchmarkResultsAsync(true)`
- `OpenCommand`: `OpenAsync`

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:160`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:162`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:163`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:165`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:166`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:168`

## 蜍穂ｽ懈擅莉ｶ

繝吶Φ繝√・繝ｼ繧ｯ譚｡莉ｶ螟画峩縺ｯ縲∝ｮ溯｡御ｸｭ縲√Μ繝輔Ξ繝・す繝･荳ｭ縲∽ｸ譎ょ●豁｢荳ｭ縺ｧ縺ｪ縺・ｴ蜷医↓險ｱ蜿ｯ縺輔ｌ縺ｾ縺吶ょｮ溯｡後・繧ｿ繝ｳ縺ｯ縲√Μ繝輔Ξ繝・す繝･荳ｭ縺ｧ縺ｪ縺上∝ｮ溯｡御ｸｭ縺ｧ縺ｪ縺上・∈謚樊ｸ医∩繝吶Φ繝√・繝ｼ繧ｯ繧ｻ繝ｫ縺後≠繧句ｴ蜷医↓險ｱ蜿ｯ縺輔ｌ縺ｾ縺吶ゆｸ譎ょ●豁｢荳ｭ縺ｯ Resume 譚｡莉ｶ縺悟挨縺ｫ譛牙柑縺ｫ縺ｪ繧翫∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:355`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:371`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:374`

菫晏ｭ・髢九￥譚｡莉ｶ縺ｯ螳溯｡檎憾諷九→譛ｪ菫晏ｭ倡ｵ先棡縺ｮ譛臥┌縺ｫ萓晏ｭ倥＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:428`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:434`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:439`

## 驕ｸ謚樒憾諷・

繧ｽ繝ｼ繧ｹ繧ｿ繝悶∝ｮ溯｡後・繝ｭ繝輔ぃ繧､繝ｫ縲√せ繧ｱ繝ｼ繝ｫ縲√＠縺阪＞蛟､縲∝ｮ溯｡悟ｱ･豁ｴ縲∵欠讓吶√メ繝｣繝ｼ繝医Δ繝ｼ繝峨∫ｵ先棡N驕ｸ謚槭・ViewModel繝励Ο繝代ユ繧｣縺ｧ菫晄戟縺輔ｌ縺ｾ縺吶ょ､画峩譎ゅ↓縺ｯ繝√Ε繝ｼ繝域峩譁ｰ縲∫ｵ先棡陦ｨ遉ｺ譖ｴ譁ｰ縲ゞI迥ｶ諷倶ｿ晏ｭ倥′陦後ｏ繧後ｋ繝励Ο繝代ユ繧｣縺後≠繧翫∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:551`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:601`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:614`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:619`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:624`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:687`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:707`

繝吶Φ繝√・繝ｼ繧ｯ陦後→繧ｻ繝ｫ縺ｮ繝・・繧ｿ讒矩縺ｯ `BenchmarkTableRow` 縺ｨ `BenchmarkProjectCell` 縺ｧ縺吶ＡBenchmarkTableRow` 縺ｯ蟾ｦ蜿ｳ繧ｻ繝ｫ繧呈戟縺｡縲√・繝ｭ繧ｸ繧ｧ繧ｯ繝亥錐縺九ｉ繧ｻ繝ｫ繧定ｧ｣豎ｺ縺ｧ縺阪∪縺吶ゅそ繝ｫ縺ｯ驕ｸ謚槭ゞI驕ｸ謚槭∝ｮ溯｡檎ｵ先棡縲，PU陦ｨ遉ｺ縲√Γ繝医Μ繧ｯ繧ｹ縲∝､ｱ謨苓｡ｨ遉ｺ繧剃ｿ晄戟縺励∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:146`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:187`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:433`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:508`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:744`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:790`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:791`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:913`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:920`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:972`

## 繝・・繧ｿ讒矩

UI荳翫・荳ｻ縺ｪ繝｢繝・Ν:
- `SourceTabOption`: 繧ｽ繝ｼ繧ｹ繧ｿ繝門ｮ夂ｾｩ縺ｨ騾ｲ謐励ｒ陦ｨ遉ｺ逕ｨ縺ｫ菫晄戟縺励∪縺吶・
- `BenchmarkProjectCell`: 蟾ｦ蜿ｳ繝励Ο繧ｸ繧ｧ繧ｯ繝医そ繝ｫ縺ｮ螳溯｡檎憾諷九→陦ｨ遉ｺ蛟､繧剃ｿ晄戟縺励∪縺吶・
- `BenchmarkTableRow`: 1繝吶Φ繝√・繝ｼ繧ｯ陦後・蟾ｦ蜿ｳ繧ｻ繝ｫ縲∝ｼ墓焚縲∵ｯ皮紫縲√た繝ｼ繝亥､繧剃ｿ晄戟縺励∪縺吶・
- `CpuSelectionItem`: 隲也炊CPU縲√さ繧｢遞ｮ蛻･縲・∈謚樒憾諷九∫樟蝨ｨ繧ｸ繝ｧ繝也憾諷九ｒ菫晄戟縺励∪縺吶・
- `CpuTableSection`: CPU陦ｨ遉ｺ繧ｻ繧ｯ繧ｷ繝ｧ繝ｳ繧剃ｿ晄戟縺励∪縺吶・
- `ChartComparisonPoint`: 繝√Ε繝ｼ繝医・蟾ｦ蜿ｳ蛟､縲√Λ繝吶Ν縲√ヤ繝ｼ繝ｫ繝√ャ繝励ゞI驕ｸ謚樒憾諷九ｒ菫晄戟縺励∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:47`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:146`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:744`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1209`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1456`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1741`

繧ｽ繝ｼ繝亥ｯｾ雎｡縺ｯ `BenchmarkGridSortColumn` 縺ｫ蛻玲嫌縺輔ｌ縲∬｡後・ `GetSortValue` 縺ｧ蛻励↓蠢懊§縺溷､繧定ｿ斐＠縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:28`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1051`

## 繝・・繧ｿ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ

蛻晄悄蛹・
- `InitializeAsync` 縺後き繧ｿ繝ｭ繧ｰ縲，SV縲∽ｿ晏ｭ俶ｸ医∩Run縲∬｡ｨ遉ｺ迥ｶ諷九ｒ蛻晄悄蛹悶＠縺ｾ縺吶・
- 繝壹・繧ｸ縺ｮ `Loaded` 縺九ｉ `InitializeAsync` 縺悟他縺ｰ繧後∪縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/MainPage.cs:112`
- `Benchmark/Shared/UI/MainPage.cs:113`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:734`

螳溯｡・
- `RunOrResumeAsync` 縺梧眠隕丞ｮ溯｡後∪縺溘・荳譎ょ●豁｢縺九ｉ縺ｮ蜀埼幕繧呈桶縺・∪縺吶・
- 螳溯｡檎憾諷九・ `runCancellation` 縺ｨ `IsBenchmarkRunning` 縺九ｉ `IsBenchmarkRunActive` 縺ｨ縺励※蟆主・縺輔ｌ縺ｾ縺吶・
- `PauseAsync` 縺ｨ `StopAsync` 縺ｯ螳溯｡檎憾諷九ｒ蛛懈ｭ｢譁ｹ蜷代∈驕ｷ遘ｻ縺輔○縲～WaitForRunStopAsync` 繧貞ｾ・■縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:472`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:482`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:495`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1766`

菫晏ｭ・隱ｭ霎ｼ:
- 邂｡逅・un菫晏ｭ倥・ `BenchmarkRunPersistenceService.SaveManagedRun` 縺・`ProgressState` 繧定､・｣ｽ縺励∵ｬ｡縺ｮRun繝ｫ繝ｼ繝医ｒ菴懈・縺励∝・蜉帙ｒ譖ｸ縺阪∪縺吶・
- 繧ｨ繧ｯ繧ｹ繝昴・繝医・謖・ｮ壹ヵ繧ｩ繝ｫ繝縺ｸ `progress.json` 縺ｨ蜃ｺ蜉帙ｒ菫晏ｭ倥＠縺ｾ縺吶・
- 隱ｭ霎ｼ縺ｯ `ReadProgressState` 縺ｨ CSV隱ｭ霎ｼ縺ｮ邨・∩蜷医ｏ縺帙〒縺吶・
- `BenchmarkStateManager.ProgressFileName` 縺ｯ `progress.json` 縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1288`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1307`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1362`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1420`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:14`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:26`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:37`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:86`
- `Shared/UI.Core/BenchmarkStateManager.cs:8`

螻･豁ｴ:
- `BenchmarkRunOption` 縺ｯ菫晏ｭ俶ｸ医∩Run縺ｮ陦ｨ遉ｺ蜷阪ヽunRoot縲√・繝ｭ繝輔ぃ繧､繝ｫ縲∝ｮ御ｺ・焚縲∫ｷ乗焚縲¨荳隕ｧ繧剃ｿ晄戟縺励∪縺吶・
- Run陦ｨ遉ｺ蜷阪・Run逡ｪ蜿ｷ縲∽ｽ懈・譌･譎ゅ∝ｮ御ｺ・邱乗焚縺九ｉ逕滓・縺輔ｌ縺ｾ縺吶・

險ｼ諡:
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:65`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:76`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:166`

## Unknown

- 逕ｻ髱｢陦ｨ遉ｺ繝・く繧ｹ繝医・荳驛ｨ縺ｯ迴ｾ陦後た繝ｼ繧ｹ縺ｧ譁・ｭ怜喧縺代＠縺ｦ縺翫ｊ縲∵э蝗ｳ縺輔ｌ縺滓律譛ｬ隱樊枚險縺ｯ螳溯｣・□縺代〒縺ｯ蛻､譁ｭ縺ｧ縺阪∪縺帙ｓ縲・
- UI迥ｶ諷九せ繝医い縺ｮ菫晏ｭ伜・螳ｹ縺ｮ螳悟・縺ｪ繧ｭ繝ｼ荳隕ｧ縺ｯ霑ｽ蜉隱ｿ譟ｻ蟇ｾ雎｡縺ｧ縺吶ら樟譎らせ縺ｧ縺ｯ `SaveUiState` 蜻ｼ縺ｳ蜃ｺ縺励→驕ｸ謚槭・繝ｭ繝代ユ繧｣縺ｮ髢｢菫ゅ∪縺ｧ繧剃ｻ墓ｧ伜喧縺励※縺・∪縺吶・
# Source: Reverse\UI\Benchmark\ScalableDifferences.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/Benchmark/ScalableDifferences.md
evidence_sources:
  - Benchmark/Scalable.UI/Scalable.UI.csproj
  - Benchmark/UI/UI.csproj
  - Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs
  - Benchmark/Shared/UI.projitems
confidence: high
review_status: draft
---

# Scalable UI Differences

## 讎りｦ・

`Benchmark\Scalable.UI` 縺ｯ `Benchmark\UI` 縺ｨ蜷後§蜈ｱ譛蔚I繧剃ｽｿ縺・√い繝励Μ隴伜挨縲∝盾辣ｧ繝励Ο繧ｸ繧ｧ繧ｯ繝医√さ繝ｳ繝代う繝ｫ螳壽焚縲√＠縺阪＞蛟､蜊倅ｽ阪□縺代ｒ蟾ｮ蛻・→縺励※謖√■縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`

## 繝励Ο繧ｸ繧ｧ繧ｯ繝亥ｷｮ蛻・

騾壼ｸｸ迚・
- 繧｢繝励Μ蜷阪・ `Seamless Benchmark` 縺ｧ縺吶・
- `DotNet.Linq.Enumerable.*`縲～Tsavorite.Linq.Enumerable.*`縲～Persisted.*`縲～Tsavorite.Collections` 繧貞盾辣ｧ縺励∪縺吶・

險ｼ諡:
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:35`
- `Benchmark/UI/UI.csproj:36`
- `Benchmark/UI/UI.csproj:37`
- `Benchmark/UI/UI.csproj:38`
- `Benchmark/UI/UI.csproj:39`
- `Benchmark/UI/UI.csproj:40`
- `Benchmark/UI/UI.csproj:41`
- `Benchmark/UI/UI.csproj:42`
- `Benchmark/UI/UI.csproj:43`
- `Benchmark/UI/UI.csproj:44`
- `Benchmark/UI/UI.csproj:45`
- `Benchmark/UI/UI.csproj:46`

Scalable迚・
- 繧｢繝励Μ蜷阪・ `Seamless Scalable Benchmark` 縺ｧ縺吶・
- `SCALABLE_BENCHMARK` 繧貞ｮ夂ｾｩ縺励∪縺吶・
- `Scalable.Persisted.Collections` 縺ｨ `Scalable.Tsavorite.Collections` 繧貞盾辣ｧ縺励∪縺吶・

險ｼ諡:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:35`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:36`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:37`

## 蜈ｱ譛蔚I

荳｡繝励Ο繧ｸ繧ｧ繧ｯ繝医・ `Benchmark\Shared\UI.projitems` 繧偵う繝ｳ繝昴・繝医＠縺ｾ縺吶ゅ％縺ｮ蜈ｱ譛蛾・岼縺ｯ `Shared\UI.Core.projitems` 繧偵＆繧峨↓繧､繝ｳ繝昴・繝医＠縲～Benchmark\Shared\UI\*.cs`縲～App.xaml`縲√・繝ｩ繝・ヨ繝輔か繝ｼ繝蛻･繧ｳ繝ｼ繝峨∫判蜒上Μ繧ｽ繝ｼ繧ｹ繧貞叙繧願ｾｼ縺ｿ縺ｾ縺吶・

險ｼ諡:
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## 縺励″縺・､蜊倅ｽ・

Scalable迚医・ `BenchmarkRunnerViewModel` 縺ｮ partial 繝｡繧ｽ繝・ラ縺ｧ縺励″縺・､險ｭ螳壹ｒ荳頑嶌縺阪＠縺ｾ縺吶ＡMillisecondsScale` 縺ｯ `1000`縲∝腰菴阪・ `s` 縺ｧ縺吶る壼ｸｸ迚医・縺薙・Scalable蟆ら畑partial繧貞性縺ｾ縺ｪ縺・◆繧√〃iewModel譛ｬ菴灘・縺ｮ譌｢螳壼､繧剃ｽｿ縺・∪縺吶・

險ｼ諡:
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:3`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:4`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:5`

## 蜍穂ｽ懈擅莉ｶ蟾ｮ蛻・

Scalable迚医ｂMAUI縲ヾyncfusion縲，ommunityToolkit縲。enchmarkDotNet縲仝orker繧ｳ繝斐・繧ｿ繝ｼ繧ｲ繝・ヨ繧剃ｽｿ縺・∪縺吶る壼ｸｸ迚医→Scalable迚医・縺ｩ縺｡繧峨ｂWindows縺ｨMac Catalyst繧貞ｯｾ雎｡縺ｫ縺励∪縺吶・

險ｼ諡:
- `Benchmark/UI/UI.csproj:3`
- `Benchmark/UI/UI.csproj:4`
- `Benchmark/UI/UI.csproj:8`
- `Benchmark/UI/UI.csproj:24`
- `Benchmark/UI/UI.csproj:25`
- `Benchmark/UI/UI.csproj:26`
- `Benchmark/UI/UI.csproj:27`
- `Benchmark/UI/UI.csproj:28`
- `Benchmark/UI/UI.csproj:29`
- `Benchmark/UI/UI.csproj:30`
- `Benchmark/UI/UI.csproj:49`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:3`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:4`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:8`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:25`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:26`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:27`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:28`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:29`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:30`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:31`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:40`
# Source: Reverse\UI\ParameterSweep\BindingAndLifecycleSpecification.md

---
phase: ui-reverse
reverse_document: true
canonical_target: Document/UI/ParameterSweep/BindingAndLifecycleSpecification.md
evidence_sources:
  - Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs
  - Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs
  - Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj
confidence: high
review_status: draft
---

# Parameter Sweep Binding And Lifecycle Specification

## Current Specification Notice

This reverse document captures historical ParameterSweep binding and persistence behavior. References to CSV persistence are retained only as reverse evidence. Current DB-canonical behavior is defined in `UI.md`.

## ViewModel讒矩

`ParameterSweepViewModel` 縺ｯ `INotifyPropertyChanged` 繧貞ｮ溯｣・☆繧狗判髱｢迥ｶ諷九さ繝ｳ繝・リ縺ｧ縺吶らｵ先棡陦後・ `Rows`縲∝ｱ･豁ｴ縺ｯ `RunHistory` 縺ｮ `ObservableCollection` 縺ｨ縺励※蜈ｬ髢九＆繧後∪縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:28`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:29`

荳ｻ隕√・繝ｭ繝代ユ繧｣:
- `SelectedBaselineRun`: 豈碑ｼ・ｯｾ雎｡縺ｮ驕主悉Run縲ょ､画峩譎ゅ↓ `ApplyBaselineComparison` 繧貞ｮ溯｡後＠縺ｾ縺吶・
- `IsRunning`: 螳溯｡御ｸｭ迥ｶ諷九ょ､画峩譎ゅ↓ `CanRun` 繧る夂衍縺励∪縺吶・
- `CanRun`: `!IsRunning`縲・
- `StatusText`: 繧ｹ繝・・繧ｿ繧ｹ陦ｨ遉ｺ縲・
- `MaxSuccessfulCountText`: 譛螟ｧ謌仙粥莉ｶ謨ｰ蜈･蜉帙・
- `MaxSweepStepsText`: 譛螟ｧ謗｢邏｢繧ｹ繝・ャ繝怜・蜉帙・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:31`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:39`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:43`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:51`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:57`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:68`

## 螳溯｡後Λ繧､繝輔し繧､繧ｯ繝ｫ

蛻晄悄蛹・
- ViewModel繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ縺ｯ繧ｽ繝ｪ繝･繝ｼ繧ｷ繝ｧ繝ｳ繝ｫ繝ｼ繝医∪縺溘・LocalApplicationData驟堺ｸ九°繧・`Work\Tsavorite.ParameterSweep` 繧偵Ν繝ｼ繝医↓縺励※ `ParameterSweepRunStore` 繧剃ｽ懈・縺励∪縺吶・
- 繧ｳ繝ｳ繧ｹ繝医Λ繧ｯ繧ｿ譛ｫ蟆ｾ縺ｧ `RefreshRuns` 繧貞他縺ｳ縲∝ｱ･豁ｴ繧定ｪｭ縺ｿ霎ｼ縺ｿ縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:17`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:23`

螳溯｡・
- `RunFullAsync` 縺ｯ `RunAsync(smoke:false)` 繧貞他縺ｳ縺ｾ縺吶・
- `RunSmokeAsync` 縺ｯ `RunAsync(smoke:true)` 繧貞他縺ｳ縺ｾ縺吶・
- `RunAsync` 縺ｯ螳溯｡御ｸｭ縺ｪ繧我ｽ輔ｂ縺励∪縺帙ｓ縲・
- 螳溯｡碁幕蟋区凾縺ｫ `IsRunning=true` 縺ｨ縺励～Rows` 繧偵け繝ｪ繧｢縺励∪縺吶・
- Smoke螳溯｡後〒縺ｯ繧ｹ繧､繝ｼ繝励こ繝ｼ繧ｹ繧貞宛髯舌＠縺ｾ縺吶・
- 騾ｲ謐励＃縺ｨ縺ｫ `Rows` 縺ｸ邨先棡陦後ｒ霑ｽ蜉縺励～StatusText` 繧呈峩譁ｰ縺励∪縺吶・
- 螳御ｺ・ｾ後・邨先棡繧剃ｿ晏ｭ倥＠縲∝ｱ･豁ｴ繧呈峩譁ｰ縺励∽ｿ晏ｭ伜・繧偵せ繝・・繧ｿ繧ｹ縺ｫ陦ｨ遉ｺ縺励∪縺吶・
- 萓句､匁凾縺ｯ `Failed: <message>` 繧偵せ繝・・繧ｿ繧ｹ縺ｫ陦ｨ遉ｺ縺励∪縺吶・
- finally縺ｧ `IsRunning=false` 縺ｫ謌ｻ縺励∪縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:90`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:92`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:104`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:107`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:110`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:116`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:118`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:120`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:121`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:122`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:128`

## 蜈･蜉帛､

`CreateOptions` 縺ｯ `MaxSuccessfulCountText` 縺ｨ `MaxSweepStepsText` 繧呈ｭ｣縺ｮ謨ｴ謨ｰ縺ｨ縺励※隗｣驥医＠縺ｾ縺吶りｧ｣驥医〒縺阪↑縺・ｴ蜷医・縲√◎繧後◇繧・`65536` 縺ｨ `8` 繧呈里螳壼､縺ｫ縺励∪縺吶４moke縺ｮ蝣ｴ蜷医・ `maxSuccessfulCount` 繧呈怙螟ｧ `256`縲～maxSweepSteps` 繧呈怙螟ｧ `2` 縺ｫ蛻ｶ髯舌＠縺ｾ縺吶ＡLookupProbeCount` 縺ｯ `4096` 縺ｨ `maxSuccessfulCount` 縺ｮ蟆上＆縺・婿縺ｧ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:133`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 豈碑ｼ・Λ繧､繝輔し繧､繧ｯ繝ｫ

螻･豁ｴ譖ｴ譁ｰ:
- `RefreshRuns` 縺ｯ迴ｾ蝨ｨ驕ｸ謚樔ｸｭ縺ｮRunId繧剃ｿ晄戟縺励√せ繝医い縺九ｉRun荳隕ｧ繧貞・隱ｭ霎ｼ縺励∪縺吶・
- 隱ｭ霎ｼ蠕後∽ｻ･蜑阪・驕ｸ謚朿un縺後≠繧後・蜀埼∈謚槭＠縲√↑縺代ｌ縺ｰ蜈磯ｭ繧帝∈縺ｳ縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:94`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:95`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:100`

繝吶・繧ｹ繝ｩ繧､繝ｳ豈碑ｼ・
- `ApplyBaselineComparison` 縺ｯ繝吶・繧ｹ繝ｩ繧､繝ｳ譛ｪ驕ｸ謚槭∪縺溘・迴ｾ蝨ｨ陦後′遨ｺ縺ｪ繧我ｽ輔ｂ縺励∪縺帙ｓ縲・
- 驕ｸ謚朿un縺ｮ邨先棡繧偵せ繝医い縺九ｉ隱ｭ縺ｿ縲∫樟蝨ｨ陦後→豈碑ｼ・＠縺ｦ `Rows` 繧呈ｯ碑ｼ・｡後∈鄂ｮ縺肴鋤縺医∪縺吶・
- 隱ｭ霎ｼ螟ｱ謨玲凾縺ｯ菴輔ｂ陦ｨ遉ｺ螟画峩縺励∪縺帙ｓ縲・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 豌ｸ邯壼喧

`ParameterSweepRunStore` 縺ｯ繝ｫ繝ｼ繝医ョ繧｣繝ｬ繧ｯ繝医Μ驟堺ｸ九↓RunId蜊倅ｽ阪・繝輔か繝ｫ繝繧剃ｽ懊ｊ縲～results.csv` 繧剃ｿ晏ｭ倥＠縺ｾ縺吶３un荳隕ｧ縺ｯ繝・ぅ繝ｬ繧ｯ繝医Μ繧貞・謖吶＠縲～results.csv` 縺ｮ陦梧焚縺九ｉ邨先棡謨ｰ繧堤ｮ怜・縺励∪縺吶りｪｭ霎ｼ縺ｯ `results.csv` 繧旦TF-8縺ｧ隱ｭ縺ｿ縲，SV繝代・繧ｵ縺ｸ貂｡縺励∪縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:5`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:7`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:12`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:18`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:20`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:28`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:45`

CSV蛻励・ `RunId`縲～MeasuredAt`縲～CaseName`縲～Collection`縲～Generic`縲～Backend`縲～Sweep`縲√し繧､繧ｺ鄒､縲∵・蜉滉ｻｶ謨ｰ縲∝､ｱ謨玲ュ蝣ｱ縲、dd/Lookup險域ｸｬ蛟､縺ｧ讒区・縺輔ｌ縺ｾ縺吶・

險ｼ諡:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:50`

## Unknown

- 螳溯｡御ｸｭ縺ｫ繝ｦ繝ｼ繧ｶ繝ｼ縺悟・蜉帶ｬ・ｒ螟画峩縺励◆蝣ｴ蜷医・繝ｭ繝・け縺ｯUI蛛ｴ縺ｧ遒ｺ隱阪〒縺阪∪縺帙ｓ縲ゅ・繧ｿ繝ｳ縺ｮ縺ｿ `CanRun` 縺ｧ蛻ｶ蠕｡縺輔ｌ縺ｦ縺・∪縺吶・
- 螳溯｡後く繝｣繝ｳ繧ｻ繝ｫ讖溯・縺ｯ螳溯｣・ｸ顔｢ｺ隱阪〒縺阪∪縺帙ｓ縲・

