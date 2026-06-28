namespace ELOR.Laney.DataModels {
    public sealed class SettingsSearchResult {
        public SettingsCategory Category { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string SearchText { get; }
        public string IconId => Category.IconId;
        public string CategoryTitle => Category.Title;

        public SettingsSearchResult(SettingsCategory category, string title, string subtitle, string searchText) {
            Category = category;
            Title = title;
            Subtitle = subtitle;
            SearchText = searchText;
        }
    }
}
