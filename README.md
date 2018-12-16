# NonInvasiveKeyboardHook
A C# hotkey manager that uses a low level global hook, but allows registering for specific keys to reduce invasion of user privacy.


Example:
```csharp
var keyboardHookManager = new KeyboardHookManager();
keyboardHookManager.Start();

// Register virtual key code 0x60 = NumPad0
keyboardHookManager.RegisterHotkey(0x60, () =>
{
    // Check if the Control modifier key is held
    if (keyboardHookManager.IsModifierDown(NonInvasiveKeyboardHookLibrary.ModifierKeys.Control))
    {
        Debug.WriteLine("Ctrl + NumPad0 detected. Unregistering keyboard hook");
        keyboardHookManager.Stop();
    }
});
```
