# RA2-LiveController (红警2·尤里复仇 全局实时调控器 & 底层引擎修复套件)

[![Platform](https://img.shields.io/badge/Platform-Windows%20(x86)-blue.svg)]()
[![Framework](https://img.shields.io/badge/.NET-10.0%20Windows-purple.svg)]()
[![Game](https://img.shields.io/badge/Game-Yuri's%20Revenge%201.001%20%2F%20Ares%20%2F%20Phobos-red.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

专为 **《红色警戒2：尤里复仇》**（兼容原版 1.001、Ares 3.0+ 拓展、各类大型 MOD 如重聚未来、心灵终结、复仇之日等）打造的**无侵入式外部实时对局调控器与西木祖传引擎修复套件**。

本项目旨在解决红警2社区二十年来困扰无数玩家与 MOD 作者的几大痛点：**零冷却出兵吞兵丢失 Bug**、**遭遇战无有效全局暂停（被基洛夫等空军偷家）**、**战局残存孤儿残兵导致无法胜利卡局**、以及**遭遇战 AI 难度不可实时双向调节**等问题。

---

## ✨ 核心特性

### 1. 🛡️ 独家内存热补丁：零冷却出兵防吞/防丢保护 (Anti-Drop Fix)
- **痛点**：在开启 0 秒瞬间建造或高攻速连造兵/造坦克时，经常出现“明明排队点了 10 个单位，进度条读完了 10 个，但重工/兵营门口只挤出 2 辆坦克，其余 8 辆凭空蒸发”的恶性吞兵 Bug。
- **技术原理**：西木头原版引擎逻辑中，当 `BuildingClass::Exit_Object` 检测到出兵口被友军、建筑物或碎屑阻挡时，会直接跳转调用 `Abandon_Production` 销毁刚造好的单位！
- **修复方案**：实时注入热补丁，将 `0x0044ec94`、`0x0044ee17`、`0x00509218` 等多处工厂判定跳转指令改为安全等待重试（`JNZ -> JMP` 等待出口畅通），**确保读条 10 个 100% 吐出 10 个单位，绝不吞兵漏兵！**

### 2. ⏸️ 绝对时空静止 / 倒水安全暂停 (Airtight Time Stop)
- **痛点**：单机遭遇战接电话、去倒水或上厕所时，简单的减速或原版暂停经常会被飞行的**基洛夫空艇、敌军战机或防御塔**继续攻击，导致“倒水回来基地被炸光”。
- **技术原理**：
  1. 西木引擎将地面部队（`FootClass`）与空军战机/空艇（`AircraftClass`）放置在**两个完全分离的全局动态数组**中；
  2. 仅仅修改移动速度为 0 并不能关闭武器投弹装填与索敌判定。
- **解决方案**：控制器同时遍历接管 `FootClass`、`AircraftClass`、`BuildingClass`、`FactoryClass`，写入 `TechnoClass::Deactivated = 1`（物理级 EMP 断电），并将武器装填计时器锁死为 `999999`。**基洛夫悬停熄火、战机静止、防御塔停电、地面石化，空地一体绝对安全！**

### 3. 🤖 AI 遭遇战全局实时双向动态调控 (Dynamic AI Buff & Nerf)
- **不仅能削弱，更能随时增强 AI 挑战噩梦难度**：
  - **经济调控**：一键清空 AI 资金（断粮削弱） / 一键赠予 AI 50万~200万 / 锁定 AI 无限金钱（999万暴兵）。
  - **建造速度**：支持冻结 (0x)、慢5倍 (0.2x)、正常 (1.0x)、快2倍 (2.0x)、快5倍 (5.0x)、**10倍狂飙秒造 (10.0x)**。
  - **部队移速**：从 0x 定身到 5.0x 疾风狂飙无级设定。
  - **火力与攻速**：从削弱 10 倍到 **强化 10 倍狂暴毁灭火力** 实时生效。

### 4. 🧹 孤儿残兵自动肃清 & 一键战局决胜 (Orphan Purge & Instant Victory)
- **自动排查**：每 150ms 自动扫描场上已无任何基地的阵营。一旦 AI 基地全灭，其残留在地图盲区、黑雾角落发呆的孤儿工程师、恐怖机器人或隐形单位将被瞬间抹除，绝不卡胜局结算。
- **全图卫星与直接决胜**：
  - 快捷键 **`[F11]`**：调用原版地图揭示函数 `MapClass::Reveal_All` 全图去黑幕。
  - 快捷键 **`[F12]`**：瞬秒全图残兵并注入执行 `HouseClass::DestroyAll` + `AcceptDefeat`，判定胜利。

---

## ⌨️ 游戏内全局热键清单

无需切屏，在红警2对局中直接敲击键盘即可瞬发执行（伴随提示音）：

| 快捷键 | 功能描述 |
| :---: | :--- |
| **`Pause`** 或 **`F10`** | **⏸️ 绝对时空静止**：全空地海防彻底断电停摆（基洛夫/战机/防御塔全面熄火，安心倒水） |
| **`F5`** | **⭐ 玩家急救箱**：玩家立即增加 100 万金钱 + 激活 0 秒瞬间建造 |
| **`F6`** | **💥 削弱 AI**：瞬间清空场上所有电脑 AI 的金钱与储矿 |
| **`F7`** | **💰 增强 AI**：全体电脑 AI 各增加 100 万金钱，激发疯狂进攻 |
| **`F8`** | **🐢 节奏切换 (削弱)**：AI 建造速度与移速慢 5 倍 (0.2x) / 恢复正常 切换 |
| **`F9`** | **⚡ 狂暴切换 (增强)**：AI 建造速度与移速狂暴 3 倍 (3.0x) / 恢复正常 切换 |
| **`F11`** | **🛰️ 间谍卫星全图开图**：破除全图黑幕，一切残兵潜艇无所遁形 |
| **`F12`** | **🏆 战局直接胜利**：瞬间歼灭全部敌军并触发投降判定 |

---

## 🏗️ 架构与内存技术细节

西木 C&C 引擎核心数据结构与本调控器的接管基址（基于 YR 1.001）：

```
[gamemd.exe 内存空间]
  ├── HouseClass::Array       (0x00A8022C / 0x00A80238) -> 阵营经济、科技、战力全局表
  ├── FactoryClass::Array     (0x00A83E34 / 0x00A83E40) -> 重工厂、兵营、船坞生产进度队列
  ├── FootClass::Array        (0x008B3DC4 / 0x008B3DD0) -> 地面坦克、机动车、各兵种步兵、水面水下舰队
  ├── AircraftClass::Array    (0x00A8E394 / 0x00A8E3A0) -> 基洛夫空艇、入侵者、黑鹰、歼10、各类直升机
  └── BuildingClass::Array    (0x00A8EB44 / 0x00A8EB50) -> 基地建筑、防御塔、超级武器
```

- **`TechnoClass::Deactivated`（偏移 `+0x1C8`）**：
  红警2引擎的底层停电/EMP状态标志位。设为 1 时，引擎跳过其 `AI()` 索敌与 `Fire()` 投弹循环，实现物理级安全石化。
- **`TechnoClass::WeaponReloadTimer`（偏移 `+0x2C4`）**：
  武器装填计时器。锁死为 999999 阻断一切攻击输出。

---

## 🚀 编译与运行指南

### 环境依赖
- Windows 10 / Windows 11 (x64 / x86)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)

### 快速编译单文件发布版
```bash
git clone https://github.com/<your-username>/RA2-LiveController.git
cd RA2-LiveController
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true
```

编译生成的可执行文件位于：
`bin/Release/net10.0-windows/win-x86/publish/RA2LiveController.exe`

无需安装任何运行时，单文件双击即可直接运行。

---

## 📜 开源协议

本项目基于 [MIT License](LICENSE) 开源。欢迎 Red Alert 2 玩家与 MOD 开发者自由使用、改进和集成。
