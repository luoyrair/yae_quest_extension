# 版本相关字段号 (v7.0.0 实测) 与校准方法

> 扩展在 `OnToInt32` 钩子里对所有解密后的 0x6745 包做解压(与 Yae 原生成就同机制),
> 再按 cmdId + 字段号解析。以下 cmdId/字段号是 **v7.0.0 实测硬编码** 的。

## 三个全量同步包

| 包 | cmdId | 列表字段 | 关键字段 |
|---|---|---|---|
| QuestListNotify | 2516 | 15 | quest_id=1, state=2, start_time=9, accept_time=4, parent_quest_id=6, finish_progress=11 |
| FinishedParentQuestNotify | 23849 | 12 | parent_quest_id=12, finish_time=2 |
| AchievementAllDataNotify | 29910 | 5 | id=5, status=8, progress=9 |

代码位置: `files/YaeAchievement/src/Parsers/FullSyncExporter.cs`
(`QuestCmd`/`ParentCmd`/`AchCmd` 常量 + `ParseQuest`/`ParseParent`/`ParseAch`)。

## 为什么是硬编码

Yae 元数据 (`AchievementInfo.proto`) 只维护成就字段号 (`pb_info`), **没有任务字段号**
—— Yae 原生不做任务解析。因此任务字段号没有权威元数据可依赖, 只能硬编码 + 实测校准。

> 已实测验证: quest 字段 4/9 在 v7.0 中与 Grasscutter 3.8 proto 命名相反
> (3.8: start_time=4/accept_time=9; v7.0 数据行为: 字段9所有任务有=创建时间, 字段4仅已接取=接取时间)。
> 成就字段号与 Yae 元数据一致 (id=5, status=8, total=9, current=6, finish=2)。

## 大版本更新后如何校准

1. 用 `YaeAchievement.exe --quest` 抓一份新版本 dump。
2. 用随包校准工具解析, 核对 cmdId 与字段号是否变化:

   ```powershell
   python tools\parse_full_sync.py packet_dump_xxx.bin
   ```

   `tools/parse_full_sync.py` 是独立 Python 解析器 (与 `FullSyncExporter.cs` 逻辑一致),
   无本地数据依赖。若它能正确解析出子任务/完成历史/成就, 说明 cmdId + 字段号未变;
   解析异常则对照新版本抓包逐字段核对, 更新 `files/YaeAchievement/src/Parsers/FullSyncExporter.cs`
   中的 `QuestCmd`/`ParentCmd`/`AchCmd` 常量与 `ParseQuest`/`ParseParent`/`ParseAch` 字段号。
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
