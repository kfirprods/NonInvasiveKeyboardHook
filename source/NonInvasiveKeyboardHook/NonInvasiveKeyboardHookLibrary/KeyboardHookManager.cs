using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NonInvasiveKeyboardHookLibrary
{
    /// <summary>
    /// A hotkey manager that uses a low-level global keyboard hook, but eventually only fires events for
    /// pre-registered hotkeys, i.e. not invading a user's privacy.
    /// </summary>
    public class KeyboardHookManager
    {
        private readonly Dictionary<int, List<Action>> _registeredCallbacks;
        private readonly HashSet<ModifierKeys> _downModifierKeys;

        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<int, List<Action>>();
            this._downModifierKeys = new HashSet<ModifierKeys>();
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

        public bool IsModifierDown(ModifierKeys modifier)
        {
            return this._downModifierKeys.Contains(modifier);
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
        private const int WM_KEYUP = 0x0101;

        private static IntPtr _hookId = IntPtr.Zero;

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            var userLibrary = LoadLibrary("User32");

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                userLibrary, 0);
        }

        public IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                var modifierKey = ModifierKeysUtilities.GetModifierKeyFromCode(vkCode);

                if (wParam == (IntPtr) WM_KEYDOWN)
                {
                    if (modifierKey != null)
                    {
                        this._downModifierKeys.Add(modifierKey.Value);
                    }
                }

                if (wParam == (IntPtr) WM_KEYUP)
                {
                    if (modifierKey != null)
                    {
                        this._downModifierKeys.Remove(modifierKey.Value);
                    }

                    this.HandleKeyPress(vkCode);
                }
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
        
        /// <summary>
        /// Loads the library.
        /// </summary>
        /// <param name="lpFileName">Name of the library</param>
        /// <returns>A handle to the library</returns>
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string lpFileName);
        #endregion
    }
}
