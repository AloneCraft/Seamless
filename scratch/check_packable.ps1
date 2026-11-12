$names = @("Seamless.Authentication", "Seamless.Database.Compilation", "Seamless.Database.Query", "Seamless.Database.SqlServer", "Seamless.Security")
foreach ($n in $names) {
    $p = "$n\$n.csproj"
    $has = Select-String -Path $p -Pattern "IsPackable"
    Write-Host "$n : $has"
}
