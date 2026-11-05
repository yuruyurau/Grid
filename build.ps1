$ErrorActionPreference = "Stop"

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$binDir = "bin"
$srcDir = "src"
$out = Join-Path $binDir "Grid.exe"

$sources = @(
    "Program.cs",
    "MainForm.cs",
    "MainForm.Input.cs",
    "MainForm.Loader.cs",
    "MainForm.Paint.cs",
    "ImageThumb.cs",
    "ShellThumb.cs",
    "AppConfig.cs"
) | ForEach-Object { Join-Path $srcDir $_ }

$refs = @(
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.dll",
    "/reference:System.Web.Extensions.dll"
)

if (-not (Test-Path $csc)) {
    Write-Error "csc.exe not found: $csc"
    exit 1
}

if (-not (Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir | Out-Null
}

$args = @(
    "/target:winexe",
    "/out:$out",
    "/nologo",
    "/nowarn:0168,0219"
) + $refs + $sources

Write-Host "Building $out ..." -ForegroundColor Cyan
& $csc @args

if ($LASTEXITCODE -eq 0) {
    $json = "Grid.toml"
    if (Test-Path $json) {
        Copy-Item -Path $json -Destination (Join-Path $binDir $json) -Force
        Write-Host "Copied $json -> $binDir\" -ForegroundColor DarkGray
    } else {
        Write-Warning "Grid.toml not found (skipped)"
    }

    Write-Host "Success: $out" -ForegroundColor Green
} else {
    Write-Host "Build failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
