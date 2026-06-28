using System;
using System.Threading.Tasks;

namespace ELOR.Laney.ViewModels.Modals {
    public sealed class CommandPaletteAction {
        private readonly Func<Task> execute;

        public string IconId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Keywords { get; }

        public CommandPaletteAction(string iconId, string title, string subtitle, string keywords, Func<Task> execute) {
            IconId = iconId;
            Title = title;
            Subtitle = subtitle;
            Keywords = keywords;
            this.execute = execute;
        }

        public bool Matches(string query) {
            if (String.IsNullOrWhiteSpace(query)) return true;

            string needle = query.Trim();
            return Contains(Title, needle) || Contains(Subtitle, needle) || Contains(Keywords, needle);
        }

        public Task ExecuteAsync() {
            return execute?.Invoke() ?? Task.CompletedTask;
        }

        private static bool Contains(string source, string query) {
            return !String.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
