<#
build.ps1 — 一键构建 Yae 任务扩展 (基于拉取的官方 Yae + 补丁)

流程: 拉取 Yae -> 重置干净 -> 应用补丁 -> 复制新文件 -> AOT 发布 GUI + Lib -> 组装 publish_quest

用法 (在 yae_quest_extension 目录):
  powershell -ExecutionPolicy Bypass -File build.ps1             # 拉取 Yae + 打补丁 + 构建
  powershell -ExecutionPolicy Bypass -File build.ps1 -SkipClone  # 用已有的 Yae 目录 (跳过 clone)
  powershell -ExecutionPolicy Bypass -File build.ps1 -YaeDir D:\Yae

产物: <YaeDir>\publish_quest\YaeAchievement.exe + YaeAchievement.dll (修改版 Lib)
运行: <YaeDir>\publish_quest\YaeAchievement.exe --quest

前置: .NET 9+ SDK, VS2022 Build Tools (NativeAOT 需要 MSVC), git
#>
param(
    [string]$YaeDir = "Yae",
    [switch]$SkipClone,
    [switch]$InstallDeps   # 自动安装缺失依赖 (git / .NET SDK / VS Build Tools)
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Test-Cmd([string]$n) { [bool](Get-Command $n -ErrorAction SilentlyContinue) }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Yae 任务扩展 一键构建" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# ---------- 0) 依赖检测 / 自动安装 (Win11 winget, 仅本机缺时才装) ----------
if (-not (Test-Cmd git)) {
    if ($InstallDeps) {
        Write-Host "[dep] 安装 git ..." -ForegroundColor Yellow
        winget install --id Git.Git -e --silent --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -ne 0) { throw "git 安装失败" }
    } else {
        throw "缺少 git。加 -InstallDeps 自动装, 或手动: winget install Git.Git"
    }
}
if (-not (Test-Cmd dotnet)) {
    if ($InstallDeps) {
        Write-Host "[dep] 安装 .NET 9 SDK ..." -ForegroundColor Yellow
        winget install --id Microsoft.DotNet.SDK.9 -e --silent --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -ne 0) { throw ".NET SDK 安装失败 (安装后需重开终端)" }
    } else {
        throw "缺少 .NET SDK。加 -InstallDeps 自动装, 或 https://dotnet.microsoft.com/download"
    }
}
# VS2022 Build Tools (NativeAOT 需要 MSVC cl.exe/link.exe)
$vsInstaller = "C:\Program Files (x86)\Microsoft Visual Studio\Installer"
$vsMsDev = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC"
$hasVs = (Test-Path "$vsInstaller\vswhere.exe") -and (Test-Path $vsMsDev)
if (-not $hasVs) {
    $vsCmd = 'winget install Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended" --accept-source-agreements --accept-package-agreements'
    if ($InstallDeps) {
        Write-Host "[dep] 缺少 VS2022 Build Tools (约5GB, 需要管理员, 耗时数分钟)..." -ForegroundColor Yellow
        $resp = Read-Host "确认安装? [y/N]"
        if ($resp -match '^[yY]') {
            Invoke-Expression $vsCmd
            if ($LASTEXITCODE -ne 0) { throw "VS Build Tools 安装失败" }
        } else {
            throw "缺少 VS Build Tools, NativeAOT 无法构建。"
        }
    } else {
        throw "缺少 VS2022 Build Tools (NativeAOT 需要 MSVC)。加 -InstallDeps 自动装, 或手动: $vsCmd"
    }
}
# vswhere 加入 PATH (NativeAOT 定位 MSVC)
if (Test-Path "$vsInstaller\vswhere.exe") {
    $env:PATH = "$vsInstaller;$env:PATH"
    Write-Host "[build] 依赖就绪 (git/dotnet/MSVC)" -ForegroundColor DarkGray
}

# ---------- 1) 拉取 / 定位 Yae ----------
if (-not (Test-Path "$YaeDir\.git")) {
    if ($SkipClone) { throw "未找到 git 仓库: $YaeDir (去掉 -SkipClone 或先 clone)" }
    Write-Host "[build] 拉取官方 Yae..."
    git clone --depth 1 https://github.com/HolographicHat/Yae.git $YaeDir
} else {
    Write-Host "[build] 使用已有 Yae: $YaeDir"
}

# ---------- 2) 重置干净 + 应用补丁 + 复制新文件 ----------
Push-Location $YaeDir
try {
    if (git status --porcelain) {
        Write-Host "[build] 工作树非干净, 丢弃本地改动并清理" -ForegroundColor Yellow
        git checkout -- .
        git clean -fd
    }
    Write-Host "[build] 应用补丁 yae_quest_ext.patch..."
    git apply "$root\patch\yae_quest_ext.patch"
    if ($LASTEXITCODE -ne 0) { throw "补丁应用失败 — 可能 Yae 上游已更新, 需重新生成补丁" }
    Write-Host "[build] 复制新文件..."
    Copy-Item "$root\files\YaeAchievement\src\Parsers\PacketCapture.cs" "YaeAchievement\src\Parsers\" -Force
    Copy-Item "$root\files\YaeAchievement\src\Parsers\FullSyncExporter.cs" "YaeAchievement\src\Parsers\" -Force
} finally {
    Pop-Location
}

# ---------- 3) AOT 发布 ----------
Push-Location $YaeDir
try {
    Write-Host "[build] 发布 GUI (AOT, 需要几分钟)..."
    dotnet publish YaeAchievement\YaeAchievement.csproj -c Release -r win-x64 -o publish_quest
    if ($LASTEXITCODE -ne 0) { throw "GUI 发布失败" }
    Write-Host "[build] 发布 Lib (AOT; 末尾 NuGet pack 报错可忽略)..."
    dotnet publish YaeAchievementLib\YaeAchievementLib.csproj -c Release -r win-x64 -o publish_lib 2>$null
    # 不检查退出码 (NuGet pack 目标对 -o 覆盖目录会报无害错误), 改为确认 DLL 已生成
    if (-not (Test-Path "publish_lib\YaeAchievementLib.dll")) { throw "Lib DLL 未生成" }
    # Lib 组装为 YaeAchievement.dll (GUI 优先用 exe 同目录本地 Lib)
    Copy-Item "publish_lib\YaeAchievementLib.dll" "publish_quest\YaeAchievement.dll" -Force
    Remove-Item "publish_lib" -Recurse -Force
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "[build] 完成!" -ForegroundColor Green
Write-Host "  产物: $YaeDir\publish_quest\YaeAchievement.exe" -ForegroundColor Green
Write-Host "  运行: $YaeDir\publish_quest\YaeAchievement.exe --quest" -ForegroundColor Green
Write-Host "         (登录进世界后自动导出 full_sync_*.json + uigf_quest_record_v1_*.json)" -ForegroundColor Green
