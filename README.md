# RA2-LiveController (红警2·尤里复仇 全局实时调控器 & 底层引擎修复套件)

<div align="center">

[![Build and Release](https://github.com/Wsolitary/RA2-LiveController/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/Wsolitary/RA2-LiveController/actions/workflows/build-and-release.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%20(x86)-blue.svg?logo=windows)]()
[![Framework](https://img.shields.io/badge/.NET-10.0%20Windows-512BD4.svg?logo=dotnet)]()
[![Game](https://img.shields.io/badge/Game-Yuri's%20Revenge%201.001%20%2F%20Ares-E60000.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

*专为《红色警戒2：尤里复仇》打造的**非侵入式外部实时对局调控器与西木祖传引擎修复套件**。*

[特性亮点](#-核心特性) • [技术原理](#-核心技术深度剖析) • [全局热键清单](#-游戏内全局热键清单) • [目录结构](#-项目目录结构) • [快速构建](#-本地编译与构建) • [English Summary](#-english-summary)

</div>

---

## 📖 项目简介 (Overview)

在《红色警戒2：尤里复仇》（YR 1.001 及各类 Ares 大型 MOD 如重聚未来、心灵终结、复仇之日等）的漫长历史中，一直存在着若干困扰玩家与 MOD 开发者的底层顽疾：
1. **高频造兵严重吞兵**：在 0 秒瞬间建造或高速连续出兵时，玩家队列点了 10 辆坦克，生产进度条也全部走完，但因为门口被轻微阻挡，最后只挤出来 2 辆，其余 8 辆直接蒸发；
2. **遭遇战无有效全局暂停**：遇到电话或离开接水时，常规暂停或减速无法阻止空中的**基洛夫空艇、敌军战机与防御塔**继续投弹开火，导致“回来基地被炸光”；
3. **残局孤儿残兵卡关**：敌方基地全灭后，AI 丢失战术小队指针，留下一两个迷路的隐形小车或工程师在黑雾盲区发呆，游戏无法判定胜利；
4. **单机 AI 难度不可实时调节**：原版只能在开局前选择固定难度，无法在对局中临时增强或削弱 AI。

**RA2-LiveController** 通过 Windows 底层内存接管技术与热补丁注入，无缝解决了上述全部痛点。

---

## ✨ 核心特性 (Key Features)

### 1. 🛡️ 独家热补丁：零冷却出兵防吞/防丢保护 (Anti-Drop Fix)
- **底层成因**：西木原版引擎逻辑中，当 `BuildingClass::Exit_Object` 检测到出兵口被友军、地形阻挡或拥挤时，会直接跳转调用 `Abandon_Production` 将已造好的单位物理销毁！
- **修复方案**：实时注入热补丁，将 `0x0044ec94`、`0x0044ee17`、`0x00509218` 等 7 处工厂判定跳转指令改为安全等待重试（`JNZ -> JMP` 等待出口畅通），**确保读条 10 个 100% 吐出 10 个单位，绝不吞兵漏兵！**

### 2. ⏸️ 绝对时空静止 / 倒水安全暂停 (Airtight Time Stop)
- **底层成因**：
  1. 西木引擎将地面部队（`FootClass`）与空军战机/基洛夫空艇（`AircraftClass`）放置在**两个完全分离的独立全局数组**中；
  2. 仅仅修改移动速度为 0 并不能关闭武器投弹装填与索敌判定。
- **解决方案**：控制器同时遍历接管 `FootClass`、`AircraftClass`、`BuildingClass`、`FactoryClass`，写入 `TechnoClass::Deactivated = 1`（物理级 EMP 断电），并将武器装填计时器锁死为 `999999`。**基洛夫悬停熄火、战机静止、防御塔停电、地面石化，空地一体绝对安全！**

### 3. 🤖 AI 遭遇战全局实时双向动态调控 (Dynamic AI Tuner)
- **不仅能削弱，更能随时增强 AI 挑战极限**：
  - **经济调控**：一键清空 AI 资金（断粮削弱） / 一键赠予 AI 50万~200万 / 锁定 AI 无限金钱（999万暴兵）。
  - **建造速度**：支持冻结 (0x)、慢5倍 (0.2x)、正常 (1.0x)、快2倍 (2.0x)、快5倍 (5.0x)、**10倍狂飙秒造 (10.0x)**。
  - **部队移速**：从 0x 定身到 5.0x 疾风狂飙无级设定。
  - **火力与攻速**：从削弱 10 倍到 **强化 10 倍狂暴毁灭火力** 实时生效。

### 4. 🧹 孤儿残兵自动肃清 & 一键战局决胜 (Orphan Purge & Instant Victory)
- **自动排查**：每 150ms 自动扫描场上已无任何基地的阵营。一旦某 AI 基地全灭，其残留在地图盲区、黑雾角落发呆的孤儿工程师、恐怖机器人或残余空军将被瞬间抹除，绝不卡胜局结算。
- **全图卫星与直接决胜**：
  - 快捷键 **`[F11]`**：调用原版地图揭示函数 `MapClass::Reveal_All` 全图去黑幕。
  - 快捷键 **`[F12]`**：瞬秒全图残兵并注入执行 `HouseClass::DestroyAll` + `AcceptDefeat`，判定胜利。

---

## 🔬 核心技术深度剖析 (Architecture & Engine Internals)

西木 C&C 引擎核心数据结构与本调控器的接管基址（基于 YR 1.001）：

```
[gamemd.exe 内存空间]
  ├── HouseClass::Array       (0x00A8022C / 0x00A80238) -> 阵营经济、科技、战力全局表
  ├── FactoryClass::Array     (0x00A83E34 / 0x00A83E40) -> 重工厂、兵营、船坞生产进度队列
  ├── FootClass::Array        (0x008B3DC4 / 0x008B3DD0) -> 地面坦克、机动车、各兵种步兵、水面水下舰队
  ├── AircraftClass::Array    (0x00A8E394 / 0x00A8E3A0) -> 基洛夫空艇、入侵者、黑鹰、歼10、各类直升机
  └── BuildingClass::Array    (0x00A8EB44 / 0x00A8EB50) -> 基地建筑、防御塔、超级武器
```

### 关键数据偏移 (Struct Offsets)

| 偏移地址 | 变量名称 | 作用说明 |
| :--- | :--- | :--- |
| `TechnoClass + 0x1C8` | `byte Deactivated` | 底层断电/EMP 状态位。为 1 时引擎完全跳过该单位的索敌和投弹循环。 |
| `TechnoClass + 0x2C4` | `int WeaponReloadTimer` | 武器射击/投弹冷却计时器。锁定 999999 阻断一切火力输出。 |
| `TechnoClass + 0x580` | `double SpeedMultiplier` | 单位移动速度倍率（影响 Locomotor 推进计算）。 |
| `TechnoClass + 0xAC`  | `int Mission` | 单位当前任务状态码（0 = Sleep, 5 = Guard, 6 = Attack 等）。 |
| `HouseClass + 0x188`  | `double FirepowerBias` | 阵营全局武器伤害倍率。 |
| `HouseClass + 0x1A8`  | `double ROFBias` | 阵营全局攻击频率（装填间隔倒数）倍率。 |
| `HouseClass + 0x1B8`  | `double BuildSpeedBias` | 阵营建造消耗时间倍率（0.0001 即瞬间秒造）。 |

---

## ⌨️ 游戏内全局热键清单 (Hotkeys Reference)

无需切屏，在红警2对局中直接按键即可生效（伴随提示音）：

| 快捷键 | 功能描述 | 适用场景 |
| :---: | :--- | :--- |
| **`Pause`** 或 **`F10`** | **⏸️ 绝对时空静止**：全空地海防彻底断电停摆 | 去倒水、上厕所、接电话、紧急暂停 |
| **`F5`** | **⭐ 玩家急救箱**：玩家立即增加 100 万金钱 + 激活瞬间建造 | 战局逆风救援、快速成型体验 |
| **`F6`** | **💥 削弱 AI**：瞬间清空场上所有电脑 AI 的金钱与储矿 | 彻底断绝对手经济暴兵 |
| **`F7`** | **💰 增强 AI**：全体电脑 AI 各增加 100 万金钱 | 刺激 AI 疯狂进攻挑战极限 |
| **`F8`** | **🐢 节奏切换 (削弱)**：AI 建造速度与移速慢 5 倍 (0.2x) / 恢复正常 | 慢动作练手、精细化微操 |
| **`F9`** | **⚡ 狂暴切换 (增强)**：AI 建造速度与移速狂暴 3 倍 (3.0x) / 恢复正常 | 刺激求生、防守地图大考验 |
| **`F11`** | **🛰️ 间谍卫星全图开图**：破除全图黑幕 | 侦察敌情、排查残存潜艇 |
| **`F12`** | **🏆 战局直接胜利**：瞬间歼灭全部敌军并触发投降判定 | 拒绝垃圾时间、快速结算 |

---

## 📁 项目目录结构 (Repository Layout)

```
RA2-LiveController/
├── .github/
│   ├── workflows/
│   │   └── build-and-release.yml  # GitHub Actions 自动化 CI 构建与 Tag Release 归档
│   └── ISSUE_TEMPLATE/            # Issue 反馈与功能提议模板
├── src/
│   ├── Core/
│   │   ├── NativeMethods.cs       # Windows API (RPM / WPM / VirtualProtect / RemoteThread)
│   │   ├── GameConstants.cs       # 西木尤里复仇 1.001 核心基址与结构体偏移量
│   │   └── MemoryManager.cs       # 进程内存安全读写与远程线程调度封装
│   ├── Models/
│   │   └── HouseSnapshot.cs       # 阵营状态快照实体模型
│   ├── Patches/
│   │   └── AntiDropPatcher.cs     # 零冷却防吞兵丢兵核心热补丁器
│   ├── UI/
│   │   ├── MainForm.cs            # 主窗体生命周期、热键捕获与实时调度主逻辑
│   │   └── MainForm.UI.cs         # 暗黑电竞风格 WinForms 界面排版与视觉控件
│   └── Program.cs                 # 程序入口点
├── .editorconfig                  # 代码风格规范配置
├── .gitignore                     # Git 忽略配置（忽略构建输出与临时缓存）
├── app.manifest                   # 管理员提权与 Windows 视觉样式清单
├── build.bat                      # Windows 双击一键本地发布脚本
├── build.ps1                      # PowerShell 自动化构建脚本
├── LICENSE                        # MIT 开源授权协议
├── RA2LiveController.csproj       # .NET 10.0 Windows Forms 工程定义
└── README.md                      # 项目说明文档
```

---

## 🛠️ 本地编译与构建 (Build & Run)

### 前置环境需求
- 操作系统：Windows 10 / Windows 11 (x64 / x86)
- 编译工具：[.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（或更高版本）

### 方式一：双击一键构建 (推荐)
直接双击根目录下的 **`build.bat`** 即可自动完成构建。

### 方式二：使用 .NET CLI
```bash
# 克隆仓库
git clone https://github.com/Wsolitary/RA2-LiveController.git
cd RA2-LiveController

# 编译并发布单文件自包含独立免安装 exe
dotnet publish RA2LiveController.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

编译产物将生成在：
`bin/Release/net10.0-windows/win-x86/publish/RA2LiveController.exe`

无需安装任何运行时，单文件双击即可直接运行。

---

## 🌐 English Summary

**RA2-LiveController** is an advanced external runtime controller and memory hot-patching toolkit for *Command & Conquer: Red Alert 2 - Yuri's Revenge* (v1.001, Ares 3.0+, and major overhaul mods like Reunion, Mental Omega, etc.).

### Key Highlights:
- **Zero-Cooldown Anti-Drop Patch**: Fixes Westwood's 20-year-old factory exit congestion bug where queueing units with instant build causes 80%+ of manufactured units to be silently discarded by `Abandon_Production`.
- **Airtight Safe Time-Stop (AFK Pause)**: Simultaneously traverses both `FootClass` (ground/naval) and `AircraftClass` (Kirov airships, fighter jets) arrays and applies `TechnoClass::Deactivated = 1` and weapon reload lockdown (`ReloadTimer = 999999`), guaranteeing complete cease-fire and engine shutdown so players can safely pause the skirmish without getting bombed by lingering airships.
- **Dynamic AI Buff & Nerf**: Real-time bidirectional tuning of AI build speed (0x - 10x), movement speed, firepower (0.1x - 10x), and economics without restarting the match.
- **Orphan Unit Auto-Purge & Instant Victory**: Automatically cleans up stranded units after an AI faction's buildings are destroyed, and provides hotkey commands for full-map reveal (`Reveal_All`) and immediate surrender triggering (`DestroyAll`).

---

## 📄 开源协议 (License)

本项目采用 [MIT License](LICENSE) 开源协议。欢迎各位红警玩家与 MOD 开发者自由使用、二次开发与代码集成。
