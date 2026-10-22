using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace RA2Console
{
    static class NativeMethods
    {
        public const int PROCESS_VM_OPERATION = 0x0008;
        public const int PROCESS_VM_READ = 0x0010;
        public const int PROCESS_VM_WRITE = 0x0020;
        public const int PROCESS_QUERY_INFORMATION = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        public const uint MEM_COMMIT = 0x1000;
        public const uint MEM_RESERVE = 0x2000;
        public const uint MEM_RELEASE = 0x8000;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }

    public class HouseSnapshot
    {
        public uint Address;
        public int Index;
        public string Name;
        public bool IsPlayer;
        public int Cash;
        public int OreValue;
        public double Firepower;
        public double ROF;
    }

    public class MainForm : Form
    {
        // YR 1.001 / Ares Memory Addresses
        private const uint ADDR_HOUSE_ITEMS = 0x00A8022C;
        private const uint ADDR_HOUSE_COUNT = 0x00A80238;
        private const uint ADDR_CURRENT_PLAYER = 0x00A83D4C;
        private const uint ADDR_FACTORY_ITEMS = 0x00A83E34;
        private const uint ADDR_FACTORY_COUNT = 0x00A83E40;
        private const uint ADDR_FOOT_ITEMS = 0x008B3DC4;
        private const uint ADDR_FOOT_COUNT = 0x008B3DD0;
        private const uint ADDR_AIRCRAFT_ITEMS = 0x00A8E394;
        private const uint ADDR_AIRCRAFT_COUNT = 0x00A8E3A0;
        private const uint ADDR_BUILDING_ITEMS = 0x00A8EB44;
        private const uint ADDR_BUILDING_COUNT = 0x00A8EB50;

        private IntPtr _hProcess = IntPtr.Zero;
        private int _attachedPid = -1;
        private bool _triedElevate = false;

        // Player state
        private bool _playerInstantBuild = true;
        private bool _playerLockMoney = false;
        private double _playerMoveSpeed = 1.0;
        private double _playerCombatMult = 1.0;
        private bool _protectUnitLoss = true; // Anti-drop / unit loss protection patch
        private bool _autoPurgeOrphans = true; // Auto purge stranded units when AI base is destroyed
        private CheckBox _chkAutoPurgeOrphans;

        // AI state
        private bool _aiLockZeroMoney = false; // Nerf: lock 0 money
        private bool _aiLockInfMoney = false;  // Buff: lock 999w money
        private bool _aiInstantBuild = false;  // Buff: instant 0-second build
        private double _aiBuildSpeed = 1.0;    // 0 = freeze, 0.2 = 5x slower, 1.0 = normal, 2.0 = 2x, 5.0 = 5x, 10.0 = 10x
        private double _aiMoveSpeed = 1.0;     // 0 = freeze, 0.2 = 5x slower, 1.0 = normal, 1.5x, 2.0x, 3.0x, 5.0x
        private double _aiCombatMult = 1.0;    // 0.1 = 10x weaker, 1.0 = normal, 2.0x, 3.0x, 5.0x, 10.0x
        private bool _isAbsoluteTimeStop = false; // Bulletproof safe pause / time stop for all air/ground/defense units

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
        private Label _lblPlayerSpeed;

        private System.Windows.Forms.Timer _timer;
        private Dictionary<int, bool> _keyWasDown = new Dictionary<int, bool>();

        public MainForm()
        {
            Text = "红警2：重聚未来 - 实时控制台 (全空地海防绝对暂停 & AI 强化/削弱调控器)";
            Size = new Size(1080, 860);
            MinimumSize = new Size(1040, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 26, 32);
            ForeColor = Color.FromArgb(235, 238, 245);
            Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            TopMost = true;

            BuildUI();

            _timer = new System.Windows.Forms.Timer { Interval = 150 };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

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
            btnLaunchGame.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = @"H:\RA2-Reunion\Reunion.exe",
                        WorkingDirectory = @"H:\RA2-Reunion",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

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
            btnResetAi.Click += (s, e) =>
            {
                _isAbsoluteTimeStop = false;
                _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                _aiBuildSpeed = 1.0;
                _aiMoveSpeed = 1.0;
                _aiCombatMult = 1.0;
                _chkAiLockZeroMoney.Checked = false;
                _chkAiLockInfMoney.Checked = false;
                _chkAiInstantBuild.Checked = false;
                _lblAiBuildSpeed.Text = "当前: 1.0x (正常)";
                _lblAiMoveSpeed.Text = "当前: 1.0x (正常)";
                _lblAiCombat.Text = "当前: 1.0x (正常)";
                SyncCustomRulesIni();
                Console.Beep(660, 100);
            };
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
                SetAntiDropMemoryPatches(_protectUnitLoss);
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

        private void ToggleAbsoluteTimeStop()
        {
            _isAbsoluteTimeStop = !_isAbsoluteTimeStop;
            if (_isAbsoluteTimeStop)
            {
                _aiBuildSpeed = 0.0;
                _aiMoveSpeed = 0.0;
                _lblAiBuildSpeed.Text = "当前: ⏸️ 已冻结 (0x)";
                _lblAiMoveSpeed.Text = "当前: ⏸️ 已定身 (0x)";
                _btnTimeStop.Text = "▶️ 解除静止 / 恢复运行 (Pause)";
                _btnTimeStop.BackColor = Color.FromArgb(40, 140, 85);
                Console.Beep(980, 150);
            }
            else
            {
                _aiBuildSpeed = 1.0;
                _aiMoveSpeed = 1.0;
                _lblAiBuildSpeed.Text = "当前: 1.0x (正常)";
                _lblAiMoveSpeed.Text = "当前: 1.0x (正常)";
                _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                Console.Beep(520, 150);
            }
            SyncCustomRulesIni();
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

        private void SetPlayerBuffs(double moveSpeed, double combatMult)
        {
            _playerMoveSpeed = moveSpeed;
            _playerCombatMult = combatMult;
            _lblPlayerSpeed.Text = $"移速: {moveSpeed:0.#}x | 火力: {combatMult:0.#}x";
        }

        // ==========================================
        // Critical Anti-Drop / Unit Loss Memory Patch
        // ==========================================
        // Prevents Westwood engine from calling Abandon_Production / Abandon()
        // when BuildingClass::Exit_Object returns 0 due to door busy or exit tile occupied.
        // Instead of destroying the completed queued unit, the factory simply waits
        // until the doorway is clear, ensuring all 10/10 queued units spawn!
        private void SetAntiDropMemoryPatches(bool enable)
        {
            if (_hProcess == IntPtr.Zero) return;

            var patches = new (uint addr, byte origByte, byte patchByte)[]
            {
                (0x0044ec94, 0x75, 0xEB), // War factory / Vehicle factory
                (0x0044ed15, 0x75, 0xEB), // Naval / Shipyard factory
                (0x0044ed96, 0x75, 0xEB), // Aircraft / Helipad factory
                (0x0044ee17, 0x75, 0xEB), // Barracks / Infantry factory #1
                (0x0044ee98, 0x75, 0xEB), // Barracks / Infantry factory #2
                (0x0044eba8, 0x75, 0xEB), // Primary Building factory
                (0x00509218, 0x75, 0xEB)  // HouseClass production queue abandon check
            };

            int patchedCount = 0;
            foreach (var (addr, origByte, patchByte) in patches)
            {
                byte target = enable ? patchByte : origByte;
                byte cur = ReadByte(addr);
                if (cur != target && (cur == origByte || cur == patchByte))
                {
                    if (NativeMethods.VirtualProtectEx(_hProcess, (IntPtr)addr, (UIntPtr)1, 0x40, out uint oldProt))
                    {
                        byte[] buf = new byte[] { target };
                        NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 1, out _);
                        NativeMethods.VirtualProtectEx(_hProcess, (IntPtr)addr, (UIntPtr)1, oldProt, out _);
                    }
                }
                if (ReadByte(addr) == target)
                    patchedCount++;
            }

            if (enable && patchedCount == patches.Length)
            {
                _lblPatchStatus.Text = "🛡️ 防吞兵: 🟢 已激活 (绝不丢兵)";
                _lblPatchStatus.ForeColor = Color.FromArgb(130, 255, 170);
            }
            else if (!enable)
            {
                _lblPatchStatus.Text = "🛡️ 防吞兵: ⚪ 未开启";
                _lblPatchStatus.ForeColor = Color.FromArgb(180, 190, 205);
            }
        }

        private void PollGlobalHotkeys()
        {
            // Pause key or F10: Absolute time stop / pause
            CheckKey((int)Keys.Pause, () => ToggleAbsoluteTimeStop());
            CheckKey((int)Keys.F10, () => ToggleAbsoluteTimeStop());

            // F5: Player money + instant build
            CheckKey((int)Keys.F5, () =>
            {
                AddPlayerMoney(1000000);
                _chkPlayerInstantBuild.Checked = true;
                Console.Beep(880, 120);
            });

            // F6: Nerf AI: clear all AI money
            CheckKey((int)Keys.F6, () =>
            {
                ClearAllAiMoney();
                Console.Beep(660, 120);
            });

            // F7: Buff AI: add 100w money to all AI
            CheckKey((int)Keys.F7, () =>
            {
                AddAllAiMoney(1000000);
                _chkAiLockZeroMoney.Checked = false;
                Console.Beep(770, 120);
            });

            // F8: Nerf AI: 0.2x build & move speed / normal
            CheckKey((int)Keys.F8, () =>
            {
                if (_aiBuildSpeed == 0.2 && _aiMoveSpeed == 0.2)
                {
                    _aiBuildSpeed = 1.0;
                    _aiMoveSpeed = 1.0;
                    _isAbsoluteTimeStop = false;
                    _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                    _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    _lblAiBuildSpeed.Text = "当前: 1.0x (正常)";
                    _lblAiMoveSpeed.Text = "当前: 1.0x (正常)";
                    Console.Beep(520, 120);
                }
                else
                {
                    _aiBuildSpeed = 0.2;
                    _aiMoveSpeed = 0.2;
                    _isAbsoluteTimeStop = false;
                    _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                    _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    _lblAiBuildSpeed.Text = "当前: 0.2x (慢5倍)";
                    _lblAiMoveSpeed.Text = "当前: 0.2x (慢5倍)";
                    Console.Beep(820, 120);
                }
                SyncCustomRulesIni();
            });

            // F9: Buff AI: 3.0x build & move speed / normal
            CheckKey((int)Keys.F9, () =>
            {
                if (_aiBuildSpeed == 3.0 && _aiMoveSpeed == 3.0)
                {
                    _aiBuildSpeed = 1.0;
                    _aiMoveSpeed = 1.0;
                    _isAbsoluteTimeStop = false;
                    _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                    _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    _lblAiBuildSpeed.Text = "当前: 1.0x (正常)";
                    _lblAiMoveSpeed.Text = "当前: 1.0x (正常)";
                    Console.Beep(520, 120);
                }
                else
                {
                    _aiBuildSpeed = 3.0;
                    _aiMoveSpeed = 3.0;
                    _isAbsoluteTimeStop = false;
                    _btnTimeStop.Text = "⏸️ 绝对时空静止 / 倒水暂停 (Pause)";
                    _btnTimeStop.BackColor = Color.FromArgb(180, 45, 65);
                    _chkAiLockZeroMoney.Checked = false;
                    _lblAiBuildSpeed.Text = "当前: 3.0x (快3倍)";
                    _lblAiMoveSpeed.Text = "当前: 3.0x (快3倍)";
                    Console.Beep(1040, 120);
                }
                SyncCustomRulesIni();
            });

            // F11: Reveal Map / SpySat
            CheckKey((int)Keys.F11, () =>
            {
                RevealMap();
                Console.Beep(1200, 120);
            });

            // F12: Instant Victory / Eliminate Stray AI
            CheckKey((int)Keys.F12, () =>
            {
                EliminateAllEnemies();
                Console.Beep(1500, 180);
            });
        }

        private void CheckKey(int vKey, Action onPressed)
        {
            bool isDown = (NativeMethods.GetAsyncKeyState(vKey) & 0x8000) != 0;
            bool wasDown = _keyWasDown.TryGetValue(vKey, out bool prev) && prev;
            _keyWasDown[vKey] = isDown;
            if (isDown && !wasDown)
            {
                onPressed();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            PollGlobalHotkeys();

            if (!EnsureGameProcess())
            {
                _lblStatus.Text = "🟡 等待进入红警2对局 (进入游戏对局后将自动连接)...";
                _lblStatus.ForeColor = Color.FromArgb(255, 214, 102);
                _lblPatchStatus.Text = "🛡️ 防吞兵: 待机";
                _lblPatchStatus.ForeColor = Color.FromArgb(160, 170, 185);
                _lvHouses.Items.Clear();
                return;
            }

            // Ensure our unit loss patch is always active when attached
            SetAntiDropMemoryPatches(_protectUnitLoss);

            var houses = ReadActiveHouses();
            if (houses.Count == 0)
            {
                _lblStatus.Text = $"🟢 已连接游戏进程 (PID: {_attachedPid}) - 正在等待对局载入...";
                _lblStatus.ForeColor = Color.FromArgb(120, 230, 150);
                _lvHouses.Items.Clear();
                return;
            }

            if (_isAbsoluteTimeStop)
            {
                _lblStatus.Text = $"⏸️ 绝对时空静止生效中 (PID: {_attachedPid} | 全空地海防彻底熄火停摆，安心倒水！)";
                _lblStatus.ForeColor = Color.FromArgb(255, 125, 125);
            }
            else
            {
                _lblStatus.Text = $"🟢 已实时接管对局 (PID: {_attachedPid} | 场上阵营数: {houses.Count})";
                _lblStatus.ForeColor = Color.FromArgb(120, 230, 150);
            }

            ApplyLiveRules(houses);
            UpdateHousesListView(houses);
        }

        private bool EnsureGameProcess()
        {
            if (_hProcess != IntPtr.Zero)
            {
                try
                {
                    var p = Process.GetProcessById(_attachedPid);
                    if (!p.HasExited) return true;
                }
                catch { }
                NativeMethods.CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
                _attachedPid = -1;
            }

            Process[] procs = Process.GetProcessesByName("gamemd");
            if (procs.Length == 0)
                procs = Process.GetProcessesByName("gamemd-spawn");

            if (procs.Length > 0)
            {
                int pid = procs[0].Id;
                IntPtr h = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE |
                    NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_QUERY_INFORMATION,
                    false, pid);
                if (h != IntPtr.Zero)
                {
                    _hProcess = h;
                    _attachedPid = pid;
                    return true;
                }
                else if (Marshal.GetLastWin32Error() == 5 && !_triedElevate)
                {
                    _triedElevate = true;
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = Application.ExecutablePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        });
                        Application.Exit();
                    }
                    catch { }
                }
            }
            return false;
        }

        private List<HouseSnapshot> ReadActiveHouses()
        {
            var result = new List<HouseSnapshot>();
            uint currentPlayer = ReadUInt32(ADDR_CURRENT_PLAYER);
            uint itemsPtr = ReadUInt32(ADDR_HOUSE_ITEMS);
            int count = ReadInt32(ADDR_HOUSE_COUNT);

            if (itemsPtr == 0 || count <= 0 || count > 32) return result;

            int aiCounter = 1;
            for (int i = 0; i < count; i++)
            {
                uint h = ReadUInt32(itemsPtr + (uint)(i * 4));
                if (h == 0) continue;

                string iniName = ReadAsciiString(h + 0x16009, 32);
                if (iniName.Equals("Neutral", StringComparison.OrdinalIgnoreCase) ||
                    iniName.Equals("Special", StringComparison.OrdinalIgnoreCase) ||
                    iniName.Equals("Civilian", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isPlayer = (h == currentPlayer) || (ReadByte(h + 0x1ED) == 1);
                string uiName = ReadUnicodeString(h + 0x1602A, 20);
                string displayName = isPlayer ? "玩家 (你)" : $"电脑 AI #{aiCounter++}";
                if (!string.IsNullOrWhiteSpace(uiName))
                    displayName += $" [{uiName}]";

                int cash = ReadInt32(h + 0x30C);
                float ore0 = ReadFloat(h + 0x2FC);
                float ore1 = ReadFloat(h + 0x300);
                float ore2 = ReadFloat(h + 0x304);
                float ore3 = ReadFloat(h + 0x308);
                int oreVal = (int)((ore0 + ore1 + ore2 + ore3) * 25.0f);

                double fp = ReadDouble(h + 0x188);
                double rof = ReadDouble(h + 0x1A8);

                result.Add(new HouseSnapshot
                {
                    Address = h,
                    Index = i,
                    Name = displayName,
                    IsPlayer = isPlayer,
                    Cash = cash,
                    OreValue = oreVal,
                    Firepower = fp,
                    ROF = rof
                });
            }
            return result;
        }

        private void ApplyLiveRules(List<HouseSnapshot> houses)
        {
            HashSet<uint> aiAddresses = new HashSet<uint>();
            uint playerAddress = 0;
            bool isFrozen = (_aiMoveSpeed <= 0.001 || _isAbsoluteTimeStop);

            foreach (var h in houses)
            {
                if (h.IsPlayer)
                {
                    playerAddress = h.Address;
                    if (_playerLockMoney && h.Cash < 9999999)
                    {
                        WriteInt32(h.Address + 0x30C, 9999999);
                        h.Cash = 9999999;
                    }

                    // Apply player combat multipliers
                    WriteDouble(h.Address + 0x188, _playerCombatMult);
                    WriteDouble(h.Address + 0x1A8, _playerCombatMult > 0.01 ? 1.0 / _playerCombatMult : 1.0);
                    WriteDouble(h.Address + 0x190, _playerMoveSpeed);
                    WriteDouble(h.Address + 0x198, _playerMoveSpeed);

                    if (_playerInstantBuild)
                    {
                        WriteDouble(h.Address + 0x1B8, 0.0001);
                    }
                }
                else
                {
                    aiAddresses.Add(h.Address);

                    if (_aiLockZeroMoney)
                    {
                        ZeroHouseMoneyAndOre(h.Address);
                        h.Cash = 0;
                        h.OreValue = 0;
                    }
                    else if (_aiLockInfMoney && h.Cash < 9999999)
                    {
                        WriteInt32(h.Address + 0x30C, 9999999);
                        h.Cash = 9999999;
                    }

                    // Apply AI combat multipliers
                    WriteDouble(h.Address + 0x188, isFrozen ? 0.0001 : _aiCombatMult);
                    WriteDouble(h.Address + 0x1A8, isFrozen ? 1000.0 : (_aiCombatMult > 0.01 ? 1.0 / _aiCombatMult : 10.0));
                    double effMove = isFrozen ? 0.000001 : _aiMoveSpeed;
                    WriteDouble(h.Address + 0x190, effMove);
                    WriteDouble(h.Address + 0x198, effMove);

                    if (_aiInstantBuild && !isFrozen)
                    {
                        WriteDouble(h.Address + 0x1B8, 0.0001);
                    }
                }
            }

            // 2. FactoryClass::Array - Real-time Build & Unit Production Speed Control
            uint facItems = ReadUInt32(ADDR_FACTORY_ITEMS);
            int facCount = ReadInt32(ADDR_FACTORY_COUNT);
            if (facItems != 0 && facCount > 0 && facCount <= 256)
            {
                for (int i = 0; i < facCount; i++)
                {
                    uint f = ReadUInt32(facItems + (uint)(i * 4));
                    if (f == 0) continue;

                    uint owner = ReadUInt32(f + 0x6C);
                    uint obj = ReadUInt32(f + 0x58);
                    byte suspended = ReadByte(f + 0x70);
                    if (obj == 0 || suspended != 0) continue;

                    int stage = ReadInt32(f + 0x24);

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        if (_playerInstantBuild && stage >= 0 && stage < 53)
                        {
                            WriteInt32(f + 0x24, 53); // Advance to 53/54
                            WriteInt32(f + 0x38, 1);  // Rate = 1 frame
                            WriteInt32(f + 0x34, 1);  // StepTimer = 1 frame
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen || _aiBuildSpeed <= 0.001)
                        {
                            // Freeze AI production completely
                            if (stage > 1) WriteInt32(f + 0x24, 1);
                            WriteInt32(f + 0x38, 999999);
                            WriteInt32(f + 0x34, 999999);
                        }
                        else if (_aiInstantBuild && stage >= 0 && stage < 53)
                        {
                            WriteInt32(f + 0x24, 53);
                            WriteInt32(f + 0x38, 1);
                            WriteInt32(f + 0x34, 1);
                        }
                        else
                        {
                            int targetRate = Math.Clamp((int)Math.Round(15.0 / _aiBuildSpeed), 1, 5000);
                            int curRate = ReadInt32(f + 0x38);

                            if (curRate > 200 || curRate <= 0 || Math.Abs(curRate - targetRate) > 1)
                            {
                                WriteInt32(f + 0x38, targetRate);
                                WriteInt32(f + 0x34, targetRate);
                                WriteByte(f + 0x70, 0); // Ensure not suspended
                            }
                        }
                    }
                }
            }

            // Identify AI factions that have lost all buildings (Orphans)
            HashSet<uint> orphanedAiHouses = new HashSet<uint>();
            if (_autoPurgeOrphans)
            {
                foreach (var h in houses)
                {
                    if (!h.IsPlayer)
                    {
                        int houseBldgs = ReadInt32(h.Address + 0x78);
                        if (houseBldgs == 0)
                        {
                            orphanedAiHouses.Add(h.Address);
                        }
                    }
                }
            }

            // 3. FootClass::Array - Real-time Ground Unit Speed & Weapon Control (Tanks, Infantry, Harvesters)
            uint footItems = ReadUInt32(ADDR_FOOT_ITEMS);
            int footCount = ReadInt32(ADDR_FOOT_COUNT);
            if (footItems != 0 && footCount > 0 && footCount <= 4096)
            {
                double effAiMove = isFrozen ? 0.000001 : _aiMoveSpeed;

                for (int i = 0; i < footCount; i++)
                {
                    uint u = ReadUInt32(footItems + (uint)(i * 4));
                    if (u == 0) continue;

                    uint owner = ReadUInt32(u + 0x21C);

                    // If unit belongs to an AI that has lost all buildings and auto-purge is enabled:
                    if (orphanedAiHouses.Contains(owner))
                    {
                        WriteInt32(u + 0x6C, 0); // Health = 0 (kill stranded engineer / tank)
                        WriteInt32(u + 0xAC, 0); // Mission = Sleep/Idle
                        continue;
                    }

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        double cur = ReadDouble(u + 0x580);
                        if (Math.Abs(cur - _playerMoveSpeed) > 0.01)
                        {
                            WriteDouble(u + 0x580, _playerMoveSpeed);
                            WriteDouble(u + 0x578, _playerMoveSpeed);
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            WriteDouble(u + 0x580, 0.000001);
                            WriteDouble(u + 0x578, 0.000001);
                            WriteByte(u + 0x1C8, 1);        // TechnoClass::Deactivated = 1 (EMP lockdown!)
                            WriteInt32(u + 0xAC, 0);       // Mission = Sleep
                            WriteInt32(u + 0x2C4, 999999); // Weapon reload timer locked (cannot fire)
                        }
                        else
                        {
                            byte curDeact = ReadByte(u + 0x1C8);
                            if (curDeact != 0)
                            {
                                WriteByte(u + 0x1C8, 0);   // Re-activate unit
                                WriteInt32(u + 0x2C4, 0);  // Weapon ready
                                int curMission = ReadInt32(u + 0xAC);
                                if (curMission == 0)
                                {
                                    WriteInt32(u + 0xAC, 5); // Guard
                                }
                                WriteInt32(u + 0xC0, 0);   // Clear evaluation timer
                            }

                            double cur = ReadDouble(u + 0x580);
                            if (Math.Abs(cur - effAiMove) > 0.0001)
                            {
                                WriteDouble(u + 0x580, effAiMove);
                                WriteDouble(u + 0x578, effAiMove);
                            }
                        }
                    }
                }
            }

            // 4. AircraftClass::Array - Real-time Air Unit Speed & Weapon Control (Kirov Airships, Harriers, Black Eagles, J-10s, Helis)
            // CRITICAL FIX: Air units are stored in AircraftClass::Array (0x00A8E394), NOT FootClass!
            uint airItems = ReadUInt32(ADDR_AIRCRAFT_ITEMS);
            int airCount = ReadInt32(ADDR_AIRCRAFT_COUNT);
            if (airItems != 0 && airCount > 0 && airCount <= 2048)
            {
                double effAiMove = isFrozen ? 0.000001 : _aiMoveSpeed;

                for (int i = 0; i < airCount; i++)
                {
                    uint a = ReadUInt32(airItems + (uint)(i * 4));
                    if (a == 0) continue;

                    uint owner = ReadUInt32(a + 0x21C);

                    // If aircraft belongs to an AI that has lost all buildings and auto-purge is enabled:
                    if (orphanedAiHouses.Contains(owner))
                    {
                        WriteInt32(a + 0x6C, 0); // Health = 0 (kill stranded Kirov / plane)
                        WriteInt32(a + 0xAC, 0);
                        continue;
                    }

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        double cur = ReadDouble(a + 0x580);
                        if (Math.Abs(cur - _playerMoveSpeed) > 0.01)
                        {
                            WriteDouble(a + 0x580, _playerMoveSpeed);
                            WriteDouble(a + 0x578, _playerMoveSpeed);
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            WriteDouble(a + 0x580, 0.000001);
                            WriteDouble(a + 0x578, 0.000001);
                            WriteByte(a + 0x1C8, 1);        // TechnoClass::Deactivated = 1 (Kirov EMP shutoff! Engines dead, cannot drop bombs!)
                            WriteInt32(a + 0xAC, 0);       // Mission = Sleep
                            WriteInt32(a + 0x2C4, 999999); // Weapon/bomb reload timer locked
                        }
                        else
                        {
                            byte curDeact = ReadByte(a + 0x1C8);
                            if (curDeact != 0)
                            {
                                WriteByte(a + 0x1C8, 0);   // Re-activate aircraft
                                WriteInt32(a + 0x2C4, 0);  // Weapon ready
                                int curMission = ReadInt32(a + 0xAC);
                                if (curMission == 0)
                                {
                                    WriteInt32(a + 0xAC, 5); // Guard
                                }
                                WriteInt32(a + 0xC0, 0);   // Clear evaluation timer
                            }

                            double cur = ReadDouble(a + 0x580);
                            if (Math.Abs(cur - effAiMove) > 0.0001)
                            {
                                WriteDouble(a + 0x580, effAiMove);
                                WriteDouble(a + 0x578, effAiMove);
                            }
                        }
                    }
                }
            }

            // 5. BuildingClass::Array - Real-time Defense Structure Control (Prism Towers, Tesla Coils, Gatlings, Patriot, Flak)
            uint bldgItems = ReadUInt32(ADDR_BUILDING_ITEMS);
            int bldgCount = ReadInt32(ADDR_BUILDING_COUNT);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 2048)
            {
                for (int i = 0; i < bldgCount; i++)
                {
                    uint b = ReadUInt32(bldgItems + (uint)(i * 4));
                    if (b == 0) continue;

                    uint owner = ReadUInt32(b + 0x21C);
                    if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            WriteByte(b + 0x1C8, 1);        // Deactivated: Towers power cut, completely shut off
                            WriteInt32(b + 0x2C4, 999999);  // Cannot charge or fire
                        }
                        else
                        {
                            byte curDeact = ReadByte(b + 0x1C8);
                            if (curDeact != 0)
                            {
                                WriteByte(b + 0x1C8, 0);    // Re-power tower
                                WriteInt32(b + 0x2C4, 0);
                            }
                        }
                    }
                }
            }
        }

        private void ZeroHouseMoneyAndOre(uint h)
        {
            WriteInt32(h + 0x30C, 0);
            byte[] zeros16 = new byte[16];
            NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)h + 0x2FC, zeros16, 16, out _);

            uint bldgItems = ReadUInt32(h + 0x6C);
            int bldgCount = ReadInt32(h + 0x78);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 512)
            {
                for (int j = 0; j < bldgCount; j++)
                {
                    uint b = ReadUInt32(bldgItems + (uint)(j * 4));
                    if (b != 0)
                    {
                        NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)b + 0x33C, zeros16, 16, out _);
                    }
                }
            }
        }

        private void ClearAllAiMoney()
        {
            if (_hProcess == IntPtr.Zero) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                    ZeroHouseMoneyAndOre(h.Address);
            }
        }

        private void SetAllAiMoney(int amount)
        {
            if (_hProcess == IntPtr.Zero) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                {
                    ZeroHouseMoneyAndOre(h.Address);
                    WriteInt32(h.Address + 0x30C, amount);
                }
            }
        }

        private void AddAllAiMoney(int delta)
        {
            if (_hProcess == IntPtr.Zero) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                {
                    int cur = ReadInt32(h.Address + 0x30C);
                    WriteInt32(h.Address + 0x30C, Math.Max(0, cur + delta));
                }
            }
        }

        private void AddPlayerMoney(int delta)
        {
            if (_hProcess == IntPtr.Zero) return;
            foreach (var h in ReadActiveHouses())
            {
                if (h.IsPlayer)
                {
                    int cur = ReadInt32(h.Address + 0x30C);
                    WriteInt32(h.Address + 0x30C, Math.Max(0, cur + delta));
                }
            }
        }

        private bool ExecuteRemoteCode(byte[] code, uint waitMs = 2000)
        {
            if (_hProcess == IntPtr.Zero || code == null || code.Length == 0) return false;
            IntPtr pAlloc = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            try
            {
                pAlloc = NativeMethods.VirtualAllocEx(_hProcess, IntPtr.Zero, (uint)code.Length, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_EXECUTE_READWRITE);
                if (pAlloc == IntPtr.Zero) return false;

                if (!NativeMethods.WriteProcessMemory(_hProcess, pAlloc, code, code.Length, out _))
                    return false;

                hThread = NativeMethods.CreateRemoteThread(_hProcess, IntPtr.Zero, 0, pAlloc, IntPtr.Zero, 0, out _);
                if (hThread == IntPtr.Zero) return false;

                NativeMethods.WaitForSingleObject(hThread, waitMs);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (hThread != IntPtr.Zero)
                    NativeMethods.CloseHandle(hThread);
                if (pAlloc != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(_hProcess, pAlloc, 0, NativeMethods.MEM_RELEASE);
            }
        }

        public void RevealMap()
        {
            if (_hProcess == IntPtr.Zero) return;
            uint currentPlayer = ReadUInt32(ADDR_CURRENT_PLAYER);
            if (currentPlayer == 0) return;

            // Set MapIsClear and SpySatActive in HouseClass
            WriteByte(currentPlayer + 0x241, 1);
            WriteByte(currentPlayer + 0x577A, 1);

            // Call MapClass::Reveal_All(PlayerHouse) natively at 0x577D90
            byte[] revealCode = new byte[] {
                0xB9, 0xE8, 0xF7, 0x87, 0x00,       // mov ecx, 0x87f7e8 (MapClass::Instance)
                0xFF, 0x35, 0x4C, 0x3D, 0xA8, 0x00, // push dword ptr [0x00A83D4C] (Player HouseClass*)
                0xB8, 0x90, 0x7D, 0x57, 0x00,       // mov eax, 0x577d90
                0xFF, 0xD0,                         // call eax
                0xC2, 0x04, 0x00                    // ret 4
            };
            ExecuteRemoteCode(revealCode, 1000);
            Console.Beep(1200, 120);
        }

        public void EliminateAllEnemies()
        {
            if (_hProcess == IntPtr.Zero) return;
            uint currentPlayer = ReadUInt32(ADDR_CURRENT_PLAYER);
            if (currentPlayer == 0) return;

            // 1. Direct memory elimination of remaining enemy Foot units (infantry, engineers, tanks)
            uint footItems = ReadUInt32(ADDR_FOOT_ITEMS);
            int footCount = ReadInt32(ADDR_FOOT_COUNT);
            if (footItems != 0 && footCount > 0 && footCount <= 4096)
            {
                for (int i = 0; i < footCount; i++)
                {
                    uint u = ReadUInt32(footItems + (uint)i * 4);
                    if (u == 0) continue;
                    uint owner = ReadUInt32(u + 0x21C);
                    if (owner != 0 && owner != currentPlayer && ReadByte(owner + 0x1ED) == 0)
                    {
                        WriteInt32(u + 0x6C, 0); // Health = 0
                        WriteInt32(u + 0xAC, 0); // Mission = Sleep/Idle
                    }
                }
            }

            // 2. Direct memory elimination of remaining enemy Aircraft units (Kirovs, Harriers, Black Eagles, J-10s, Helis)
            uint airItems = ReadUInt32(ADDR_AIRCRAFT_ITEMS);
            int airCount = ReadInt32(ADDR_AIRCRAFT_COUNT);
            if (airItems != 0 && airCount > 0 && airCount <= 2048)
            {
                for (int i = 0; i < airCount; i++)
                {
                    uint a = ReadUInt32(airItems + (uint)i * 4);
                    if (a == 0) continue;
                    uint owner = ReadUInt32(a + 0x21C);
                    if (owner != 0 && owner != currentPlayer && ReadByte(owner + 0x1ED) == 0)
                    {
                        WriteInt32(a + 0x6C, 0); // Health = 0 (kill Kirovs, aircraft)
                        WriteInt32(a + 0xAC, 0);
                    }
                }
            }

            // 3. Direct memory elimination of remaining enemy Buildings (Prism towers, Tesla coils, etc.)
            uint bldgItems = ReadUInt32(ADDR_BUILDING_ITEMS);
            int bldgCount = ReadInt32(ADDR_BUILDING_COUNT);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 2048)
            {
                for (int i = 0; i < bldgCount; i++)
                {
                    uint b = ReadUInt32(bldgItems + (uint)i * 4);
                    if (b == 0) continue;
                    uint owner = ReadUInt32(b + 0x21C);
                    if (owner != 0 && owner != currentPlayer && ReadByte(owner + 0x1ED) == 0)
                    {
                        WriteInt32(b + 0x6C, 0); // Health = 0 (destroy building)
                    }
                }
            }

            // 4. Execute HouseClass::DestroyAll and HouseClass::AcceptDefeat for all enemy houses
            byte[] destroyCode = new byte[] {
                0x60,
                0x8B, 0x15, 0x2C, 0x02, 0xA8, 0x00,
                0x8B, 0x35, 0x38, 0x02, 0xA8, 0x00,
                0x8B, 0x1D, 0x4C, 0x3D, 0xA8, 0x00,
                0x31, 0xFF,
                // loop_start (0x15):
                0x39, 0xF7,
                0x7D, 0x33,
                0x8B, 0x0C, 0xBA,
                0x85, 0xC9,
                0x74, 0x29,
                0x39, 0xD9,
                0x74, 0x25,
                0x80, 0xB9, 0xED, 0x01, 0x00, 0x00, 0x01,
                0x74, 0x1C,
                0x51, 0x52, 0x56, 0x57, 0x53,
                0xB8, 0xD0, 0xC6, 0x4F, 0x00, // DestroyAll (0x4FC6D0)
                0xFF, 0xD0,
                0x8B, 0x4C, 0x24, 0x10,       // mov ecx, [esp + 16]
                0xB8, 0xB0, 0xC0, 0x4F, 0x00, // AcceptDefeat (0x4FC0B0)
                0xFF, 0xD0,
                0x5B, 0x5F, 0x5E, 0x5A, 0x59,
                // next_house (0x49):
                0x47,
                0xEB, 0xC9,
                // loop_end (0x4C):
                0x61,
                0xC2, 0x04, 0x00
            };
            ExecuteRemoteCode(destroyCode, 2000);
            Console.Beep(1500, 180);
        }

        private void SyncCustomRulesIni()
        {
            try
            {
                string customIni = @"H:\RA2-Reunion\Client\custom_rules_all.ini";
                if (Directory.Exists(Path.GetDirectoryName(customIni)))
                {
                    string playerBuild = _playerInstantBuild ? ".0001" : "1.0";
                    string aiBuild = _aiInstantBuild ? ".0001"
                                   : (_isAbsoluteTimeStop || _aiBuildSpeed <= 0.01) ? "100.0"
                                   : (1.0 / _aiBuildSpeed).ToString("0.####");
                    string content =
                        $"[Normal]\r\nBuildTime={playerBuild}\r\nBuildSlowdown=no\r\n\r\n" +
                        $"[Easy]\r\nBuildTime={aiBuild}\r\n\r\n" +
                        $"[Difficult]\r\nBuildTime={aiBuild}\r\n";
                    File.WriteAllText(customIni, content, Encoding.ASCII);
                }
            }
            catch { }
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

        // Memory Helpers
        private byte ReadByte(uint addr)
        {
            byte[] buf = new byte[1];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 1, out _);
            return buf[0];
        }

        private int ReadInt32(uint addr)
        {
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToInt32(buf, 0);
        }

        private uint ReadUInt32(uint addr)
        {
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToUInt32(buf, 0);
        }

        private float ReadFloat(uint addr)
        {
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToSingle(buf, 0);
        }

        private double ReadDouble(uint addr)
        {
            byte[] buf = new byte[8];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 8, out _);
            return BitConverter.ToDouble(buf, 0);
        }

        private void WriteByte(uint addr, byte val)
        {
            byte[] buf = new byte[] { val };
            NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 1, out _);
        }

        private void WriteInt32(uint addr, int val)
        {
            byte[] buf = BitConverter.GetBytes(val);
            NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
        }

        private void WriteDouble(uint addr, double val)
        {
            byte[] buf = BitConverter.GetBytes(val);
            NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 8, out _);
        }

        private string ReadAsciiString(uint addr, int maxLen)
        {
            byte[] buf = new byte[maxLen];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, maxLen, out _);
            int len = Array.IndexOf(buf, (byte)0);
            if (len < 0) len = maxLen;
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        private string ReadUnicodeString(uint addr, int maxChars)
        {
            byte[] buf = new byte[maxChars * 2];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, buf.Length, out _);
            int len = 0;
            while (len + 1 < buf.Length && (buf[len] != 0 || buf[len + 1] != 0))
                len += 2;
            return Encoding.Unicode.GetString(buf, 0, len);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Restore original patches if exiting
            if (_hProcess != IntPtr.Zero)
            {
                SetAntiDropMemoryPatches(false);
                NativeMethods.CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
            base.OnFormClosed(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
