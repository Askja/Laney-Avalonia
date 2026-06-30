using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Styling;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Extensions;
using ELOR.VKAPILib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ELOR.Laney {
    public sealed class App : Application {
        private static App _current;
        public static new App Current => _current;
        public ClassicDesktopStyleApplicationLifetime DesktopLifetime { get; private set; }
        public double DPI { get; private set; } = 2;
        public static bool StartMinimized => HasCmdLineValue("minimized");

        public override void Initialize() {
            _current = this;

            AvaloniaXamlLoader.Load(this);
#if DEBUG
            this.AttachDeveloperTools();
#endif

            ActualThemeVariantChanged += App_ActualThemeVariantChanged;
            ChangeTheme(Settings.AppTheme);
            AppearanceManager.ApplyAppearanceSettings();
            Settings.SettingChanged += Settings_SettingChanged;
        }

        public static bool HasCmdLineValue(string key) {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args) {
                if (arg.StartsWith($"-{key}")) return true;
            }
            return false;
        }

        public static string GetCmdLineValue(string key) {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string arg in args) {
                if (arg.StartsWith($"-{key}")) {
                    string[] p = arg.Split("=");
                    if (p.Length == 2) return p[1];
                }
            }
            return null;
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is ClassicDesktopStyleApplicationLifetime desktop) {
                DesktopLifetime = desktop;
                if (Platform == OSPlatform.FreeBSD) desktop.Shutdown();
                Prepare();
                if (DesktopLifetime.MainWindow != null) return; // признак того, что открыто окно expired info.

                if (Program.Mode == LaunchMode.Default) {
                    // Demo mode
                    if (DemoMode.Check()) {
                        var usessions = DemoMode.Data.Sessions.Where(s => s.Id.IsUser());
                        int ucount = usessions.Count();
                        if (ucount == 0) throw new Exception("No user session found!");
                        if (ucount > 1) throw new Exception("There can be only 1 user session!");
                        VKSession.StartDemoSession(usessions.FirstOrDefault());
                        desktop.MainWindow = VKSession.Main.Window;
                    } else {
                        try {
                            long uid = Settings.Get<long>(Settings.VK_USER_ID);
                            string dt = Settings.GetVkAccessToken();
                            if (uid > 0 && !String.IsNullOrEmpty(dt)) {
                                Log.Information($"Authorized user: {uid}");
                                VKSession.StartUserSession(uid, dt);
                                desktop.MainWindow = VKSession.Main.Window;
                            } else {
                                Log.Information($"Not authorized. Opening sign in window...");
                                desktop.MainWindow = new Views.SignInWindow();
                            }
                        } catch (Exception ex) {
                            Log.Error(ex, $"Cannot check authorization. Opening sign in window...");
                            desktop.MainWindow = new Views.SignInWindow();
                        }
                    }
                    PlatformSettings.ColorValuesChanged += (a, b) => UpdateTrayIcon();
                } else if (Program.Mode == LaunchMode.FieldTest) {
                    desktop.MainWindow = new FieldTestWindow();
                } else if (Program.Mode == LaunchMode.APIConsole) {
                    desktop.MainWindow = new APIConsoleWindow();
                }

                double rs = desktop.MainWindow.RenderScaling;
                double ds = desktop.MainWindow.DesktopScaling;
                DPI = Math.Max(rs, ds);
                Log.Information($"Desktop scaling: {ds}; Render scaling: {rs}. Using maximal scaling ({DPI})");
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void Prepare() {
            ApplyPerfOverrides();
            VKAPI.ConfigureDefaults(Settings.ApiDomain, Settings.ApiVersion);
            VKAPI.ConfigureProxy(Settings.ProxyEnabled, Settings.ProxyUri, Settings.ProxyBypassLocal);
            AutostartService.ApplyConfiguredState();
            MessageEmojiInlineRenderer.Prewarm();
            Debug.WriteLine("Getting and loading language...");
            string lang = Settings.Get(Settings.LANGUAGE, Constants.DefaultLang);
            Localizer.LoadLanguage(lang);
            Debug.WriteLine("Language loaded!");
            LMediaPlayer.InitStaticInstances();

#if !RELEASE && !DEBUG
            if (IsExpired) {
                DesktopLifetime.MainWindow = new Views.Modals.VKUIDialog(Assets.i18n.Resources.error, "This version is expired!");
                DesktopLifetime.MainWindow.Closed += (a, b) => Process.GetCurrentProcess().Kill();
                DesktopLifetime.MainWindow.Show();
            }
#endif
        }

        private static void ApplyPerfOverrides() {
            string animationMode = GetCmdLineValue("perf-sticker-animation");
            if (!String.IsNullOrWhiteSpace(animationMode) && Enum.TryParse(animationMode, true, out StickerAnimationMode parsedMode)) {
                Settings.StickerAnimation = parsedMode;
                Log.Information("Perf override: sticker animation mode = {Mode}", parsedMode);
            }

            string imageCacheMb = GetCmdLineValue("perf-image-cache-mb");
            if (Int32.TryParse(imageCacheMb, out int parsedCacheMb) && parsedCacheMb > 0) {
                Settings.ImageCacheRamLimitMb = parsedCacheMb;
                Log.Information("Perf override: image cache RAM limit = {LimitMb} Mb", Settings.ImageCacheRamLimitMb);
            }
        }

        private static void UpdateTrayIcon() {
            TrayIcons icons = Application.Current.GetValue(TrayIcon.IconsProperty);
            if (icons != null && icons.Count > 0) {
                TrayIcon icon = icons[0];
                WindowIcon trayIcon = AssetsManager.GetTrayWindowIcon();
                if (trayIcon != null) icon.Icon = trayIcon;
            }
        }

        #region Theme

        public List<Action<ThemeVariant>> ThemeChangedActions = new List<Action<ThemeVariant>>();

        private void App_ActualThemeVariantChanged(object sender, EventArgs e) {
            AppearanceManager.ApplyAppearanceSettings();
            foreach (var action in CollectionsMarshal.AsSpan(_current.ThemeChangedActions)) {
                action?.Invoke(_current.ActualThemeVariant);
            }
        }

        public static void ChangeTheme(int id) {
            switch (id) {
                case 1:
                    _current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case 2:
                    _current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                default:
                    _current.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }
        }

        public static T GetResource<T>(string key) {
            object resource = null;
            if (App.Current.TryFindResource(key, out resource) && resource is T) {
                return (T)resource;
            } else {
                if (resource == null) {
                    Log.Error("Resource \"{0}\" is not found!", key);
                } else {
                    Log.Error("Resource \"{0}\" is not {1}, but {2}", key, typeof(T), resource.GetType());
                }
                return default(T);
            }
        }

        #endregion

        private void Settings_SettingChanged(string key, object value) {
            switch (key) {
                case Settings.THEME:
                    ChangeTheme((int)value);
                    AppearanceManager.ApplyAppearanceSettings();
                    break;
                case Settings.ACCENT_COLOR:
                case Settings.APP_FONT_FAMILY:
                case Settings.CHAT_BACKGROUND:
                case Settings.CHAT_BACKGROUND_IMAGE:
                case Settings.CHAT_LIST_DENSITY:
                case Settings.CHAT_LIST_AVATAR_SIZE:
                case Settings.CHAT_LIST_AVATAR_SHAPE:
                case Settings.CHAT_LIST_FONT_SIZE:
                case Settings.MESSAGE_AVATAR_SIZE:
                case Settings.MESSAGE_FONT_SIZE:
                case Settings.MESSAGE_BUBBLE_WIDTH:
                case Settings.MESSAGE_BUBBLE_DENSITY:
                case Settings.MESSAGE_BUBBLE_STYLE:
                case Settings.MESSAGE_BUBBLE_OPACITY:
                case Settings.MESSAGE_BUBBLE_AUTO_COLOR:
                    AppearanceManager.ApplyAppearanceSettings();
                    break;
                case Settings.APP_ICON_VARIANT:
                    UpdateTrayIcon();
                    break;
            }
        }

        public DataTemplate GetCommonTemplate(string key) {
            // 1 - это порядковый номер (с нуля) CommonTemplates.axaml в App.axaml > MergedDictionaries
            var dictionary = App.Current.Resources.MergedDictionaries[17] as ResourceDictionary;
            return dictionary[key] as DataTemplate;
        }

        #region Platform, version and other infos

        public static OSPlatform Platform => GetCurrentPlatform();

        private static OSPlatform GetCurrentPlatform() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return OSPlatform.Windows;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return OSPlatform.OSX;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return OSPlatform.Linux;
            return OSPlatform.FreeBSD; // not supported
        }

        private static string _buildInfo;
        public static string BuildInfoFull => _buildInfo ?? GetFullBuildInfo();
        public static string BuildInfo => GetBuildInfo();
        public static string BuildHost => GetBuilderInfo();
        public static string RepoInfo => GetRepoInfo();
        public static DateTime BuildTime => GetBuildTime();

#if RELEASE
#else
        public static DateTime ExpirationDate => BuildTime.Date.AddDays(90);
        public static bool IsExpired => DateTime.Now.Date > ExpirationDate;
#endif

        public static string UserAgent => GetUserAgent();

        private static string GetFullBuildInfo() {
            Assembly assembly = Assembly.GetEntryAssembly();
            if (assembly != null) {
                var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                _buildInfo = attr;
                return attr;
            }
            return string.Empty;
        }

        private static string GetBuildInfo() {
            string[] sections = GetBuildInfoSections();
            string version = GetBuildInfoSection(sections, 0, "0.0.0");
            string platform = GetBuildInfoSection(sections, 1, Platform.ToString().ToLowerInvariant());
            string architecture = GetBuildInfoSection(sections, 2, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
            return $"{version} {platform}-{architecture}";
        }

        private static DateTime GetBuildTime() {
            string[] sections = GetBuildInfoSections();
            string date = GetBuildInfoSection(sections, 4, null);
            string time = GetBuildInfoSection(sections, 5, null);
            string datetime = $"{date}-{time}";

            if (DateTime.TryParseExact(datetime, "yyMMdd-HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result)) {
                return result;
            }

            string assemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if (!String.IsNullOrWhiteSpace(assemblyLocation) && File.Exists(assemblyLocation)) {
                return File.GetLastWriteTime(assemblyLocation);
            }

            return Directory.GetLastWriteTime(AppContext.BaseDirectory);
        }

        private static string GetBuilderInfo() {
            string[] sections = GetBuildInfoSections();
            return DecodeBuildInfoSection(GetBuildInfoSection(sections, 3, null), "unknown");
        }

        private static string GetRepoInfo() {
            string[] sections = GetBuildInfoSections();
            return DecodeBuildInfoSection(GetBuildInfoSection(sections, 6, null), "unknown");
        }

        private static string GetUserAgent() {
            string[] sections = GetBuildInfoSections();
            string version = GetBuildInfoSection(sections, 0, "0.0.0");
            string platform = GetBuildInfoSection(sections, 1, Platform.ToString().ToLowerInvariant());
            string architecture = GetBuildInfoSection(sections, 2, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
            return $"LaneyMessenger (2; {version}; {platform}; {architecture})";
        }

        private static string[] GetBuildInfoSections() {
            return BuildInfoFull.Split('-', StringSplitOptions.None);
        }

        private static string GetBuildInfoSection(string[] sections, int index, string fallback) {
            if (sections.Length > index && !String.IsNullOrWhiteSpace(sections[index])) return sections[index];
            return fallback;
        }

        private static string DecodeBuildInfoSection(string encoded, string fallback) {
            if (String.IsNullOrWhiteSpace(encoded)) return fallback;

            try {
                if (encoded.Contains('+')) encoded = encoded.Split('+')[0];
                encoded = encoded.Replace(".4444", "=");

                var bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot decode build info section.");
                return fallback;
            }
        }

        public static List<string> UsedLibs { get; } = new List<string>(10) {
            "Avalonia.Labs.Panels",
            "Avalonia.Labs.Lottie",
            "Avalonia.Labs.Qr",
            "ColorTextBlock.Avalonia by whistyun",
            "LibVLCSharp",
            "Noto Emoji by Google",
            "PanAndZoom by Wiesław Šoltés",
            "Serilog",
            "Svg.Skia by Wiesław Šoltés",
            "Twemoji graphics by Twitter and contributors",
            "Unicode.net",
            "ZstdSharp.Port"
        };

        #endregion

        #region Paths

        private static string _localDataPath;
        private static bool? _isPortableMode;

        public static string LocalDataPath { get => _localDataPath ??= GetLocalDataPath(); }
        public static bool IsPortableMode { get => _isPortableMode ??= DetectPortableMode(); }

        private static string GetLocalDataPath() {
            string custom = GetCmdLineValue("ldp");
            if (!String.IsNullOrWhiteSpace(custom)) return NormalizeDataPath(custom);
            if (IsPortableMode) return Path.Combine(AppContext.BaseDirectory, "data");

            string appdataroot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appdataroot, "ELOR", "Laney");
        }

        private static bool DetectPortableMode() {
            if (HasCmdLineValue("portable")) return true;

            string markerPath = Path.Combine(AppContext.BaseDirectory, "laney.portable");
            return File.Exists(markerPath);
        }

        private static string NormalizeDataPath(string path) {
            string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (!Path.IsPathRooted(expanded)) expanded = Path.Combine(AppContext.BaseDirectory, expanded);
            return Path.GetFullPath(expanded);
        }

        #endregion

        #region Non-production things

        public static void UpdateBranding(Grid brand) {
#if RELEASE
            byte i = 2;
#elif BETA
            TextBlock t = new TextBlock { 
                    Text = "BETA",
                    Foreground = new SolidColorBrush(Color.Parse("#000000")),
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeight.Bold
                };
                t.Classes.Add("Caption2");

                Border b = new Border {
                    Width = 36,
                    Height = 14,
                    CornerRadius = new Avalonia.CornerRadius(0, 2, 2, 0),
                    Background = new SolidColorBrush(Color.Parse("#D1C097")),
                    Child = t
                };

                Avalonia.Controls.Shapes.Path p = new Avalonia.Controls.Shapes.Path { 
                    Data = Geometry.Parse("M 0,14 L 10,24 L 10,14 z"),
                    Fill = new SolidColorBrush(Color.Parse("#857250"))
                };

                Canvas c = new Canvas { 
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                    Width = 36,
                    Height = 26,
                    Margin = new Avalonia.Thickness(2, 0, 0, 7)
                };

                c.Children.Add(b);
                c.Children.Add(p);
                brand.Children.Add(c);
#else
            TextBlock t = new TextBlock {
                Text = "DEV",
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.Bold
            };
            t.Classes.Add("Caption2");

            Border b = new Border {
                Width = 36,
                Height = 14,
                CornerRadius = new Avalonia.CornerRadius(0, 2, 2, 0),
                Background = new SolidColorBrush(Color.Parse("#FF0000")),
                Child = t
            };

            Avalonia.Controls.Shapes.Path p = new Avalonia.Controls.Shapes.Path {
                Data = Geometry.Parse("M 0,14 L 10,24 L 10,14 z"),
                Fill = new SolidColorBrush(Color.Parse("#9F0000"))
            };

            Canvas c = new Canvas {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
                Width = 36,
                Height = 26,
                Margin = new Avalonia.Thickness(2, 0, 0, 7)
            };

            c.Children.Add(b);
            c.Children.Add(p);
            brand.Children.Add(c);
#endif
        }

        #endregion
    }
}
