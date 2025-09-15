using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Diagnostics;

namespace NitroShell
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ConfigureWindow();
        }

        private void ConfigureWindow()
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            this.ExtendsContentIntoTitleBar = true;
            SetTitleBar(null);

            if (appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = true;
            }
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                string command = InputBox.Text;
                InputBox.Text = "";

                RunCommand(command);
            }
        }

        private void RunCommand(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();

                OutputBox.Text += $">> {command}\n{output}{error}\n";

                OutputBox.UpdateLayout();
                ((ScrollViewer)OutputBox.Parent).ChangeView(null, double.MaxValue, null);
            }
            catch (System.Exception ex)
            {
                OutputBox.Text += $"Error: {ex.Message}\n";
            }
        }
    }
}