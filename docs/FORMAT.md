# 输出格式: UIGF Quest Record v1.0

设计原则: 只含**游戏内原始记录**, 不含展示层派生信息
(名称/章节/视频/主线聚合由**消费端**解析, 数据端与展示完全解耦)。

Schema: `Genshin Mainline Tracker/schema/quest-record.schema.json` (JSON Schema draft-07)
(消费端 `Genshin Mainline Tracker` 定义了该格式与展示解析逻辑)

## 文件结构

> 以下数值均为占位示例, 非真实数据。完整占位示例见 `examples/uigf_quest_record_v1_example.json`。

```json
{
  "info": {
    "export_app": "YaeAchievement(quest)",
    "export_app_version": "1.0.0",
    "uigf_quest_version": "v1.0",
    "export_timestamp": 1111111111,
    "export_time": "2030-01-01 00:00:00",
    "timezone": "UTC+8",
    "source": {
      "uid": "",
      "packet_dumps": ["（示例）抓包文件.bin"],
      "game_version": "7.0.0"
    }
  },
  "list": {
    "finished_parent_quests": [
      { "parent_quest_id": 100001, "finish_time": 1111111111 }
    ],
    "quest_book": [
      { "quest_id": 100101, "parent_quest_id": 100001, "state": 3,
        "start_time": 1111111111, "accept_time": 1111111111, "finish_progress": 0 }
    ]
  }
}
```

## list 字段

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `finished_parent_quests` | array | ✅ | 完成历史 (FinishedParentQuestNotify): 玩家完成过的所有父任务 |
| `quest_book` | array | ✅ | 任务簿 (QuestListNotify): 玩家任务面板中的子任务及状态 |

### finished_parent_quests 项

| 字段 | 类型 | 说明 |
|---|---|---|
| parent_quest_id | integer | 父任务 ID (MainQuest ID) |
| finish_time | integer | 完成 Unix 秒 |

### quest_book 项

| 字段 | 类型 | 说明 |
|---|---|---|
| quest_id | integer | 子任务 ID (QuestExcel subId) |
| parent_quest_id | integer | 所属父任务 ID |
| state | integer | 0=NONE 1=UNSTARTED 2=UNFINISHED 3=FINISHED 4=REWARD_TAKEN 5=FAILED |
| start_time | integer/null | 开始时间 (0 表示缺席) |
| accept_time | integer/null | 接取时间 (0 表示缺席) |
| finish_progress | integer/null | 完成进度 (0 表示缺席) |

## 消费端解析职责

展示/引用信息由消费端 (前端) 解析, 数据端不产生:

1. 任务名称: animegamedata2 配置 (MainQuest titleTextMapHash -> 名称)
2. 章节归属: animegamedata2 配置 (QuestCodex / ChapterExcel 成员判定)
3. 开图主线定义: 影月月清单 (可选加载)
4. 主线状态: 由 finished_parent_quests + quest_book 计算
   - completed 全链完成: 主线内所有父任务在完成历史
   - in_progress 进行中: 部分完成或任务簿有挂账
   - not_started 未接取: 均无

参考消费端实现: `Genshin Mainline Tracker` (生成开图主线完成报告)。
