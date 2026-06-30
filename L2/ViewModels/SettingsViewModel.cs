using Avalonia.Controls;
using Avalonia.LogicalTree;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using ELOR.Laney.ViewModels.SettingsCategories;
using ELOR.Laney.Views.SettingsCategories;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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

        public SettingsMutationSmokeReport RunMutationSmoke() {
            SettingsMutationSmokeReport report = new SettingsMutationSmokeReport(Categories.Count);
            ApplyCategoryDataContexts();

            foreach (SettingsCategory category in Categories) {
                object viewModel = category?.ViewModel;
                if (viewModel == null) {
                    report.SkippedViewModels++;
                    continue;
                }

                report.ViewModelsChecked++;
                List<PropertyInfo> properties = GetSmokeCandidateProperties(viewModel).ToList();
                foreach (PropertyInfo property in properties) {
                    SmokeProperty(category.Title, viewModel, property, report);
                }

                SmokeBoolPairs(category.Title, viewModel, properties, report);
                ValidateSmokeCollections(category.Title, viewModel, report);
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

        private static IEnumerable<PropertyInfo> GetSmokeCandidateProperties(object viewModel) {
            return viewModel.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
                .Where(p => IsSmokeSupportedType(p.PropertyType));
        }

        private static bool IsSmokeSupportedType(Type type) {
            Type actual = Nullable.GetUnderlyingType(type) ?? type;
            return actual == typeof(bool)
                || actual == typeof(string)
                || actual == typeof(int)
                || actual == typeof(double)
                || actual == typeof(TwoStringTuple)
                || actual == typeof(Tuple<int, string>)
                || actual == typeof(AppearanceOption)
                || actual == typeof(AppFontFamilyOption)
                || actual == typeof(AppIconVariantOption);
        }

        private static void SmokeProperty(string category, object viewModel, PropertyInfo property, SettingsMutationSmokeReport report) {
            report.PropertiesChecked++;

            if (ShouldSkipSmokeProperty(property)) {
                report.AddSkipped(category, property.Name, "skip_side_effect");
                return;
            }

            object original = null;
            bool hasOriginal = false;
            try {
                original = property.GetValue(viewModel);
                hasOriginal = true;
                if (!TryBuildSmokeValue(viewModel, property, original, out object smokeValue, out string skipReason)) {
                    report.AddSkipped(category, property.Name, skipReason);
                    return;
                }

                property.SetValue(viewModel, smokeValue);
                object mutated = property.GetValue(viewModel);
                if (!SmokeValuesEqual(mutated, smokeValue)) {
                    report.AddFailure(category, property.Name, $"mutation_mismatch:{FormatSmokeValue(mutated)}!={FormatSmokeValue(smokeValue)}");
                } else {
                    report.RoundTripsPassed++;
                }

                property.SetValue(viewModel, original);
                object restored = property.GetValue(viewModel);
                if (!SmokeValuesEqual(restored, original)) {
                    report.AddFailure(category, property.Name, $"restore_mismatch:{FormatSmokeValue(restored)}!={FormatSmokeValue(original)}");
                }
            } catch (Exception ex) {
                report.AddFailure(category, property.Name, $"{ex.GetType().Name}:{ex.InnerException?.Message ?? ex.Message}");
                if (hasOriginal) TrySetProperty(viewModel, property, original);
            }
        }

        private static void SmokeBoolPairs(string category, object viewModel, List<PropertyInfo> properties, SettingsMutationSmokeReport report) {
            List<PropertyInfo> bools = properties
                .Where(p => p.PropertyType == typeof(bool) && !ShouldSkipSmokeProperty(p))
                .Take(12)
                .ToList();
            if (bools.Count < 2) return;

            for (int i = 0; i < bools.Count - 1; i++) {
                PropertyInfo first = bools[i];
                PropertyInfo second = bools[i + 1];
                object firstOriginal = null;
                object secondOriginal = null;
                try {
                    firstOriginal = first.GetValue(viewModel);
                    secondOriginal = second.GetValue(viewModel);
                    first.SetValue(viewModel, !(bool)firstOriginal);
                    second.SetValue(viewModel, !(bool)secondOriginal);
                    _ = first.GetValue(viewModel);
                    _ = second.GetValue(viewModel);
                    report.BoolPairsPassed++;
                } catch (Exception ex) {
                    report.AddFailure(category, $"{first.Name}+{second.Name}", $"bool_pair:{ex.GetType().Name}:{ex.InnerException?.Message ?? ex.Message}");
                } finally {
                    TrySetProperty(viewModel, first, firstOriginal);
                    TrySetProperty(viewModel, second, secondOriginal);
                }
            }
        }

        private static bool ShouldSkipSmokeProperty(PropertyInfo property) {
            string name = property.Name.ToLowerInvariant();
            return ContainsAny(name,
                "autostart",
                "currentlanguage",
                "interfaceprofile",
                "localdata",
                "backup",
                "folder",
                "directory",
                "path",
                "filepath",
                "filename",
                "token",
                "keymap",
                "hotkey",
                "clipboard",
                "panic",
                "autolock",
                "proxyuri",
                "apidomain",
                "apiversion");
        }

        private static bool TryBuildSmokeValue(object viewModel, PropertyInfo property, object original, out object value, out string skipReason) {
            Type actual = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            skipReason = null;

            if (actual == typeof(bool)) {
                value = !(bool)(original ?? false);
                return true;
            }

            if (actual == typeof(int)) {
                int current = original is int i ? i : 0;
                value = current == 1 ? 2 : 1;
                return true;
            }

            if (actual == typeof(double)) {
                double current = original is double d && !Double.IsNaN(d) && !Double.IsInfinity(d) ? d : 1.0;
                value = BuildSafeDoubleValue(property.Name, current);
                return true;
            }

            if (actual == typeof(string)) {
                value = BuildSafeStringValue(property.Name, original as string);
                if (value == null) {
                    skipReason = "skip_string";
                    return false;
                }

                return true;
            }

            if (actual == typeof(TwoStringTuple)) {
                TwoStringTuple next = FindNextOption(viewModel, property.Name, original as TwoStringTuple, o => o.Item1);
                if (next == null) {
                    skipReason = "no_tuple_option";
                    value = null;
                    return false;
                }

                value = next;
                return true;
            }

            if (actual == typeof(Tuple<int, string>)) {
                Tuple<int, string> next = FindNextOption(viewModel, property.Name, original as Tuple<int, string>, o => o.Item1.ToString());
                if (next == null) {
                    skipReason = "no_tuple_option";
                    value = null;
                    return false;
                }

                value = next;
                return true;
            }

            if (actual == typeof(AppearanceOption)) {
                AppearanceOption next = FindNextOption(viewModel, property.Name, original as AppearanceOption, o => o.Id);
                if (next == null) {
                    skipReason = "no_appearance_option";
                    value = null;
                    return false;
                }

                value = next;
                return true;
            }

            if (actual == typeof(AppFontFamilyOption)) {
                AppFontFamilyOption next = FindNextOption(viewModel, property.Name, original as AppFontFamilyOption, o => o.FamilyName);
                if (next == null) {
                    skipReason = "no_font_option";
                    value = null;
                    return false;
                }

                value = next;
                return true;
            }

            if (actual == typeof(AppIconVariantOption)) {
                AppIconVariantOption next = FindNextOption(viewModel, property.Name, original as AppIconVariantOption, o => o.Id);
                if (next == null) {
                    skipReason = "no_icon_option";
                    value = null;
                    return false;
                }

                value = next;
                return true;
            }

            value = null;
            skipReason = "unsupported_type";
            return false;
        }

        private static double BuildSafeDoubleValue(string propertyName, double current) {
            string name = propertyName.ToLowerInvariant();
            if (name.Contains("volume")) return current == 70 ? 80 : 70;
            if (name.Contains("rate")) return Math.Abs(current - 1.25) < 0.001 ? 1.0 : 1.25;
            if (name.Contains("seek")) return current == 15 ? 30 : 15;
            return Math.Abs(current - 1.0) < 0.001 ? 2.0 : 1.0;
        }

        private static string BuildSafeStringValue(string propertyName, string original) {
            string name = propertyName.ToLowerInvariant();
            if (name.Contains("chatlistwidth")) return original == "360" ? "420" : "360";
            if (name.Contains("mediamemorybudget")) return original == "128" ? "160" : "128";
            if (name.Contains("imagecacheramlimit")) return original == "64" ? "96" : "64";
            if (name.Contains("budgetmb")) return original == "128" ? "160" : "128";
            if (name.Contains("timeout")) return "5";
            if (name.Contains("minutes")) return "5";
            if (name.Contains("hour")) return "2";
            if (name.EndsWith("text") || name.Contains("limit")) return "2";
            if (name.Contains("language")) return String.Equals(original, "auto", StringComparison.OrdinalIgnoreCase) ? "ru" : "auto";
            if (name.Contains("keywords")) return "urgent, smoke";
            if (name.Contains("uri") || name.Contains("domain") || name.Contains("version")) return null;
            return String.IsNullOrWhiteSpace(original) ? "laney-smoke" : $"{original} smoke";
        }

        private static T FindNextOption<T>(object viewModel, string targetPropertyName, T current, Func<T, string> keySelector) where T : class {
            if (current == null) return null;

            var candidates = new List<(int Score, List<T> Options)>();
            foreach (PropertyInfo property in viewModel.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)) {
                if (!typeof(IEnumerable).IsAssignableFrom(property.PropertyType) || property.PropertyType == typeof(string)) continue;

                IEnumerable enumerable = property.GetValue(viewModel) as IEnumerable;
                if (enumerable == null) continue;

                List<T> options = enumerable.OfType<T>().ToList();
                string currentKey = keySelector(current);
                if (options.Count < 2 || !options.Any(o => String.Equals(keySelector(o), currentKey, StringComparison.OrdinalIgnoreCase))) continue;

                candidates.Add((GetOptionCollectionScore(targetPropertyName, property.Name), options));
            }

            foreach ((int _, List<T> options) in candidates.OrderByDescending(c => c.Score)) {
                string currentKey = keySelector(current);
                int index = options.FindIndex(o => String.Equals(keySelector(o), currentKey, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) return options[(index + 1) % options.Count];
            }

            return null;
        }

        private static int GetOptionCollectionScore(string targetPropertyName, string collectionPropertyName) {
            string target = NormalizeOptionName(targetPropertyName);
            string collection = NormalizeOptionName(collectionPropertyName);
            if (collection == target) return 100;
            if (collection.Contains(target, StringComparison.OrdinalIgnoreCase)) return 80;
            if (target.Contains(collection, StringComparison.OrdinalIgnoreCase)) return 60;

            int score = 0;
            string[] tokens = {
                "account", "accent", "animation", "avatar", "background", "bubble", "chat", "delivery",
                "density", "emoji", "family", "font", "hour", "icon", "idle", "layout", "list", "message",
                "minute", "mode", "opacity", "position", "send", "size", "status", "sticker", "style",
                "theme", "width"
            };

            foreach (string token in tokens) {
                if (target.Contains(token, StringComparison.OrdinalIgnoreCase) && collection.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 10;
            }

            return score;
        }

        private static string NormalizeOptionName(string value) {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;
            return value
                .Replace("Current", String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Options", String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Cards", String.Empty, StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();
        }

        private static bool SmokeValuesEqual(object left, object right) {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left is TwoStringTuple lt && right is TwoStringTuple rt) return lt.Item1 == rt.Item1;
            if (left is Tuple<int, string> lti && right is Tuple<int, string> rti) return lti.Item1 == rti.Item1;
            if (left is AppearanceOption la && right is AppearanceOption ra) return la.Id == ra.Id;
            if (left is AppFontFamilyOption lf && right is AppFontFamilyOption rf) return lf.FamilyName == rf.FamilyName;
            if (left is AppIconVariantOption li && right is AppIconVariantOption ri) return li.Id == ri.Id;
            if (left is double ld && right is double rd) return Math.Abs(ld - rd) < 0.001;
            return Equals(left, right);
        }

        private static string FormatSmokeValue(object value) {
            return value switch {
                null => "null",
                TwoStringTuple tuple => tuple.Item1,
                Tuple<int, string> tuple => tuple.Item1.ToString(),
                AppearanceOption option => option.Id,
                AppFontFamilyOption font => font.FamilyName,
                AppIconVariantOption icon => icon.Id,
                _ => value.ToString()
            };
        }

        private static void TrySetProperty(object viewModel, PropertyInfo property, object value) {
            try {
                property.SetValue(viewModel, value);
            } catch {
            }
        }

        private static void ValidateSmokeCollections(string category, object viewModel, SettingsMutationSmokeReport report) {
            if (viewModel is not AppearanceViewModel appearance) return;

            foreach (AppIconVariantOption option in appearance.AppIconVariantCards) {
                if (option.PreviewBitmap == null) {
                    report.AddFailure(category, $"AppIconVariantCards.{option.Id}", $"preview_missing:{option.PreviewUri}");
                }
            }

            foreach (AppFontFamilyOption option in appearance.AppFontFamilyOptions) {
                if (option.Family == null) {
                    report.AddFailure(category, $"AppFontFamilyOptions.{option.FamilyName}", "font_family_missing");
                }
            }
        }

        private static bool ContainsAny(string text, params string[] values) {
            foreach (string value in values) {
                if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
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

    public sealed class SettingsMutationSmokeReport {
        private readonly List<SettingsMutationSmokeIssue> _issues = new List<SettingsMutationSmokeIssue>();

        public int CategoriesTotal { get; }
        public int ViewModelsChecked { get; set; }
        public int SkippedViewModels { get; set; }
        public int PropertiesChecked { get; set; }
        public int RoundTripsPassed { get; set; }
        public int BoolPairsPassed { get; set; }
        public int SkippedProperties { get; private set; }
        public int FailedProperties { get; private set; }
        public IReadOnlyList<SettingsMutationSmokeIssue> Issues => _issues;
        public bool Passed => FailedProperties == 0;

        public SettingsMutationSmokeReport(int categoriesTotal) {
            CategoriesTotal = categoriesTotal;
        }

        public void AddSkipped(string category, string property, string reason) {
            SkippedProperties++;
            _issues.Add(new SettingsMutationSmokeIssue(category, property, reason, true));
        }

        public void AddFailure(string category, string property, string reason) {
            FailedProperties++;
            _issues.Add(new SettingsMutationSmokeIssue(category, property, reason, false));
        }
    }

    public sealed class SettingsMutationSmokeIssue {
        public string Category { get; }
        public string Property { get; }
        public string Reason { get; }
        public bool IsSkipped { get; }

        public SettingsMutationSmokeIssue(string category, string property, string reason, bool isSkipped) {
            Category = category;
            Property = property;
            Reason = reason;
            IsSkipped = isSkipped;
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
