$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$allowed = @{
    'DnD.CharSheet.Domain' = @()
    'DnD.CharSheet.Application' = @('DnD.CharSheet.Domain')
    'DnD.CharSheet.Infrastructure' = @('DnD.CharSheet.Application', 'DnD.CharSheet.Domain')
    'DnD.CharSheet.Desktop' = @('DnD.CharSheet.Application', 'DnD.CharSheet.Infrastructure')
    'DnD.CharSheet.Domain.Tests' = @('DnD.CharSheet.Domain')
    'DnD.CharSheet.Application.Tests' = @('DnD.CharSheet.Application', 'DnD.CharSheet.Domain')
    'DnD.CharSheet.Infrastructure.Tests' = @('DnD.CharSheet.Infrastructure', 'DnD.CharSheet.Application', 'DnD.CharSheet.Domain')
}
$projects = @(Get-ChildItem "$root/src", "$root/tests" -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })
if ($projects.Count -ne $allowed.Count) { throw 'Unexpected project inventory.' }
foreach ($project in $projects) {
    if (-not $allowed.ContainsKey($project.BaseName)) { throw "Unknown project: $project" }
    [xml]$xml = Get-Content $project.FullName
    foreach ($reference in $xml.SelectNodes('//ProjectReference')) {
        $target = [IO.Path]::GetFullPath((Join-Path $project.DirectoryName $reference.Include))
        $known = @($projects | Where-Object FullName -eq $target)
        if ($known.Count -ne 1 -or $known[0].BaseName -notin $allowed[$project.BaseName]) {
            throw "Forbidden reference: $($project.BaseName) -> $($reference.Include)"
        }
    }
}
Write-Host 'Project dependency boundaries passed.'
