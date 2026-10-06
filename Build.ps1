$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\PreviewMark.cs'
$manifest = Join-Path $root 'src\PreviewMark.manifest'
$output = Join-Path $root 'dist'
$release = Join-Path $root 'release'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '未找到 .NET Framework x64 C# 编译器（csc.exe）。'
}
if (-not (Test-Path -LiteralPath $source)) {
    throw '未找到 src\PreviewMark.cs。'
}
if (-not (Test-Path -LiteralPath $manifest)) {
    throw '未找到 src\PreviewMark.manifest。'
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
New-Item -ItemType Directory -Path $release -Force | Out-Null
$common = @(
    '/nologo',
    '/platform:x64',
    '/optimize+',
    ('/win32manifest:' + $manifest),
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    ('/out:' + (Join-Path $output 'PreviewMark.exe')),
    '/target:winexe',
    $source
)
& $compiler @common
if ($LASTEXITCODE -ne 0) {
    throw "GUI 构建失败，csc.exe 返回 $LASTEXITCODE。"
}

$cli = @(
    '/nologo',
    '/platform:x64',
    '/optimize+',
    ('/win32manifest:' + $manifest),
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    ('/out:' + (Join-Path $output 'PreviewMark.Cli.exe')),
    '/target:exe',
    $source
)
& $compiler @cli
if ($LASTEXITCODE -ne 0) {
    throw "命令行构建失败，csc.exe 返回 $LASTEXITCODE。"
}

Copy-Item -LiteralPath (Join-Path $output 'PreviewMark.exe') -Destination (Join-Path $release 'PreviewMark.exe') -Force
Copy-Item -LiteralPath (Join-Path $output 'PreviewMark.Cli.exe') -Destination (Join-Path $release 'PreviewMark.Cli.exe') -Force
$checksums = Get-ChildItem -LiteralPath $release -Filter 'PreviewMark*.exe' | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    '{0}  {1}' -f $hash, $_.Name
}
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ASCII
Get-ChildItem -LiteralPath $release -Filter 'PreviewMark*.exe' | Select-Object Name,Length,LastWriteTime

