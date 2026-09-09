# 维护：上游更新 / 字段号调整后重新生成补丁

## 场景

- 上游 Yae 仓库更新，补丁 `git apply` 失败
- 游戏大版本更新，任务 cmdId/字段号变化（见 `docs/FIELDS.md` 校准步骤）
- 本地修改了扩展代码

## 重新生成补丁

1. 在**修改后的 Yae 目录**（git 仓库，改动未提交）里改好代码。
2. 运行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts\regenerate_patch.ps1 -ModifiedYae <你的Yae目录>
   ```

   该脚本会：
   - 用 `git diff` 重新生成 `patch/yae_quest_ext.patch`（`cmd /c` 原生重定向，保留 UTF-8 字节，无 BOM）
   - 重新复制新文件 `files/YaeAchievement/src/Parsers/FullSyncExporter.cs`
3. 验证补丁仍可应用 + 构建：

   ```powershell
   powershell -ExecutionPolicy Bypass -File build.ps1
   ```

## CI

`.github/workflows/build.yml` 在 push / PR 时自动在 `windows-latest` 执行完整构建，
补丁应用失败会直接报错，防止上游更新后补丁悄悄失效。
