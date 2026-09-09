# 使用流程与产出

## 捕获并导出

```powershell
# 运行（游戏先关闭；启动后游戏会拉起，登录进世界即触发全量同步）
Yae\publish_quest\YaeAchievement.exe --quest
# 额外导出 full_sync（可选，默认只输出 UIGF）
Yae\publish_quest\YaeAchievement.exe --quest --full-sync
```

登录进世界后，全量同步完成即自动导出（不产生任何 `.bin` 中间文件）。

## 离线重导出

```powershell
# 从已有的 packet_dump.bin 重导出（不开游戏）
Yae\publish_quest\YaeAchievement.exe --export-dump packet_dump_xxx.bin
```

## 命令行标志

| 标志 | 说明 |
|---|---|
| `--quest` | 任务捕获 + UIGF Quest Record 导出（跳过成就导出） |
| `--quest-uigf` | 同 `--quest` |
| `--full-sync` | 额外导出 `full_sync_*.json`（账号状态） |
| `--export-dump <path>` | 离线模式：从已有抓包重导出，不启动游戏 |

## 产出

默认只输出 **UIGF Quest Record**：

| 文件 | 内容 |
|---|---|
| `uigf_quest_record_v1_*.json` | **UIGF Quest Record v1.1**（默认，纯数据：完成历史 + 任务簿） |
| `full_sync_*.json` | 账号状态（`--full-sync` 时额外导出，可选） |

```json
{
  "info": {
    "export_app": "YaeAchievement(quest)",
    "uigf_quest_version": "v1.1",
    "source": { "packet_dumps": [], "game_version": "7.0.0" }
  },
  "list": {
    "finished_parent_quests": [ { "parent_quest_id": 100001, "accept_time": 1111111111 } ],
    "quest_book": [ { "quest_id": 100101, "parent_quest_id": 100001, "state": 3,
                      "start_time": 1111111111, "accept_time": 1111111111, "finish_progress": 0 } ]
  }
}
```

> 上表数值为占位示例，非真实数据。完整占位示例见 `examples/uigf_quest_record_v1_example.json`。

格式定义与展示解析（名称/章节/视频/主线状态）见消费端项目 [Genshin Mainline Tracker]
与 `docs/FORMAT.md`。

[Genshin Mainline Tracker]: https://github.com/luoyrair/Genshin_Mainline_Tracker
