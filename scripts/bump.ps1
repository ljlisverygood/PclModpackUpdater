# 版本号自增脚本
# 规则：常规更新 patch = +0.0.1；大功能更新 minor = +0.1.0
# 用法：powershell -File scripts\bump.ps1 patch   或   powershell -File scripts\bump.ps1 minor

param([Parameter(Mandatory = $true)][ValidateSet('patch', 'minor', 'major')][string]$Type)

$csprojPath = (Resolve-Path (Join-Path $PSScriptRoot "..\src\PclModpackUpdater\PclModpackUpdater.csproj")).Path
$content = [System.IO.File]::ReadAllText($csprojPath)
$match = [regex]::Match($content, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) {
    Write-Error "csproj 中未找到 <Version>Major.Minor.Patch</Version>"
    exit 1
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value

switch ($Type) {
    'major' { $major++; $minor = 0; $patch = 0 }
    'minor' { $minor++; $patch = 0 }
    'patch' { $patch++ }
}

$newVersion = "$major.$minor.$patch"
$content = $content.Replace($match.Groups[0].Value, "<Version>$newVersion</Version>")
[System.IO.File]::WriteAllText($csprojPath, $content)
Write-Host "版本号已更新为 $newVersion —— 提交并推送到 main 后会自动创建 Release v$newVersion"
