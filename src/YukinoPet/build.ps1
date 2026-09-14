$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectRoot 'dist'
$outputExe = Join-Path $outputDir 'YukinoPet.exe'
$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw '未找到 .NET Framework C# 编译器。请在 Windows 功能中启用 .NET Framework 4.x。'
}

function Resolve-FrameworkAssembly([string] $name) {
    $referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework'
    if (Test-Path -LiteralPath $referenceRoot) {
        $reference = Get-ChildItem -LiteralPath $referenceRoot -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($reference) { return $reference.FullName }
    }

    $gacRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly'
    foreach ($gacKind in @('GAC_MSIL', 'GAC_64', 'GAC_32')) {
        $candidateRoot = Join-Path $gacRoot $gacKind
        if (-not (Test-Path -LiteralPath $candidateRoot)) { continue }
        $candidate = Get-ChildItem -LiteralPath $candidateRoot -Recurse -File -Filter $name -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }
    throw "未找到系统程序集：$name"
}

$frameworkAssemblies = @(
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'WindowsBase.dll',
    'System.Xaml.dll',
    'System.IO.Compression.dll',
    'System.IO.Compression.FileSystem.dll'
) | ForEach-Object { Resolve-FrameworkAssembly $_ }

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
if (Test-Path -LiteralPath $outputExe) {
    Remove-Item -LiteralPath $outputExe -Force
}

$arguments = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    "/out:$outputExe",
    "/win32icon:$(Join-Path $projectRoot 'assets\YukinoPet.ico')",
    "/reference:$(Join-Path $projectRoot 'lib\NAudio.dll')",
    "/reference:$(Join-Path $projectRoot 'lib\NAudio.Vorbis.dll')",
    "/reference:$(Join-Path $projectRoot 'lib\NVorbis.dll')",
    "/resource:$(Join-Path $projectRoot 'assets\spritesheet.png'),YukinoPet.Sprites.png",
    "/resource:$(Join-Path $projectRoot 'assets\walking-right.png'),YukinoPet.WalkingRight.png",
    "/resource:$(Join-Path $projectRoot 'assets\walking-left.png'),YukinoPet.WalkingLeft.png",
    "/resource:$(Join-Path $projectRoot 'voice\YukinoVoices.zip'),YukinoPet.Voices.zip",
    "/resource:$(Join-Path $projectRoot 'lib\NAudio.dll'),YukinoPet.NAudio.dll",
    "/resource:$(Join-Path $projectRoot 'lib\NAudio.Vorbis.dll'),YukinoPet.NAudio.Vorbis.dll",
    "/resource:$(Join-Path $projectRoot 'lib\NVorbis.dll'),YukinoPet.NVorbis.dll"
)
$arguments += $frameworkAssemblies | ForEach-Object { "/reference:$_" }
$arguments += Get-ChildItem -LiteralPath $projectRoot -File -Filter '*.cs' |
    Sort-Object Name |
    ForEach-Object { $_.FullName }

& $compiler @arguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputExe)) {
    throw "编译失败，csc 退出代码：$LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $projectRoot 'quotes.json') -Destination $outputDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Destination $outputDir -Force

$built = Get-Item -LiteralPath $outputExe
Write-Host "编译完成：$($built.FullName)"
Write-Host "文件大小：$($built.Length) bytes"
