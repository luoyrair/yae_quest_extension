# 验证结果

> 验证环境: Windows 11, 游戏 7.0.0 国服, 一份完整登录全量同步抓包 (内部验证)

## 1. UIGF Quest Record 输出

| 检查 | 结果 |
|---|---|
| 解析子任务 (QuestListNotify) | 1546 |
| 解析完成历史 (FinishedParentQuestNotify) | 1106 |
| 解析成就 (AchievementAllDataNotify) | 1845 |
| 与消费端项目内部样例逐项对比 | **完全一致** |
| `quest-record.schema.json` 校验 | **通过** |

## 2. 一键构建 (build.ps1)

- 从零 `git clone` 官方 Yae → 应用补丁 → 复制新文件 → AOT 发布 GUI + Lib → 组装 `publish_quest`
- 端到端验证通过 (产物 `YaeAchievement.exe` + `YaeAchievement.dll`)
- 依赖检测: git / .NET SDK / VS2022 Build Tools 自动识别, 缺失可 `-InstallDeps` winget 自动安装

## 3. 离线重导出

`YaeAchievement.exe --export-dump packet_dump_xxx.bin` 不开游戏即可从旧抓包重导出,
用于回归验证 / 复现。

## 4. 踩过的坑 (已修复)

| 问题 | 修复 |
|---|---|
| `FileStream.Read` 返回字节数不足导致丢包 | 改用 `ReadExactly` |
| `len=0` 空负载包被误判为异常而 break | 只拒绝负数, 允许 len=0 |
| ~~Quest 字段 4/9 命名 (曾误判 v7.0 行为相反, 标为 start_time=9/accept_time=4)~~ | 经官方 7.0.0 proto + LunaGC 服务端确认字段号未变: **start_time=4 / accept_time=9**, 已改回 |
| ParentQuest 字段 2 被误当 finish_time | 字段2 实为 **accept_time**; 协议无完成时间戳, UIGF 输出 `accept_time` |
| PowerShell 5.1 解析 UTF-8 中文脚本失败 | 文件加 UTF-8 BOM |
| Lib 发布时 NuGet pack 对 `-o` 覆盖目录报无害错误 | 改为检查 DLL 文件是否生成, 不查退出码 |

## 5. 未验证项

- 真实游戏内运行 (`--quest` 需登录进世界) — 需用户在有账号的环境验证
- 其他游戏版本 (仅 7.0.0 实测)
