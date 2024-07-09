using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;

namespace MouseKeyboardRecorder
{
    public partial class Form1 : Form
    {
        private readonly InputSimulator _inputSimulator = new InputSimulator();
        private readonly List<InputEvent> _events = new List<InputEvent>();
        private bool _isRecording = false;
        private int _eventIndex = 0;
        private DateTime _lastEventTime;

        private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelHookProc _keyboardProc;
        private LowLevelHookProc _mouseProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        private Timer _playbackTimer;

        public Form1()
        {
            InitializeComponent();
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = new LowLevelHookProc(MouseHookCallback);

            _playbackTimer = new Timer();
            _playbackTimer.Interval = 10;
            _playbackTimer.Tick += PlaybackTimer_Tick;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _keyboardHookId = SetHook(_keyboardProc, WH_KEYBOARD_LL);
            _mouseHookId = SetHook(_mouseProc, WH_MOUSE_LL);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            UnhookWindowsHookEx(_mouseHookId);
            base.OnFormClosed(e);
        }

        private void RecordButton_Click(object sender, EventArgs e)
        {
            if (!_isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }

        private void StartRecording()
        {
            _isRecording = true;
            _events.Clear();
            _lastEventTime = DateTime.Now;
            MessageBox.Show("Kayıt başladı.");
            this.WindowState = FormWindowState.Minimized;
            RecordButton.Text = "Durdur";
        }

        private void StopRecording()
        {
            _isRecording = false;
            MessageBox.Show("Kayıt durdu.");

            RecordButton.Text = "Kaydet";
        }

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (_events.Count == 0)
            {
                MessageBox.Show("Kaydedilmiş olay yok.");
                return;
            }
            _eventIndex = 0;
            _playbackTimer.Start();
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_eventIndex >= _events.Count)
            {
                _playbackTimer.Stop();
                MessageBox.Show("Oynatma tamamlandı.");
                return;
            }

            var inputEvent = _events[_eventIndex];
            TimeSpan timeSinceLastEvent = inputEvent.Timestamp - _lastEventTime;
            _lastEventTime = inputEvent.Timestamp;

            if (inputEvent.IsKeyboard)
            {
                if (inputEvent.IsKeyDown)
                {
                    _inputSimulator.Keyboard.KeyDown((VirtualKeyCode)inputEvent.KeyCode);
                }
                else
                {
                    _inputSimulator.Keyboard.KeyUp((VirtualKeyCode)inputEvent.KeyCode);
                }
            }
            else
            {
                if (inputEvent.IsLeftButtonClick)
                {
                    var virtualX = (int)(65535.0 * inputEvent.MouseX / Screen.PrimaryScreen.Bounds.Width);
                    var virtualY = (int)(65535.0 * inputEvent.MouseY / Screen.PrimaryScreen.Bounds.Height);

                    _inputSimulator.Mouse.MoveMouseToPositionOnVirtualDesktop(virtualX, virtualY);
                    _inputSimulator.Mouse.LeftButtonClick();
                }
            }
            _eventIndex++;
            _playbackTimer.Interval = 650;
        }


        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYDOWN || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (_isRecording)
                {
                    bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                    _events.Add(new InputEvent { IsKeyboard = true, KeyCode = vkCode, IsKeyDown = isKeyDown, Timestamp = DateTime.Now });
                }
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                if (_isRecording)
                {
                    _events.Add(new InputEvent { IsKeyboard = false, MouseX = hookStruct.pt.x, MouseY = hookStruct.pt.y, IsLeftButtonClick = true, Timestamp = DateTime.Now });
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private static IntPtr SetHook(LowLevelHookProc proc, int hookId)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookId, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_LBUTTONDOWN = 0x0201;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private struct InputEvent
        {
            public bool IsKeyboard;
            public int KeyCode;
            public int MouseX;
            public int MouseY;
            public bool IsLeftButtonClick;
            public DateTime Timestamp;
            public bool IsKeyDown;
        }

        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private struct POINT
        {
            public int x;
            public int y;
        }
    }
}