# -*- coding: utf-8 -*-
"""
parse_full_sync.py — 从 packet_dump 解析全量同步, 输出 full_sync JSON

用途: 作为版本字段号校准的参考工具 (与扩展内 FullSyncExporter.cs 逻辑一致)。
自包含, 无本地数据依赖。

字段号 (v7.0.0 实测):
  cmd 2516  QuestListNotify            -> 任务簿, 列表字段 15
  cmd 23849 FinishedParentQuestNotify  -> 完成历史, 列表字段 12, parent_id=12, accept_time=2
  cmd 29910 AchievementAllDataNotify   -> 成就, 列表字段 5, id=5, status=8, progress=9
  Quest: quest_id=1, state=2, start_time=4, accept_time=9, parent_quest_id=6, finish_progress=11
  (字段号以官方 7.0.0 proto + LunaGC 为准; ParentQuest 无 finish_time, 完成时间不可得)

用法:
  python parse_full_sync.py <packet_dump_*.bin>... [-o out.json]
"""
import struct, sys, json, datetime
from collections import Counter

QUESTS_CMD, PARENT_CMD, ACH_CMD = 2516, 23849, 29910
QUEST_STATE = {0: "NONE", 1: "UNSTARTED", 2: "UNFINISHED", 3: "FINISHED",
               4: "REWARD_TAKEN", 5: "FAILED"}
ACH_STATUS = {0: "INVALID", 1: "UNFINISHED", 2: "FINISHED", 3: "REWARD_TAKEN"}


def read_varint(b, o):
    r, s = 0, 0
    while True:
        if o >= len(b):
            raise ValueError("overrun")
        x = b[o]; o += 1
        r |= (x & 0x7F) << s
        if x < 0x80:
            return r, o
        s += 7


def walk_message(b):
    fields = {}
    o = 0
    try:
        while o < len(b):
            tag, o = read_varint(b, o)
            field, wire = tag >> 3, tag & 7
            if field == 0:
                break
            if wire == 0:
                v, o = read_varint(b, o)
                fields.setdefault(field, []).append(v)
            elif wire == 2:
                ln, o = read_varint(b, o)
                if o + ln > len(b):
                    break
                fields.setdefault(field, []).append(bytes(b[o:o + ln]))
                o += ln
            elif wire == 1:
                if o + 8 > len(b):
                    break
                fields.setdefault(field, []).append(int.from_bytes(b[o:o + 8], "little"))
                o += 8
            elif wire == 5:
                if o + 4 > len(b):
                    break
                fields.setdefault(field, []).append(int.from_bytes(b[o:o + 4], "little"))
                o += 4
            else:
                break
    except (ValueError, IndexError):
        pass
    return fields


def first_varint(fields, field):
    for v in fields.get(field, []):
        if isinstance(v, int):
            return v
    return None


def load_dump(path):
    data = open(path, "rb").read()
    out = []
    off = 0
    while off + 8 <= len(data):
        cmd, ln = struct.unpack_from("<II", data, off)
        off += 8
        if ln < 0 or off + ln > len(data):
            break
        out.append((cmd, data[off:off + ln]))
        off += ln
    return out


def parse_quest(data):
    f = walk_message(data)
    qid = first_varint(f, 1)
    st = first_varint(f, 2)
    if qid is None or st is None or st > 15:
        return None
    return {"quest_id": qid, "state": st,
            "state_name": QUEST_STATE.get(st, f"UNKNOWN_{st}"),
            "start_time": first_varint(f, 4) or 0,
            "accept_time": first_varint(f, 9) or 0,
            "parent_quest_id": first_varint(f, 6) or 0,
            "finish_progress": first_varint(f, 11) or 0}


def parse_parent(data):
    f = walk_message(data)
    pid = first_varint(f, 12)
    if pid is None:
        return None
    return {"parent_quest_id": pid, "accept_time": first_varint(f, 2) or 0}


def parse_ach(data):
    f = walk_message(data)
    aid = first_varint(f, 5)
    st = first_varint(f, 8)
    if aid is None or st is None or st > 4:
        return None
    return {"achievement_id": aid, "status": st,
            "status_name": ACH_STATUS.get(st, f"UNKNOWN_{st}"),
            "progress": first_varint(f, 9) or 0}


def extract_ld_list(payload, field):
    f = walk_message(payload)
    return [v for v in f.get(field, []) if isinstance(v, bytes)]


def main():
    args = sys.argv[1:]
    if not args:
        print(__doc__)
        return
    out_path = None
    paths = []
    i = 0
    while i < len(args):
        if args[i] in ("-o", "--out") and i + 1 < len(args):
            out_path = args[i + 1]
            i += 2
        else:
            paths.append(args[i])
            i += 1

    quests, parents, achs = {}, {}, {}
    for path in paths:
        for cmd, payload in load_dump(path):
            if cmd == QUESTS_CMD:
                for ld in extract_ld_list(payload, 15):
                    q = parse_quest(ld)
                    if q:
                        quests[q["quest_id"]] = q
            elif cmd == PARENT_CMD:
                for ld in extract_ld_list(payload, 12):
                    p = parse_parent(ld)
                    if p:
                        parents[p["parent_quest_id"]] = p
            elif cmd == ACH_CMD:
                for ld in extract_ld_list(payload, 5):
                    a = parse_ach(ld)
                    if a:
                        achs[a["achievement_id"]] = a

    print(f"子任务: {len(quests)} | 完成历史: {len(parents)} | 成就: {len(achs)}")
    print(f"成就状态: {dict(Counter(a['status_name'] for a in achs.values()))}")

    out = {"source": paths, "sub_quests": len(quests),
           "generated_at": datetime.datetime.now().isoformat(),
           "quests": sorted(quests.values(), key=lambda x: x["quest_id"]),
           "parent_quests": sorted(parents.values(), key=lambda x: x["parent_quest_id"]),
           "achievements": sorted(achs.values(), key=lambda x: x["achievement_id"])}
    out_path = out_path or f"full_sync_{datetime.datetime.now():%Y%m%d%H%M%S}.json"
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"已导出 -> {out_path}")


if __name__ == "__main__":
    main()
