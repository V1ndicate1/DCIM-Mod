$asm = [System.Reflection.Assembly]::LoadFrom('D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll')
try {
    $types = $asm.GetTypes()
} catch [System.Reflection.ReflectionTypeLoadException] {
    $types = $_.Exception.Types | Where-Object { $_ -ne $null }
}
Write-Output "Total types: $($types.Count)"
$types | Select-Object -First 30 | ForEach-Object { Write-Output $_.FullName }
