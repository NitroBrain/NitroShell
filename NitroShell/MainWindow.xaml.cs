using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Diagnostics;
using System.IO;

namespace NitroShell
{
    public sealed partial class MainWindow : Window
    {
        private string currentDirectory = $@"C:\Users\{Environment.UserName}";

        public MainWindow()
        {
            this.InitializeComponent();
            ConfigureWindow();
            PrintShellHeader();
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

        private void PrintShellHeader()
        {
            string version = Environment.OSVersion.VersionString;
            string header =
            $@"
                _   ________________  ____  _____ __  __________    __ 
               / | / /  _/_  __/ __ \/ __ \/ ___// / / / ____/ /   / / 
              /  |/ // /  / / / /_/ / / / /\__ \/ /_/ / __/ / /   / /  
             / /|  // /  / / / _, _/ /_/ /___/ / __  / /___/ /___/ /___
            /_/ |_/___/ /_/ /_/ |_|\____//____/_/ /_/_____/_____/_____/
                                                                       
            Microsoft Windows [Version {version}]
            (c) NitroBrain Corporation. All rights reserved.

            {currentDirectory}>";

            OutputBox.Text = header + "\n";
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
                if (command.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        string newPath = Path.GetFullPath(parts[1], currentDirectory);
                        if (Directory.Exists(newPath))
                        {
                            currentDirectory = newPath;
                        }
                        else
                        {
                            OutputBox.Text += $"The system cannot find the path specified.\n";
                        }
                    }

                    OutputBox.Text += $"\n{currentDirectory}>";
                    return;
                }

                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = currentDirectory
                };

                var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();

                OutputBox.Text += $"\n{currentDirectory}>{command}\n{output}{error}";

                OutputBox.UpdateLayout();
                ((ScrollViewer)OutputBox.Parent).ChangeView(null, double.MaxValue, null);
            }
            catch (Exception ex)
            {
                OutputBox.Text += $"Error: {ex.Message}\n";
            }
        }
    }
}
