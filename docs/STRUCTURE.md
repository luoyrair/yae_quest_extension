# 目录结构

```
yae_quest_extension/
├── build.ps1                       # 一键构建（依赖检测/安装 + 拉取 + 补丁 + 发布）
├── patch/yae_quest_ext.patch       # 对官方 Yae 的补丁（5 个修改文件）
├── files/YaeAchievement/src/Parsers/
│   ├── PacketCapture.cs            # 新文件：全量包进程内解析 + 离线导出
│   └── FullSyncExporter.cs         # 新文件：完成历史/任务簿解析 + UIGF 导出
├── scripts/regenerate_patch.ps1    # 维护：从修改后的 Yae 重新生成补丁
├── tools/parse_full_sync.py        # 校准：独立 Python 解析器（无本地依赖）
├── docs/
│   ├── STRUCTURE.md                # 本文件：目录结构
│   ├── BUILD.md                    # 构建需求 + 一键构建
│   ├── USAGE.md                    # 使用流程 + 产出
│   ├── FORMAT.md                   # UIGF Quest Record 输出格式
│   ├── FIELDS.md                   # 版本相关字段号 + 大版本更新校准方法
│   ├── VERIFICATION.md             # 验证结果
│   └── MAINTENANCE.md              # 维护：重新生成补丁
├── examples/                       # 脱敏示例输出
└── .github/workflows/build.yml     # CI：Windows 构建验证
```
