using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NonInvasiveKeyboardHookLibrary
{
    /// <summary>
    /// A hotkey manager that uses a low-level global keyboard hook, but eventually only fires events for
    /// pre-registered hotkeys, i.e. not invading a user's privacy.
    /// </summary>
    public class KeyboardHookManager
    {
        private readonly Dictionary<KeybindStruct, Action> _registeredCallbacks;
        private readonly HashSet<ModifierKeys> _downModifierKeys;
        private LowLevelKeyboardProc _hook;

        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<KeybindStruct, Action>();
            this._downModifierKeys = new HashSet<ModifierKeys>();
        }

        public void Start()
        {
            this._hook = this.HookCallback;
            _hookId = SetHook(this._hook);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(_hookId);
        }

        public void RegisterHotkey(int virtualKeyCode, Action action)
        {
            this.RegisterHotkey(new ModifierKeys[0], virtualKeyCode, action);
        }

        public void RegisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode, Action action)
        {
            var keybind = new KeybindStruct(modifiers, virtualKeyCode);
            if (this._registeredCallbacks.ContainsKey(keybind))
            {
                throw new HotkeyAlreadyRegisteredException();
            }

            this._registeredCallbacks[keybind] = action;
        }

        public void UnregisterAll()
        {
            this._registeredCallbacks.Clear();
        }

        public void UnregisterHotkey(int virtualKeyCode)
        {
            this.UnregisterHotkey(new ModifierKeys[0], virtualKeyCode);
        }

        public void UnregisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode)
        {
            var keybind = new KeybindStruct(modifiers, virtualKeyCode);

            if (!this._registeredCallbacks.Remove(keybind))
            {
                throw new HotkeyNotRegisteredException();
            }
        }
        
        private void HandleKeyPress(int virtualKeyCode)
        {
            var currentKey = new KeybindStruct(this._downModifierKeys, virtualKeyCode);

            if (!this._registeredCallbacks.ContainsKey(currentKey))
            {
                return;
            }

            if (this._registeredCallbacks.TryGetValue(currentKey, out var callback))
            {
                callback.Invoke();
            }
        }

        #region Low level keyboard hook
        // Source: https://blogs.msdn.microsoft.com/toub/2006/05/03/low-level-keyboard-hook-in-c/

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

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

                if (wParam == (IntPtr) WM_KEYDOWN || wParam == (IntPtr) WM_SYSKEYDOWN)
                {
                    if (modifierKey != null)
                    {
                        this._downModifierKeys.Add(modifierKey.Value);
                    }
                }

                if (wParam == (IntPtr) WM_KEYUP || wParam == (IntPtr) WM_SYSKEYUP)
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

    public class NonInvasiveKeyboardHookException : Exception
    {
    }

    public class HotkeyAlreadyRegisteredException : NonInvasiveKeyboardHookException
    {
    }

    public class HotkeyNotRegisteredException : NonInvasiveKeyboardHookException
    {
    }
}
