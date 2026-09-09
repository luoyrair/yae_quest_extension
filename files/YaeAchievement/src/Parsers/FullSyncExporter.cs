using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

// ReSharper disable UnusedMember.Local

namespace YaeAchievement.Parsers;

/// <summary>
/// 全量同步账号状态导出器 (v7.0.0 实测字段号, 见 tools/parse_full_sync.py):
///   cmd 2516  QuestListNotify            -> 任务簿 (子任务状态; start_time=4, accept_time=9)
///   cmd 23849 FinishedParentQuestNotify  -> 父任务完成历史 (成员关系=完成; 无完成时间戳, accept_time=2)
///   cmd 29910 AchievementAllDataNotify   -> 成就状态
///
/// 进程内累加: PacketCapture 把每个网包喂给 AddPacket, 结束后 Export 导出:
///   uigf_quest_record_v1_<时间>.json   UIGF Quest Record v1.0 (默认, 纯数据)
///   full_sync_<时间>.json              账号状态 (仅 --full-sync 时)
/// 不产生任何 .bin 中间文件。
/// </summary>
public static class FullSyncExporter {

    private const uint QuestCmd = 2516;   // QuestListNotify
    private const uint ParentCmd = 23849; // FinishedParentQuestNotify
    private const uint AchCmd = 29910;    // AchievementAllDataNotify

    private static readonly string[] QuestStateNames = [
        "NONE", "UNSTARTED", "UNFINISHED", "FINISHED", "REWARD_TAKEN", "FAILED"
    ];

    private static readonly string[] AchStatusNames = [
        "INVALID", "UNFINISHED", "FINISHED", "REWARD_TAKEN"
    ];

    private static string QuestStateName(ulong s) => s < (ulong) QuestStateNames.Length ? QuestStateNames[s] : $"UNKNOWN_{s}";
    private static string AchStatusName(ulong s) => s < (ulong) AchStatusNames.Length ? AchStatusNames[s] : $"UNKNOWN_{s}";

    private static readonly Dictionary<ulong, SyncQuest> Quests = [];
    private static readonly Dictionary<ulong, SyncParent> Parents = [];
    private static readonly Dictionary<ulong, SyncAchievement> Achs = [];

    /// <summary>是否额外导出 full_sync_*.json (--full-sync)。</summary>
    public static bool EmitFullSync { get; set; }

    public static void Clear() {
        Quests.Clear();
        Parents.Clear();
        Achs.Clear();
    }

    /// <summary>把单个网包加入累加器 (进程内解析, 不落盘 .bin)。</summary>
    public static void AddPacket(uint cmdId, ReadOnlySpan<byte> payload) {
        if (cmdId == QuestCmd) {
            foreach (var ld in ProtoWalker.Walk(payload).GetLD(15)) {
                var q = ParseQuest(ld.Data);
                if (q != null) Quests[q.QuestId] = q;
            }
        } else if (cmdId == ParentCmd) {
            foreach (var ld in ProtoWalker.Walk(payload).GetLD(12)) {
                var p = ParseParent(ld.Data);
                if (p != null) Parents[p.ParentQuestId] = p;
            }
        } else if (cmdId == AchCmd) {
            foreach (var ld in ProtoWalker.Walk(payload).GetLD(5)) {
                var a = ParseAch(ld.Data);
                if (a != null) Achs[a.AchievementId] = a;
            }
        }
    }

    /// <summary>导出 UIGF Quest Record (默认) + full_sync (--full-sync)。sourceLabel: 实时捕获为空, 离线为 dump 路径。</summary>
    public static void Export(string sourceLabel) {
        AnsiConsole.WriteLine($"全量同步解析: 子任务 {Quests.Count} | 完成历史 {Parents.Count} | 成就 {Achs.Count}");
        var now = DateTime.Now;
        var source = string.IsNullOrEmpty(sourceLabel) ? [] : new List<string> { sourceLabel };

        // UIGF Quest Record v1.1 (默认输出, 纯数据)
        ExportQuestRecord(now, source);

        // full_sync (可选)
        if (EmitFullSync) {
            var fsJson = new FullSyncJson {
                Source = source,
                SubQuests = Quests.Count,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Quests = [.. Quests.Values.OrderBy(q => q.QuestId)],
                ParentQuests = [.. Parents.Values.OrderBy(p => p.ParentQuestId)],
                Achievements = [.. Achs.Values.OrderBy(a => a.AchievementId)],
            };
            var fsPath = Path.GetFullPath($"full_sync_{now:yyyyMMddHHmmss}.json");
            File.WriteAllText(fsPath, JsonSerializer.Serialize(fsJson, FullSyncJsonContext.Default.FullSyncJson));
            AnsiConsole.WriteLine($"已导出账号状态 -> {fsPath}");
        }
    }

    private static SyncQuest? ParseQuest(ReadOnlySpan<byte> data) {
        var f = ProtoWalker.Walk(data);
        var qid = f.GetFirstVarint(1);
        var st = f.GetFirstVarint(2);
        if (qid == null || st == null || st.Value > 15) return null;
        return new SyncQuest {
            QuestId = qid.Value,
            State = st.Value,
            StateName = QuestStateName(st.Value),
            StartTime = f.GetFirstVarint(4) ?? 0,   // start_time=字段4 (官方/LunaGC 约定)
            AcceptTime = f.GetFirstVarint(9) ?? 0,  // accept_time=字段9 (入册即记, 未接取任务也有)
            ParentQuestId = f.GetFirstVarint(6) ?? 0,
            FinishProgress = f.GetFirstVarint(11) ?? 0,
        };
    }

    private static SyncParent? ParseParent(ReadOnlySpan<byte> data) {
        var f = ProtoWalker.Walk(data);
        var pid = f.GetFirstVarint(12);
        if (pid == null) return null;
        // ParentQuest 无 finish_time 字段; 字段2 是 accept_time (官方 7.0.0 proto + LunaGC 双确认)
        return new SyncParent { ParentQuestId = pid.Value, AcceptTime = f.GetFirstVarint(2) ?? 0 };
    }

    private static SyncAchievement? ParseAch(ReadOnlySpan<byte> data) {
        var f = ProtoWalker.Walk(data);
        var aid = f.GetFirstVarint(5);
        var st = f.GetFirstVarint(8);
        if (aid == null || st == null || st.Value > 4) return null;
        return new SyncAchievement {
            AchievementId = aid.Value,
            Status = st.Value,
            StatusName = AchStatusName(st.Value),
            Progress = f.GetFirstVarint(9) ?? 0,
        };
    }

    /// <summary>导出 UIGF Quest Record v1.1 (纯数据: 完成历史 + 任务簿, 展示由消费端解析)。</summary>
    private static void ExportQuestRecord(DateTime now, List<string> source) {
        var outJson = new QuestRecordJson {
            Info = new QuestRecordInfo {
                ExportApp = "YaeAchievement(quest)",
                ExportAppVersion = "1.0.0",
                UigfQuestVersion = "v1.1",
                ExportTimestamp = (long) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExportTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                Timezone = "UTC+8",
                Source = new QuestRecordSource {
                    Uid = "",
                    PacketDumps = source,
                    GameVersion = TryGetGameVersion(),
                },
            },
            List = new QuestRecordList {
                FinishedParentQuests = [.. Parents.Values.OrderBy(p => p.ParentQuestId)],
                QuestBook = [.. Quests.Values.OrderBy(q => q.QuestId).Select(q => new QuestBookItem {
                    QuestId = q.QuestId,
                    ParentQuestId = q.ParentQuestId,
                    State = q.State,
                    StartTime = q.StartTime,
                    AcceptTime = q.AcceptTime,
                    FinishProgress = q.FinishProgress,
                })],
            },
        };
        var outPath = Path.GetFullPath($"uigf_quest_record_v1_{now:yyyyMMddHHmmss}.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(outJson, FullSyncJsonContext.Default.QuestRecordJson));
        AnsiConsole.WriteLine($"已导出 UIGF Quest Record (完成历史 {outJson.List.FinishedParentQuests.Count} / 任务簿 {outJson.List.QuestBook.Count}) -> {outPath}");
    }

    private static string TryGetGameVersion() {
        try {
            var v = GlobalVars.AchievementInfo.Version;
            return string.IsNullOrEmpty(v) ? "" : v;
        } catch {
            return "";
        }
    }
}

// ================= 数据模型 =================

// ---------- UIGF Quest Record (format.md v1.0 / quest-record.schema.json) ----------

public sealed class QuestRecordSource {
    public string Uid { get; set; } = "";
    public List<string> PacketDumps { get; set; } = [];
    public string GameVersion { get; set; } = "";
}

public sealed class QuestRecordInfo {
    public string ExportApp { get; set; } = "YaeAchievement(quest)";
    public string ExportAppVersion { get; set; } = "1.0.0";
    public string UigfQuestVersion { get; set; } = "v1.1";
    public long ExportTimestamp { get; set; }
    public string ExportTime { get; set; } = "";
    public string Timezone { get; set; } = "UTC+8";
    public QuestRecordSource Source { get; set; } = new();
}

public sealed class QuestBookItem {
    public ulong QuestId { get; set; }
    public ulong ParentQuestId { get; set; }
    public ulong State { get; set; }
    public ulong StartTime { get; set; }
    public ulong AcceptTime { get; set; }
    public ulong FinishProgress { get; set; }
}

public sealed class QuestRecordList {
    public List<SyncParent> FinishedParentQuests { get; set; } = [];
    public List<QuestBookItem> QuestBook { get; set; } = [];
}

public sealed class QuestRecordJson {
    public QuestRecordInfo Info { get; set; } = new();
    public QuestRecordList List { get; set; } = new();
}

internal static class ProtoWalkerExt {
    public static List<ProtoWalker.LD> GetLD(this Dictionary<int, List<object>> fields, int field) {
        if (!fields.TryGetValue(field, out var list)) return [];
        return list.OfType<ProtoWalker.LD>().ToList();
    }
}

// ================= 数据模型 =================

public sealed class SyncQuest {
    public ulong QuestId { get; set; }
    public ulong State { get; set; }
    public string StateName { get; set; } = "";
    public ulong StartTime { get; set; }
    public ulong AcceptTime { get; set; }
    public ulong ParentQuestId { get; set; }
    public ulong FinishProgress { get; set; }
}

public sealed class SyncParent {
    public ulong ParentQuestId { get; set; }
    /// <summary>父任务接取时间 (ParentQuest.accept_time=字段2)。完成时间协议不提供。</summary>
    public ulong AcceptTime { get; set; }
}

public sealed class SyncAchievement {
    public ulong AchievementId { get; set; }
    public ulong Status { get; set; }
    public string StatusName { get; set; } = "";
    public ulong Progress { get; set; }
}

public sealed class FullSyncJson {
    public List<string> Source { get; set; } = [];
    public int SubQuests { get; set; }
    public string GeneratedAt { get; set; } = "";
    public List<SyncQuest> Quests { get; set; } = [];
    public List<SyncParent> ParentQuests { get; set; } = [];
    public List<SyncAchievement> Achievements { get; set; } = [];
}

[JsonSerializable(typeof(FullSyncJson))]
[JsonSerializable(typeof(QuestRecordJson))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Default,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower
)]
public sealed partial class FullSyncJsonContext : JsonSerializerContext;
