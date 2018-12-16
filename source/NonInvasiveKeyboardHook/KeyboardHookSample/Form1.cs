using System.Diagnostics;
using System.Windows.Forms;
using NonInvasiveKeyboardHookLibrary;

namespace KeyboardHookSample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void HookNumpad0()
        {
            var keyboardHookManager = new KeyboardHookManager();
            keyboardHookManager.Start();
            keyboardHookManager.RegisterHotkey(0x60, () =>
            {
                Debug.WriteLine("NumPad0 detected. Unregistering keyboard hook");
                keyboardHookManager.Stop();
            });
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            HookNumpad0();
        }
        
        private void HookCtrlNumPad0()
        {
            var keyboardHookManager = new KeyboardHookManager();
            keyboardHookManager.Start();

            keyboardHookManager.RegisterHotkey(0x60, () =>
            {
                if (keyboardHookManager.IsModifierDown(NonInvasiveKeyboardHookLibrary.ModifierKeys.Control))
                {
                    Debug.WriteLine("Ctrl + NumPad0 detected. Unregistering keyboard hook");
                    keyboardHookManager.Stop();
                }
            });
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            HookCtrlNumPad0();
        }
    }
}
