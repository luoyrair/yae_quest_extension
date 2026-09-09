<#
regenerate_patch.ps1 — 从修改后的 Yae 目录重新生成补丁 + 新文件

用途: 上游 Yae 更新后, 或字段号/代码改动后, 重新生成交付包的补丁, 保持可应用。

用法:
  powershell -ExecutionPolicy Bypass -File scripts\regenerate_patch.ps1
      # 默认从 ..\Yae (修改后的工作目录) 生成
  powershell -ExecutionPolicy Bypass -File scripts\regenerate_patch.ps1 -ModifiedYae D:\yae_modified

前置: ModifiedYae 必须是 git 仓库, 且包含本扩展的改动 (补丁已应用或手工修改)。
#>
param(
    [string]$ModifiedYae = "..\Yae",   # 含扩展改动的 Yae git 仓库
    [string]$OutDir = (Split-Path $PSScriptRoot -Parent)   # 默认交付包根目录
)
$ErrorActionPreference = "Stop"
$root = (Resolve-Path $OutDir).Path

if (-not (Test-Path "$ModifiedYae\.git")) { throw "不是 git 仓库: $ModifiedYae" }

Push-Location $ModifiedYae
try {
    # 1) 补丁 = 跟踪文件的 git diff (5 个修改文件)
    #    用 cmd 原生重定向保留 git 输出的 UTF-8 字节 (避免 PowerShell 编码转换/加 BOM)
    $patchPath = Join-Path $root "patch\yae_quest_ext.patch"
    New-Item -ItemType Directory -Path (Split-Path $patchPath) -Force | Out-Null
    cmd /c "git diff > `"$patchPath`""
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $patchPath)) { throw "git diff 失败" }
    if ((Get-Item $patchPath).Length -eq 0) { throw "git diff 为空 — 未检测到对跟踪文件的修改 (补丁是否已提交?)" }
    $lineCount = (Get-Content $patchPath).Count
    Write-Host "[regen] 补丁已生成: $patchPath ($lineCount 行)"

    # 2) 新文件 = 未跟踪的 Parsers 新增文件
    $newFiles = @(
        "YaeAchievement\src\Parsers\FullSyncExporter.cs"
    )
    foreach ($rel in $newFiles) {
        $src = Join-Path $ModifiedYae $rel
        if (-not (Test-Path $src)) { throw "未找到新文件: $rel (可能未应用扩展)" }
        $dst = Join-Path $root "files\$rel"
        New-Item -ItemType Directory -Path (Split-Path $dst) -Force | Out-Null
        Copy-Item $src $dst -Force
        Write-Host "[regen] 新文件已复制: $rel"
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "[regen] 完成。交付包已更新。" -ForegroundColor Green
Write-Host "        运行 build.ps1 验证补丁仍可应用。" -ForegroundColor Green
