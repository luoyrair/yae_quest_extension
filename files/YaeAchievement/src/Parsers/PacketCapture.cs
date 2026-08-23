using Google.Protobuf;
using Spectre.Console;

// ReSharper disable UnusedMember.Local

namespace YaeAchievement.Parsers;

/// <summary>
/// 全量同步包捕获器 (进程内解析, 不产生 .bin 中间文件)。
///
/// DLL 侧把所有解密后、解压后的网包 (cmdId + protobuf payload) 通过管道推过来:
///   类型 4 = [cmdId:uint32][length:int32][payload bytes]
/// 本类把每个网包喂给 FullSyncExporter 累加, 全量同步结束后导出 UIGF Quest Record。
/// </summary>
public static class PacketCapture {

    private static readonly Dictionary<uint, int> CmdCount = [];
    private static readonly Dictionary<uint, int> CmdBytes = [];

    public static bool OnReceive(BinaryReader reader) {
        var cmdId = reader.ReadUInt32();
        var bytes = reader.ReadBytes();
        CmdCount[cmdId] = CmdCount.GetValueOrDefault(cmdId) + 1;
        CmdBytes[cmdId] = CmdBytes.GetValueOrDefault(cmdId) + bytes.Length;
        FullSyncExporter.AddPacket(cmdId, bytes);
        return false; // 持续捕获, 直到 DLL 发 0xFF
    }

    /// <summary>离线模式: 直接对已有 packet_dump.bin 执行导出 (读入累加器, 不启动游戏)。</summary>
    public static void ExportDump(string path) {
        var dumpPath = Path.GetFullPath(path);
        if (!File.Exists(dumpPath)) {
            AnsiConsole.WriteLine($"文件不存在: {dumpPath}");
            return;
        }
        FullSyncExporter.Clear();
        CmdCount.Clear();
        CmdBytes.Clear();
        using var fs = File.OpenRead(dumpPath);
        var h = new byte[8];
        var b = new byte[1 << 20];
        while (fs.Position < fs.Length) {
            try { fs.ReadExactly(h, 0, 8); } catch (EndOfStreamException) { break; }
            var c = BitConverter.ToUInt32(h, 0);
            var l = BitConverter.ToInt32(h, 4);
            if (l < 0 || l > (1 << 24)) break; // len=0 是合法的空负载包
            if (l > b.Length) b = new byte[l];
            try { fs.ReadExactly(b, 0, l); } catch (EndOfStreamException) { break; }
            CmdCount[c] = CmdCount.GetValueOrDefault(c) + 1;
            CmdBytes[c] = CmdBytes.GetValueOrDefault(c) + l;
            FullSyncExporter.AddPacket(c, b.AsSpan(0, l));
        }
        AnsiConsole.WriteLine($"离线导出模式: {dumpPath}");
        OnFinish(dumpPath);
    }

    /// <summary>DLL 发 0xFF (全量同步完成) 时由 GUI 调用。sourceLabel 为空表示实时捕获。</summary>
    public static void OnFinish(string sourceLabel = "") {
        var totalBytes = CmdBytes.Values.Sum();
        var totalPkts = CmdCount.Values.Sum();
        AnsiConsole.WriteLine($"已捕获 {totalPkts} 包 / {totalBytes / 1024.0:F1} KB");
        var lines = CmdCount.OrderByDescending(kv => kv.Value)
            .Select(kv => $"  cmdId={kv.Key,6}  x{kv.Value,5}  {CmdBytes[kv.Key] / 1024.0,8:F1} KB");
        AnsiConsole.WriteLine($"包类型分布 (top {Math.Min(lines.Count(), 10)}):");
        foreach (var line in lines.Take(10)) {
            AnsiConsole.WriteLine(line);
        }
        FullSyncExporter.Export(sourceLabel);
    }
}

/// <summary>极简通用 protobuf 树解析器 (仅用于结构识别, 不做字段语义推断)。</summary>
internal static class ProtoWalker {

    public readonly record struct LD(byte[] Data);

    public static Dictionary<int, List<object>> Walk(ReadOnlySpan<byte> b) {
        var fields = new Dictionary<int, List<object>>();
        var off = 0;
        while (off < b.Length) {
            if (!TryReadVarint(b, ref off, out var tag)) break;
            var field = (int) (tag >> 3);
            var wire = (int) (tag & 7);
            if (field == 0) break;
            switch (wire) {
                case 0:
                    if (TryReadVarint(b, ref off, out var v)) {
                        Add(fields, field, v);
                    }
                    break;
                case 1:
                    if (off + 8 > b.Length) return fields;
                    Add(fields, field, BitConverter.ToUInt64(b.Slice(off, 8)));
                    off += 8;
                    break;
                case 2:
                    if (!TryReadVarint(b, ref off, out var len) || len > 16 * 1024 * 1024 || off + (int) len > b.Length) {
                        return fields;
                    }
                    Add(fields, field, new LD(b.Slice(off, (int) len).ToArray()));
                    off += (int) len;
                    break;
                case 5:
                    if (off + 4 > b.Length) return fields;
                    Add(fields, field, BitConverter.ToUInt32(b.Slice(off, 4)));
                    off += 4;
                    break;
                default:
                    return fields;
            }
        }
        return fields;
    }

    private static void Add(Dictionary<int, List<object>> fields, int field, object value) {
        if (!fields.TryGetValue(field, out var list)) {
            list = [];
            fields[field] = list;
        }
        list.Add(value);
    }

    public static ulong? GetFirstVarint(this Dictionary<int, List<object>> fields, int field) {
        if (!fields.TryGetValue(field, out var list)) return null;
        foreach (var v in list) {
            if (v is ulong u) return u;
        }
        return null;
    }

    /// <summary>取 repeated uint32 字段, 兼容 packed(字节块内 varint) 与 unpacked(逐个 varint)。</summary>
    public static List<ulong> GetPackedList(this Dictionary<int, List<object>> fields, int field) {
        var outList = new List<ulong>();
        if (!fields.TryGetValue(field, out var list)) return outList;
        foreach (var v in list) {
            switch (v) {
                case ulong u:
                    outList.Add(u);
                    break;
                case LD ld:
                    var off = 0;
                    while (off < ld.Data.Length && TryReadVarint(ld.Data.AsSpan(), ref off, out var n)) {
                        outList.Add(n);
                    }
                    break;
            }
        }
        return outList;
    }

    private static bool TryReadVarint(ReadOnlySpan<byte> b, ref int off, out ulong value) {
        value = 0;
        var shift = 0;
        while (off < b.Length && shift < 64) {
            var x = b[off++];
            value |= (ulong) (x & 0x7F) << shift;
            if (x < 0x80) return true;
            shift += 7;
        }
        return false;
    }
}
