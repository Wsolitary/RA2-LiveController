using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using RA2LiveController.Core;
using RA2LiveController.Models;
using RA2LiveController.Patches;

namespace RA2LiveController.UI
{
    public partial class MainForm : Form
    {
        private readonly MemoryManager _mm = new MemoryManager();
        private int _attachedPid = -1;
        private bool _triedElevate = false;

        // Player state
        private bool _playerInstantBuild = true;
        private bool _playerLockMoney = false;
        private double _playerMoveSpeed = 1.0;
        private double _playerCombatMult = 1.0;
        private bool _protectUnitLoss = true; // Anti-drop / unit loss protection patch
        private bool _autoPurgeOrphans = true; // Auto purge stranded units when AI base is destroyed

        // AI state
        private bool _aiLockZeroMoney = false; // Nerf: lock 0 money
        private bool _aiLockInfMoney = false;  // Buff: lock 999w money
        private bool _aiInstantBuild = false;  // Buff: instant 0-second build
        private double _aiBuildSpeed = 1.0;    // 0 = freeze, 0.2 = 5x slower, 1.0 = normal, 2.0 = 2x, 5.0 = 5x, 10.0 = 10x
        private double _aiMoveSpeed = 1.0;     // 0 = freeze, 0.2 = 5x slower, 1.0 = normal, 1.5x, 2.0x, 3.0x, 5.0x
        private double _aiCombatMult = 1.0;    // 0.1 = 10x weaker, 1.0 = normal, 2.0x, 3.0x, 5.0x, 10.0x
        private bool _isAbsoluteTimeStop = false; // Bulletproof safe pause / time stop for all air/ground/defense units

        private readonly System.Windows.Forms.Timer _timer;
        private readonly Dictionary<int, bool> _keyWasDown = new Dictionary<int, bool>();

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

        private void LaunchGameClient()
        {
            try
            {
                string reunionExe = @"H:\RA2-Reunion\Reunion.exe";
                if (File.Exists(reunionExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = reunionExe,
                        WorkingDirectory = Path.GetDirectoryName(reunionExe),
                        UseShellExecute = true
                    });
                }
            }
            catch { }
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

        private void ResetAiState()
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
        }

        private void SetPlayerBuffs(double moveSpeed, double combatMult)
        {
            _playerMoveSpeed = moveSpeed;
            _playerCombatMult = combatMult;
            _lblPlayerSpeed.Text = $"移速: {moveSpeed:0.#}x | 火力: {combatMult:0.#}x";
        }

        private void UpdateAntiDropStatus()
        {
            if (!_mm.IsAttached) return;
            bool ok = AntiDropPatcher.Apply(_mm, _protectUnitLoss);
            if (_protectUnitLoss && ok)
            {
                _lblPatchStatus.Text = "🛡️ 防吞兵: 🟢 已激活 (绝不丢兵)";
                _lblPatchStatus.ForeColor = Color.FromArgb(130, 255, 170);
            }
            else if (!_protectUnitLoss)
            {
                _lblPatchStatus.Text = "🛡️ 防吞兵: ⚪ 未开启";
                _lblPatchStatus.ForeColor = Color.FromArgb(180, 190, 205);
            }
        }

        private void PollGlobalHotkeys()
        {
            CheckKey((int)Keys.Pause, () => ToggleAbsoluteTimeStop());
            CheckKey((int)Keys.F10, () => ToggleAbsoluteTimeStop());

            CheckKey((int)Keys.F5, () =>
            {
                AddPlayerMoney(1000000);
                _chkPlayerInstantBuild.Checked = true;
                Console.Beep(880, 120);
            });

            CheckKey((int)Keys.F6, () =>
            {
                ClearAllAiMoney();
                Console.Beep(660, 120);
            });

            CheckKey((int)Keys.F7, () =>
            {
                AddAllAiMoney(1000000);
                _chkAiLockZeroMoney.Checked = false;
                Console.Beep(770, 120);
            });

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

            CheckKey((int)Keys.F11, () =>
            {
                RevealMap();
                Console.Beep(1200, 120);
            });

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

            UpdateAntiDropStatus();

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
            if (_mm.IsAttached)
            {
                try
                {
                    var p = Process.GetProcessById(_attachedPid);
                    if (!p.HasExited) return true;
                }
                catch { }
                _mm.Detach();
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
                    _mm.Attach(h);
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
            uint currentPlayer = _mm.ReadUInt32(GameConstants.ADDR_CURRENT_PLAYER);
            uint itemsPtr = _mm.ReadUInt32(GameConstants.ADDR_HOUSE_ITEMS);
            int count = _mm.ReadInt32(GameConstants.ADDR_HOUSE_COUNT);

            if (itemsPtr == 0 || count <= 0 || count > 32) return result;

            int aiCounter = 1;
            for (int i = 0; i < count; i++)
            {
                uint h = _mm.ReadUInt32(itemsPtr + (uint)(i * 4));
                if (h == 0) continue;

                string iniName = _mm.ReadAsciiString(h + GameConstants.OFFSET_HOUSE_INI_NAME, 32);
                if (iniName.Equals("Neutral", StringComparison.OrdinalIgnoreCase) ||
                    iniName.Equals("Special", StringComparison.OrdinalIgnoreCase) ||
                    iniName.Equals("Civilian", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isPlayer = (h == currentPlayer) || (_mm.ReadByte(h + GameConstants.OFFSET_HOUSE_IS_PLAYER) == 1);
                string uiName = _mm.ReadUnicodeString(h + GameConstants.OFFSET_HOUSE_UI_NAME, 20);
                string displayName = isPlayer ? "玩家 (你)" : $"电脑 AI #{aiCounter++}";
                if (!string.IsNullOrWhiteSpace(uiName))
                    displayName += $" [{uiName}]";

                int cash = _mm.ReadInt32(h + GameConstants.OFFSET_HOUSE_CASH);
                float ore0 = _mm.ReadFloat(h + GameConstants.OFFSET_HOUSE_ORE_TIB0);
                float ore1 = _mm.ReadFloat(h + GameConstants.OFFSET_HOUSE_ORE_TIB0 + 4);
                float ore2 = _mm.ReadFloat(h + GameConstants.OFFSET_HOUSE_ORE_TIB0 + 8);
                float ore3 = _mm.ReadFloat(h + GameConstants.OFFSET_HOUSE_ORE_TIB0 + 12);
                int oreVal = (int)((ore0 + ore1 + ore2 + ore3) * 25.0f);

                double fp = _mm.ReadDouble(h + GameConstants.OFFSET_HOUSE_FIREPOWER_MULT);
                double rof = _mm.ReadDouble(h + GameConstants.OFFSET_HOUSE_ROF_MULT);

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
                        _mm.WriteInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH, 9999999);
                        h.Cash = 9999999;
                    }

                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_FIREPOWER_MULT, _playerCombatMult);
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_ROF_MULT, _playerCombatMult > 0.01 ? 1.0 / _playerCombatMult : 1.0);
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_GROUND_SPEED, _playerMoveSpeed);
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_AIR_SPEED, _playerMoveSpeed);

                    if (_playerInstantBuild)
                    {
                        _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_BUILD_SPEED, 0.0001);
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
                        _mm.WriteInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH, 9999999);
                        h.Cash = 9999999;
                    }

                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_FIREPOWER_MULT, isFrozen ? 0.0001 : _aiCombatMult);
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_ROF_MULT, isFrozen ? 1000.0 : (_aiCombatMult > 0.01 ? 1.0 / _aiCombatMult : 10.0));
                    double effMove = isFrozen ? 0.000001 : _aiMoveSpeed;
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_GROUND_SPEED, effMove);
                    _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_AIR_SPEED, effMove);

                    if (_aiInstantBuild && !isFrozen)
                    {
                        _mm.WriteDouble(h.Address + GameConstants.OFFSET_HOUSE_BUILD_SPEED, 0.0001);
                    }
                }
            }

            // 2. FactoryClass::Array - Real-time Build & Unit Production Speed Control
            uint facItems = _mm.ReadUInt32(GameConstants.ADDR_FACTORY_ITEMS);
            int facCount = _mm.ReadInt32(GameConstants.ADDR_FACTORY_COUNT);
            if (facItems != 0 && facCount > 0 && facCount <= 256)
            {
                for (int i = 0; i < facCount; i++)
                {
                    uint f = _mm.ReadUInt32(facItems + (uint)(i * 4));
                    if (f == 0) continue;

                    uint owner = _mm.ReadUInt32(f + 0x6C);
                    uint obj = _mm.ReadUInt32(f + 0x58);
                    byte suspended = _mm.ReadByte(f + 0x70);
                    if (obj == 0 || suspended != 0) continue;

                    int stage = _mm.ReadInt32(f + 0x24);

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        if (_playerInstantBuild && stage >= 0 && stage < 53)
                        {
                            _mm.WriteInt32(f + 0x24, 53); // Advance to 53/54
                            _mm.WriteInt32(f + 0x38, 1);  // Rate = 1 frame
                            _mm.WriteInt32(f + 0x34, 1);  // StepTimer = 1 frame
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen || _aiBuildSpeed <= 0.001)
                        {
                            if (stage > 1) _mm.WriteInt32(f + 0x24, 1);
                            _mm.WriteInt32(f + 0x38, 999999);
                            _mm.WriteInt32(f + 0x34, 999999);
                        }
                        else if (_aiInstantBuild && stage >= 0 && stage < 53)
                        {
                            _mm.WriteInt32(f + 0x24, 53);
                            _mm.WriteInt32(f + 0x38, 1);
                            _mm.WriteInt32(f + 0x34, 1);
                        }
                        else
                        {
                            int targetRate = Math.Clamp((int)Math.Round(15.0 / _aiBuildSpeed), 1, 5000);
                            int curRate = _mm.ReadInt32(f + 0x38);

                            if (curRate > 200 || curRate <= 0 || Math.Abs(curRate - targetRate) > 1)
                            {
                                _mm.WriteInt32(f + 0x38, targetRate);
                                _mm.WriteInt32(f + 0x34, targetRate);
                                _mm.WriteByte(f + 0x70, 0);
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
                        int houseBldgs = _mm.ReadInt32(h.Address + GameConstants.OFFSET_HOUSE_BLDG_COUNT);
                        if (houseBldgs == 0)
                        {
                            orphanedAiHouses.Add(h.Address);
                        }
                    }
                }
            }

            // 3. FootClass::Array - Ground Units (Tanks, Infantry, Harvesters)
            uint footItems = _mm.ReadUInt32(GameConstants.ADDR_FOOT_ITEMS);
            int footCount = _mm.ReadInt32(GameConstants.ADDR_FOOT_COUNT);
            if (footItems != 0 && footCount > 0 && footCount <= 4096)
            {
                double effAiMove = isFrozen ? 0.000001 : _aiMoveSpeed;

                for (int i = 0; i < footCount; i++)
                {
                    uint u = _mm.ReadUInt32(footItems + (uint)(i * 4));
                    if (u == 0) continue;

                    uint owner = _mm.ReadUInt32(u + GameConstants.OFFSET_TECHNO_OWNER);

                    if (orphanedAiHouses.Contains(owner))
                    {
                        _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_HEALTH, 0);
                        _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_MISSION, 0);
                        continue;
                    }

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        double cur = _mm.ReadDouble(u + GameConstants.OFFSET_TECHNO_SPEED_MULT);
                        if (Math.Abs(cur - _playerMoveSpeed) > 0.01)
                        {
                            _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_MULT, _playerMoveSpeed);
                            _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_PCT, _playerMoveSpeed);
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_MULT, 0.000001);
                            _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_PCT, 0.000001);
                            _mm.WriteByte(u + GameConstants.OFFSET_TECHNO_DEACTIVATED, 1);
                            _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_MISSION, 0);
                            _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 999999);
                        }
                        else
                        {
                            byte curDeact = _mm.ReadByte(u + GameConstants.OFFSET_TECHNO_DEACTIVATED);
                            if (curDeact != 0)
                            {
                                _mm.WriteByte(u + GameConstants.OFFSET_TECHNO_DEACTIVATED, 0);
                                _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 0);
                                int curMission = _mm.ReadInt32(u + GameConstants.OFFSET_TECHNO_MISSION);
                                if (curMission == 0)
                                {
                                    _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_MISSION, 5); // Guard
                                }
                                _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_TIMER, 0);
                            }

                            double cur = _mm.ReadDouble(u + GameConstants.OFFSET_TECHNO_SPEED_MULT);
                            if (Math.Abs(cur - effAiMove) > 0.0001)
                            {
                                _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_MULT, effAiMove);
                                _mm.WriteDouble(u + GameConstants.OFFSET_TECHNO_SPEED_PCT, effAiMove);
                            }
                        }
                    }
                }
            }

            // 4. AircraftClass::Array - Air Units (Kirovs, Harriers, Black Eagles, J-10s, Helis)
            uint airItems = _mm.ReadUInt32(GameConstants.ADDR_AIRCRAFT_ITEMS);
            int airCount = _mm.ReadInt32(GameConstants.ADDR_AIRCRAFT_COUNT);
            if (airItems != 0 && airCount > 0 && airCount <= 2048)
            {
                double effAiMove = isFrozen ? 0.000001 : _aiMoveSpeed;

                for (int i = 0; i < airCount; i++)
                {
                    uint a = _mm.ReadUInt32(airItems + (uint)(i * 4));
                    if (a == 0) continue;

                    uint owner = _mm.ReadUInt32(a + GameConstants.OFFSET_TECHNO_OWNER);

                    if (orphanedAiHouses.Contains(owner))
                    {
                        _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_HEALTH, 0);
                        _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_MISSION, 0);
                        continue;
                    }

                    if (owner == playerAddress && playerAddress != 0)
                    {
                        double cur = _mm.ReadDouble(a + GameConstants.OFFSET_TECHNO_SPEED_MULT);
                        if (Math.Abs(cur - _playerMoveSpeed) > 0.01)
                        {
                            _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_MULT, _playerMoveSpeed);
                            _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_PCT, _playerMoveSpeed);
                        }
                    }
                    else if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_MULT, 0.000001);
                            _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_PCT, 0.000001);
                            _mm.WriteByte(a + GameConstants.OFFSET_TECHNO_DEACTIVATED, 1);
                            _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_MISSION, 0);
                            _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 999999);
                        }
                        else
                        {
                            byte curDeact = _mm.ReadByte(a + GameConstants.OFFSET_TECHNO_DEACTIVATED);
                            if (curDeact != 0)
                            {
                                _mm.WriteByte(a + GameConstants.OFFSET_TECHNO_DEACTIVATED, 0);
                                _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 0);
                                int curMission = _mm.ReadInt32(a + GameConstants.OFFSET_TECHNO_MISSION);
                                if (curMission == 0)
                                {
                                    _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_MISSION, 5); // Guard
                                }
                                _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_TIMER, 0);
                            }

                            double cur = _mm.ReadDouble(a + GameConstants.OFFSET_TECHNO_SPEED_MULT);
                            if (Math.Abs(cur - effAiMove) > 0.0001)
                            {
                                _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_MULT, effAiMove);
                                _mm.WriteDouble(a + GameConstants.OFFSET_TECHNO_SPEED_PCT, effAiMove);
                            }
                        }
                    }
                }
            }

            // 5. BuildingClass::Array - Defense Structures (Prism, Tesla, Gatling, Patriot, Flak)
            uint bldgItems = _mm.ReadUInt32(GameConstants.ADDR_BUILDING_ITEMS);
            int bldgCount = _mm.ReadInt32(GameConstants.ADDR_BUILDING_COUNT);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 2048)
            {
                for (int i = 0; i < bldgCount; i++)
                {
                    uint b = _mm.ReadUInt32(bldgItems + (uint)(i * 4));
                    if (b == 0) continue;

                    uint owner = _mm.ReadUInt32(b + GameConstants.OFFSET_TECHNO_OWNER);
                    if (aiAddresses.Contains(owner))
                    {
                        if (isFrozen)
                        {
                            _mm.WriteByte(b + GameConstants.OFFSET_TECHNO_DEACTIVATED, 1);
                            _mm.WriteInt32(b + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 999999);
                        }
                        else
                        {
                            byte curDeact = _mm.ReadByte(b + GameConstants.OFFSET_TECHNO_DEACTIVATED);
                            if (curDeact != 0)
                            {
                                _mm.WriteByte(b + GameConstants.OFFSET_TECHNO_DEACTIVATED, 0);
                                _mm.WriteInt32(b + GameConstants.OFFSET_TECHNO_RELOAD_TIMER, 0);
                            }
                        }
                    }
                }
            }
        }

        private void ZeroHouseMoneyAndOre(uint h)
        {
            _mm.WriteInt32(h + GameConstants.OFFSET_HOUSE_CASH, 0);
            byte[] zeros16 = new byte[16];
            _mm.WriteBytes(h + GameConstants.OFFSET_HOUSE_ORE_TIB0, zeros16);

            uint bldgItems = _mm.ReadUInt32(h + GameConstants.OFFSET_HOUSE_BLDG_ITEMS);
            int bldgCount = _mm.ReadInt32(h + GameConstants.OFFSET_HOUSE_BLDG_COUNT);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 512)
            {
                for (int j = 0; j < bldgCount; j++)
                {
                    uint b = _mm.ReadUInt32(bldgItems + (uint)(j * 4));
                    if (b != 0)
                    {
                        _mm.WriteBytes(b + 0x33C, zeros16);
                    }
                }
            }
        }

        private void ClearAllAiMoney()
        {
            if (!_mm.IsAttached) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                    ZeroHouseMoneyAndOre(h.Address);
            }
        }

        private void SetAllAiMoney(int amount)
        {
            if (!_mm.IsAttached) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                {
                    ZeroHouseMoneyAndOre(h.Address);
                    _mm.WriteInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH, amount);
                }
            }
        }

        private void AddAllAiMoney(int delta)
        {
            if (!_mm.IsAttached) return;
            foreach (var h in ReadActiveHouses())
            {
                if (!h.IsPlayer)
                {
                    int cur = _mm.ReadInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH);
                    _mm.WriteInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH, Math.Max(0, cur + delta));
                }
            }
        }

        private void AddPlayerMoney(int delta)
        {
            if (!_mm.IsAttached) return;
            foreach (var h in ReadActiveHouses())
            {
                if (h.IsPlayer)
                {
                    int cur = _mm.ReadInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH);
                    _mm.WriteInt32(h.Address + GameConstants.OFFSET_HOUSE_CASH, Math.Max(0, cur + delta));
                }
            }
        }

        public void RevealMap()
        {
            if (!_mm.IsAttached) return;
            uint currentPlayer = _mm.ReadUInt32(GameConstants.ADDR_CURRENT_PLAYER);
            if (currentPlayer == 0) return;

            _mm.WriteByte(currentPlayer + GameConstants.OFFSET_HOUSE_MAP_CLEAR, 1);
            _mm.WriteByte(currentPlayer + GameConstants.OFFSET_HOUSE_SPYSAT, 1);

            // Call MapClass::Reveal_All(PlayerHouse) natively
            byte[] revealCode = new byte[] {
                0xB9, 0xE8, 0xF7, 0x87, 0x00,       // mov ecx, 0x87f7e8 (MapClass::Instance)
                0xFF, 0x35, 0x4C, 0x3D, 0xA8, 0x00, // push dword ptr [0x00A83D4C] (Player HouseClass*)
                0xB8, 0x90, 0x7D, 0x57, 0x00,       // mov eax, 0x577d90
                0xFF, 0xD0,                         // call eax
                0xC2, 0x04, 0x00                    // ret 4
            };
            _mm.ExecuteRemoteCode(revealCode, 1000);
            Console.Beep(1200, 120);
        }

        public void EliminateAllEnemies()
        {
            if (!_mm.IsAttached) return;
            uint currentPlayer = _mm.ReadUInt32(GameConstants.ADDR_CURRENT_PLAYER);
            if (currentPlayer == 0) return;

            // 1. Foot units
            uint footItems = _mm.ReadUInt32(GameConstants.ADDR_FOOT_ITEMS);
            int footCount = _mm.ReadInt32(GameConstants.ADDR_FOOT_COUNT);
            if (footItems != 0 && footCount > 0 && footCount <= 4096)
            {
                for (int i = 0; i < footCount; i++)
                {
                    uint u = _mm.ReadUInt32(footItems + (uint)i * 4);
                    if (u == 0) continue;
                    uint owner = _mm.ReadUInt32(u + GameConstants.OFFSET_TECHNO_OWNER);
                    if (owner != 0 && owner != currentPlayer && _mm.ReadByte(owner + GameConstants.OFFSET_HOUSE_IS_PLAYER) == 0)
                    {
                        _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_HEALTH, 0);
                        _mm.WriteInt32(u + GameConstants.OFFSET_TECHNO_MISSION, 0);
                    }
                }
            }

            // 2. Aircraft units (Kirovs, aircraft)
            uint airItems = _mm.ReadUInt32(GameConstants.ADDR_AIRCRAFT_ITEMS);
            int airCount = _mm.ReadInt32(GameConstants.ADDR_AIRCRAFT_COUNT);
            if (airItems != 0 && airCount > 0 && airCount <= 2048)
            {
                for (int i = 0; i < airCount; i++)
                {
                    uint a = _mm.ReadUInt32(airItems + (uint)i * 4);
                    if (a == 0) continue;
                    uint owner = _mm.ReadUInt32(a + GameConstants.OFFSET_TECHNO_OWNER);
                    if (owner != 0 && owner != currentPlayer && _mm.ReadByte(owner + GameConstants.OFFSET_HOUSE_IS_PLAYER) == 0)
                    {
                        _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_HEALTH, 0);
                        _mm.WriteInt32(a + GameConstants.OFFSET_TECHNO_MISSION, 0);
                    }
                }
            }

            // 3. Buildings
            uint bldgItems = _mm.ReadUInt32(GameConstants.ADDR_BUILDING_ITEMS);
            int bldgCount = _mm.ReadInt32(GameConstants.ADDR_BUILDING_COUNT);
            if (bldgItems != 0 && bldgCount > 0 && bldgCount <= 2048)
            {
                for (int i = 0; i < bldgCount; i++)
                {
                    uint b = _mm.ReadUInt32(bldgItems + (uint)i * 4);
                    if (b == 0) continue;
                    uint owner = _mm.ReadUInt32(b + GameConstants.OFFSET_TECHNO_OWNER);
                    if (owner != 0 && owner != currentPlayer && _mm.ReadByte(owner + GameConstants.OFFSET_HOUSE_IS_PLAYER) == 0)
                    {
                        _mm.WriteInt32(b + GameConstants.OFFSET_TECHNO_HEALTH, 0);
                    }
                }
            }

            // 4. DestroyAll and AcceptDefeat for all enemy houses
            byte[] destroyCode = new byte[] {
                0x60,
                0x8B, 0x15, 0x2C, 0x02, 0xA8, 0x00,
                0x8B, 0x35, 0x38, 0x02, 0xA8, 0x00,
                0x8B, 0x1D, 0x4C, 0x3D, 0xA8, 0x00,
                0x31, 0xFF,
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
                0x47,
                0xEB, 0xC9,
                0x61,
                0xC2, 0x04, 0x00
            };
            _mm.ExecuteRemoteCode(destroyCode, 2000);
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_mm.IsAttached)
            {
                AntiDropPatcher.Apply(_mm, false);
                _mm.Detach();
            }
            base.OnFormClosed(e);
        }
    }
}
