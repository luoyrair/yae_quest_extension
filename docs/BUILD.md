# 构建需求与一键构建

## 需求（仅 Windows 11）

| 依赖 | 大小 | 用途 |
|---|---|---|
| git | 小 | 拉取 Yae |
| .NET SDK 9+ | ~200MB | 编译 + AOT 发布 |
| VS2022 Build Tools（MSVC） | ~5GB | NativeAOT 需要 cl.exe/link.exe |

`build.ps1 -InstallDeps` 会用 winget 自动安装缺失依赖（git / .NET SDK 用 winget 装；
VS Build Tools 约 5GB，会先询问确认）。

## 一键构建

```powershell
cd yae_quest_extension
powershell -ExecutionPolicy Bypass -File build.ps1 -InstallDeps
```

流程：拉取官方 Yae → 重置干净 → 应用补丁 → 复制新文件 → AOT 发布 GUI + Lib → 组装 `publish_quest/`。

产物：`<YaeDir>\publish_quest\YaeAchievement.exe` + `YaeAchievement.dll`（修改版 Lib）。

> 运行产物是 NativeAOT **自包含二进制**，无需 .NET 运行时，直接在 Win11 上运行。
> CI（`.github/workflows/build.yml`）在 push 时自动在 `windows-latest` 验证构建并上传产物。
