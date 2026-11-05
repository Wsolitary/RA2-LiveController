using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RA2LiveController.Models;

namespace RA2LiveController.UI
{
    public partial class MainForm : Form
    {
        // UI Controls
        private Label _lblStatus;
        private Label _lblPatchStatus;
        private CheckBox _chkTopMost;
        private ListView _lvHouses;
        private Button _btnTimeStop;

        // AI UI elements
        private CheckBox _chkAiLockZeroMoney;
        private CheckBox _chkAiLockInfMoney;
        private CheckBox _chkAiInstantBuild;
        private Label _lblAiBuildSpeed;
        private Label _lblAiMoveSpeed;
        private Label _lblAiCombat;

        // Player UI elements
        private CheckBox _chkPlayerInstantBuild;
        private CheckBox _chkPlayerLockMoney;
        private CheckBox _chkAntiDrop;
        private CheckBox _chkAutoPurgeOrphans;
        private Label _lblPlayerSpeed;

        private void BuildUI()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 1,
                RowCount = 5
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));  // Status bar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160)); // Live table
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 245)); // AI Controls (Buff + Nerf + Safe AFK)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 195)); // Player Controls + Anti-Drop + Victory Tools
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Hotkeys footer

            // 1. Top Status Bar
            var topPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(34, 38, 48) };
            _lblStatus = new Label
            {
                Text = "🟡 正在扫描对局进程 (gamemd.exe)...",
                AutoSize = true,
                Location = new Point(10, 10),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 214, 102)
            };

            var btnLaunchGame = CreateButton("🚀 启动游戏", Color.FromArgb(45, 110, 75), 110);
            btnLaunchGame.Location = new Point(330, 6);
            btnLaunchGame.Click += (s, e) => LaunchGameClient();

            // Dedicated Safe AFK / Absolute Pause Button right on Top Bar
            _btnTimeStop = CreateButton("⏸️ 绝对时空静止 / 倒水暂停 (Pause)", Color.FromArgb(180, 45, 65), 245);
            _btnTimeStop.Location = new Point(450, 6);
            _btnTimeStop.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            _btnTimeStop.Click += (s, e) => ToggleAbsoluteTimeStop();

            _lblPatchStatus = new Label
            {
                Text = "🛡️ 防吞兵: 待机",
                AutoSize = true,
                Location = new Point(710, 10),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 220, 255)
            };

            _chkTopMost = new CheckBox
            {
                Text = "置顶",
                Checked = true,
                AutoSize = true,
                Location = new Point(965, 10),
                ForeColor = Color.FromArgb(180, 200, 255)
            };
            _chkTopMost.CheckedChanged += (s, e) => TopMost = _chkTopMost.Checked;

            topPanel.Controls.Add(_lblStatus);
            topPanel.Controls.Add(btnLaunchGame);
            topPanel.Controls.Add(_btnTimeStop);
            topPanel.Controls.Add(_lblPatchStatus);
            topPanel.Controls.Add(_chkTopMost);
            mainLayout.Controls.Add(topPanel, 0, 0);

            // 2. Live Houses Table
            _lvHouses = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(30, 33, 42),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _lvHouses.Columns.Add("阵营槽位", 110);
            _lvHouses.Columns.Add("身份", 85);
            _lvHouses.Columns.Add("现金余额", 120);
            _lvHouses.Columns.Add("矿场储矿折算", 120);
            _lvHouses.Columns.Add("建造速度状态", 130);
            _lvHouses.Columns.Add("部队移速倍率", 110);
            _lvHouses.Columns.Add("火力/攻速倍率", 110);
            mainLayout.Controls.Add(_lvHouses, 0, 1);

            // 3. AI Controls Group (Both Buff & Nerf + Airtight Freeze)
            var grpAi = CreateGroupBox("🤖 电脑 AI 全局实时调控（可削弱可增强，绝对静止定身基洛夫/战机/防御塔全面熄火）", Color.FromArgb(255, 140, 140));
            var aiPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(6, 2, 6, 2)
            };
            aiPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            aiPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            aiPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            aiPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            // Row 1: AI Money (Nerf & Buff)
            var rowAiMoney = CreateFlowRow();
            rowAiMoney.Controls.Add(CreateRowLabel("AI 金钱经济："));

            var btnClearAiMoney = CreateButton("💥 清空AI全部金钱", Color.FromArgb(170, 50, 50), 125);
            btnClearAiMoney.Click += (s, e) => ClearAllAiMoney();
            rowAiMoney.Controls.Add(btnClearAiMoney);

            _chkAiLockZeroMoney = new CheckBox
            {
                Text = "🔒 锁死AI为0 (经济断绝)",
                AutoSize = true,
                Margin = new Padding(4, 7, 6, 0),
                ForeColor = Color.FromArgb(255, 160, 160),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
            };
            _chkAiLockZeroMoney.CheckedChanged += (s, e) =>
            {
                _aiLockZeroMoney = _chkAiLockZeroMoney.Checked;
                if (_aiLockZeroMoney) _chkAiLockInfMoney.Checked = false;
            };
            rowAiMoney.Controls.Add(_chkAiLockZeroMoney);

            var btnAiMoney10k = CreateButton("设为1万", Color.FromArgb(55, 62, 78), 70);
            btnAiMoney10k.Click += (s, e) => { _chkAiLockZeroMoney.Checked = false; _chkAiLockInfMoney.Checked = false; SetAllAiMoney(10000); };
            rowAiMoney.Controls.Add(btnAiMoney10k);

            var btnAiMoney500k = CreateButton("💰 AI全体+50万", Color.FromArgb(40, 115, 75), 115);
            btnAiMoney500k.Click += (s, e) => { _chkAiLockZeroMoney.Checked = false; AddAllAiMoney(500000); };
            rowAiMoney.Controls.Add(btnAiMoney500k);

            var btnAiMoney2m = CreateButton("💎 AI全体+200万", Color.FromArgb(35, 125, 95), 120);
            btnAiMoney2m.Click += (s, e) => { _chkAiLockZeroMoney.Checked = false; AddAllAiMoney(2000000); };
            rowAiMoney.Controls.Add(btnAiMoney2m);

            _chkAiLockInfMoney = new CheckBox
            {
                Text = "🔒 锁定AI无限金钱 (999万暴兵)",
                AutoSize = true,
                Margin = new Padding(6, 7, 4, 0),
                ForeColor = Color.FromArgb(130, 255, 170),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
            };
            _chkAiLockInfMoney.CheckedChanged += (s, e) =>
            {
                _aiLockInfMoney = _chkAiLockInfMoney.Checked;
                if (_aiLockInfMoney) _chkAiLockZeroMoney.Checked = false;
            };
            rowAiMoney.Controls.Add(_chkAiLockInfMoney);

            aiPanel.Controls.Add(rowAiMoney, 0, 0);

            // Row 2: AI Build Speed (Nerf & Buff)
            var rowAiBuild = CreateFlowRow();
            rowAiBuild.Controls.Add(CreateRowLabel("AI 建造速度："));
            _lblAiBuildSpeed = CreateValueBadge("当前: 1.0x (正常)", Color.FromArgb(140, 220, 255));
            rowAiBuild.Controls.Add(_lblAiBuildSpeed);

            // Nerf presets
            AddPresetButton(rowAiBuild, "❄️ 冻结(0x)", 0.0, true);
            AddPresetButton(rowAiBuild, "🐢 慢5倍(0.2x)", 0.2, true);
            AddPresetButton(rowAiBuild, "慢2倍(0.5x)", 0.5, true);
            AddPresetButton(rowAiBuild, "正常(1.0x)", 1.0, true);

            // Buff presets (Strengthen AI)
            AddPresetButton(rowAiBuild, "⚡ 快2倍(2x)", 2.0, true);
            AddPresetButton(rowAiBuild, "⚡ 快5倍(5x)", 5.0, true);
            AddPresetButton(rowAiBuild, "⚡ 10倍狂飙(10x)", 10.0, true);

            _chkAiInstantBuild = new CheckBox
            {
                Text = "⚡ AI秒造/瞬间完成",
                AutoSize = true,
                Margin = new Padding(6, 7, 4, 0),
                ForeColor = Color.FromArgb(255, 215, 0),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
            };
            _chkAiInstantBuild.CheckedChanged += (s, e) =>
            {
                _aiInstantBuild = _chkAiInstantBuild.Checked;
                if (_aiInstantBuild)
                {
                    _aiBuildSpeed = 10.0;
                    _lblAiBuildSpeed.Text = "当前: ⚡ AI秒造";
                }
                else
                {
                    _lblAiBuildSpeed.Text = $"当前: {_aiBuildSpeed:0.##}x";
                }
                SyncCustomRulesIni();
            };
            rowAiBuild.Controls.Add(_chkAiInstantBuild);

            var btnResetAi = CreateButton("🔄 全面解冻/恢复正常", Color.FromArgb(45, 105, 125), 135);
            btnResetAi.Click += (s, e) => ResetAiState();
            rowAiBuild.Controls.Add(btnResetAi);

            aiPanel.Controls.Add(rowAiBuild, 0, 1);

            // Row 3: AI Move Speed (Nerf & Buff)
            var rowAiMove = CreateFlowRow();
            rowAiMove.Controls.Add(CreateRowLabel("AI 部队移速："));
            _lblAiMoveSpeed = CreateValueBadge("当前: 1.0x (正常)", Color.FromArgb(140, 220, 255));
            rowAiMove.Controls.Add(_lblAiMoveSpeed);

            // Nerf presets
            AddPresetButton(rowAiMove, "❄️ 定身(0x)", 0.0, false);
            AddPresetButton(rowAiMove, "🐢 慢5倍(0.2x)", 0.2, false);
            AddPresetButton(rowAiMove, "慢2倍(0.5x)", 0.5, false);
            AddPresetButton(rowAiMove, "正常(1.0x)", 1.0, false);

            // Buff presets (Strengthen AI)
            AddPresetButton(rowAiMove, "🚀 1.5倍(1.5x)", 1.5, false);
            AddPresetButton(rowAiMove, "🚀 2倍狂飙(2x)", 2.0, false);
            AddPresetButton(rowAiMove, "🚀 3倍神速(3x)", 3.0, false);
            AddPresetButton(rowAiMove, "🚀 5倍疾风(5x)", 5.0, false);

            aiPanel.Controls.Add(rowAiMove, 0, 2);

            // Row 4: AI Combat Firepower & Attack Speed (Nerf & Buff)
            var rowAiCombat = CreateFlowRow();
            rowAiCombat.Controls.Add(CreateRowLabel("AI 火力攻速："));
            _lblAiCombat = CreateValueBadge("当前: 1.0x (正常)", Color.FromArgb(140, 220, 255));
            rowAiCombat.Controls.Add(_lblAiCombat);

            // Nerf presets
            AddCombatButton(rowAiCombat, "🛡️ 削弱10倍(0.1x)", 0.1);
            AddCombatButton(rowAiCombat, "🛡️ 削弱5倍(0.2x)", 0.2);
            AddCombatButton(rowAiCombat, "削弱2倍(0.5x)", 0.5);
            AddCombatButton(rowAiCombat, "正常(1.0x)", 1.0);

            // Buff presets (Strengthen AI)
            AddCombatButton(rowAiCombat, "🔥 强化2倍(2x)", 2.0);
            AddCombatButton(rowAiCombat, "🔥 强化3倍(3x)", 3.0);
            AddCombatButton(rowAiCombat, "💥 狂暴5倍(5x)", 5.0);
            AddCombatButton(rowAiCombat, "⚡ 毁灭10倍(10x)", 10.0);

            aiPanel.Controls.Add(rowAiCombat, 0, 3);

            grpAi.Controls.Add(aiPanel);
            mainLayout.Controls.Add(grpAi, 0, 2);

            // 4. Player Controls Group
            var grpPlayer = CreateGroupBox("⭐ 玩家本人实时调控（基地出兵 & 零冷却防吞兵核心修复）", Color.FromArgb(120, 230, 150));
            var playerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(6, 2, 6, 2)
            };
            playerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            playerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            playerPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            // Player Row 1: Money & Instant Build & Anti-Drop Bug Fix
            var rowPlMoney = CreateFlowRow();
            rowPlMoney.Controls.Add(CreateRowLabel("玩家金钱/建造："));

            var btnPlAdd100k = CreateButton("💰 +10万 金钱", Color.FromArgb(40, 120, 75), 105);
            btnPlAdd100k.Click += (s, e) => AddPlayerMoney(100000);
            rowPlMoney.Controls.Add(btnPlAdd100k);

            var btnPlAdd1m = CreateButton("💎 +100万 金钱", Color.FromArgb(40, 120, 75), 115);
            btnPlAdd1m.Click += (s, e) => AddPlayerMoney(1000000);
            rowPlMoney.Controls.Add(btnPlAdd1m);

            _chkPlayerLockMoney = new CheckBox
            {
                Text = "🔒 锁定999万无限金钱",
                AutoSize = true,
                Margin = new Padding(6, 7, 6, 0),
                ForeColor = Color.FromArgb(150, 255, 180)
            };
            _chkPlayerLockMoney.CheckedChanged += (s, e) => _playerLockMoney = _chkPlayerLockMoney.Checked;
            rowPlMoney.Controls.Add(_chkPlayerLockMoney);

            _chkPlayerInstantBuild = new CheckBox
            {
                Text = "⚡ 玩家 0 秒瞬间建造/出兵",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 7, 8, 0),
                ForeColor = Color.FromArgb(255, 230, 100),
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            _chkPlayerInstantBuild.CheckedChanged += (s, e) =>
            {
                _playerInstantBuild = _chkPlayerInstantBuild.Checked;
                SyncCustomRulesIni();
            };
            rowPlMoney.Controls.Add(_chkPlayerInstantBuild);

            // CRITICAL BUG FIX TOGGLE: Anti-Drop / Unit Loss Protection
            _chkAntiDrop = new CheckBox
            {
                Text = "🛡️ 开启出兵防吞/防丢保护 (修复读10出2 Bug)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 7, 4, 0),
                ForeColor = Color.FromArgb(130, 240, 255),
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            _chkAntiDrop.CheckedChanged += (s, e) =>
            {
                _protectUnitLoss = _chkAntiDrop.Checked;
                UpdateAntiDropStatus();
            };
            rowPlMoney.Controls.Add(_chkAntiDrop);

            playerPanel.Controls.Add(rowPlMoney, 0, 0);

            // Player Row 2: Move Speed & Combat
            var rowPlSpeed = CreateFlowRow();
            rowPlSpeed.Controls.Add(CreateRowLabel("玩家移速/火力："));
            _lblPlayerSpeed = CreateValueBadge("移速: 1.0x | 火力: 1.0x", Color.FromArgb(160, 240, 180));
            rowPlSpeed.Controls.Add(_lblPlayerSpeed);

            var btnPlMove1 = CreateButton("正常移速(1x)", Color.FromArgb(55, 62, 78), 95);
            btnPlMove1.Click += (s, e) => SetPlayerBuffs(1.0, _playerCombatMult);
            rowPlSpeed.Controls.Add(btnPlMove1);

            var btnPlMove2 = CreateButton("🚀 2倍移速", Color.FromArgb(45, 95, 145), 90);
            btnPlMove2.Click += (s, e) => SetPlayerBuffs(2.0, _playerCombatMult);
            rowPlSpeed.Controls.Add(btnPlMove2);

            var btnPlMove3 = CreateButton("🚀 3倍狂飙", Color.FromArgb(45, 95, 145), 90);
            btnPlMove3.Click += (s, e) => SetPlayerBuffs(3.0, _playerCombatMult);
            rowPlSpeed.Controls.Add(btnPlMove3);

            var btnPlCombat1 = CreateButton("正常火力(1x)", Color.FromArgb(55, 62, 78), 95);
            btnPlCombat1.Click += (s, e) => SetPlayerBuffs(_playerMoveSpeed, 1.0);
            rowPlSpeed.Controls.Add(btnPlCombat1);

            var btnPlCombat3 = CreateButton("🔥 3倍火力+射速", Color.FromArgb(160, 85, 35), 120);
            btnPlCombat3.Click += (s, e) => SetPlayerBuffs(_playerMoveSpeed, 3.0);
            rowPlSpeed.Controls.Add(btnPlCombat3);

            var btnPlCombat5 = CreateButton("💥 5倍狂暴火力", Color.FromArgb(180, 60, 35), 120);
            btnPlCombat5.Click += (s, e) => SetPlayerBuffs(_playerMoveSpeed, 5.0);
            rowPlSpeed.Controls.Add(btnPlCombat5);

            playerPanel.Controls.Add(rowPlSpeed, 0, 1);

            // Player Row 3: Victory & Reveal Map Tools
            var rowPlVictory = CreateFlowRow();
            rowPlVictory.Controls.Add(CreateRowLabel("战局胜利/辅助："));

            var btnRevealMap = CreateButton("🛰️ 全图点亮 / 间谍卫星 (F11)", Color.FromArgb(35, 120, 150), 185);
            btnRevealMap.Click += (s, e) => RevealMap();
            rowPlVictory.Controls.Add(btnRevealMap);

            var btnEliminateEnemies = CreateButton("💥 歼灭全部AI残兵 (直接胜利) (F12)", Color.FromArgb(180, 45, 45), 235);
            btnEliminateEnemies.Click += (s, e) => EliminateAllEnemies();
            rowPlVictory.Controls.Add(btnEliminateEnemies);

            _chkAutoPurgeOrphans = new CheckBox
            {
                Text = "🧹 自动肃清无基地孤儿残兵 (防卡对局)",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(10, 7, 6, 0),
                ForeColor = Color.FromArgb(255, 220, 140),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
            };
            _chkAutoPurgeOrphans.CheckedChanged += (s, e) => _autoPurgeOrphans = _chkAutoPurgeOrphans.Checked;
            rowPlVictory.Controls.Add(_chkAutoPurgeOrphans);

            playerPanel.Controls.Add(rowPlVictory, 0, 2);
            grpPlayer.Controls.Add(playerPanel);
            mainLayout.Controls.Add(grpPlayer, 0, 3);

            // 5. Hotkeys Info Footer
            var lblHotkeys = new Label
            {
                Dock = DockStyle.Fill,
                Text = "⌨️ 游戏内全局热键（对局中直接按键立即生效，伴随提示音）：\r\n" +
                       "  [Pause] 或 [F10] ⏸️ 绝对时空静止 (基洛夫/战机/防御塔全面熄火停火定身，安心倒水！)\r\n" +
                       "  [F5] 玩家 +100万金钱 & 瞬间建造        [F6] 清空所有 AI 金钱 (削弱 AI)       [F7] AI 全体 +100万金钱 (加强 AI)\r\n" +
                       "  [F8] 切换: AI 建造/移速慢 5 倍 (0.2x)    [F9] 切换: AI 建造/移速狂暴 3 倍 (3.0x)\r\n" +
                       "  [F11] 🛰️ 间谍卫星全图开图 (破除黑幕/残兵显形)    [F12] 💥 一键歼灭全部AI残兵 (含空军基地全灭直接胜利)",
                ForeColor = Color.FromArgb(190, 200, 220),
                BackColor = Color.FromArgb(30, 34, 44),
                Padding = new Padding(10, 6, 10, 6),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular)
            };
            mainLayout.Controls.Add(lblHotkeys, 0, 4);

            Controls.Add(mainLayout);
        }

        private GroupBox CreateGroupBox(string title, Color titleColor)
        {
            return new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = titleColor,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
        }

        private FlowLayoutPanel CreateFlowRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };
        }

        private Label CreateRowLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(2, 8, 4, 0),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular)
            };
        }

        private Label CreateValueBadge(string text, Color color)
        {
            return new Label
            {
                Text = text,
                Width = 145,
                Margin = new Padding(2, 8, 6, 0),
                ForeColor = color,
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold)
            };
        }

        private Button CreateButton(string text, Color backColor, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Margin = new Padding(3, 3, 3, 3),
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 8.75f, FontStyle.Regular)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(90, 100, 120);
            return btn;
        }

        private void AddPresetButton(FlowLayoutPanel row, string label, double mult, bool isBuildSpeed)
        {
            Color bg = mult == 0.0 ? Color.FromArgb(140, 45, 70)
                     : mult < 1.0 ? Color.FromArgb(45, 90, 135)
                     : mult == 1.0 ? Color.FromArgb(55, 62, 78)
                     : mult <= 2.0 ? Color.FromArgb(130, 85, 35)
                     : Color.FromArgb(160, 55, 90);
            var btn = CreateButton(label, bg, label.Length > 9 ? 115 : (label.Length > 7 ? 100 : 82));
            btn.Click += (s, e) =>
            {
                if (isBuildSpeed)
                {
                    _aiBuildSpeed = mult;
                    _chkAiInstantBuild.Checked = false;
                    _lblAiBuildSpeed.Text = mult == 0.0 ? "当前: 0x (已冻结)" : $"当前: {mult:0.##}x";
                    if (mult > 0.0 && _chkAiLockZeroMoney.Checked)
                    {
                        _chkAiLockZeroMoney.Checked = false;
                    }
                    if (mult == 0.0 && _aiMoveSpeed == 0.0)
                    {
                        _isAbsoluteTimeStop = true;
                        _btnTimeStop.Text = "▶️ 解除静止 / 恢复运行 (Pause)";
                        _btnTimeStop.BackColor = Color.FromArgb(40, 140, 85);
                    }
                    else if (mult > 0.0)
                    {
                        _isAbsoluteTimeStop = false;
                        _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                        _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    }
                    SyncCustomRulesIni();
                }
                else
                {
                    _aiMoveSpeed = mult;
                    _lblAiMoveSpeed.Text = mult == 0.0 ? "当前: 0x (已定身)" : $"当前: {mult:0.##}x";
                    if (mult == 0.0 && _aiBuildSpeed == 0.0)
                    {
                        _isAbsoluteTimeStop = true;
                        _btnTimeStop.Text = "▶️ 解除静止 / 恢复运行 (Pause)";
                        _btnTimeStop.BackColor = Color.FromArgb(40, 140, 85);
                    }
                    else if (mult > 0.0)
                    {
                        _isAbsoluteTimeStop = false;
                        _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                        _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    }
                }
            };
            row.Controls.Add(btn);
        }

        private void AddCombatButton(FlowLayoutPanel row, string label, double mult)
        {
            Color bg = mult < 1.0 ? Color.FromArgb(45, 105, 95)
                     : mult == 1.0 ? Color.FromArgb(55, 62, 78)
                     : mult <= 3.0 ? Color.FromArgb(140, 70, 45)
                     : Color.FromArgb(175, 45, 45);
            var btn = CreateButton(label, bg, label.Length > 9 ? 120 : (label.Length > 7 ? 105 : 85));
            btn.Click += (s, e) =>
            {
                _aiCombatMult = mult;
                _lblAiCombat.Text = $"当前: {mult:0.##}x";
            };
            row.Controls.Add(btn);
        }

        private void UpdateHousesListView(List<HouseSnapshot> houses)
        {
            _lvHouses.BeginUpdate();
            while (_lvHouses.Items.Count > houses.Count)
                _lvHouses.Items.RemoveAt(_lvHouses.Items.Count - 1);

            for (int i = 0; i < houses.Count; i++)
            {
                var h = houses[i];
                string role = h.IsPlayer ? "⭐ 玩家本人" : "🤖 电脑 AI";
                string cashStr = $"$ {h.Cash:N0}";
                string oreStr = $"$ {h.OreValue:N0}";
                string buildStr = h.IsPlayer
                    ? (_playerInstantBuild ? "⚡ 0秒秒造完成" : "1.0x (正常)")
                    : (_isAbsoluteTimeStop ? "⏸️ 冻结静止 (0x)" : (_aiInstantBuild ? "⚡ AI秒造完成" : (_aiBuildSpeed <= 0.001 ? "❄️ 已冻结 (0x)" : $"{_aiBuildSpeed:0.##}x")));
                string moveStr = h.IsPlayer
                    ? $"{_playerMoveSpeed:0.##}x"
                    : (_isAbsoluteTimeStop ? "⏸️ 定身静止 (0x)" : (_aiMoveSpeed <= 0.001 ? "❄️ 已定身 (0x)" : $"{_aiMoveSpeed:0.##}x"));
                string combatStr = h.IsPlayer
                    ? $"{_playerCombatMult:0.##}x"
                    : (_isAbsoluteTimeStop ? "⏸️ 停火熄火 (0x)" : $"{_aiCombatMult:0.##}x");

                if (i >= _lvHouses.Items.Count)
                {
                    var item = new ListViewItem(new[] { h.Name, role, cashStr, oreStr, buildStr, moveStr, combatStr });
                    item.ForeColor = h.IsPlayer ? Color.FromArgb(140, 255, 160) : Color.FromArgb(255, 190, 190);
                    _lvHouses.Items.Add(item);
                }
                else
                {
                    var item = _lvHouses.Items[i];
                    if (item.SubItems[0].Text != h.Name) item.SubItems[0].Text = h.Name;
                    if (item.SubItems[1].Text != role) item.SubItems[1].Text = role;
                    if (item.SubItems[2].Text != cashStr) item.SubItems[2].Text = cashStr;
                    if (item.SubItems[3].Text != oreStr) item.SubItems[3].Text = oreStr;
                    if (item.SubItems[4].Text != buildStr) item.SubItems[4].Text = buildStr;
                    if (item.SubItems[5].Text != moveStr) item.SubItems[5].Text = moveStr;
                    if (item.SubItems[6].Text != combatStr) item.SubItems[6].Text = combatStr;
                    item.ForeColor = h.IsPlayer ? Color.FromArgb(140, 255, 160) : Color.FromArgb(255, 190, 190);
                }
            }
            _lvHouses.EndUpdate();
        }
    }
}
