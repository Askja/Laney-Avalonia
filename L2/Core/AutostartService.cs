using Microsoft.Win32;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;

namespace ELOR.Laney.Core {
    public static class AutostartService {
        private const string AppName = "Laney";
        private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string LinuxDesktopFileName = "laney.desktop";
        private const string MacLaunchAgentId = "ru.askja.laney";

        public static string StatusText {
            get {
                try {
                    return IsEnabledInSystem() ? "Включён" : "Выключен";
                } catch (Exception ex) {
                    Log.Warning(ex, "Cannot read autostart status.");
                    return "Не удалось проверить";
                }
            }
        }

        public static void ApplyConfiguredState() {
            try {
                if (Settings.AutostartEnabled) {
                    Enable();
                } else {
                    Disable();
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot apply autostart state.");
            }
        }

        public static bool IsEnabledInSystem() {
            if (OperatingSystem.IsWindows()) return IsEnabledWindows();
            if (OperatingSystem.IsMacOS()) return File.Exists(GetMacLaunchAgentPath());
            if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD()) return File.Exists(GetLinuxDesktopFilePath());
            return false;
        }

        private static void Enable() {
            if (OperatingSystem.IsWindows()) {
                EnableWindows();
                return;
            }

            if (OperatingSystem.IsMacOS()) {
                EnableMacOS();
                return;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD()) {
                EnableLinux();
            }
        }

        private static void Disable() {
            if (OperatingSystem.IsWindows()) {
                DisableWindows();
                return;
            }

            DeleteFileIfExists(GetLinuxDesktopFilePath());
            DeleteFileIfExists(GetMacLaunchAgentPath());
        }

        [SupportedOSPlatform("windows")]
        private static void DisableWindows() {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(WindowsRunKey, true);
            key?.DeleteValue(AppName, false);
        }

        [SupportedOSPlatform("windows")]
        private static bool IsEnabledWindows() {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(WindowsRunKey, false);
            return key?.GetValue(AppName) is string value && value.Contains(GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
        }

        [SupportedOSPlatform("windows")]
        private static void EnableWindows() {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(WindowsRunKey, true);
            key.SetValue(AppName, BuildShellCommand());
        }

        private static void EnableLinux() {
            string path = GetLinuxDesktopFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildDesktopFile(), new UTF8Encoding(false));
        }

        private static void EnableMacOS() {
            string path = GetMacLaunchAgentPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildLaunchAgentPlist(), new UTF8Encoding(false));
        }

        private static string BuildShellCommand() {
            string args = BuildLaunchArguments();
            return String.IsNullOrWhiteSpace(args)
                ? Quote(GetExecutablePath())
                : $"{Quote(GetExecutablePath())} {args}";
        }

        private static string BuildLaunchArguments() {
            StringBuilder args = new StringBuilder();

            if (Settings.AutostartMinimized) args.Append(" -minimized");
            if (App.IsPortableMode) args.Append(" -portable");

            string commandLineDataPath = App.GetCmdLineValue("ldp");
            if (!String.IsNullOrWhiteSpace(commandLineDataPath)) {
                args.Append(" -ldp=");
                args.Append(Quote(App.LocalDataPath));
            }

            return args.ToString().Trim();
        }

        private static string BuildDesktopFile() {
            return $"""
                [Desktop Entry]
                Type=Application
                Name=Laney
                Comment=Laney VK client
                Exec={BuildShellCommand()}
                Terminal=false
                X-GNOME-Autostart-enabled=true
                """;
        }

        private static string BuildLaunchAgentPlist() {
            string[] args = BuildLaunchArguments()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            StringBuilder arguments = new StringBuilder();
            arguments.AppendLine($"        <string>{SecurityElement.Escape(GetExecutablePath())}</string>");
            foreach (string arg in args) {
                arguments.AppendLine($"        <string>{SecurityElement.Escape(arg)}</string>");
            }

            return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{MacLaunchAgentId}</string>
                    <key>ProgramArguments</key>
                    <array>
                {arguments.ToString().TrimEnd()}
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                </dict>
                </plist>
                """;
        }

        private static string GetExecutablePath() {
            try {
                string processPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!String.IsNullOrWhiteSpace(processPath)) return processPath;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read current process executable path.");
            }

            return Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "laney");
        }

        private static string GetLinuxDesktopFilePath() {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (String.IsNullOrWhiteSpace(configHome)) {
                configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return Path.Combine(configHome, "autostart", LinuxDesktopFileName);
        }

        private static string GetMacLaunchAgentPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "LaunchAgents",
                $"{MacLaunchAgentId}.plist");
        }

        private static void DeleteFileIfExists(string path) {
            if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
        }

        private static string Quote(string value) {
            if (String.IsNullOrWhiteSpace(value)) return "\"\"";
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }
    }
}
