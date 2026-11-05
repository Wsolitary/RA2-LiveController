using System;
using RA2LiveController.Core;

namespace RA2LiveController.Patches
{
    /// <summary>
    /// Critical Anti-Drop / Unit Loss Memory Hot-Patcher.
    /// 
    /// Technical Background:
    /// In Westwood's original engine, when BuildingClass::Exit_Object returns 0
    /// (e.g. exit tile temporarily blocked or occupied by friendly units/debris during instant 0-sec build),
    /// the engine jumps to Abandon_Production() and permanently destroys the newly manufactured unit.
    /// This causes the notorious bug: "queued 10 units, finished 10 progress bars, but only 2 spawned".
    /// 
    /// This patcher alters the failure jumps (JNZ -> JMP retry wait) so that completed units wait safely
    /// until the doorway clears, guaranteeing 100% (10/10) spawn rate without unit loss.
    /// </summary>
    public static class AntiDropPatcher
    {
        private static readonly (uint addr, byte origByte, byte patchByte)[] Patches = new[]
        {
            (0x0044ec94u, (byte)0x75, (byte)0xEB), // War factory / Vehicle factory
            (0x0044ed15u, (byte)0x75, (byte)0xEB), // Naval / Shipyard factory
            (0x0044ed96u, (byte)0x75, (byte)0xEB), // Aircraft / Helipad factory
            (0x0044ee17u, (byte)0x75, (byte)0xEB), // Barracks / Infantry factory #1
            (0x0044ee98u, (byte)0x75, (byte)0xEB), // Barracks / Infantry factory #2
            (0x0044eba8u, (byte)0x75, (byte)0xEB), // Primary Building factory
            (0x00509218u, (byte)0x75, (byte)0xEB)  // HouseClass production queue abandon check
        };

        public static bool Apply(MemoryManager mm, bool enable)
        {
            if (!mm.IsAttached) return false;

            int patchedCount = 0;
            foreach (var (addr, origByte, patchByte) in Patches)
            {
                byte target = enable ? patchByte : origByte;
                byte cur = mm.ReadByte(addr);

                if (cur != target && (cur == origByte || cur == patchByte))
                {
                    if (NativeMethods.VirtualProtectEx(mm.ProcessHandle, (IntPtr)addr, (UIntPtr)1, 0x40, out uint oldProt))
                    {
                        mm.WriteByte(addr, target);
                        NativeMethods.VirtualProtectEx(mm.ProcessHandle, (IntPtr)addr, (UIntPtr)1, oldProt, out _);
                    }
                }

                if (mm.ReadByte(addr) == target)
                    patchedCount++;
            }

            return patchedCount == Patches.Length;
        }
    }
}
