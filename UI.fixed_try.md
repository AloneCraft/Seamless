# UI �d�l������

# Source: Document\UI\UiStorageSpecification.md

# UI Storage Specification

Benchmark�X�i�b�v�V���`E??�ɕۑ����鍶�E��LoopCount�A��?E�o�b�`?E���v�������ԁA����?E����E???E��?E��?E??��?E [Benchmark Measurement Specification](BenchmarkMeasurementSpecification.md) �𐳂Ƃ���AE
## 目皁E

Benchmark�ʏ�UI�AScalable UI�AParameterSweep UI��Run����A���s��ԁA���ʁA��?E??UI�I���Ԃ����SQLite DB�֊�E??E??��B���sUI�ł�CSV�A`progress.json`�A`latest-run.json`�A`benchmark-results.json`�A`benchmark-ui-state.json`�ASave/Save As/Open�A�O��CSV�Ǎ���ۑ��d�l�Ƃ��Ďg��Ȃ�E??E

����?E `Work` �z���Ɏc���Ă�E??��CSV/JSON�t�@�C���͈�?E�̂��ߍ폜���Ȃ�E??�������A���sUI�͋��t�@�C������̈ڍs?E?E??�݊��Ǎ���񋟂��Ȃ�E??E

## DB配置

- 通常: `Work/UI/runs.db`
- fallback: `%LocalApplicationData%/Seamless/Work/UI/runs.db`
- 解決口: `Seamless.UI.Storage.UiRunDatabasePathResolver`

## �ΏۃA�v��

`Runs` �� `Apps` ��Q�Ƃ��A���̃A�v����ʂ�ێ�����AE

- `Benchmark`
- `ScalableBenchmark`
- `ParameterSweep`

## Run���C�t�T�C�N��

����Run���?E���̒ʂ�AE

- `Queued`
- `Running`
- `Paused`
- `Stopped`
- `Completed`
- `Failed`

���?E�ڗ���� `RunStates` �ɒǋL����B`Runs.CurrentStatus` �͍ŐV��Ԃ�ێ�����AEenchmark�ʏ�UI/Scalable UI��Run�J�n�AE??�s�A��~�A�ꎞ��~�A��?E??���s��DB�֕ۑ�����BParameterSweep UI��Run����Ƒ��茋�ʂ𓯂�DB�֕ۑ�����AE

Benchmark�ʏ�UI/Scalable UI�ł́A�N�����Ɂu���݂�SourceTab�ƌ��݂̎��s?E���t�@�C���v�Ɉ�v����ŐVRun���������?E�J���Ƃ��Ċm�F����B�ŐVRun��E`Running` / `Queued` / `Paused` �ŁAE??��ς�BenchmarkCell�ɖ���?Eob���c��ꍇ?EDB��?E��Ԃ� `Paused` �֕␳���ARun�ꗗ�ł�?ERun��I�����B`Stopped`�A`Completed`�A`Failed` �͖���?E??�I?E??�ԂƂ��Ĉ���E???E������E??�Ȃ�E??E

Run�J�n���ɂ́ARunRoot�AE??��ElementCount�ꗗ�AAdaptive臒l�AE??��_��CPU�ꗗ�AE??��BenchmarkCell�A��?E�ς�Benchmark��E??��DB�֕ۑ�����B?E�J���̐��{��SQLite DB�ł���A`ProgressState` JSON�ADB?E`BenchmarkProgressStateJson`�Aprogress�nJSON�t�@�C���͎g��Ȃ�E??E

## �X�L�[�}���{

��ȃ`E?E�u��:

- `SchemaMigrations`: schema version
- `Apps`: UI�A�v�����
- `Runs`: Run単位�E履歴
- `RunStates`: Run状態履歴
- `Cases`: Benchmark/ParameterSweep�̎��s�Ώ�
- `CaseArguments`: ���s�Ώۂ̈����Ɛݒ�l
- `Jobs`: Case�ɑ΂���ʎ�?E
- `Measurements`: ���蔭������
- `MetricValues`: ����l
- `Comparisons`: ��?E??�ʃL���`E??��
- `UiSelections`: �Z���A�s�ACPU�Ȃǂ̑I���?E
- `UiViewStates`: �I��^�u�AE??��N�Ȃǂ̌y�ʕ\����?E

�\���Ɣ�?E?E���{�� `Runs`�A`Cases`�A`CaseArguments`�A`Jobs`�A`Measurements`�A`MetricValues` �Ƃ���B`Comparisons` ��DB?E?E��������E??�L���`E??���ł���A��?E??���� `MetricValues` ����č\�z�\�ł��邱�Ƃ�O��ɂ���AE

Benchmark�ʏ�UI/Scalable UI�̕ۑ��P��?E�A�\����?E�y�A�s�� `SourceTab + �s�L�[ + ��(ElementCount/LoopCount)` �Ƃ��Ĉ���E??����E???E `Cases` / `Jobs` �̐��K����ێ����A`RowKey` �ƍ��E�� `Side`�A`Project`�A`TypeFullName`�A`ArgumentKey` / `Arguments` �œ����\���s?E���E�Z����Ή��t����B�eJob�ɂ� `Status`�A`MeanNanoseconds`�A`AllocatedBytes`�AE???E�I?E??���A���s���R�A�_��CPU�ARandomSeed�AResultDigest�AResultCheckText ��ۑ����A��������?E??�E������?E??�Б����s������\���s�L�[�֕�?E??�V����AE

Schema version �s��v��?E��Run�݊���s�킸�A`runs.db`�A`runs.db-wal`�A`runs.db-shm` ��폜���Č��sschema�ōč�?E����B������݊��͎����Ȃ�E??E

## 行単位CPU固宁E

Adaptive���s�ł́AUI�̍s�O���[�s���O�Ɠ����L�[�Ŏ��s�s����AE�̕\���s��1�̘_��CPU����蓖�Ă�B�����s?E���v���W�F�N�`E�E�v���W�F�N�g?E�����_��CPU�ŕ�E??�Ɏ��s����B���s��E?E�u���v���W�F�N�g?E�� ?E�E�v���W�F�N�g?E���v�Ƃ��A�Б������s���Ă���Α��͓���CPU�ő��s����B�Б������I�����Ă�E??�s��s�P��CPU����AE

Adaptive臒l��E??�ɂ��ł�?E??�́A���v���ł͂Ȃ���?E??��E?E�X�L�`E?E�\���Ƃ��Ĉ���E??���E�̑ł�?E??����?E�Ɨ�?E�܂܈ێ�����AE

UI��?ECPU�\���� `�^`�A`�x���`?E�[�N` �̒���ɂ��� `CPU` ��֊�E??E??��B�e�v���W�F�N�g?E���ɂ̓`�F�`E??�A��ԁA������c���ACPU�A�C�R����CPU�ԍ��͕\�����Ȃ�E??EPU�Z���̓R�A��ʃA�C�R���Ƙ_��CPU�ԍ���\�����A���J�n�s?E��?E??����BTooltip�� `P core / CPU 8` �̂悤�ȏڍז��ɂ���AEPU��?E�\�[�g�ΏۂŁA��?E?E��ɖ����ɕ��ׂ�B�N���`E??��?E�s�I��Ƃ��Ĉ���E??Run�ꗗ��E??�u�i��?E `��?E�I��` �J�E���g?E�]���ʂ荶�E�Z���P�ʂŐ�����AE

## 役割刁E��

- `Shared/UI.Storage`: SQLite�̒჌�x����?E�Ǎ���ՁAEB�p�X����Aschema��?E�ARun/Case/Job/Metric/Comparison/UI���?ECRUD��S������AEenchmark�ŗL?E�\�����`E??��UI���f�͎����Ȃ�E??E
- `UiRunStore`: `Shared/UI.Storage` �̒��SAPI�B����l�ۑ�?E����Job�̊���Measurement��u�����AE???E??��������AE
- `BenchmarkUiRunRepository`: Benchmark�ʏ�UI/Scalable UI�ŗL?EDB�ϊ��w�B`UiRunStore` �� `BenchmarkResultItem` / `BenchmarkRunSession` �̕ϊ��ASQLite�������݃L���[�ARun����E??�O�񌋉ʌ�����S������AE
- `BenchmarkRunSession`: Benchmark UI?E?E�B��̃��C�u���ʐ��{�B�L�[�� `SourceTab + Project + TypeFullName + Method + ArgumentKey + ElementCount`�AE
- `BenchmarkRunProjectionService`: `BenchmarkRunSession` �܂�?EDB����E??�ʂ��� `ResultItems`�A`VisibleResultItems`�A��?E??�A�\�Z���K�p�p�`E?E�^��h������AE
- `BenchmarkRunnerViewModel`: �R�}���h�A��ʑI���ԁAE??�m�A�T�[�r�X�Ăяo����?E??����B`ResultItems` / `VisibleResultItems` ��Binding�p�o�̓o�`E??�@�ł���A���{�ł͂Ȃ�E??E

## UI����E??�[��

- Run�ꗗ�� `UiRunStore.ListRuns(Benchmark/ScalableBenchmark/ParameterSweep)` ����\�z����AE
- Benchmark�ʏ�UI/Scalable UI�̌��ʕ\���A���𕜈�E??��?E??�A?E�J�p��ProgressState��DB���畜��E??��AE
- �^�u?E�֎�?EDB�Ǎ���胁������?E `BenchmarkRunSession` ��D�悵�A���߃��C�u���ʂ����Ȃ�E??E
- �v���ς݃Z���� `Completed` �\����D�悵�A�^�u�ړ������� `Waiting` �\���֖߂��Ȃ�E??E
- �I���ԃL�[��SourceTab��܂߁A�ʃ^�u?E����?E����Project�֘R��Ȃ�E??��E??����AE
- �I?E??�F?E��Save������o�����A�u�j?E??�ĕ���v�Ɓu�L�����Z���v��?E??����E??E

## ���ۑ��`��?E����E

��Run�݊��͕s�v�Ƃ���AESV�A`progress.json`�A��UI��ԃt�@�C���AParameterSweep�� `results.csv` �͌��s�ۑ��d�l?E���͂ɂ�?E�͂ɂ�g��Ȃ�E??���t�@�C�������[�J���ɑ��݂��Ă�폜�����ADB���{��Run����Ƃ͕ʕ��Ƃ��Ĉ���E??ParameterSweep�͋�����݊�������Ȃ�E??�Afresh DB��Run��?E�A���ʕۑ��A����Ǎ��Abaseline��?E??��E??�ł����Ԃ�ێ�����AE
# Source: Document\UI\RebuildImplementationStatus.md

# UI Rebuild Implementation Status

## 完亁E

- ����SQLite�ۑ�Project `Shared/UI.Storage` ��ǉ����ABenchmark�ʏ�UI�AScalable UI�AParameterSweep UI�ATest project����Q?E�����AE
- DB schema��E`Run`�A`Case`�A`Job`�A`Measurement`�A`Metric`�A`Comparison`�A`UiSelection`�A`UiViewState` �֐��K�������AE
- Benchmark�ʏ�UI/Scalable UI��Run�ꗗ�A���ʕ���E??��?E??�A?E�J�psnapshot��DB���{��?E??�ւ����AE
- Benchmark�ʏ�UI/Scalable UI��Toolbar���� `Save`�A`Save As`�A`Open`�A�O��CSV�Ǎ�������폜�����AE
- `BenchmarkCsvDiscoveryService`�A`BenchmarkRunPersistenceService`�A`BenchmarkResultWriter`�A`CsvStringBuilderExtensions`�A`IBenchmarkCsvService`�A`BenchmarkCsvService`�A��UI interaction�ۑ��o�H��폜�����AE
- Core�� `BenchmarkOrchestrator` ��CSV���ʕ��_?E??�������Ain-memory `ProgressState` ��progress update��Ԃ��݌v�֕ύX�����AE
- ParameterSweep UI�̋�CSV�ۑ���폜���ARun����A���ʕۑ��Abaseline��?E??��E??DB�ۑ��֓��ꂵ���AE
- Benchmark�J�n����UI�t���[�Y�����邽�߁ASQLite�������݂�Repository���̒���o�`E??�O���E���h�L���[�֕����߂��AE
- �^�u���A��?E���ʏ����Ɛ�E??�\��������C�����A���C�u���ʂ� `BenchmarkRunSession` ���畜��E??��悟E??�����AE
- �I?E??�F�Łu�L�����Z���v�������o���Ԃ�C�����A���ۑ����ʂ�����ꍇ�����I�����\������悤�ɂ����AE
- `BenchmarkRunSession`�A`BenchmarkRunProjectionService`�A`BenchmarkUiRunRepository` ��Core���̃`E??�g�\��?E??�֐�?E??���AE
- `MemoryOnlyDevice` �� `TsavoriteCheckpointCapacity` �̊����e�X�g���s��A?E�̃e�X�g�O���[����?E�t���C���Ƃ��Ĕ��f�����AE
- Adaptive���s?ECPU������\���s�P�ʂ֕ύX���A�����s?E���v���W�F�N�`E�E�v���W�F�N�g�������_��CPU�ŕ�E??�ɑ���悟E??�����AE
- Benchmark�\��CPU�\����?E���W�F�N�g?E����O���A`�^`�A`�x���`?E�[�N` �̉E�ɍs�P��?E `CPU` ���ǉ������AEPU��?E�R�A��ʃA�C�R���A�_��CPU�ԍ��ATooltip�A�\�[�g�A�s�I��N���`E??����AE
- Run�J�n����RunRoot�AE??��ElementCount�AAdaptive臒l�AE??��CPU�AE??��BenchmarkCell�A��?E�ς�Benchmark��E??��DB�֕ۑ�����悟E??�����AE
- `BenchmarkProgressStateJson` / progress�nJSON��?E�J���{�Ƃ��Ďg��E??�H��폜���ADB��Run�ݒ�ACases�AJobs�AMetrics�AUiSelections����ĊJ�p `ProgressState` ��?E�\�z����悟E??�����AE
- �N����Run�ꗗ�X�V�ŁA���݂�SourceTab/���s?E���t�@�C���Ɉ�v����ŐV�� `Running` / `Queued` / `Paused` Run�ɖ���?E??��Z�����c��ꍇ�A?E���� `Paused` �Ƃ��đI�����悟E??�����B`Stopped`�A`Completed`�A`Failed` �͎�������E??�ۊO�AE
- UI DB schema version��グ�A�� `runs.db*` �݊���j?E??�Č��sschema�ōč�?E������j�֍X�V�����AE

## 検証済み

- `dotnet build Shared\UI.Storage\UI.Storage.csproj -v:minimal`
- `dotnet build Benchmark\UI\Benchmark.UI.csproj -v:minimal`
- `dotnet build Benchmark\Scalable.UI\Scalable.UI.csproj -v:minimal`
- `dotnet build Benchmark\Tsavorite.ParameterSweep.UI\Tsavorite.ParameterSweep.UI.csproj -v:minimal`
- `dotnet test Test\Test.csproj -v:minimal`
- `dotnet test Test\Test.csproj --filter "FullyQualifiedName~BenchmarkOrchestrator" -v:minimal`
- `dotnet test Test.Tsavorite.ParameterSweep\Test.Tsavorite.ParameterSweep.csproj -v:minimal`
- 3 UI EXE�̒Z���ԋN���X���[�N

## �ǉ��ς݉�A�`E??�`E

- `BenchmarkRunSession`: ����L�[Upsert�u���ASourceTab�ʎ擾�AElementCount����E??ProgressState��?E�AE
- `BenchmarkRunProjectionService`: �I��N���e�A��?E??�ʂ���?E��?E??�����A�ŐV���ʕ���E??E
- `BenchmarkUiRunRepository`: temp SQLite DB��Run����E??Measurement�u���AScalable�� `TypeFullName` / `ArgumentKey` �ێ��AE
- `BenchmarkOrchestrator`: �\���s�P�ʂō��E������CPU�ɂȂ�A�s���Ƃ�CPU��?E�U����A�s?E��E??�����S�����E�S���ɂȂ邱�ƁAE
- `UiRunStore` / Run����E fresh schema��?E�AProgressState JSON��ۑ����Ȃ�E??�ƁADB����Run�ݒ�ƌ��ʂ𕜈�E??�邱�ƁAE
- Benchmark UI�\��: �sCPU�\��������ƁA?E���W�F�N�g?E����CPU�\���Ɉˑ����Ȃ�E??�ƁAE
- Close logic: ���ۑ���Ԃŕ���I����ƃL�����Z����?E�邱�ƁAE

## 残件

- ��E??��UI��������e�X�g�����B?E�̓I�ɂ́ABenchmark�J�n�A�^�u?E�ցA���𕜈�E??CPU��\�[�g�A��?E??���A�I?E??�F�����ʑ���Ō��؂���AE
- DB��?E??�v?E����?E���K����ێ����A`RowKey` �ƍ��E `Side` �Ńy�A�s��\?E���Ă�E??�B��?E�ȁu���E���1��?E??�R�[�h�Ɏ��v�`��?E??�ɂȂ����ꍇ?E�A�ǉ��}�C�O���[�V�����ł͂Ȃ�fresh schema�O��ōĐ݌v����AE

## ��?E???E??��

- `Artifacts/UiSmoke` �̃X�N���[���V���`E??�̓��[�J�����؏ؐՂł���A�ŏI��?E??�͊܂߂Ȃ�E??E
- ��?E`Work` �z��?E��CSV/JSON�͍폜���Ȃ�E??E
- ��schema�� `Work/UI/runs.db*` �͌݊��ΏۊO�ł���A���sschema��?E??�ւ���^�C�~���O�ō폜/�č�?E����AE
- `csv` ?E???E�̎c��?E�uCSV����������Ȃ�E??�Ɓv����؂���`E??�g�A�܂�?EReverse�z��?E��j?E??�Ղ������e����AE
# Source: Document\UI\BenchmarkMeasurementSpecification.md

# Benchmark Measurement Specification

## 目皁E
�ʏ�Benchmark UI��LoopCount�n�^�u�ɂ����āAFLinq��ZLinq��?E���ȏ���ő��肵�A�w�葪�莞�Ԃ֓��B����O�Ɂu��?E��?E??�ƕ\������邱�Ƃ�h�~����AE
�{�d�l?E `BenchmarkWorkloadAxis.UsesLoopCount` ���^�ƂȂ��?E??�ɓK�p����BScalable�n�^�u?E�v�f���ł�?E??�K���͕ύX���Ȃ�E??E
## 用誁E
- ���莞?E Worker���Ԃ���?E�o�b�`?E��?E���v�������ԁB�P��?Ens�ŕۑ�����AE- �\������: ���v�������Ԃ�LoopCount�ŏ�����1�񂠂���̎��ԁB�\�ł�ns�Aus�Ams���ɐ��`���ĕ\������AE- ��E??���莞?E UI�́u���莞�ԁv�őI����ꂽ�l�AE024 ms��I������ꍇ?E `1,024,000,000 ns` �Ƃ���AE- ��?E��?E ������?E???EFLinq��ZLinq���A����LoopCount�̍ŏI�o�`E??�ŁA�Ƃ�Ɏ�E??���莞�Ԉȏ�ƂȂ�����ԁAE
�\�����ԂƔ���p�̍��v�������Ԃ͕ʂ̒l�ł���B�\�������\ns�ł����Ă�A`�\������ AELoopCount` �ɑ�������ۑ��ςݍ��v�������Ԃœ��B���肷��AE
## �����E??E
1. ���s�P�ʂ�u��?E??E?ELoopCount ?E���E�P�[�X�v�Ƃ���AE2. ������?E???EFLinq��ZLinq�́A�e�i�K�ŏ�ɓ���LoopCount��g�p����AE3. ���E�����̐������ʂ������Ă���A�e���v�������Ԃ�w�葪�莞�ԂƔ�?E??��AE4. ?E?E�ł�w�葪�莞�Ԗ����Ȃ�LoopCount��E�{�ɂ��A����LoopCount��FLinq��ZLinq�̗�����?E���肷��AE5. ����LoopCount�ō��E�Ƃ�w�葪�莞�Ԉȏ�ɂȂ����ꍇ�����A���̔�?E??���?E��?E??����AE6. `int.MaxValue` ��LoopCount�ł�Б����w�葪�莞�Ԃ֓͂��Ȃ�E??��?E�A���R��L�^���Ď��s�Ƃ���AE
?E?E��?E��臒l�֓��B���Ă�A���̑�������ȑO?ELoopCount�̌��ʂŊm�肵�Ă͂Ȃ�Ȃ�E??���i�K�ł͓��B�ςݑ���܂߂č��E�Ƃ�?E���肷�邽�߁A��?E��?E??�̍�?EoopCount��?E??��v����AE
## ��Ԋ�?E??�ĊJ

- ���ILoopCount�́ASourceTab�AType�AMethod�AArgumentKey�A��?E?E�A�Ŏ��ʂ�����?E??�P�ʂŊ�?E??��AE- �����?E??�ő�������LoopCount��A�ʃ��\�`E??�A�ʈ����A�ʔ�?E???E�J�n�l�Ƃ��ċ��L���Ȃ�E??E- `ProgressState.ElementCounts` �ɂ̓��[�U�[���I�����?E����������ێ����A���s���ɐ�?E�������ILoopCount��ǉ����Ȃ�E??E- ���f�E�ĊJ��?E�A�s�ŗL?E������獶�E�Ƃ�?E�����Ă�E??�ő�̋���LoopCount��m�F���A���̒i�K?E�����A�����B�Ȃ玟��LoopCount�֐i�ށAE- ?E?E�����ۑ����ꂽ�i�K?E��?E��?E??�͂Ȃ�E???E�J��ɓ���LoopCount�̕s�����𑪒肵�A���E�������Ă��画�肷��AE- ?E??CPU�Ŏ��s���Ă�A���ILoopCount�͍s�L�[�ɕ��A�s�Ԃŋ��L���Ȃ�E??E
## ��?E��?E?E?E??���

LoopCount�n�̔�?E???E�A������ׂĖ������ꍇ������?E��?E??����AE
- �s������ΏۂƂ��đI�����Ă�E??�AE- FLinq��ZLinq�̗�����?E�����Ă�E??�AE- ���E��LoopCount������ł���AE- ���E���ꂼ��̍��v�������Ԃ��ۑ�����Ă�E??�AE- ���E���ꂼ��̍��v�������Ԃ��w�葪�莞�Ԉȏ�ł���AE
���̏�Ԃ��犮?E��?E??�⊮�܂�?E?E??���Ă͂Ȃ�Ȃ�E??E
- ����l��E���ȏ㑶�݂��邾��?E��?E- �ʏ�i������?E??������?E��?E- ?E?E��������E??���莞�Ԃ֓��B������?E- ���ݕۑ�����Ă�E??�ő厲��A�v�����ꂽ�ŏI���Ƃ݂Ȃ�����?E- ���s�A��O�A�^�C���A�E�`E- LoopCount�܂�?E���v�������Ԃ𕜈�E??���Ȃ�E??�`���X�i�b�v�V���`E??

## �ۑ��ƕ���E
- �X�i�b�v�V���`E??�͍��E���ꂼ��� `LoopCount` �� `TotalNanoseconds` ��ۑ�����AE- ��?E��?E??��ۑ�����Ƃ�?E�A���E��?E�����ALoopCount����v���A�����̍��v�������Ԃ��w�葪�莞�Ԉȏ�ł��邱�Ƃ���؂���B�s�������͕ۑ�?E?E??���s������AE- ����E???E��?E��?E??��?E�A�ۑ����ꂽLoopCount�A���v�������ԁA�X�`E?E�^�X����Čv�Z����AE- ���`����LoopCount�܂�?E���v�������Ԃ��Ȃ�E??�ʂ͑���l�Ƃ��ĕ\���ł��邪�A��?E��?E??�͂��Ȃ�E??E- Schema version 15�ł� `BenchmarkSnapshotMeasurement` �ɍ��E�ʂ�LoopCount�ƍ��v�������Ԃ�ۑ�����AE
## 回帰履歴

### 2026-07-10: `Any` ��E024 ms�����B�Ŋ�?E��?E??���s?E��E
System.Array�� `Any` ��LoopCount 524,288�ŁAFLinq�̍��v�������Ԃ�E4.8 ms�AZLinq��E3.2 ms�������B�����Ƃ�w�肳�ꂽ1024 ms�֓͂��Ă�E??��E??�������炸�A����l�̑��݂ƒʏ�i����?E??��?E��?E???E??����UI�⊮�ɂ��A�s�ƃ^�u����?E��?E??�Ȃ����AE
�����ɁA���ILoopCount��E`ProgressState.ElementCounts` �֒ǉ����đS�s�ŋ��L���Ă�E??���߁A?E�s�s���ǉ�����524,288��㑱?E `Any` ��?E���ő厲�Ƃ��Ď󂯎�����B��?E??����J�n�������͈�E?E�����ő厲�Ƃ̈�v��v�����Ă�E??���߁A`Any` ��524,288��1�񂾂��Ŕ{����I?E??���AE
�΍�Ƃ��āA���ILoopCount���?E??���[�J���ɕύX���A���E����LoopCount�ł̍��v���Ԕ���ɓ��ꂵ���B����l�̑��݁AE??��i����?E??�Б�臒l���B�ɂ�銮?E��?E??��?E�폜�����AE
## ����E???E��A�h�~���

- ��s�s?E���ILoopCount���㑱�s�։e�����Ȃ�E??�ƁAE- �eLoopCount�ō��E������LoopCount��g�p���邱�ƁAE- ?E?E�������B�������i�K�ł���E�Ƃ���s����邱�ƁAE- ns�P��?E�\�����Ԃł͂Ȃ����v�������ԂŔ��肷�邱�ƁAE- ���E�̓��BLoopCount���قȂ�ꍇ?E��?E��?E??���Ȃ�E??�ƁAE- ���s���ʂ�E??�v����?E������������E??�ʂ��?E��?E??���Ȃ�E??�ƁAE
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

## �Ώ�

��?E?E??�� `Benchmark\UI` �� `Benchmark\Scalable.UI` ��?E�L����EMAUI �x���`?E�[�N���sUI�̌���d�l��A����E??�畜��E??����̂ł��B�ʏ��?E�A�v����E`Seamless Benchmark`�AScalable��?E `Seamless Scalable Benchmark` �Ƃ��č\?E����܂��B�����Ƃ�E`Benchmark\Shared\UI.projitems` �o�R�œ������LUI�R�[�h���荞�݂܂��AE

証拠:
- `Benchmark/UI/UI.csproj:3`
- `Benchmark/UI/UI.csproj:8`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:3`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:8`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## 画面構�E

Benchmark UI �͒P��� `ContentPage` �ł��B�R���X�g���N�^�Ń^�C�g���A�w�i�F�A`BenchmarkRunnerViewModel`�A�c�[���o?E�A���C���R���`E??�`E??��?E���AE�s?E���[�`E`Grid` �ɔz�u���܂��B��i�̓`E?E���o?E�A���i�̓x���`?E�[�N����̈�ł��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:10`
- `Benchmark/Shared/UI/MainPage.cs:72`
- `Benchmark/Shared/UI/MainPage.cs:80`
- `Benchmark/Shared/UI/MainPage.cs:81`

主要E��域:
- �`E?E���o?E: ���s�A��~�A�ꎞ��~�A�ۑ��A���O��t���ĕۑ��AE??���A�������l�A���s?E���t�@�C���A���s�����\�����܂��AE
- �\�[�X�^�`E �x���`?E�[�N�Ώۂ̔�?E??�`E??����?E��ւ��܂��AE
- Benchmark �����^�`E CPU�ALog�AImprovement Prompt ��?E��ւ��܂��AE
- �x���`?E�[�N�\: ���\�`E??�s�A���E�v���W�F�N�g?E�I?E��?E�v���l�A������A�䗦���\�����܂��AE
- �`���[�`E �I������v���l����E��?E??��?E�X�P�[�����ڂŕ\�����܂��AE
- CPU �\: �_��CPU��P core�AE core�AUnknown core�P�ʂŕ\�����A���s�Ώ�CPU��I����܂��AE
- �_�C�A���O: �I?E??���ۑ��A�㏑���A�ꎞ��~������?E�m�F��\�����܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:836`
- `Benchmark/Shared/UI/MainPage.cs:980`
- `Benchmark/Shared/UI/MainPage.cs:1124`
- `Benchmark/Shared/UI/MainPage.cs:1310`
- `Benchmark/Shared/UI/MainPage.cs:1448`
- `Benchmark/Shared/UI/MainPage.cs:2474`
- `Benchmark/Shared/UI/MainPage.cs:2511`
- `Benchmark/Shared/UI/MainPage.cs:2558`

## �`E?E���o?E�d��E

�`E?E���o?E�� `ImageButton` ��?E??�\?E����܂��B?E�^���̓��[�����Ƃɍ�?E����A�N���`E??���� `HandleToolbarButtonClickAsync` ��Ăт܂��BRun �{�^������?E�`E?E���`�b�v�� `RunButtonTooltipText` �Ƀo�C���h����܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:462`
- `Benchmark/Shared/UI/MainPage.cs:604`
- `Benchmark/Shared/UI/MainPage.cs:618`
- `Benchmark/Shared/UI/MainPage.cs:628`
- `Benchmark/Shared/UI/MainPage.cs:672`

�`E?E���o?E���:
- ������E??Picker�� `ThresholdOptions` �� `SelectedThreshold` �Ƀo�C���h����A?E���`?E�[�N����ύX�\�������L���ł��AE
- ���s?E���t�@�C��Picker�� `ExecutionProfiles` �� `SelectedExecutionProfile` �Ƀo�C���h����A?E���`?E�[�N����ύX�\�������L���ł��AE
- ���s����Picker�� `RunOptions` �� `SelectedRunOption` �Ƀo�C���h����܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:495`
- `Benchmark/Shared/UI/MainPage.cs:496`
- `Benchmark/Shared/UI/MainPage.cs:504`
- `Benchmark/Shared/UI/MainPage.cs:505`
- `Benchmark/Shared/UI/MainPage.cs:506`
- `Benchmark/Shared/UI/MainPage.cs:514`
- `Benchmark/Shared/UI/MainPage.cs:515`

## �x���`?E�[�N�\�d��E

�x���`?E�[�N�\�� Syncfusion `SfDataGrid` �ō\�z����A`BenchmarkRows` ��EItemsSource �ɂ��܂��B�s���ƂɑI��`�F�`E??�A���E�v���W�F�N�g�Z���A�X�`E?E�^�X�ACPU�\���A���s�e�L�X�g�A���s�e�L�X�g�A�v���l�A�c�[���`�b�v��\�����܂��AE

証拠:
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

選択操佁E
- �s�P�ʁA?E�O���[�v�P�ʁA���E�Z���P��?E�I���Ԃ�����܂��AE
- �w�b�_�[�̊�E??E??�F�`E??�� `IsAllBenchmarkSelectionChecked` �܂�?E `BenchmarkColumnSelectionGroup.IsChecked` ��EOneWay �ŕ\�����A�^�`E?E����ViewModel���̑I�𑀍��Ăт܂��AE
- �`���[�g��?E?E�o?E��^�`E?E����ƁA�Ή�����?E���`?E�[�N�s�܂�?E���E�Z����I����܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:1798`
- `Benchmark/Shared/UI/MainPage.cs:1802`
- `Benchmark/Shared/UI/MainPage.cs:1816`
- `Benchmark/Shared/UI/MainPage.cs:1820`
- `Benchmark/Shared/UI/MainPage.cs:1823`
- `Benchmark/Shared/UI/MainPage.cs:1830`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3331`

## �`���[�g�d��E

�`���[�g?E Syncfusion `SfCartesianChart` �� `StackingColumn100Series` �ō��E��?E??�\�����܂��B�n��?E ItemsSource �� `ChartPoints` �ł��B������ `ChartHeight` �Ƀo�C���h����܂��B�`���[�g�ɂ͓Ǝ��� `GraphicsView` �I�[�o?E���C���d�˂��AE??��g�A���x���A�q�`E??�`E??�g��?E??���܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:3114`
- `Benchmark/Shared/UI/MainPage.cs:3138`
- `Benchmark/Shared/UI/MainPage.cs:3154`
- `Benchmark/Shared/UI/MainPage.cs:3174`
- `Benchmark/Shared/UI/MainPage.cs:3209`
- `Benchmark/Shared/UI/MainPage.cs:3266`
- `Benchmark/Shared/UI/MainPage.cs:3306`
- `Benchmark/Shared/UI/MainPage.cs:3331`

�`���[�g?E�\�����[�h?E `BenchmarkChartMode.ElementComparison` �� `BenchmarkChartMode.ScaleTrend` �ł��B�w�W?E `Time` �� `Allocated` �ł��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:18`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:23`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`

## CPU選択仕槁E

CPU�\���� `CpuSections` ��?E�ɍ���܂��BViewModel��CPU�g?E���W��ǂݍ��݁AP core�AE core�AUnknown core �̃Z�N�V�����֍č\�z���܂��BUI��ł̓Z�N�V�����A�s�A?E�A��CPU�P�ʂőI��ł��܂��B���Ȃ��Ƃ�E��CPU���I������悤��ViewModel���␳���܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:229`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:230`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:203`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3594`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:750`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:758`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:767`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:780`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3676`

## �_�C�A���O/�I?E??��E

�I?E??�� `RequestCloseAsync` �����s���A��~���A���ۑ����ʂ̗L����m�F���܂��B?E??�ȏꍇ?E�Ǝ��I�[�o?E���C�̊m�F�_�C�A���O��\�����AE??��ɉ����Ē�~�A�ۑ��A�j?E??�L�����Z����s���܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:122`
- `Benchmark/Shared/UI/MainPage.cs:141`
- `Benchmark/Shared/UI/MainPage.cs:145`
- `Benchmark/Shared/UI/MainPage.cs:155`
- `Benchmark/Shared/UI/MainPage.cs:255`
- `Benchmark/Shared/UI/MainPage.cs:269`

�t�@�C��/�t�H���_��?E
- `Open` ��CSV�܂�?E�t�H���_��I��ł��܂��AE
- CSV�̓v���`E??�t�H�[���ʂ̃t�@�C���^�C�v�w�������܂��AE
- �ۑ�?E/�G�N�X�|?E�g?E�̓t�H���_Picker��g��E??���AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:413`
- `Benchmark/Shared/UI/MainPage.cs:421`
- `Benchmark/Shared/UI/MainPage.cs:449`

## Visual Verification

2026-07-01�̌��؂ŁAMAUI�A�v���N���ƃX�N���[���V���`E??�擾����s���܂������AE??��UI�v���Z�X�� `MainWindowHandle=0` �̂܂܃E�B���h�E��?E�ł����A�X�N���[���V���`E??�͖��擾�ł��BScalable UI�͊���?E���Z�X��?E�̓t�@�C������`E??���Ă�E??�������A������ `MainWindowHandle=0` �ŉ�ʏؐՂƂ��ė��p�ł��܂���ł����AE

## Unknown

- ���s��ʂ̃X�N���[���V���`E??�ƍ�?E����?E??���AEAUI�A�v�����ʏ�̃g�b�v���x���E�B���h�E�Ƃ��Č�?E�ł����?E??�Ď擾��?E??�ł��AE
- �����\�[�X?E???E??�������Ă�E??���{��\����?E??����܂��B����?E����E??���Ƃ��ċL�^���A��?E??�f�� `UI.md` ��?E??���܂��AE


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

## �Ώ�

��?E?E??�� `Benchmark\Tsavorite.ParameterSweep.UI` �̌���UI�d�l�����E??�畜��E??����̂ł��B���̃A�v����MAUI�P��y?E�W�A�v���ŁA�^�C�g���� `Tsavorite Parameter Sweep` �ł��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`

## 画面構�E

��ʂ� `ContentPage` �ŁA�R���X�g���N�^�� `ParameterSweepViewModel` ��E`BindingContext` �ɐݒ肵�A`CreateContent` �ō�����R���`E??�`E??�\�����܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:3`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:10`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`

��ʂ�3�s?E `Grid` �ł��AE
- 1�s��: �`E?E���o?E�AE
- 2�s��: �X�`E?E�^�X�AE
- 3�s��: ���X�N���[���\�Ȍ��ʕ\�AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:13`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:67`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:69`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`

## �`E?E���o?E�d��E

�`E?E���o?E�ɂ͈ȉ�?EUI�v�f������܂��AE
- `Run sweep` �{�^��: `CanRun` �ɗL����Ԃ�o�C���h���A�N���`E??�� `RunFullAsync` ����s���܂��AE
- `Smoke` �{�^��: `CanRun` �ɗL����Ԃ�o�C���h���A�N���`E??�� `RunSmokeAsync` ����s���܂��AE
- `Refresh runs` �{�^��: �N���`E??�� `RefreshRuns` ����s���܂��AE
- `MaxCount` ����?E `MaxSuccessfulCountText` �Ƀo�C���h���܂��AE
- `Steps` ����?E `MaxSweepStepsText` �Ƀo�C���h���܂��AE
- ����Picker: `RunHistory` ��ItemsSource�A`SelectedBaselineRun` ��SelectedItem�Ƀo�C���h���܂��AE

証拠:
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

## 結果表仕槁E

���ʕ\�� `CollectionView` �ŁA`Rows` ��ItemsSource�ɂ��܂��B?E�`E??�[�ƙ�E???E������?E??�`�� `Grid` ��g��E??���B�s�I��?E�����ł��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:70`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:73`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:75`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:129`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:153`

刁E
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

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:105`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:118`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:149`

��E??���� `ParameterSweepComparisonViewRow` �̓����܂�?E�Ή�?E���p�e�B�� `Label.Text` ��o�C���h���܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:198`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:206`

## Visual Verification

2026-07-01�̌��؂ŁAMAUI�A�v���N���ƃX�N���[���V���`E??�擾����s���܂������AE??��UI�̃E�B���h�E��?E���s���_�ŏ�?E??��~�������߁AParameterSweep UI�̃X�N���[���V���`E??�͖��擾�ł��AE

## Unknown

- ��?EUI�ɂ͖���?E??�G���[�_�C�A���O��E??�����Z������?E����E??�m�F�ł��܂���B���s?E `StatusText` �ɕ\������܂��AE
- �X�N���[���V���`E??�ƍ�?E����?E??���AEAUI�A�v�����ʏ�̃g�b�v���x���E�B���h�E�Ƃ��Č�?E�ł����?E??�Ď擾��?E??�ł��AE
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

## 1. ����ȃR�[�h�r�n�C���h��ViewModel

`Benchmark\Shared\UI\MainPage.cs` �͉�ʐ�?E�A�C�x���g?E���A�_�C�A���O�A�`���[�g�ACPU�\�A�f�[�^�O���`E??�񐶐��A�R���o?E�^�A�`��I�[�o?E���C��E�t�@�C���ŕ����Ă�E??���B`BenchmarkRunnerViewModel.cs` ����s����A��?E�Ǎ��AE??���ԁA�`���[�g�����ACPU�I��A���ʕϊ���E�N���X�Ɋ�E??E??�Ă�E??���BUI������ł́A��ʍ\�z�A����n���h���A�`��⏕�A��ԕϊ��A�i�����A�g��?E??������ł��AE

証拠:
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

## 2. �\��?E??��?E??����E

Benchmark UI �̓��{��\��?E???E��?E??�������c���Ă�E??���B���[�U�[�ɕ\�������\�������邽�߁A�d�l���ł͌��󎖎��Ƃ��ċL�^���A�C���ΏۂƂ��Ĉ���E??���ł��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:3237`
- `Benchmark/Shared/UI/MainPage.cs:215`
- `Benchmark/Shared/UI/MainPage.cs:390`

## 3. �R�[�h����UI�̃o�C���`E??���O���菑���ŎU��

Benchmark UI ��XAML�ł͂Ȃ�C#�R�[�h��UI��\�z���A������ `SetBinding` �ƃC�x���g�n���h����ʂɐݒ肵�Ă�E??���B�o�C���`E??���O�o�H����ʍ\�z�R�[�h�ɎU�݂��Ă�E??���߁A�ύX���Ɉˑ��֌W������Ƃ��₷���\���ł��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:494`
- `Benchmark/Shared/UI/MainPage.cs:1470`
- `Benchmark/Shared/UI/MainPage.cs:1987`
- `Benchmark/Shared/UI/MainPage.cs:2049`
- `Benchmark/Shared/UI/MainPage.cs:2784`
- `Benchmark/Shared/UI/MainPage.cs:3138`

## 4. ���s��Ԃƕ\����Ԃ�?E??��

Benchmark UI �͎��s��ԁA�ۑ���ԁAE??���ԁA�`���[�g�\���ACPU�\���𓯈�ViewModel?E??�A���X�V���܂��B`IsBenchmarkRunning` ��E`IsRefreshing` ��setter�������̊֘A�v���p�e�B�ƃR�}���h��Ԃ�ʒm���邽�߁A��Ԓǉ���?E����p�E??����E??�ł��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:313`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:316`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:317`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:345`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:346`

## 5. ParameterSweep UI �͊Ȍ��������s���䂪�s��

ParameterSweep UI �͍\����������E??���A���s���ɓ���?E??����Picker�𖳌����������E?E�m�F�ł��܂���B�L�����Z����E??�׃G���[�\����m�F�ł��܂���B�����Ԏ��s�⎸�s������UX�Ƃ��Ă͒ǉ��݌v��?E??�ł��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:14`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:15`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:26`
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs:29`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:103`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:125`

## 6. ����ʌ��؂̏ؐՂ�����?E

UI�d�l����艻����ɂ́A?E���\���A���s���A��~���A���ۑ��m�F�ACSV�Ǎ��A�`���[�g�I��ACPU�I��?E�X�N���[���V���`E??�܂�?EUI�`E??�g�ؐՂ�?E??�ł��B�����_�ł̓R�[�h�؋�����?E??�A����ʂ̌��؏ؐՂ͒ǉ��擾��?E??�ł��AE

証拠:
- `Reverse/UI/UiSpecification.md`

## 後続改修候裁E

- Benchmark UI �̉�ʐ�?E��AToolbar�ASourceTabs�ABenchmarkGrid�AChartPanel�ACpuPanel�ADialogOverlay �̒P�ʂ�?E??����AE
- `BenchmarkRunnerViewModel` ����ARunLifecycle�ASelectionState�AChartState�ACpuSelection�APersistenceCoordinator ��?E������AE
- �\��?E??��萔�܂�?E���\�[�X�ֈڂ��A����������������AE
- ParameterSweep UI �ɓ��͖������A�L�����Z���A�ڍ׃G���[�A���s���O��ǉ�����AE
- �X�N���[���V���`E??�܂�?EUI�X�i�b�v�V���`E??���؂𐮔�����AE
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

��?E??�݂̐����d�l?E `Document/UI/` �z���֐V�݂��܂����AE

- `UI.md`
- `UI.md`

`Reverse/UI` �͎���E??�畜��E??���؋���E??�Ƃ��Ďc���A����?E���{�� `Document/UI` ��D�悵�܂��AE

## Generated

- `Reverse/UI/UiSpecification.md`
- `UI.md`
- `UI.md`

- `UI.md`
- `UI.md`
- `UI.md`

## Code Evidence

Benchmark app:
- `Benchmark/UI/UI.csproj`: MAUI�A�A�v�����A�Q�ƃv���W�F�N�g�ASyncfusion/CommunityToolkit�ˑ��A?E�LUI�C���|?E�g�AE
- `Benchmark/Scalable.UI/Scalable.UI.csproj`: Scalable��?E�A�v�����A`SCALABLE_BENCHMARK`�A�Q�ƃv���W�F�N�g�A?E�LUI�C���|?E�g�AE
- `Benchmark/Shared/UI.projitems`: ���LUI�� `Shared\UI.Core` �̎�荞�݁AE
- `Benchmark/Shared/UI/MainPage.cs`: ��ʍ\�z�A�o�C���`E??���O�A�C�x���g�A�_�C�A���O�A�`���[�g�ACPU�\�AE
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs`: ��ʏ�ԁA�R�}���h�AE??��A���s�A��?E�Ǎ��A�`���[�g�X�V�ACPU�I��AE
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs`: �\�����`E??�A�Z�����`E??�ACPU���`E??�A�`���[�g�_�AE
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs`: Run�ۑ��A�G�N�X�|?E�g�Aprogress�Ǎ��AE
- `Shared/UI.Core/BenchmarkStateManager.cs`: `progress.json` ��Run���[�g��?E??E

ParameterSweep app:
- `Benchmark/Tsavorite.ParameterSweep.UI/Tsavorite.ParameterSweep.UI.csproj`: MAUI�A�A�v�����AParameterSweep�Q?E�AE
- `Benchmark/Tsavorite.ParameterSweep.UI/MainPage.cs`: �P��y?E�W��ʁA�c�[���o?E�A���ʕ\�A�o�C���`E??���O�AE
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs`: ���s��ԁA?E�́A����A��?E??�X�`E?E�^�X�AE
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs`: CSV��?E�Ǎ��ARun����A��?E??E

## Visual Evidence

���擾�ł��AE026-07-01�ɃX�N���[���V���`E??�擾����s���܂������AE??��UI�v���Z�X�� `MainWindowHandle=0` �̂܂܃E�B���h�E��?E�ł��܂���ł����BScalable UI�͊���?E���Z�X���N�����Ă�E??�������A������� `MainWindowHandle=0` �ł����BParameterSweep UI�͒ʏ�UI�̌�?E���s���_�ŏ�?E??��~�������ߖ��B�e�ł��AE

�Ď擾�Ώ�:
- Benchmark通常UI 初期表示、E
- Scalable UI 初期表示、E
- ParameterSweep UI 初期表示、E

�擾�ł����ꍇ?E `Reverse/UI/Screenshots/` �ɕۑ����A���̐߂փt�@�C���p�X�Ǝ擾�����ǋL���Ă��������AE

## Confirmed

- Benchmark�ʏ�UI��Scalable UI�͓������LUI�R�[�h��g��E??���AE
- Benchmark�ʏ�UI��Scalable UI��MAUI�ASyncfusion�ACommunityToolkit�ABenchmarkDotNet�Ɉˑ����܂��AE
- Scalable UI�͒ʏ�UI�ƈقȂ�Q�ƃv���W�F�N�g�Ƃ�����E??�P�ʂ�����܂��AE
- Benchmark UI��ViewModel��?E?E�菑��C# UI�ł��AE
- ParameterSweep UI�͏��K�͂Ȏ菑��C# UI�ł��AE
- ParameterSweep���ʂ� `results.csv` �ɕۑ�����܂��AE

## Inferred

- Benchmark UI���̒�?E?E�A��ʍ\�z�R�[�h��ViewModel�̐Ӗ��W���ł��Bconfidence: medium
- ParameterSweep UI���̒�?E?E�A���s���̓��͐���A�L�����Z���A�G���[�ڍו\���̕s���ł��Bconfidence: medium
- �\��?E??��?E??����?E���[�U�[��?E�i�����ł��Bconfidence: medium

## Unknown

- GUI�N����?E���ۂ̏����\���A�E�B���h�E�T�C�Y�A�\������?E�L���AE
- ?E??�������Ă�E???E??�̖{���̓��{��AE
- UI��ԃX�g�A�̑S�L�[�Ɗ���E??��E??�l�AE
- Mac Catalyst�ł̎���ʍ�?E??E

## Review Gate: UI Reverse

### Generated
��?E?E??�𐶐����܂����AE

### Confirmed
��v��ʍ\���A�o�C���`E??���O�A�f�[�^�\���A��?E�Ǎ����C�t�T�C�N���AScalable��?E??ParameterSweep��?E??����E??������m�F���܂����AE

### Inferred
�����?E����E?E�Ӗ��W���A�o�C���`E??���O�U�݁A���������A���s������s����?E??��?E??�܂����AE

### Unknown
����ʃX�N���[���V���`E??�ƍ�?E���擾�ł��B����̎��s�ł́A�Ώۃv���Z�X���g�`E?E���x���E�B���h�E�n���h����?E�J�����擾�ł��܂���ł����AE

### Questions
�Ȃ��B�����_�̒ǉ������̓X�N���[���V���`E??�擾��UI��ԃX�g�A�ڍׂł��AE
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

## ViewModel構造

`BenchmarkRunnerViewModel` �� `ObservableObject` ��p������P��̉�ʏ�ԃR���`E??�ł��B��ʂ̎�v�f�[�^�� `ObservableCollection` �ƃv���p�e�B�ʒm�Ō��J����A�R�}���h?E CommunityToolkit �� `IRelayCommand` / `IAsyncRelayCommand` �Ƃ��Ē񋟂���܂��AE

証拠:
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

��v�R���N�V����:
- `BenchmarkRows`: �x���`?E�[�N�\�̍s�AE
- `ResultItems`: ��?E�Ǎ����ʂ̑S�́AE
- `VisibleResultItems`: ��ʕ\�����̌��ʁAE
- `CpuItems`: 論理CPU、E
- `CpuSections`: CPU�\�̃Z�N�V�����AE
- `ChartPoints`: �`���[�g�\���_�AE
- `RunOptions`: �ۑ��ς�/����?E�O���Ǎ��̎��s����AE
- `LogEntries`: ���O�\���AE

## �R�}���`E

ViewModel�̓R���X�g���N�^�ŊeUI����?E�R�}���h���?E���܂��AE

�R�}���h��?E
- `RunCommand`: `RunOrResumeAsync`
- `StopCommand`: `StopAsync`
- `PauseCommand`: `PauseAsync`
- `LoadCsvCommand`: `LoadCsvAsync`
- `SaveCommand`: `SaveCurrentBenchmarkResultsAsync(true)`
- `OpenCommand`: `OpenAsync`

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:160`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:162`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:163`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:165`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:166`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:168`

## 動作条件

�x���`?E�[�N����ύX�́A���s���A���t���`E??�����A�ꎞ��~���łȂ�E??���ɋ�����܂��B���s?E�^���́A���t���`E??�����łȂ��A���s���łȂ��AE??��ς݃x���`?E�[�N�Z��������ꍇ�ɋ�����܂��B�ꎞ��~���� Resume ������ʂɗL���ɂȂ�܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:355`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:371`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:374`

��?E�J������͎��s��ԂƖ��ۑ����ʂ̗L���Ɉˑ����܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:428`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:434`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:439`

## 選択状慁E

�\�[�X�^�u�A���s?E���t�@�C���A�X�P�[���A�������l�A���s����A�w�W�A�`���[�g���[�h�A����N�I��?EViewModel�v���p�e�B�ŕێ�����܂��B�ύX���ɂ̓`���[�g�X�V�A���ʕ\���X�V�AUI��ԕۑ����s����v���p�e�B������܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:551`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:601`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:614`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:619`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:624`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:648`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:677`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:687`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:707`

�x���`?E�[�N�s�ƃZ���̃`E?E�^�\���� `BenchmarkTableRow` �� `BenchmarkProjectCell` �ł��B`BenchmarkTableRow` �͍��E�Z��������A?E���W�F�N�g������Z�������ł��܂��B�Z���͑I��AUI�I��A���s���ʁACPU�\���A���g���N�X�A���s�\����ێ����܂��AE

証拠:
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

## �`E?E�^�\��

UI��?E��ȃ��`E??:
- `SourceTabOption`: �\�[�X�^�u��`�Ɛi����\���p�ɕێ����܂��AE
- `BenchmarkProjectCell`: ���E�v���W�F�N�g�Z���̎��s��Ԃƕ\���l��ێ����܂��AE
- `BenchmarkTableRow`: 1�x���`?E�[�N�s?E���E�Z���A�����A�䗦�A�\�[�g�l��ێ����܂��AE
- `CpuSelectionItem`: �_��CPU�A�R�A��ʁAE??���ԁA���݃W���u��Ԃ�ێ����܂��AE
- `CpuTableSection`: CPU�\���Z�N�V������ێ����܂��AE
- `ChartComparisonPoint`: �`���[�g?E���E�l�A���x���A�c�[���`�b�v�AUI�I���Ԃ�ێ����܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:47`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:146`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:744`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1209`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1456`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1741`

�\�[�g�Ώۂ� `BenchmarkGridSortColumn` �ɗ񋓂���A�s?E `GetSortValue` �ŗ�ɉ������l��Ԃ��܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:28`
- `Benchmark/Shared/UI/BenchmarkRunnerModels.cs:1051`

## �`E?E�^���C�t�T�C�N��

初期匁E
- `InitializeAsync` ���J�^���O�ACSV�A�ۑ��ς�Run�A�\����Ԃ���������܂��AE
- �y?E�W�� `Loaded` ���� `InitializeAsync` ���Ă΂�܂��AE

証拠:
- `Benchmark/Shared/UI/MainPage.cs:112`
- `Benchmark/Shared/UI/MainPage.cs:113`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:734`

��?E
- `RunOrResumeAsync` ���V�K���s�܂�?E�ꎞ��~����̍ĊJ�����E??���AE
- ���s���?E `runCancellation` �� `IsBenchmarkRunning` ���� `IsBenchmarkRunActive` �Ƃ��ē�?E����܂��AE
- `PauseAsync` �� `StopAsync` �͎��s��Ԃ��~�����֑J�ڂ����A`WaitForRunStopAsync` ��E??�܂��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:363`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:472`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:482`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:495`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1766`

��?E�Ǎ�:
- ��?Eun�ۑ�?E `BenchmarkRunPersistenceService.SaveManagedRun` ��E`ProgressState` ��?E??���A����Run���[�g���?E���A?E�͂�����܂��AE
- �G�N�X�|?E�g?E��E??�t�H���_�� `progress.json` �Əo�͂�ۑ����܂��AE
- �Ǎ��� `ReadProgressState` �� CSV�Ǎ���?E??���킹�ł��AE
- `BenchmarkStateManager.ProgressFileName` �� `progress.json` �ł��AE

証拠:
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1288`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1307`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1362`
- `Benchmark/Shared/UI/BenchmarkRunnerViewModel.cs:1420`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:14`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:26`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:37`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:86`
- `Shared/UI.Core/BenchmarkStateManager.cs:8`

履歴:
- `BenchmarkRunOption` �͕ۑ��ς�Run�̕\�����ARunRoot�A?E���t�@�C���A��?E??�A�����AN�ꗗ��ێ����܂��AE
- Run�\����?ERun�ԍ��A��?E�����A��?E�������琶?E����܂��AE

証拠:
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:65`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:76`
- `Shared/UI.Core/BenchmarkRunPersistenceService.cs:166`

## Unknown

- ��ʕ\���`E??�X�g?E�ꕔ�͌��s�\�[�X��?E??�������Ă���A�Ӑ}���ꂽ���{�ꕶ���͎���E??���ł͔��f�ł��܂���AE
- UI��ԃX�g�A�̕ۑ�?E�e�̊�?E�ȃL�[�ꗗ�͒ǉ������Ώۂł��B�����_�ł� `SaveUiState` �Ăяo���ƑI��?E���p�e�B�̊֌W�܂ł�d�l�����Ă�E??���AE
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

## �T�vE

`Benchmark\Scalable.UI` �� `Benchmark\UI` �Ɠ������LUI��g��E??�A�v�����ʁA�Q�ƃv���W�F�N�g�A�R���p�C���萔�A�������l�P�ʂ������?E??���Ď����܂��AE

証拠:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/UI/UI.csproj:13`
- `Benchmark/UI/UI.csproj:63`

## �v���W�F�N�g��?E

通常牁E
- �A�v����?E `Seamless Benchmark` �ł��AE
- `DotNet.Linq.Enumerable.*`�A`Tsavorite.Linq.Enumerable.*`�A`Persisted.*`�A`Tsavorite.Collections` ��Q�Ƃ��܂��AE

証拠:
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

Scalable牁E
- �A�v����?E `Seamless Scalable Benchmark` �ł��AE
- `SCALABLE_BENCHMARK` ���`���܂��AE
- `Scalable.Persisted.Collections` �� `Scalable.Tsavorite.Collections` ��Q�Ƃ��܂��AE

証拠:
- `Benchmark/Scalable.UI/Scalable.UI.csproj:14`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:35`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:36`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:37`

## 共有UI

���v���W�F�N�g?E `Benchmark\Shared\UI.projitems` ��C���|?E�g���܂��B���̋��L��E??�� `Shared\UI.Core.projitems` �����ɃC���|?E�g���A`Benchmark\Shared\UI\*.cs`�A`App.xaml`�A?E���`E??�t�H�[���ʃR�[�h�A�摜���\�[�X���荞�݂܂��AE

証拠:
- `Benchmark/UI/UI.csproj:63`
- `Benchmark/Scalable.UI/Scalable.UI.csproj:54`
- `Benchmark/Shared/UI.projitems:8`

## ������E??�P?E

Scalable��?E `BenchmarkRunnerViewModel` �� partial ���\�`E??�ł�����E??�ݒ��㏑�����܂��B`MillisecondsScale` �� `1000`�A�P��?E `s` �ł��B�ʏ��?E��?EScalable��ppartial��܂܂Ȃ�E??�߁AViewModel�{��?E�̊���l��g��E??���AE

証拠:
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:3`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:4`
- `Benchmark/Scalable.UI/BenchmarkRunnerViewModel.Scalable.cs:5`

## 動作条件差刁E

Scalable�ł�MAUI�ASyncfusion�ACommunityToolkit�ABenchmarkDotNet�AWorker�R�s?E�^�[�Q�`E??��g��E??���B�ʏ�ł�Scalable��?E�ǂ����Windows��Mac Catalyst��Ώۂɂ��܂��AE

証拠:
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

## ViewModel構造

`ParameterSweepViewModel` �� `INotifyPropertyChanged` �����E??���ʏ�ԃR���`E??�ł��B���ʍs?E `Rows`�A����� `RunHistory` �� `ObservableCollection` �Ƃ��Č��J����܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:8`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:28`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:29`

��v?E���p�e�B:
- `SelectedBaselineRun`: ��?E??�ۂ̉ߋ�Run�B�ύX���� `ApplyBaselineComparison` ����s���܂��AE
- `IsRunning`: ���s����ԁB�ύX���� `CanRun` ��ʒm���܂��AE
- `CanRun`: `!IsRunning`、E
- `StatusText`: �X�`E?E�^�X�\���AE
- `MaxSuccessfulCountText`: 最大成功件数入力、E
- `MaxSweepStepsText`: �ő�T���X�`E??�v?E�́AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:31`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:39`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:43`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:51`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:55`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:57`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:68`

## ���s���C�t�T�C�N��

初期匁E
- ViewModel�R���X�g���N�^�̓\�����[�V�������[�g�܂�?ELocalApplicationData�z������E`Work\Tsavorite.ParameterSweep` ����[�g�ɂ��� `ParameterSweepRunStore` ���?E���܂��AE
- �R���X�g���N�^������ `RefreshRuns` ��ĂсA�����ǂݍ��݂܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:17`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:23`

��?E
- `RunFullAsync` �� `RunAsync(smoke:false)` ��Ăт܂��AE
- `RunSmokeAsync` �� `RunAsync(smoke:true)` ��Ăт܂��AE
- `RunAsync` �͎��s���Ȃ牽����܂���AE
- ���s�J�n���� `IsRunning=true` �Ƃ��A`Rows` ��N���A���܂��AE
- Smoke���s�ł̓X�C�[�v�P�[�X�𐧌����܂��AE
- �i�����Ƃ� `Rows` �֌��ʍs��ǉ����A`StatusText` ��X�V���܂��AE
- ��?E???E���ʂ�ۑ����A�����X�V���A�ۑ�?E��X�`E?E�^�X�ɕ\�����܂��AE
- ��O���� `Failed: <message>` ��X�`E?E�^�X�ɕ\�����܂��AE
- finally�� `IsRunning=false` �ɖ߂��܂��AE

証拠:
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

## 入力値

`CreateOptions` �� `MaxSuccessfulCountText` �� `MaxSweepStepsText` �𐳂̐����Ƃ��ĉ�߂��܂��B��߂ł��Ȃ�E??��?E�A���ꂼ��E`65536` �� `8` �����l�ɂ��܂��BSmoke�̏ꍇ?E `maxSuccessfulCount` ��ő� `256`�A`maxSweepSteps` ��ő� `2` �ɐ������܂��B`LookupProbeCount` �� `4096` �� `maxSuccessfulCount` �̏�����E??�ł��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:132`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:133`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## ��?E??�C�t�T�C�N��

履歴更新:
- `RefreshRuns` �͌��ݑI�𒆂�RunId��ێ����A�X�g�A����Run�ꗗ��?E�Ǎ����܂��AE
- �Ǎ���A�ȑO?E�I��Run������?E�đI����A�Ȃ���ΐ擪��I�т܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:94`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:95`
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:100`

�x?E�X���C����?E
- `ApplyBaselineComparison` �̓x?E�X���C�����I��܂�?E���ݍs����Ȃ牽����܂���AE
- �I��Run�̌��ʂ�X�g�A����ǂ݁A���ݍs�Ɣ�?E??�� `Rows` ���?E??�֒u�������܂��AE
- �Ǎ����s���͉���\���ύX���܂���AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep.UI/ParameterSweepViewModel.cs:151`

## 永続化

`ParameterSweepRunStore` �̓��[�g�f�B���N�g���z����RunId�P��?E�t�H���_����A`results.csv` ��ۑ����܂��BRun�ꗗ�̓`E??���N�g����?E�����A`results.csv` �̍s�����猋�ʐ���Z?E���܂��B�Ǎ��� `results.csv` ��UTF-8�œǂ݁ACSV�p?E�T�֓n���܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:5`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:7`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:12`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:18`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:20`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:28`
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:45`

CSV��?E `RunId`�A`MeasuredAt`�A`CaseName`�A`Collection`�A`Generic`�A`Backend`�A`Sweep`�A�T�C�Y�Q�A?E�������A���s���AAdd/Lookup�v���l�ō\?E����܂��AE

証拠:
- `Benchmark/Tsavorite.ParameterSweep/ParameterSweepPersistence.cs:50`

## Unknown

- ���s���Ƀ��[�U�[��?E��?E??�ύX�����ꍇ?E���`E??��UI���Ŋm�F�ł��܂���B?E�^���̂� `CanRun` �Ő��䂳��Ă�E??���AE
- ���s�L�����Z���@?E�͎���E??�m�F�ł��܂���AE


