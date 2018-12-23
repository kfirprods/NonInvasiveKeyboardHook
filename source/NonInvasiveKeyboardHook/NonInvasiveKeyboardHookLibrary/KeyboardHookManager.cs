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
        #region Private Attributes
        private readonly Dictionary<KeybindStruct, Action> _registeredCallbacks;
        private readonly HashSet<ModifierKeys> _downModifierKeys;
        private LowLevelKeyboardProc _hook;
        #endregion

        #region Constructors
        /// <summary>
        /// Instantiates an empty keyboard hook manager.
        /// It is best practice to keep a single instance per process.
        /// Start() must be called to start the low-level keyboard hook manager
        /// </summary>
        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<KeybindStruct, Action>();
            this._downModifierKeys = new HashSet<ModifierKeys>();
        }
        #endregion

        #region Public API
        /// <summary>
        /// Starts the low-level keyboard hook.
        /// Hotkeys can be registered regardless of the low-level keyboard hook's state, but their callbacks
        /// will only ever be invoked if the low-level keyboard hook is running and intercepting keys.
        /// </summary>
        public void Start()
        {
            this._hook = this.HookCallback;
            _hookId = SetHook(this._hook);
        }

        /// <summary>
        /// Pauses the low-level keyboard hook without unregistering the existing hotkeys
        /// </summary>
        public void Stop()
        {
            UnhookWindowsHookEx(_hookId);
        }

        /// <summary>
        /// Registers a hotkey.
        /// </summary>
        /// <param name="virtualKeyCode">The virtual key code of the hotkey</param>
        /// <param name="action">The callback action to invoke when this hotkey is pressed</param>
        /// <exception cref="HotkeyAlreadyRegisteredException">Thrown when the given key is already mapped to a callback</exception>
        public void RegisterHotkey(int virtualKeyCode, Action action)
        {
            this.RegisterHotkey(new ModifierKeys[0], virtualKeyCode, action);
        }

        /// <summary>
        /// Registers a new key combination.
        /// </summary>
        /// <param name="modifiers">Modifiers that must be held while hitting the key</param>
        /// <param name="virtualKeyCode">The virtual key code of the standard key</param>
        /// <param name="action">The callback action to invoke when this combination is pressed</param>
        /// <exception cref="HotkeyAlreadyRegisteredException">Thrown when the given key combination is already mapped to a callback</exception>
        public void RegisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode, Action action)
        {
            var keybind = new KeybindStruct(modifiers, virtualKeyCode);
            if (this._registeredCallbacks.ContainsKey(keybind))
            {
                throw new HotkeyAlreadyRegisteredException();
            }

            this._registeredCallbacks[keybind] = action;
        }

        /// <summary>
        /// Unregisters all hotkeys (the low-level keyboard hook continues running)
        /// </summary>
        public void UnregisterAll()
        {
            this._registeredCallbacks.Clear();
        }

        /// <summary>
        /// Unregisters a specific single-key hotkey
        /// </summary>
        /// <param name="virtualKeyCode">Virtual key code of the unregistered key</param>
        /// <exception cref="HotkeyNotRegisteredException">Thrown when the given key combination is not registered</exception>
        public void UnregisterHotkey(int virtualKeyCode)
        {
            this.UnregisterHotkey(new ModifierKeys[0], virtualKeyCode);
        }

        /// <summary>
        /// Unregisters a specific key combination
        /// </summary>
        /// <param name="modifiers">The modifiers of the combination</param>
        /// <param name="virtualKeyCode">The key of the combination</param>
        /// <exception cref="HotkeyNotRegisteredException">Thrown when the given key combination is not registered</exception>
        public void UnregisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode)
        {
            var keybind = new KeybindStruct(modifiers, virtualKeyCode);

            if (!this._registeredCallbacks.Remove(keybind))
            {
                throw new HotkeyNotRegisteredException();
            }
        }
        #endregion

        #region Private methods
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
        #endregion

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

    #region Exceptions
    public class NonInvasiveKeyboardHookException : Exception
    {
    }

    public class HotkeyAlreadyRegisteredException : NonInvasiveKeyboardHookException
    {
    }

    public class HotkeyNotRegisteredException : NonInvasiveKeyboardHookException
    {
    }
    #endregion
}
