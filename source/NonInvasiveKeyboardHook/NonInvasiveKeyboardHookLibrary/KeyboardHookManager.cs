﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace NonInvasiveKeyboardHookLibrary
{
    /// <summary>
    /// A hotkey manager that uses a low-level global keyboard hook, but eventually only fires events for
    /// pre-registered hotkeys, i.e. not invading a user's privacy.
    /// </summary>
    public class KeyboardHookManager
    {
        internal class CallbackStruct
        {
            public readonly Action Action;
            public readonly bool Blocking;

            public CallbackStruct(Action action, bool blocking)
            {
                this.Action = action;
                this.Blocking = blocking;
            }
        }

        #region Private Attributes
        /// <summary>
        /// Keeps track of all registered hotkeys
        /// </summary>
        private readonly Dictionary<KeybindStruct, CallbackStruct> _registeredCallbacks;
        /// <summary>
        /// Keeps track of modifier keys that are held down
        /// </summary>
        private readonly HashSet<ModifierKeys> _downModifierKeys;
        /// <summary>
        /// Keeps track of all keys that are held down to prevent firing callbacks
        /// more than once for a single keypress
        /// </summary>
        private readonly HashSet<int> _downKeys;
        private readonly object _modifiersLock = new object();
        private LowLevelKeyboardProc _hook;
        private bool _isStarted;
        #endregion

        #region Constructors
        /// <summary>
        /// Instantiates an empty keyboard hook manager.
        /// It is best practice to keep a single instance per process.
        /// Start() must be called to start the low-level keyboard hook manager
        /// </summary>
        public KeyboardHookManager()
        {
            this._registeredCallbacks = new Dictionary<KeybindStruct, CallbackStruct>();
            this._downModifierKeys = new HashSet<ModifierKeys>();
            this._downKeys = new HashSet<int>();
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
            if (this._isStarted) return;

            this._hook = this.HookCallback;
            _hookId = SetHook(this._hook);
            this._isStarted = true;
        }

        /// <summary>
        /// Pauses the low-level keyboard hook (without unregistering the existing hotkeys)
        /// </summary>
        public void Stop()
        {
            if (this._isStarted)
            {
                UnhookWindowsHookEx(_hookId);
                this._isStarted = false;
            }
        }

        /// <summary>
        /// Registers a hotkey.
        /// </summary>
        /// <param name="virtualKeyCode">The virtual key code of the hotkey</param>
        /// <param name="action">The callback action to invoke when this hotkey is pressed</param>
        /// <param name="blocking">Boolean indicating wether to block the original shortcut from propagating</param>
        /// <exception cref="HotkeyAlreadyRegisteredException">Thrown when the given key is already mapped to a callback</exception>
        /// /// <returns>Unique identifier of this hotkey, which can be used to remove it later</returns>
        public List<Guid> RegisterHotkey(int virtualKeyCode, Action action, bool blocking = false, List<int> alternativVirtualKeyCode = null)
        {
            return this.RegisterHotkey(new ModifierKeys[0], virtualKeyCode, action, blocking, alternativVirtualKeyCode);
        }

        /// <summary>
        /// Registers a new key combination with one or more modifiers
        /// </summary>
        /// <param name="modifiers">Modifiers that must be held while hitting the key. Multiple modifiers can be provided using the flags bitwise OR operation</param>
        /// <param name="virtualKeyCode">The virtual key code of the standard key</param>
        /// <param name="action">The callback action to invoke when this combination is pressed</param>
        /// <param name="blocking">Boolean indicating wether to block the original shortcut from propagating</param>
        /// <exception cref="HotkeyAlreadyRegisteredException">Thrown when the given key combination is already mapped to a callback</exception>
        /// <returns>Unique identifier of this hotkey, which can be used to remove it later</returns>
        public List<Guid> RegisterHotkey(ModifierKeys modifiers, int virtualKeyCode, Action action, bool blocking = false, List<int> alternativVirtualKeyCode = null)
        {
            var allModifiers = Enum.GetValues(typeof(ModifierKeys)).Cast<ModifierKeys>().ToArray();

            // Get the modifiers that were chained with bitwise OR operation as an array of modifiers
            var selectedModifiers = allModifiers.Where(modifier => modifiers.HasFlag(modifier)).ToArray();

            return RegisterHotkey(selectedModifiers, virtualKeyCode, action, blocking, alternativVirtualKeyCode);
        }

        /// <summary>
        /// Registers a new key combination.
        /// </summary>
        /// <param name="modifiers">Modifiers that must be held while hitting the key</param>
        /// <param name="virtualKeyCode">The virtual key code of the standard key</param>
        /// <param name="action">The callback action to invoke when this combination is pressed</param>
        /// <param name="blocking">Boolean indicating wether to block the original shortcut from propagating</param>
        /// <exception cref="HotkeyAlreadyRegisteredException">Thrown when the given key combination is already mapped to a callback</exception>
        /// <returns>Unique identifier of this hotkey, which can be used to remove it later</returns>
        public List<Guid> RegisterHotkey(ModifierKeys[] modifiers, int virtualKeyCode, Action action, bool blocking = false, List<int> alternativVirtualKeyCode = null)
        {
            List<Guid> resultIdentitys = new List<Guid>();

            List<int> keysToRegister = new List<int>();
            keysToRegister.Add(virtualKeyCode);
            if(alternativVirtualKeyCode != null)
            {
                alternativVirtualKeyCode.Remove(virtualKeyCode);
                keysToRegister.AddRange(alternativVirtualKeyCode);
            }

            foreach (int key in keysToRegister)
            {
                var keybindIdentity = Guid.NewGuid();
                var keybind = new KeybindStruct(modifiers, key, keybindIdentity);
                if (this._registeredCallbacks.ContainsKey(keybind))
                {
                    throw new HotkeyAlreadyRegisteredException();
                }

                var callbackStruct = new CallbackStruct(action, blocking);
                this._registeredCallbacks[keybind] = callbackStruct;

                resultIdentitys.Add(keybindIdentity);
            }
           
           
            return resultIdentitys;
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

        /// <summary>
        /// Unregisters a specific key by its unique identifier
        /// </summary>
        /// <param name="keybindIdentity">Keybind GUID, as returned by RegisterHotkey</param>
        public void UnregisterHotkey(Guid keybindIdentity)
        {
            var keybindToRemove = this._registeredCallbacks.Keys.FirstOrDefault(keybind =>
                keybind.Identifier.HasValue && keybind.Identifier.Value.Equals(keybindIdentity));

            if (keybindToRemove == null || !this._registeredCallbacks.Remove(keybindToRemove))
            {
                throw new HotkeyNotRegisteredException();
            }
        }
        #endregion

        #region Private methods
        private void InvokeAction(object actionObj)
        {
            var action = (Action)actionObj;
            action.Invoke();
        }

        private bool HandleKeyPress(int virtualKeyCode)
        {
            var currentKey = new KeybindStruct(this._downModifierKeys, virtualKeyCode);

            if (this._registeredCallbacks.ContainsKey(currentKey) &&
                this._registeredCallbacks.TryGetValue(currentKey, out var callbackStruct))
            {
                ThreadPool.QueueUserWorkItem(this.InvokeAction, callbackStruct.Action);
                return callbackStruct.Blocking;
            }

            return false;
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

            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, userLibrary, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var vkCode = Marshal.ReadInt32(lParam);

                var blocking = this.HandleSingleKeyboardInput(wParam, vkCode);

                if (blocking)
                    return (IntPtr)1;
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool HandleSingleKeyboardInput(IntPtr wParam, int vkCode)
        {
            var modifierKey = ModifierKeysUtilities.GetModifierKeyFromCode(vkCode);
            var blocking = false;

            // If the keyboard event is a KeyDown event (i.e. key pressed)
            if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
            {
                // In this case, we only care about modifier keys
                if (modifierKey != null)
                {
                    lock (this._modifiersLock)
                    {
                        this._downModifierKeys.Add(modifierKey.Value);
                    }
                }

                // Trigger callbacks that are registered for this key, but only once per key press
                if (!this._downKeys.Contains(vkCode))
                {
                    blocking = this.HandleKeyPress(vkCode);
                    this._downKeys.Add(vkCode);
                }
            }

            // If the keyboard event is a KeyUp event (i.e. key released)
            if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
            {
                // If the released key is a modifier key, remove it from the HashSet of modifier keys
                if (modifierKey != null)
                {
                    lock (this._modifiersLock)
                    {
                        this._downModifierKeys.Remove(modifierKey.Value);
                    }
                }

                this._downKeys.Remove(vkCode);
            }

            return blocking;
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