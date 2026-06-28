using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class PluginActionIds {
        public const string OpenUrl = "open_url";
        public const string CopyText = "copy_text";
        public const string BuiltInScenario = "built_in_scenario";
        public const string SlashCommand = "slash_command";
    }

    public sealed class PluginManifest {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public bool? Enabled { get; set; }
        public List<PluginPaletteCommand> Commands { get; set; }
    }

    public sealed class PluginPaletteCommand {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Keywords { get; set; }
        public string IconId { get; set; }
        public string ActionId { get; set; }
        public string Value { get; set; }
        public bool RequiresChat { get; set; }
    }

    public sealed class PluginPaletteCommandDescriptor {
        public PluginManifest Plugin { get; set; }
        public PluginPaletteCommand Command { get; set; }
    }

    public static class PluginRegistry {
        private const int MaxPluginFiles = 64;
        private const int MaxCommandsPerPlugin = 64;
        private static readonly string DirectoryPath = Path.Combine(App.LocalDataPath, "plugins");
        private static readonly string SampleManifestPath = Path.Combine(DirectoryPath, "sample.plugin.json");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true
        };

        public static IReadOnlyList<PluginManifest> GetEnabledManifests() {
            EnsureSampleManifestFile();

            List<PluginManifest> manifests = new List<PluginManifest>();
            foreach (string path in Directory.EnumerateFiles(DirectoryPath, "*.json").Take(MaxPluginFiles)) {
                try {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    PluginManifest manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);
                    manifest = NormalizeManifest(manifest, path);
                    if (manifest != null && manifest.Enabled != false) manifests.Add(manifest);
                } catch {
                    // Битый локальный манифест не должен валить весь клиент. Иначе это не plugin-ready, а мина.
                }
            }

            return manifests;
        }

        public static IReadOnlyList<PluginPaletteCommandDescriptor> GetPaletteCommands() {
            List<PluginPaletteCommandDescriptor> commands = new List<PluginPaletteCommandDescriptor>();
            foreach (PluginManifest manifest in GetEnabledManifests()) {
                foreach (PluginPaletteCommand command in manifest.Commands ?? Enumerable.Empty<PluginPaletteCommand>()) {
                    commands.Add(new PluginPaletteCommandDescriptor {
                        Plugin = manifest,
                        Command = command
                    });
                }
            }

            return commands;
        }

        private static PluginManifest NormalizeManifest(PluginManifest manifest, string path) {
            if (manifest == null) return null;

            string id = NormalizeRequired(manifest.Id);
            if (String.IsNullOrWhiteSpace(id)) id = Path.GetFileNameWithoutExtension(path);

            List<PluginPaletteCommand> commands = (manifest.Commands ?? new List<PluginPaletteCommand>())
                .Where(c => c != null && IsAllowedAction(c.ActionId) && !String.IsNullOrWhiteSpace(c.Title))
                .Take(MaxCommandsPerPlugin)
                .Select(NormalizeCommand)
                .Where(c => c != null)
                .ToList();

            return new PluginManifest {
                Id = id,
                Title = String.IsNullOrWhiteSpace(manifest.Title) ? id : manifest.Title.Trim(),
                Version = String.IsNullOrWhiteSpace(manifest.Version) ? "0.0.0" : manifest.Version.Trim(),
                Author = manifest.Author?.Trim() ?? String.Empty,
                Enabled = manifest.Enabled,
                Commands = commands
            };
        }

        private static PluginPaletteCommand NormalizeCommand(PluginPaletteCommand command) {
            string actionId = NormalizeRequired(command.ActionId);
            string value = command.Value?.Trim() ?? String.Empty;
            if ((actionId == PluginActionIds.OpenUrl || actionId == PluginActionIds.BuiltInScenario || actionId == PluginActionIds.SlashCommand)
                && String.IsNullOrWhiteSpace(value)) {
                return null;
            }

            return new PluginPaletteCommand {
                Id = String.IsNullOrWhiteSpace(command.Id) ? NormalizeRequired(command.Title) : command.Id.Trim(),
                Title = command.Title.Trim(),
                Subtitle = command.Subtitle?.Trim() ?? String.Empty,
                Keywords = command.Keywords?.Trim() ?? String.Empty,
                IconId = command.IconId?.Trim() ?? String.Empty,
                ActionId = actionId,
                Value = value,
                RequiresChat = command.RequiresChat
            };
        }

        private static bool IsAllowedAction(string actionId) {
            string normalized = NormalizeRequired(actionId);
            return normalized == PluginActionIds.OpenUrl
                || normalized == PluginActionIds.CopyText
                || normalized == PluginActionIds.BuiltInScenario
                || normalized == PluginActionIds.SlashCommand;
        }

        private static string NormalizeRequired(string value) {
            return value?.Trim().ToLowerInvariant() ?? String.Empty;
        }

        private static void EnsureSampleManifestFile() {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(SampleManifestPath)) return;

            PluginManifest sample = new PluginManifest {
                Id = "sample",
                Title = "Sample plugin",
                Version = "0.1.0",
                Author = "Laney",
                Enabled = false,
                Commands = new List<PluginPaletteCommand> {
                    new PluginPaletteCommand {
                        Id = "open-docs",
                        Title = "Открыть документацию",
                        Subtitle = "Пример safe URL action",
                        Keywords = "docs help",
                        IconId = "Icon28LinkCircleOutline",
                        ActionId = PluginActionIds.OpenUrl,
                        Value = "https://github.com/Elorucov/Laney-Avalonia"
                    },
                    new PluginPaletteCommand {
                        Id = "focus-hour",
                        Title = "Тихий час",
                        Subtitle = "Пример built-in scenario",
                        Keywords = "mute quiet",
                        IconId = "Icon28NotificationDisableOutline",
                        ActionId = PluginActionIds.BuiltInScenario,
                        Value = QuickActionScenarioActionIds.LocalQuiet1h,
                        RequiresChat = true
                    },
                    new PluginPaletteCommand {
                        Id = "download-last-month",
                        Title = "Скачать медиа за 30 дней",
                        Subtitle = "Пример slash template",
                        Keywords = "download media",
                        IconId = "Icon28DocumentOutline",
                        ActionId = PluginActionIds.SlashCommand,
                        Value = "/download media last 30d",
                        RequiresChat = true
                    }
                }
            };

            File.WriteAllText(SampleManifestPath, JsonSerializer.Serialize(sample, JsonOptions), Encoding.UTF8);
        }
    }
}
