$path = "G:\GitHub\Seamless\MultiProjectTemplate\Client\bin\Debug\net10.0\Seamless.Database.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($path)
$asm.GetExportedTypes() | ForEach-Object { $_.FullName } | Sort-Object
