# 版本相关字段号 (v7.0.0 实测) 与校准方法

> 扩展**只处理任务包** (纯增量, 不碰 Yae 原生功能)。
> 包捕获复用上游 Yae 5.8.0 内置机制 (DLL `RequiredPackets` 白名单 + 类型 4 推送),
> GUI 侧 `Utils.cs` 在 0xFA 白名单注册任务 cmd、0x04 把任务包喂给 `FullSyncExporter`。
> 成就 (AchievementAllDataNotify) 是 **Yae 原生领域**, 本扩展不解析, 此处不列出。

## 两个任务包

| 包 | cmdId | 列表字段 | 关键字段 |
|---|---|---|---|
| QuestListNotify | 2516 | 15 | quest_id=1, state=2, start_time=4, accept_time=9, parent_quest_id=6, finish_progress=11 |
| FinishedParentQuestNotify | 23849 | 12 | parent_quest_id=12, accept_time=2 (**无 finish_time**) |

> 字段号以官方 7.0.0 proto (`capyb2222/genshin-protocol`) + LunaGC 服务端实现为准。
> `FinishedParentQuestNotify` 里的父任务**只有接取时间 (accept_time=2)**，
> 协议不含完成时间戳；完成状态由"是否在该列表"表达，完成时刻不可得。

代码位置: `files/YaeAchievement/src/Parsers/FullSyncExporter.cs`
(`QuestListCmd`/`ParentCmd` 常量 + `ParseQuest`/`ParseParent`) 与 `Utils.cs` 0x04/0xFA。

## 为什么是硬编码

Yae 元数据 (`AchievementInfo.proto`) 只维护成就字段号 (`pb_info`), **没有任务字段号**
—— Yae 原生不做任务解析。因此任务字段号没有权威元数据可依赖, 只能硬编码 + 实测校准。

> 字段号核对: 官方 7.0.0 proto 与 LunaGC 均标 `Quest.start_time=4 / accept_time=9`,
> 与 Grasscutter 3.8 一致 (v7.0 未改号)。数据行为佐证: 未接取 (state=1) 任务有 accept_time(9)
> 无 start_time(4) —— 入册即记 accept_time, 开始才记 start_time。

## 大版本更新后如何校准

1. 用 `YaeAchievement.exe --quest` 抓一份新版本 dump。
2. 用随包校准工具解析, 核对 cmdId 与字段号是否变化:

   ```powershell
   python tools\parse_full_sync.py packet_dump_xxx.bin
   ```

   `tools/parse_full_sync.py` 是独立 Python 解析器 (与 `FullSyncExporter.cs` 逻辑一致),
   无本地数据依赖。若它能正确解析出子任务/完成历史, 说明 cmdId + 字段号未变;
   解析异常则对照新版本抓包逐字段核对, 更新 `files/YaeAchievement/src/Parsers/FullSyncExporter.cs`
   中的 `QuestListCmd`/`ParentCmd` 常量与 `ParseQuest`/`ParseParent` 字段号。
3. 重新生成补丁 (`scripts/regenerate_patch.ps1`) 并 `build.ps1` 验证。

## 任务状态码 (quest_book.state)

| 值 | 含义 |
|---|---|
| 0 | NONE |
| 1 | UNSTARTED |
| 2 | UNFINISHED |
| 3 | FINISHED |
| 4 | REWARD_TAKEN |
| 5 | FAILED |
