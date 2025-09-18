using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NitroShell
{
    public sealed partial class MainWindow : Window
    {
        private string currentDirectory = $@"C:\Users\{Environment.UserName}";

        private bool completionSessionActive = false;
        private List<string> completionCandidates = new();
        private int completionIndex = 0;
        private string sessionBaseDir = "";
        private string sessionPrefixDirWithSep = "";

        private readonly List<string> commandHistory = new();
        private int historyIndex = -1;

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
███╗   ██╗██╗████████╗██████╗  ██████╗ ███████╗██╗  ██╗███████╗██╗     ██╗     
████╗  ██║██║╚══██╔══╝██╔══██╗██╔═══██╗██╔════╝██║  ██║██╔════╝██║     ██║     
██╔██╗ ██║██║   ██║   ██████╔╝██║   ██║███████╗███████║█████╗  ██║     ██║     
██║╚██╗██║██║   ██║   ██╔══██╗██║   ██║╚════██║██╔══██║██╔══╝  ██║     ██║     
██║ ╚████║██║   ██║   ██║  ██║╚██████╔╝███████║██║  ██║███████╗███████╗███████╗
╚═╝  ╚═══╝╚═╝   ╚═╝   ╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═╝╚══════╝╚══════╝╚══════╝
                                                             NitroShell v1.0.0
Microsoft Windows [Version {version}]
(c) NitroBrain Corporation. All rights reserved.

{currentDirectory}> ";

            OutputBox.Text = header + "\n";
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                string command = InputBox.Text.Trim();
                if (!string.IsNullOrEmpty(command))
                {
                    commandHistory.Add(command);
                    historyIndex = commandHistory.Count;
                }

                InputBox.Text = "";
                RunCommand(command);
                ResetCompletionSession();
            }
            else if (e.Key == Windows.System.VirtualKey.Tab)
            {
                HandleTabCompletion();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Up)
            {
                if (commandHistory.Count > 0 && historyIndex > 0)
                {
                    historyIndex--;
                    InputBox.Text = commandHistory[historyIndex];
                    InputBox.SelectionStart = InputBox.Text.Length;
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down)
            {
                if (commandHistory.Count > 0 && historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    InputBox.Text = commandHistory[historyIndex];
                }
                else
                {
                    historyIndex = commandHistory.Count;
                    InputBox.Text = "";
                }
                InputBox.SelectionStart = InputBox.Text.Length;
                e.Handled = true;
            }
            else
            {
                ResetCompletionSession();
            }
        }

        private void ResetCompletionSession()
        {
            completionSessionActive = false;
            completionCandidates.Clear();
            completionIndex = 0;
            sessionBaseDir = "";
            sessionPrefixDirWithSep = "";
        }

        private void HandleTabCompletion()
        {
            string text = InputBox.Text ?? "";

            if (!text.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                return;

            if (!completionSessionActive)
            {
                string partialRaw = text.Length > 2 ? text.Substring(2).Trim() : "";

                string baseDir = currentDirectory;
                string searchPrefix = partialRaw;

                if (!string.IsNullOrEmpty(partialRaw))
                {
                    if (Path.IsPathRooted(partialRaw))
                    {
                        if (partialRaw.EndsWith(Path.DirectorySeparatorChar) || partialRaw.EndsWith(Path.AltDirectorySeparatorChar))
                        {
                            baseDir = partialRaw;
                            searchPrefix = "";
                        }
                        else
                        {
                            baseDir = Path.GetDirectoryName(partialRaw) ?? currentDirectory;
                            searchPrefix = Path.GetFileName(partialRaw);
                        }
                    }
                    else if (partialRaw.Contains(Path.DirectorySeparatorChar) || partialRaw.Contains(Path.AltDirectorySeparatorChar))
                    {
                        var dirPart = Path.GetDirectoryName(partialRaw) ?? "";
                        baseDir = Path.GetFullPath(Path.Combine(currentDirectory, dirPart));
                        searchPrefix = Path.GetFileName(partialRaw);
                    }
                    else
                    {
                        baseDir = currentDirectory;
                        searchPrefix = partialRaw;
                    }
                }

                if (!Directory.Exists(baseDir)) return;

                string prefixDirWithSep = "";
                if (!string.IsNullOrEmpty(partialRaw))
                {
                    int idx = partialRaw.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                    if (idx >= 0)
                    {
                        prefixDirWithSep = partialRaw.Substring(0, idx + 1);
                    }
                    else if (Path.IsPathRooted(partialRaw))
                    {
                        var parent = Path.GetDirectoryName(partialRaw);
                        if (!string.IsNullOrEmpty(parent))
                            prefixDirWithSep = parent + Path.DirectorySeparatorChar;
                    }
                }

                completionCandidates = Directory.GetDirectories(baseDir)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name) &&
                                   name.StartsWith(searchPrefix ?? "", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (completionCandidates.Count == 0)
                    return;

                completionSessionActive = true;
                sessionBaseDir = baseDir;
                sessionPrefixDirWithSep = prefixDirWithSep;
                completionIndex = 0;
            }

            if (completionCandidates.Count == 0)
                return;

            string selected = completionCandidates[completionIndex];
            completionIndex = (completionIndex + 1) % completionCandidates.Count;

            string completedPath = string.IsNullOrEmpty(sessionPrefixDirWithSep)
                ? selected
                : sessionPrefixDirWithSep + selected;

            InputBox.Text = "cd " + completedPath;
            InputBox.SelectionStart = InputBox.Text.Length;
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
