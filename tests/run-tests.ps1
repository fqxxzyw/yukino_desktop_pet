$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$compiler = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework 4.x C# compiler is required.' }
$references = @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml') | ForEach-Object {
    $name = $_
    $assembly = @('GAC_MSIL', 'GAC_64', 'GAC_32') | ForEach-Object {
        $path = "$env:WINDIR\Microsoft.NET\assembly\$_\$name"
        if (Test-Path -LiteralPath $path) { Get-ChildItem $path -Recurse -Filter "$name.dll" }
    } | Select-Object -First 1
    if (-not $assembly) { throw "$name.dll is required." }
    "/reference:$($assembly.FullName)"
}
$outputDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$output = Join-Path $outputDir 'RegressionTests.exe'
& $compiler /nologo /target:exe "/out:$output" /reference:System.Drawing.dll $references `
    (Join-Path $root 'src\YukinoPet\AnimationEngine.cs') `
    (Join-Path $root 'src\YukinoPet\WindowPlacement.cs') `
    (Join-Path $root 'src\YukinoPet\SpeechBubbleWindow.cs') `
    (Join-Path $PSScriptRoot 'RegressionTests.cs')
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $LASTEXITCODE" }
& $output $root
if ($LASTEXITCODE -ne 0) { throw "Regression tests failed: $LASTEXITCODE" }
