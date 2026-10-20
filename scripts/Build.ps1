param(
    [ValidateSet('Restore', 'Build', 'Test', 'Package', 'Verify')]
    [string]$Task = 'Verify'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME = Join-Path $root '.local/dotnet'
$env:DOTNET_NOLOGO = '1'

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

Push-Location $root
try {
    & "$PSScriptRoot/Test-BuildArguments.ps1"
    & "$PSScriptRoot/Test-Architecture.ps1"
    if ($Task -in @('Restore', 'Verify')) {
        Invoke-DotNet restore DnD.CharSheet.slnx --locked-mode
    }
    if ($Task -in @('Build', 'Verify')) {
        Invoke-DotNet build DnD.CharSheet.slnx -c Release --no-restore
    }
    if ($Task -in @('Test', 'Verify')) {
        Invoke-DotNet test DnD.CharSheet.slnx -c Release --no-build --no-restore
    }
    if ($Task -in @('Package', 'Verify')) {
        $publish = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/publish/win-x64'))
        $artifactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
        if (-not $publish.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publish path escaped artifacts directory.' }
        # Clear only the fixed generated publish directory to exclude stale files.
        if (Test-Path $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
        # Quote MSBuild properties so PowerShell forwards each colon-bearing token intact.
        Invoke-DotNet restore src/DnD.CharSheet.Desktop/DnD.CharSheet.Desktop.csproj -r win-x64 --locked-mode '-p:SelfContained=true'
        Invoke-DotNet publish src/DnD.CharSheet.Desktop/DnD.CharSheet.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $publish
        foreach ($asset in @('DnD.CharSheet.Desktop.exe', 'coreclr.dll', 'PresentationFramework.dll', 'e_sqlite3.dll')) {
            if (-not (Test-Path (Join-Path $publish $asset))) { throw "Missing publish asset: $asset" }
        }
        Compress-Archive -Path "$publish/*" -DestinationPath "$root/artifacts/DnD.CharSheet-win-x64.zip" -Force
        Write-Host 'Self-contained ZIP created; launch acceptance belongs to task 1.4.'
    }
}
finally { Pop-Location }

