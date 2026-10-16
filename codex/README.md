# 嘉然 · 番茄炒蛋：Codex 版

供支持本地 v2 宠物格式的 Codex Desktop 加载。此包提供 9 种标准动作与 16 个视线方向，图集为 1536 × 2288，每格 192 × 208，共 73 个有效帧。

## 安装

在 PowerShell 中进入本目录，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

脚本将 `pet.json` 和 `spritesheet.webp` 复制到 `$env:CODEX_HOME/pets/jiaran-classic-v3`；没有设置 `CODEX_HOME` 时使用 `$env:USERPROFILE/.codex/pets/jiaran-classic-v3`。

打开 Codex 的宠物设置，刷新本地宠物列表，再选择 **嘉然 · 番茄炒蛋**。部分宿主版本的入口为 `Settings → Mini & Pets → Refresh`；界面名称可能随版本变化。安装脚本不改写 `config.toml`，也不强制重启当前应用。

如果目标目录已经存在，脚本会保留它并报错。确认需要更新时加 `-Force`；原文件会备份到用户临时目录中的 `JiaranPetBackups`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -Force
```

可用 `-DestinationRoot` 指定自定义宠物根目录，或用 `-WhatIf` 查看计划，不实际复制。

也可手动创建 `pets/jiaran-classic-v3/`，只复制 `pet.json` 与 `spritesheet.webp`。其他说明、脚本与预览无需装入宿主目录。

## 兼容性与限制

- 清单格式为 `spriteVersionNumber: 2`；当前宿主需支持此本地格式和宠物功能。
- Codex 负责显示并选择动作。本包没有脚本、网络请求或音频播放功能。
- 想使用番茄钟和独立互动，请使用仓库中的 Windows 桌面版。
- 本地复制成功不代表运行中的宿主已经刷新缓存；请在宿主设置里刷新并选中。

## 卸载

先在 Codex 设置中切换到其他宠物，再删除 `pets/jiaran-classic-v3/`。保留 `.codex` 里的其他文件和配置。

图集 SHA-256：

```text
951a2bb9f5ff2fec689394b0fdfa45e0c8e17ccdb43a82350e4725c90159155b
```

角色和素材权利范围见仓库根目录的 `ASSETS.md`。
