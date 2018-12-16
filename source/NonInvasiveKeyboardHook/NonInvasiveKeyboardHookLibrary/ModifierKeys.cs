namespace NonInvasiveKeyboardHookLibrary
{
    public enum ModifierKeys
    {
        Alt,
        Control,
        Shift
    }

    public static class ModifierKeysUtilities
    {
        public static ModifierKeys? GetModifierKeyFromCode(int keyCode)
        {
            switch (keyCode)
            {
                case 0xA0:
                case 0xA1:
                case 0x10:
                    return ModifierKeys.Shift;

                case 0xA2:
                case 0xA3:
                case 0x11:
                    return ModifierKeys.Control;

                case 0x12:
                case 0xA4:
                case 0xA5:
                    return ModifierKeys.Alt;

                default:
                    return null;
            }
        }
    }
}
