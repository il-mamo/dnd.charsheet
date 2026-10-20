$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'Build.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Build.ps1 contains syntax errors.' }
$commands = @($ast.FindAll({
    param($node)
    $node -is [System.Management.Automation.Language.CommandAst] -and
        $node.GetCommandName() -eq 'Invoke-DotNet' -and
        $node.Extent.Text -match '-p:'
}, $true))
if ($commands.Count -ne 2) { throw 'Expected restore and publish property-bearing commands.' }

# Execute the actual call sites with a capture function, without invoking dotnet or publishing.
function Invoke-DotNet { $script:captured = @($args) }
$publish = 'C:\build output\win-x64'
foreach ($command in $commands) {
    & ([scriptblock]::Create($command.Extent.Text))
    $expected = if ($captured[0] -eq 'restore') {
        @('-p:SelfContained=true')
    } else {
        @('-p:PublishSingleFile=false', '-p:PublishTrimmed=false')
    }
    foreach ($property in $expected) {
        if ($property -cnotin $captured) { throw "MSBuild property was split or changed: $property" }
    }
    if ($captured[0] -eq 'publish' -and $captured[-1] -cne $publish) {
        throw 'Publish output path with spaces was split or changed.'
    }
}
Write-Host 'Build argument forwarding passed.'
