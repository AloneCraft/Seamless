# EGen 1.14.0 を現行の MSVC でビルドする(EGen の本体ソースは書き換えない)。
# 使い方: powershell -File build-egen.ps1 [-EGenDirectory <EGen の展開先>] [-OutputDirectory <出力先>]
# 出力: <OutputDirectory>\EGenLoader.exe(データ生成)、EGenDriver.exe(運転側)、EGenCore.lib。
#
# 回避しているのは次の 2 つだけで、どちらも EGen の動作には関係しない。
#  1. EGenStandardTypes.h の `#define snprintf _snprintf` が新しい Windows SDK の stdio.h と衝突する
#     → /FIstdio.h で標準ヘッダーを先に読み込む。
#  2. EGenVersion.cpp の `__DATE__" "__TIME__` と EGenLoader.cpp の `"%"PRId64` が、C++11 の文字列リテラル接尾辞と解釈される
#     → 出力先のコピーで空白を足してからコンパイルする。バージョン番号(GetEGenVersion)と動作には触れない。
param(
    [string]$EGenDirectory='G:\GitHub\Seamless\Work\TpcE\egen',
    [string]$OutputDirectory='G:\GitHub\Seamless\Work\TpcE\egen-build'
)
$ErrorActionPreference='Stop'
$vcvars='C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat'
$objects=Join-Path $OutputDirectory 'obj'
$loaderObjects=Join-Path $OutputDirectory 'obj-loader'
$driverObjects=Join-Path $OutputDirectory 'obj-driver'
New-Item -ItemType Directory -Force $objects,$loaderObjects,$driverObjects | Out-Null
$driver=Join-Path $PSScriptRoot 'driver\EGenDriver.cpp'
$harnessSource=Join-Path $PSScriptRoot 'harness\HarnessBridge.cpp'
$harnessInclude=Join-Path $PSScriptRoot 'harness'
$harnessObjects=Join-Path $OutputDirectory 'obj-harness'
New-Item -ItemType Directory -Force $harnessObjects | Out-Null
$patched=Join-Path $OutputDirectory 'EGenVersion.cpp'
(Get-Content (Join-Path $EGenDirectory 'Utilities\src\EGenVersion.cpp') -Raw).Replace('__DATE__" "__TIME__','__DATE__ " " __TIME__') | Set-Content $patched -Encoding utf8
$patchedLoader=Join-Path $OutputDirectory 'EGenLoader.cpp'
(Get-Content (Join-Path $EGenDirectory 'src\EGenLoader.cpp') -Raw) -replace '"(PRI[A-Za-z0-9]+)','" $1' | Set-Content $patchedLoader -Encoding utf8
$excluded='EGenLoader.cpp','EGenValidate.cpp','bucketsimulator.cpp'
$sources=@(Get-ChildItem (Join-Path $EGenDirectory 'src\*.cpp') | Where-Object { $excluded -notcontains $_.Name } | ForEach-Object { $_.FullName })
$sources+=@(Get-ChildItem (Join-Path $EGenDirectory 'Utilities\src\*.cpp') | Where-Object { $_.Name -ne 'EGenVersion.cpp' } | ForEach-Object { $_.FullName })
$sources+=@(Get-ChildItem (Join-Path $EGenDirectory 'InputFiles\src\*.cpp') | ForEach-Object { $_.FullName })
$sources+=$patched
$list=($sources | ForEach-Object { '"'+$_+'"' }) -join ' '
$loader='"'+$patchedLoader+'"'
$include="/I`"$EGenDirectory\inc`" /I`"$EGenDirectory\Utilities\inc`" /I`"$EGenDirectory\InputFiles\inc`""
$flags='/nologo /utf-8 /O2 /MD /EHsc /std:c++14 /FIstdio.h /wd4996 /D_CRT_SECURE_NO_DEPRECATE /DWIN32 /DCOMPILE_FLAT_FILE_LOAD'
$log=Join-Path $OutputDirectory 'build.log'
$commands=@(
    "`"$vcvars`" >nul 2>&1",
    "cl $flags /c $include /Fo`"$objects\\`" $list > `"$log`" 2>&1",
    "cl $flags /c $include /Fo`"$loaderObjects\\`" $loader >> `"$log`" 2>&1",
    "lib /nologo /OUT:`"$OutputDirectory\EGenCore.lib`" `"$objects\*.obj`" >> `"$log`" 2>&1",
    "link /nologo /OUT:`"$OutputDirectory\EGenLoader.exe`" `"$loaderObjects\EGenLoader.obj`" `"$OutputDirectory\EGenCore.lib`" >> `"$log`" 2>&1",
    "cl $flags /c $include /Fo`"$driverObjects\\`" `"$driver`" >> `"$log`" 2>&1",
    "link /nologo /OUT:`"$OutputDirectory\EGenDriver.exe`" `"$driverObjects\EGenDriver.obj`" `"$OutputDirectory\EGenCore.lib`" ws2_32.lib >> `"$log`" 2>&1",
    "cl $flags /c $include /I`"$harnessInclude`" /Fo`"$harnessObjects\\`" `"$harnessSource`" >> `"$log`" 2>&1",
    "link /nologo /DLL /OUT:`"$OutputDirectory\EGenHarness.dll`" `"$harnessObjects\HarnessBridge.obj`" `"$OutputDirectory\EGenCore.lib`" >> `"$log`" 2>&1"
)
cmd /c ($commands -join ' && ')
if($LASTEXITCODE -ne 0){
    Write-Error "build failed. See $log"
}
Write-Output "built: $OutputDirectory\EGenLoader.exe, $OutputDirectory\EGenDriver.exe, $OutputDirectory\EGenHarness.dll"
