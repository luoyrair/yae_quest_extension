# Yae 任务扩展 (Yae Quest Extension)

> 基于 [HolographicHat/Yae](https://github.com/HolographicHat/Yae) (GPL-3.0) 的最小补丁，
> 让 Yae 在捕获成就的同时，额外导出**任务数据**与 **UIGF Quest Record**（纯数据格式）。
>
> 本仓库**不含完整扩展版源码**，只提供：补丁 + 新增文件 + 一键构建脚本，
> 在拉取的官方 Yae 上应用即可产出可用的扩展版。

## 功能

- **UIGF Quest Record v1.0 导出**：完成历史 + 任务簿，**纯数据**，展示由消费端解析
- **一键构建**：`build.ps1` 拉取 → 打补丁 → AOT 发布，依赖自动检测/安装（Win11 winget）

## 快速开始

```powershell
# 1) 一键构建（拉取官方 Yae + 打补丁 + AOT 发布）
cd yae_quest_extension
powershell -ExecutionPolicy Bypass -File build.ps1 -InstallDeps

# 2) 运行（游戏先关闭；登录进世界后自动导出 uigf_quest_record）
Yae\publish_quest\YaeAchievement.exe --quest
```

## 限制与风险

- **版本漂移**：任务 cmdId/字段号为硬编码（Yae 元数据只维护成就），大版本更新需校准后重新生成补丁 → 见 [docs/FIELDS.md](docs/FIELDS.md)
- **封号风险**：进程注入 + 钩子，存在低概率封号风险（非零），请自行评估账号价值
- **仅供学习研究**，请遵守游戏用户协议

## 许可

本项目修改自 [HolographicHat/Yae](https://github.com/HolographicHat/Yae)，遵循 **GPL-3.0**。
补丁与新文件随本仓库以 GPL-3.0 发布（见 [LICENSE](LICENSE)）。

## 文档

| 文档 | 内容 |
|---|---|
| [docs/BUILD.md](docs/BUILD.md) | 构建需求 + 一键构建 |
| [docs/USAGE.md](docs/USAGE.md) | 使用流程 + 产出格式 |
| [docs/FORMAT.md](docs/FORMAT.md) | UIGF Quest Record 输出格式 |
| [docs/FIELDS.md](docs/FIELDS.md) | 版本相关字段号 + 校准方法 |
| [docs/VERIFICATION.md](docs/VERIFICATION.md) | 验证结果 |
| [docs/MAINTENANCE.md](docs/MAINTENANCE.md) | 维护：重新生成补丁 |
| [docs/STRUCTURE.md](docs/STRUCTURE.md) | 目录结构 |
