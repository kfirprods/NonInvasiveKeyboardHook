using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NonInvasiveKeyboardHookLib
{
    public class KeyboardHookManager
    {
        private readonly Dictionary<int, List<Action>> _registeredCallbacks;

        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<int, List<Action>>();
        }

        public void Start()
        {
            _hookId = SetHook(this.HookCallback);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(_hookId);
        }

        public void RegisterHotkey(int virtualKeyCode, Action action)
        {
            if (!this._registeredCallbacks.ContainsKey(virtualKeyCode))
            {
                this._registeredCallbacks[virtualKeyCode] = new List<Action>();
            }

            this._registeredCallbacks[virtualKeyCode].Add(action);
        }

        private void HandleKeyPress(int virtualKeyCode)
        {
            if (!this._registeredCallbacks.ContainsKey(virtualKeyCode))
            {
                return;
            }

            foreach (var callback in this._registeredCallbacks[virtualKeyCode])
            {
                callback.Invoke();
            }
        }

        #region Low level keyboard hook
        // Source: https://blogs.msdn.microsoft.com/toub/2006/05/03/low-level-keyboard-hook-in-c/

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static IntPtr _hookId = IntPtr.Zero;

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr) WM_KEYDOWN)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                this.HandleKeyPress(vkCode);
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion
    }
}
