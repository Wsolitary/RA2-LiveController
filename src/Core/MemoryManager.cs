using System;
using System.Text;

namespace RA2LiveController.Core
{
    public class MemoryManager
    {
        private IntPtr _hProcess = IntPtr.Zero;
        public IntPtr ProcessHandle => _hProcess;
        public bool IsAttached => _hProcess != IntPtr.Zero;

        public void Attach(IntPtr handle)
        {
            Detach();
            _hProcess = handle;
        }

        public void Detach()
        {
            if (_hProcess != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
        }

        public byte ReadByte(uint addr)
        {
            if (_hProcess == IntPtr.Zero) return 0;
            byte[] buf = new byte[1];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 1, out _);
            return buf[0];
        }

        public int ReadInt32(uint addr)
        {
            if (_hProcess == IntPtr.Zero) return 0;
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToInt32(buf, 0);
        }

        public uint ReadUInt32(uint addr)
        {
            if (_hProcess == IntPtr.Zero) return 0;
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToUInt32(buf, 0);
        }

        public float ReadFloat(uint addr)
        {
            if (_hProcess == IntPtr.Zero) return 0f;
            byte[] buf = new byte[4];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
            return BitConverter.ToSingle(buf, 0);
        }

        public double ReadDouble(uint addr)
        {
            if (_hProcess == IntPtr.Zero) return 0.0;
            byte[] buf = new byte[8];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, 8, out _);
            return BitConverter.ToDouble(buf, 0);
        }

        public byte[] ReadBytes(uint addr, int size)
        {
            byte[] buf = new byte[size];
            if (_hProcess == IntPtr.Zero) return buf;
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, size, out _);
            return buf;
        }

        public bool WriteByte(uint addr, byte val)
        {
            if (_hProcess == IntPtr.Zero) return false;
            byte[] buf = new byte[] { val };
            return NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 1, out _);
        }

        public bool WriteInt32(uint addr, int val)
        {
            if (_hProcess == IntPtr.Zero) return false;
            byte[] buf = BitConverter.GetBytes(val);
            return NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
        }

        public bool WriteUInt32(uint addr, uint val)
        {
            if (_hProcess == IntPtr.Zero) return false;
            byte[] buf = BitConverter.GetBytes(val);
            return NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 4, out _);
        }

        public bool WriteDouble(uint addr, double val)
        {
            if (_hProcess == IntPtr.Zero) return false;
            byte[] buf = BitConverter.GetBytes(val);
            return NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, buf, 8, out _);
        }

        public bool WriteBytes(uint addr, byte[] data)
        {
            if (_hProcess == IntPtr.Zero || data == null || data.Length == 0) return false;
            return NativeMethods.WriteProcessMemory(_hProcess, (IntPtr)addr, data, data.Length, out _);
        }

        public string ReadAsciiString(uint addr, int maxLen)
        {
            if (_hProcess == IntPtr.Zero) return string.Empty;
            byte[] buf = new byte[maxLen];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, maxLen, out _);
            int len = Array.IndexOf(buf, (byte)0);
            if (len < 0) len = maxLen;
            return Encoding.ASCII.GetString(buf, 0, len);
        }

        public string ReadUnicodeString(uint addr, int maxChars)
        {
            if (_hProcess == IntPtr.Zero) return string.Empty;
            byte[] buf = new byte[maxChars * 2];
            NativeMethods.ReadProcessMemory(_hProcess, (IntPtr)addr, buf, buf.Length, out _);
            int len = 0;
            while (len + 1 < buf.Length && (buf[len] != 0 || buf[len + 1] != 0))
                len += 2;
            return Encoding.Unicode.GetString(buf, 0, len);
        }

        public bool ExecuteRemoteCode(byte[] code, uint waitMs = 2000)
        {
            if (_hProcess == IntPtr.Zero || code == null || code.Length == 0) return false;
            IntPtr pAlloc = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            try
            {
                pAlloc = NativeMethods.VirtualAllocEx(_hProcess, IntPtr.Zero, (uint)code.Length,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_EXECUTE_READWRITE);
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
    }
}
