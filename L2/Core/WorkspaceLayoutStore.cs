using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public sealed class WorkspaceLayout {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ChatFilterId { get; set; }
        public string ChatFilterTitle { get; set; }
        public string ChatListLayout { get; set; }
        public string ChatListDensity { get; set; }
        public string ChatListAvatarSize { get; set; }
        public string ChatListAvatarShape { get; set; }
        public string ChatListFontSize { get; set; }
        public long CurrentPeerId { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    public static class WorkspaceLayoutStore {
        private const int MaxLayouts = 16;
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("workspaces");
        private static string LayoutsPath => Path.Combine(DirectoryPath, "layouts.json");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true
        };

        public static IReadOnlyList<WorkspaceLayout> GetAll() {
            try {
                if (!File.Exists(LayoutsPath)) return Array.Empty<WorkspaceLayout>();

                List<WorkspaceLayout> layouts = JsonSerializer.Deserialize<List<WorkspaceLayout>>(File.ReadAllText(LayoutsPath, Encoding.UTF8)) ?? new List<WorkspaceLayout>();
                return layouts
                    .Where(l => !String.IsNullOrWhiteSpace(l?.Id) && !String.IsNullOrWhiteSpace(l.Title))
                    .OrderByDescending(l => l.CreatedAtUnix)
                    .Take(MaxLayouts)
                    .ToList();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read workspace layouts.");
                return Array.Empty<WorkspaceLayout>();
            }
        }

        public static WorkspaceLayout SaveCurrent(VKSession session, string title) {
            if (session == null) throw new ArgumentNullException(nameof(session));

            WorkspaceLayout layout = new WorkspaceLayout {
                Id = Guid.NewGuid().ToString("N"),
                Title = NormalizeTitle(title),
                ChatFilterId = session.ImViewModel?.CurrentChatFilterId,
                ChatFilterTitle = session.ImViewModel?.CurrentChatFilterTitle,
                ChatListLayout = Settings.ChatListLayout,
                ChatListDensity = Settings.ChatListDensity,
                ChatListAvatarSize = Settings.ChatListAvatarSize,
                ChatListAvatarShape = Settings.ChatListAvatarShape,
                ChatListFontSize = Settings.ChatListFontSize,
                CurrentPeerId = session.CurrentOpenedChat?.PeerId ?? 0,
                CreatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds()
            };

            List<WorkspaceLayout> layouts = GetAll().Where(l => !String.Equals(l.Title, layout.Title, StringComparison.OrdinalIgnoreCase)).ToList();
            layouts.Insert(0, layout);
            Save(layouts.Take(MaxLayouts));
            return layout;
        }

        public static void Apply(VKSession session, WorkspaceLayout layout) {
            if (session == null || layout == null) return;

            if (!String.IsNullOrWhiteSpace(layout.ChatListLayout)) Settings.ChatListLayout = layout.ChatListLayout;
            if (!String.IsNullOrWhiteSpace(layout.ChatListDensity)) Settings.ChatListDensity = layout.ChatListDensity;
            if (!String.IsNullOrWhiteSpace(layout.ChatListAvatarSize)) Settings.ChatListAvatarSize = layout.ChatListAvatarSize;
            if (!String.IsNullOrWhiteSpace(layout.ChatListAvatarShape)) Settings.ChatListAvatarShape = layout.ChatListAvatarShape;
            if (!String.IsNullOrWhiteSpace(layout.ChatListFontSize)) Settings.ChatListFontSize = layout.ChatListFontSize;

            if (!String.IsNullOrWhiteSpace(layout.ChatFilterId)) {
                session.ImViewModel?.TrySetChatFilter(layout.ChatFilterId);
            }

            if (layout.CurrentPeerId != 0) {
                session.GoToChat(layout.CurrentPeerId);
            }
        }

        private static void Save(IEnumerable<WorkspaceLayout> layouts) {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(LayoutsPath, JsonSerializer.Serialize(layouts.ToList(), JsonOptions), Encoding.UTF8);
        }

        private static string NormalizeTitle(string title) {
            string normalized = title?.Trim();
            if (String.IsNullOrWhiteSpace(normalized)) normalized = $"Workspace {DateTime.Now:dd.MM HH:mm}";
            return normalized.Length <= 80 ? normalized : normalized[..80];
        }
    }
}
