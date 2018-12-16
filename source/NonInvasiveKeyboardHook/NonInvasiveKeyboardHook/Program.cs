using System;
using NonInvasiveKeyboardHookLib;

namespace NonInvasiveKeyboardHook
{
    public class Program
    {
        private static void Main(string[] args)
        {
            /*var keyboardHookManager = new KeyboardHookManager();
            keyboardHookManager.Start();

            keyboardHookManager.RegisterHotkey(0x60, () => Console.WriteLine("NUMPAD0"));
            keyboardHookManager.RegisterHotkey(0x61, () => Console.WriteLine("NUMPAD1"));

            Console.ReadLine();

            keyboardHookManager.Stop();*/
            var h = new GlobalKeyboardHook();
            h.KeyboardPressed += (sender, eventArgs) =>
            {
                Console.WriteLine(eventArgs.KeyboardData.VirtualCode);
            };
            Console.ReadLine();
        }
    }
}
