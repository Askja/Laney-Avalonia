using Avalonia.Controls;
using Avalonia.LogicalTree;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using ELOR.Laney.ViewModels.SettingsCategories;
using ELOR.Laney.Views.SettingsCategories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VKUI.Controls;

namespace ELOR.Laney.ViewModels {
    public class SettingsViewModel : ViewModelBase {
        private ObservableCollection<SettingsCategory> _categories;
        private SettingsCategory _selectedCategory;
        private readonly List<SettingsSearchResult> _searchIndex = new List<SettingsSearchResult>();
        private string _searchQuery;
        private bool _isSearchActive;
        private bool _hasSearchResults;
        private bool _showNoSearchResults;

        public ObservableCollection<SettingsCategory> Categories { get { return _categories; } private set { _categories = value; OnPropertyChanged(); } }
        public SettingsCategory SelectedCategory { get { return _selectedCategory; } set { _selectedCategory = value; OnPropertyChanged(); } }
        public ObservableCollection<SettingsSearchResult> SearchResults { get; } = new ObservableCollection<SettingsSearchResult>();
        public string SearchQuery { get { return _searchQuery; } set { _searchQuery = value; OnPropertyChanged(); UpdateSearchResults(); } }
        public bool IsSearchActive { get { return _isSearchActive; } private set { _isSearchActive = value; OnPropertyChanged(); } }
        public bool HasSearchResults { get { return _hasSearchResults; } private set { _hasSearchResults = value; OnPropertyChanged(); } }
        public bool ShowNoSearchResults { get { return _showNoSearchResults; } private set { _showNoSearchResults = value; OnPropertyChanged(); } }

        public SettingsViewModel() {
            Categories = new ObservableCollection<SettingsCategory> {
                new SettingsCategory(VKIconNames.Icon28SettingsOutline, Localizer.Get("settings_general"), new General(), new GeneralViewModel()),
                new SettingsCategory(VKIconNames.Icon28PaletteOutline, Localizer.Get("settings_appearance"), new Appearance(), new AppearanceViewModel()),
                new SettingsCategory(VKIconNames.Icon28MessageOutline, Localizer.Get("settings_chats"), new Chats(), new ChatsViewModel()),
                new SettingsCategory(VKIconNames.Icon28WriteSquareOutline, Localizer.Get("settings_messages"), new Messages(), new ChatsViewModel()),
                new SettingsCategory(VKIconNames.Icon28Notifications, Localizer.Get("settings_notifications"), new NotificationsPage(), new NotificationsViewModel()),
                new SettingsCategory(VKIconNames.Icon28PrivacyOutline, Localizer.Get("settings_privacy"), new Privacy(), new PrivacyViewModel()),
                new SettingsCategory(VKIconNames.Icon28SmileOutline, Localizer.Get("settings_stickers"), new Stickers(), new StickersViewModel()),
                new SettingsCategory(VKIconNames.Icon28DocumentOutline, Localizer.Get("settings_attachments"), new AttachmentVault(), new AttachmentsViewModel()),
                new SettingsCategory(VKIconNames.Icon28MusicOutline, Localizer.Get("settings_audio"), new Audio(), new AudioViewModel()),
                new SettingsCategory(VKIconNames.Icon28UserOutgoingOutline, Localizer.Get("settings_automation"), new Automation(), new AutomationViewModel()),
                new SettingsCategory(VKIconNames.Icon28PictureOutline, Localizer.Get("settings_performance"), new Memory(), new MemoryViewModel()),
                new SettingsCategory(VKIconNames.Icon28LinkCircleOutline, Localizer.Get("settings_network"), new Network(), new NetworkViewModel()),
                new SettingsCategory(VKIconNames.Icon28PrivacyOutline, Localizer.Get("settings_security"), new E2E(), new E2ESettingsViewModel()),
                new SettingsCategory(VKIconNames.Icon28BugOutline, Localizer.Get("settings_experiments"), new Experiments(), new ExperimentsViewModel()),
#if RELEASE
#else
                new SettingsCategory(VKIconNames.Icon28BugOutline, Localizer.Get("settings_debug"), new DebugPage(), null)
#endif
            };

#if RELEASE
            if (ELOR.Laney.Core.Settings.Get("god", false)) Categories.Add(new SettingsCategory(VKIconNames.Icon28BugOutline, Localizer.Get("settings_debug"), new DebugPage(), null));
#endif

            ApplyCategoryDataContexts();
            SelectedCategory = Categories.FirstOrDefault();
            BuildSearchIndex();
            UpdateSearchResults();
        }

        public void SetAccountId(long accountId) {
            foreach (AppearanceViewModel viewModel in Categories.Select(c => c.ViewModel).OfType<AppearanceViewModel>()) {
                viewModel.SetAccountId(accountId);
            }
        }

        public SettingsAuditReport RunAudit() {
            SettingsAuditReport report = new SettingsAuditReport(Categories.Count);
            ApplyCategoryDataContexts();

            foreach (SettingsCategory category in Categories) {
                try {
                    AuditCategory(category, report);
                } catch (Exception ex) {
                    report.AddIssue(category?.Title ?? "unknown", String.Empty, $"category_exception:{ex.GetType().Name}:{ex.Message}", null);
                }
            }

            return report;
        }

        private void AuditCategory(SettingsCategory category, SettingsAuditReport report) {
            if (category?.View == null) return;

            report.CategoryViewsChecked++;
            foreach (Control control in EnumerateLogicalControls(category.View)) {
                if (control is not Cell cell) continue;

                report.CellsChecked++;
                string header = NormalizeText(cell.Header);
                if (String.IsNullOrWhiteSpace(header)) {
                    report.AddIssue(category.Title, header, "empty_header", null);
                    continue;
                }

                bool canShowSemanticIcon = cell.AutoBeforeIcon && Cell.ShouldShowSemanticIconFor(cell.Before);
                bool hasExplicitBefore = cell.Before != null && !Cell.ShouldShowSemanticIconFor(cell.Before);
                if (!canShowSemanticIcon && !hasExplicitBefore) {
                    report.AddIssue(category.Title, header, "missing_icon", null);
                    continue;
                }

                if (canShowSemanticIcon) {
                    string iconId = Cell.GetSemanticIconIdForHeader(header);
                    if (iconId == Cell.DefaultSemanticIconId) report.AddIssue(category.Title, header, "generic_icon", iconId);
                }
            }
        }

        private void ApplyCategoryDataContexts() {
            foreach (SettingsCategory category in Categories) {
                if (category?.View == null || category.ViewModel == null) continue;
                category.View.DataContext = category.ViewModel;
            }
        }

        private void BuildSearchIndex() {
            _searchIndex.Clear();

            foreach (SettingsCategory category in Categories) {
                if (category == null) continue;

                List<string> categoryTexts = new List<string> { category.Title };
                AddCategoryCells(category, categoryTexts);
                AddCategoryTextBlocks(category, categoryTexts);

                _searchIndex.Add(new SettingsSearchResult(
                    category,
                    category.Title,
                    Localizer.Get("settings_open_section"),
                    JoinSearchText(categoryTexts)));
            }
        }

        private void AddCategoryCells(SettingsCategory category, List<string> categoryTexts) {
            foreach (Control control in EnumerateLogicalControls(category.View)) {
                if (control is Cell cell) {
                    string title = NormalizeText(cell.Header);
                    string subtitle = NormalizeText(cell.Subtitle);
                    if (String.IsNullOrWhiteSpace(title)) continue;

                    categoryTexts.Add(title);
                    categoryTexts.Add(subtitle);
                    _searchIndex.Add(new SettingsSearchResult(
                        category,
                        title,
                        String.IsNullOrWhiteSpace(subtitle) ? category.Title : subtitle,
                        JoinSearchText(category.Title, title, subtitle)));
                }
            }
        }

        private void AddCategoryTextBlocks(SettingsCategory category, List<string> categoryTexts) {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SettingsSearchResult result in _searchIndex.Where(r => r.Category == category)) {
                seen.Add(result.Title);
            }

            foreach (Control control in EnumerateLogicalControls(category.View)) {
                if (control is TextBlock textBlock) {
                    string text = NormalizeText(textBlock.Text);
                    if (String.IsNullOrWhiteSpace(text) || text.Length > 96 || !seen.Add(text)) continue;

                    categoryTexts.Add(text);
                    _searchIndex.Add(new SettingsSearchResult(
                        category,
                        text,
                        Localizer.GetFormatted("settings_search_result_section", category.Title),
                        JoinSearchText(category.Title, text)));
                }
            }
        }

        private void UpdateSearchResults() {
            SearchResults.Clear();

            string[] terms = NormalizeForSearch(SearchQuery)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            IsSearchActive = terms.Length > 0;
            if (!IsSearchActive) {
                HasSearchResults = false;
                ShowNoSearchResults = false;
                return;
            }

            foreach (SettingsSearchResult result in _searchIndex
                .Where(r => IsMatch(r.SearchText, terms))
                .Take(32)) {
                SearchResults.Add(result);
            }

            HasSearchResults = SearchResults.Count > 0;
            ShowNoSearchResults = !HasSearchResults;
        }

        private static bool IsMatch(string text, string[] terms) {
            string normalized = NormalizeForSearch(text);
            return terms.All(t => normalized.Contains(t));
        }

        private static string JoinSearchText(params string[] parts) {
            return JoinSearchText((IEnumerable<string>)parts);
        }

        private static string JoinSearchText(IEnumerable<string> parts) {
            return String.Join(" ", parts.Where(p => !String.IsNullOrWhiteSpace(p)));
        }

        private static string NormalizeText(string text) {
            return text?.Trim() ?? String.Empty;
        }

        private static string NormalizeForSearch(string text) {
            return NormalizeText(text).Replace('ё', 'е').Replace('Ё', 'Е').ToLowerInvariant();
        }

        private static IEnumerable<Control> EnumerateLogicalControls(Control root) {
            if (root == null) yield break;

            foreach (ILogical child in root.GetLogicalChildren()) {
                if (child is Control control) {
                    yield return control;

                    foreach (Control nested in EnumerateLogicalControls(control)) {
                        yield return nested;
                    }
                }
            }
        }
    }

    public sealed class SettingsAuditReport {
        private readonly List<SettingsAuditIssue> _issues = new List<SettingsAuditIssue>();

        public int CategoriesTotal { get; }
        public int CategoryViewsChecked { get; set; }
        public int CellsChecked { get; set; }
        public int EmptyHeaderCount { get; private set; }
        public int MissingIconCount { get; private set; }
        public int GenericIconCount { get; private set; }
        public int CategoryExceptionCount { get; private set; }
        public IReadOnlyList<SettingsAuditIssue> Issues => _issues;
        public bool Passed => EmptyHeaderCount == 0 && MissingIconCount == 0 && GenericIconCount == 0 && CategoryExceptionCount == 0;

        public SettingsAuditReport(int categoriesTotal) {
            CategoriesTotal = categoriesTotal;
        }

        public void AddIssue(string category, string header, string reason, string iconId) {
            switch (reason.Split(':')[0]) {
                case "empty_header":
                    EmptyHeaderCount++;
                    break;
                case "missing_icon":
                    MissingIconCount++;
                    break;
                case "generic_icon":
                    GenericIconCount++;
                    break;
                case "category_exception":
                    CategoryExceptionCount++;
                    break;
            }

            _issues.Add(new SettingsAuditIssue(category, header, reason, iconId));
        }
    }

    public sealed class SettingsAuditIssue {
        public string Category { get; }
        public string Header { get; }
        public string Reason { get; }
        public string IconId { get; }

        public SettingsAuditIssue(string category, string header, string reason, string iconId) {
            Category = category;
            Header = header;
            Reason = reason;
            IconId = iconId;
        }
    }
}
