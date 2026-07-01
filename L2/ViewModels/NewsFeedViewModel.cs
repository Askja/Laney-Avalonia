using Avalonia.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.ViewModels {
    public sealed class NewsFeedPostViewModel {
        public NewsFeedPostViewModel(VKSession session, WallPost post, long sourceId, bool isAdLike, string adReason) {
            Session = session;
            Post = post;
            SourceId = sourceId;
            IsAdLike = isAdLike;
            AdReason = adReason;
            SourceText = BuildSourceText(sourceId);
        }

        public VKSession Session { get; }
        public WallPost Post { get; }
        public long SourceId { get; }
        public string SourceText { get; }
        public bool IsAdLike { get; }
        public bool IsAdBadgeVisible => IsAdLike;
        public string AdReason { get; }
        public string Link => Post == null ? "https://vk.com/feed" : $"https://vk.com/wall{Post.OwnerOrToId}_{Post.Id}";

        private static string BuildSourceText(long sourceId) {
            if (sourceId == 0) return "источник неизвестен";

            Tuple<string, string, Uri> owner = CacheManager.GetNameAndAvatar(sourceId);
            if (owner != null) {
                string name = $"{owner.Item1} {owner.Item2}".Trim();
                if (!String.IsNullOrWhiteSpace(name)) return name;
            }

            return sourceId < 0 ? $"club{-sourceId}" : $"id{sourceId}";
        }
    }

    public sealed class NewsFeedViewModel : CommonViewModel {
        private const int PageSize = 25;
        private const int MaxAdScanTextLength = 12000;
        private const int MaxAdScanDepth = 5;
        private const string CleaningModeOff = "off";
        private const string CleaningModeSoft = "soft";
        private const string CleaningModeStrict = "strict";

        private readonly VKSession session;
        private TwoStringTuple _currentFilter;
        private TwoStringTuple _currentCleaningMode;
        private bool _hideAds = true;
        private bool _strictAds;
        private string _adKeywords;
        private IReadOnlyList<string> _customAdKeywords = Array.Empty<string>();
        private string _blockedSources;
        private HashSet<long> _blockedSourceIds = new HashSet<long>();
        private bool _initialized;
        private bool _hasMore = true;
        private string _nextFrom;
        private int _blockedAdsCount;
        private int _blockedSourcePostsCount;

        public NewsFeedViewModel(VKSession session) {
            this.session = session;
            _currentFilter = FilterOptions.FirstOrDefault(f => f.Item1 == Settings.NewsFeedFilter) ?? FilterOptions[0];
            _hideAds = Settings.NewsFeedHideAds;
            _strictAds = Settings.NewsFeedStrictAds;
            _adKeywords = Settings.NewsFeedAdKeywords;
            _customAdKeywords = ParseCustomAdKeywords(_adKeywords);
            _blockedSources = Settings.NewsFeedBlockedSources;
            _blockedSourceIds = ParseBlockedSourceIds(_blockedSources);
            _currentCleaningMode = ResolveCleaningMode();
        }

        public ObservableCollection<NewsFeedPostViewModel> Posts { get; } = new ObservableCollection<NewsFeedPostViewModel>();
        public ObservableCollection<TwoStringTuple> FilterOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(NewsFeedFilterIds.All, "Все посты"),
            new TwoStringTuple(NewsFeedFilterIds.Friends, "Друзья"),
            new TwoStringTuple(NewsFeedFilterIds.Communities, "Сообщества"),
            new TwoStringTuple(NewsFeedFilterIds.Photo, "С фото"),
            new TwoStringTuple(NewsFeedFilterIds.Video, "С видео"),
            new TwoStringTuple(NewsFeedFilterIds.Audio, "С музыкой"),
            new TwoStringTuple(NewsFeedFilterIds.Links, "Ссылки"),
            new TwoStringTuple(NewsFeedFilterIds.Rich, "С вложениями"),
            new TwoStringTuple(NewsFeedFilterIds.Text, "Только текст")
        };
        public ObservableCollection<TwoStringTuple> CleaningModeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(CleaningModeSoft, "Мягкая чистка"),
            new TwoStringTuple(CleaningModeStrict, "Строгая чистка"),
            new TwoStringTuple(CleaningModeOff, "Без чистки")
        };

        public TwoStringTuple CurrentFilter {
            get { return _currentFilter; }
            set {
                if (value == null || value.Item1 == _currentFilter?.Item1) return;
                _currentFilter = value;
                Settings.NewsFeedFilter = value.Item1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FeedStatusText));
                RefreshAfterUserChange();
            }
        }

        public bool HideAds {
            get { return _hideAds; }
            set {
                if (_hideAds == value) return;
                _hideAds = value;
                Settings.NewsFeedHideAds = value;
                _currentCleaningMode = ResolveCleaningMode();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentCleaningMode));
                OnPropertyChanged(nameof(IsAdRulesVisible));
                OnPropertyChanged(nameof(FeedStatusText));
                RefreshAfterUserChange();
            }
        }

        public bool StrictAds {
            get { return _strictAds; }
            set {
                if (_strictAds == value) return;
                _strictAds = value;
                Settings.NewsFeedStrictAds = value;
                _currentCleaningMode = ResolveCleaningMode();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentCleaningMode));
                OnPropertyChanged(nameof(FeedStatusText));
                OnPropertyChanged(nameof(IsAdRulesVisible));
                RefreshAfterUserChange();
            }
        }

        public TwoStringTuple CurrentCleaningMode {
            get { return _currentCleaningMode; }
            set {
                if (value == null || value.Item1 == _currentCleaningMode?.Item1) return;

                _currentCleaningMode = value;
                _hideAds = value.Item1 != CleaningModeOff;
                _strictAds = value.Item1 == CleaningModeStrict;
                Settings.NewsFeedHideAds = _hideAds;
                Settings.NewsFeedStrictAds = _strictAds;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HideAds));
                OnPropertyChanged(nameof(StrictAds));
                OnPropertyChanged(nameof(IsAdRulesVisible));
                OnPropertyChanged(nameof(FeedStatusText));
                RefreshAfterUserChange();
            }
        }

        public string AdKeywords {
            get { return _adKeywords; }
            set {
                string normalized = value ?? String.Empty;
                if (_adKeywords == normalized) return;
                _adKeywords = normalized;
                _customAdKeywords = ParseCustomAdKeywords(normalized);
                Settings.NewsFeedAdKeywords = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FeedStatusText));
            }
        }

        public string BlockedSources {
            get { return _blockedSources; }
            set {
                string normalized = value?.Trim() ?? String.Empty;
                if (_blockedSources == normalized) return;

                _blockedSources = normalized;
                _blockedSourceIds = ParseBlockedSourceIds(normalized);
                Settings.NewsFeedBlockedSources = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BlockedSourcesListText));
                OnPropertyChanged(nameof(HasBlockedSourceRules));
                OnPropertyChanged(nameof(FeedStatusText));
            }
        }

        public int BlockedAdsCount { get { return _blockedAdsCount; } private set { _blockedAdsCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BlockedAdsText)); OnPropertyChanged(nameof(HasBlockedAds)); OnPropertyChanged(nameof(FeedStatusText)); } }
        public int BlockedSourcePostsCount { get { return _blockedSourcePostsCount; } private set { _blockedSourcePostsCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(BlockedSourcesText)); OnPropertyChanged(nameof(HasBlockedSourcePosts)); OnPropertyChanged(nameof(FeedStatusText)); } }
        public bool HasBlockedAds => BlockedAdsCount > 0;
        public bool HasBlockedSourcePosts => BlockedSourcePostsCount > 0;
        public bool HasBlockedSourceRules => _blockedSourceIds.Count > 0;
        public bool IsAdRulesVisible => HideAds;
        public string BlockedAdsText => BlockedAdsCount == 0 ? null : $"скрыто промо: {BlockedAdsCount}";
        public string BlockedSourcesText => BlockedSourcePostsCount == 0 ? null : $"скрыто источников: {BlockedSourcePostsCount}";
        public string BlockedSourcesListText => _blockedSourceIds.Count == 0 ? "источники не заблокированы" : $"source id: {String.Join(", ", _blockedSourceIds.OrderBy(i => i))}";
        public string FeedStatusText => HideAds
            ? $"{(_strictAds ? "строго" : "мягко")}{(_customAdKeywords.Count > 0 ? $" · ключей {_customAdKeywords.Count}" : String.Empty)}{(_blockedSourceIds.Count > 0 ? $" · источников {_blockedSourceIds.Count}" : String.Empty)}"
            : $"промо видно{(_blockedSourceIds.Count > 0 ? $" · источников {_blockedSourceIds.Count}" : String.Empty)}";
        public bool HasPosts => Posts.Count > 0;
        public bool IsLoadingFirstPage => IsLoading && Posts.Count == 0;
        public bool CanLoadMore => _hasMore && !IsLoading;
        public string FeedCounterText => Posts.Count == 0 ? "постов нет" : $"показано: {Posts.Count}";

        public async Task ApplyRulesAsync() {
            if (!_initialized) return;
            _blockedSources = FormatBlockedSources(_blockedSourceIds);
            Settings.NewsFeedBlockedSources = _blockedSources;
            OnPropertyChanged(nameof(BlockedSources));
            OnPropertyChanged(nameof(BlockedSourcesListText));
            OnPropertyChanged(nameof(HasBlockedSourceRules));
            OnPropertyChanged(nameof(FeedStatusText));
            await RefreshAsync();
        }

        public async Task InitializeAsync() {
            if (_initialized) return;
            _initialized = true;
            await RefreshAsync();
        }

        public async Task RefreshAsync() {
            Posts.Clear();
            _nextFrom = null;
            _hasMore = true;
            BlockedAdsCount = 0;
            BlockedSourcePostsCount = 0;
            Placeholder = null;
            RefreshPostState();
            await LoadNextAsync();
        }

        public void BlockSource(NewsFeedPostViewModel item) {
            if (item == null || item.SourceId == 0 || _blockedSourceIds.Contains(item.SourceId)) return;

            _blockedSourceIds.Add(item.SourceId);
            _blockedSources = FormatBlockedSources(_blockedSourceIds);
            Settings.NewsFeedBlockedSources = _blockedSources;
            OnPropertyChanged(nameof(BlockedSources));
            OnPropertyChanged(nameof(BlockedSourcesListText));
            OnPropertyChanged(nameof(HasBlockedSourceRules));
            OnPropertyChanged(nameof(FeedStatusText));

            List<NewsFeedPostViewModel> removed = Posts.Where(p => p.SourceId == item.SourceId).ToList();
            foreach (NewsFeedPostViewModel post in removed) Posts.Remove(post);
            if (removed.Count > 0) BlockedSourcePostsCount += removed.Count;

            RefreshPostState();
            UpdateEmptyState();
        }

        public async Task LoadNextAsync() {
            if (IsLoading || !_hasMore) return;

            SetLoading(true);
            try {
                if (DemoMode.IsEnabled) {
                    LoadDemoPosts();
                    return;
                }

                await LoadApiPageAsync();
            } catch (Exception ex) {
                Placeholder = PlaceholderViewModel.GetForException(ex, _ => new System.Action(async () => await RefreshAsync())());
            } finally {
                SetLoading(false);
                UpdateEmptyState();
            }
        }

        public async Task<NewsFeedRulesQaReport> RunRulesQaAsync() {
            TwoStringTuple originalFilter = _currentFilter;
            bool originalHideAds = _hideAds;
            bool originalStrictAds = _strictAds;
            string originalKeywords = _adKeywords;
            string originalBlockedSources = _blockedSources;
            bool wasInitialized = _initialized;

            try {
                _initialized = true;
                await ApplyRulesForQaAsync(NewsFeedFilterIds.All, true, true, "casino; промокод; erid; adfox", String.Empty);
                int hiddenPosts = Posts.Count;
                int blocked = BlockedAdsCount;
                bool hiddenHasPromos = Posts.Any(p => p.IsAdLike);

                await ApplyRulesForQaAsync(NewsFeedFilterIds.All, true, true, "casino; промокод; erid; adfox", "-10001");
                int sourceBlocked = BlockedSourcePostsCount;
                bool blockedSourceVisible = Posts.Any(p => p.SourceId == -10001);

                await ApplyRulesForQaAsync(NewsFeedFilterIds.All, false, true, "casino; промокод; erid; adfox", String.Empty);
                int visiblePosts = Posts.Count;
                int visiblePromos = Posts.Count(p => p.IsAdLike);

                return new NewsFeedRulesQaReport {
                    Passed = blocked >= 3 && !hiddenHasPromos && sourceBlocked >= 1 && !blockedSourceVisible && visiblePromos >= blocked && visiblePosts > hiddenPosts,
                    HiddenPosts = hiddenPosts,
                    BlockedAds = blocked,
                    HiddenHasPromos = hiddenHasPromos,
                    BlockedSourcePosts = sourceBlocked,
                    BlockedSourceVisible = blockedSourceVisible,
                    VisiblePosts = visiblePosts,
                    VisiblePromos = visiblePromos
                };
            } finally {
                _initialized = wasInitialized;
                await ApplyRulesForQaAsync(originalFilter?.Item1 ?? NewsFeedFilterIds.All, originalHideAds, originalStrictAds, originalKeywords, originalBlockedSources);
            }
        }

        private void RefreshAfterUserChange() {
            if (!_initialized) return;
            new System.Action(async () => await RefreshAsync())();
        }

        private async Task ApplyRulesForQaAsync(string filterId, bool hideAds, bool strictAds, string keywords, string blockedSources) {
            _currentFilter = FilterOptions.FirstOrDefault(f => f.Item1 == NewsFeedFilterIds.Normalize(filterId)) ?? FilterOptions[0];
            _hideAds = hideAds;
            _strictAds = strictAds;
            _currentCleaningMode = ResolveCleaningMode();
            _adKeywords = keywords ?? String.Empty;
            _customAdKeywords = ParseCustomAdKeywords(_adKeywords);
            _blockedSources = NormalizeBlockedSources(blockedSources);
            _blockedSourceIds = ParseBlockedSourceIds(_blockedSources);
            OnPropertyChanged(nameof(CurrentFilter));
            OnPropertyChanged(nameof(CurrentCleaningMode));
            OnPropertyChanged(nameof(HideAds));
            OnPropertyChanged(nameof(StrictAds));
            OnPropertyChanged(nameof(AdKeywords));
            OnPropertyChanged(nameof(BlockedSources));
            OnPropertyChanged(nameof(BlockedSourcesListText));
            OnPropertyChanged(nameof(HasBlockedSourceRules));
            OnPropertyChanged(nameof(IsAdRulesVisible));
            OnPropertyChanged(nameof(FeedStatusText));
            await RefreshAsync();
        }

        private async Task LoadApiPageAsync() {
            Dictionary<string, string> parameters = new Dictionary<string, string> {
                { "filters", "post" },
                { "count", PageSize.ToString() },
                { "max_photos", "6" },
                { "return_banned", "0" },
                { "fields", "photo_50,photo_100,photo_200,screen_name" }
            };
            if (!String.IsNullOrWhiteSpace(_nextFrom)) parameters["start_from"] = _nextFrom;

            using JsonDocument document = await session.API.CallMethodAsync("newsfeed.get", parameters);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)) {
                string message = error.TryGetProperty("error_msg", out JsonElement errorMessage)
                    ? errorMessage.GetString()
                    : "VK API вернул ошибку без текста.";
                throw new InvalidOperationException(message);
            }

            if (!root.TryGetProperty("response", out JsonElement response)) {
                _hasMore = false;
                return;
            }

            CacheNewsOwners(response);
            _nextFrom = response.TryGetProperty("next_from", out JsonElement nextFrom) ? nextFrom.GetString() : null;
            _hasMore = !String.IsNullOrWhiteSpace(_nextFrom);

            if (!response.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array) return;

            foreach (JsonElement item in items.EnumerateArray()) AddJsonNewsItem(item);

            RefreshPostState();
        }

        private void LoadDemoPosts() {
            IEnumerable<ChatViewModel> chats = session?.ImViewModel?.SortedChats ?? Enumerable.Empty<ChatViewModel>();
            int index = 0;

            AddDemoJsonPost(
                ++index,
                -10001,
                "Нормальный пост Laney",
                "Новая сборка: меньше RAM, аккуратнее скролл, без рекламной шелухи.");
            AddDemoJsonPost(
                ++index,
                -10002,
                "Промо с маркировкой",
                "Скидка дня. erid: demo-12345. #реклама",
                "\"marked_as_ads\":1");
            AddDemoJsonPost(
                ++index,
                -10003,
                "Промо во вложении",
                "Подборка ссылок без явной метки в тексте",
                "\"attachments\":[{\"type\":\"link\",\"link\":{\"url\":\"https://promo.example/?utm_source=adfox&erid=demo-link\",\"title\":\"Промокод внутри\"}}]");
            AddDemoJsonPost(
                ++index,
                -10004,
                "Строгий промо-фильтр",
                "Промокод LANEY на casino-тест, который должен уйти только в strict режиме.");

            foreach (ChatViewModel chat in chats.Take(16)) {
                string preview = chat.LastMessage?.DisplayPreviewText;
                AddDemoJsonPost(
                    ++index,
                    chat.PeerId,
                    chat.DisplayTitle,
                    String.IsNullOrWhiteSpace(preview)
                        ? "Новость из demo-ленты Laney."
                        : preview);
            }

            _hasMore = false;
            RefreshPostState();
        }

        private void AddDemoJsonPost(int id, long sourceId, string title, string text, string extraJson = null) {
            string escapedText = JsonEncodedText.Encode($"{title}\n{text}".Trim()).ToString();
            string extra = String.IsNullOrWhiteSpace(extraJson) ? String.Empty : $",{extraJson}";
            string json = $"{{\"type\":\"post\",\"source_id\":{sourceId},\"post_id\":{id},\"date\":{DateTimeOffset.Now.AddMinutes(-id * 17).ToUnixTimeSeconds()},\"text\":\"{escapedText}\"{extra}}}";

            using JsonDocument document = JsonDocument.Parse(json);
            AddJsonNewsItem(document.RootElement);
        }

        private void AddJsonNewsItem(JsonElement item) {
            WallPost post = TryParsePost(item);
            if (post == null || !MatchesCurrentFilter(post)) return;

            long sourceId = ResolvePostSourceId(post);
            if (_blockedSourceIds.Contains(sourceId)) {
                BlockedSourcePostsCount++;
                return;
            }

            bool isAd = IsAdLike(item, post, out string reason);
            if (HideAds && isAd) {
                BlockedAdsCount++;
                return;
            }

            Posts.Add(new NewsFeedPostViewModel(session, post, sourceId, isAd, reason));
            OnPropertyChanged(nameof(FeedCounterText));
        }

        private bool MatchesCurrentFilter(WallPost post) {
            string filter = NewsFeedFilterIds.Normalize(CurrentFilter?.Item1);
            bool hasAttachments = post.Attachments?.Count > 0;
            long sourceId = ResolvePostSourceId(post);

            return filter switch {
                NewsFeedFilterIds.Friends => sourceId > 0,
                NewsFeedFilterIds.Communities => sourceId < 0,
                NewsFeedFilterIds.Photo => HasAttachment(post, AttachmentType.Photo),
                NewsFeedFilterIds.Video => HasAttachment(post, AttachmentType.Video),
                NewsFeedFilterIds.Audio => HasAttachment(post, AttachmentType.Audio) || HasAttachment(post, AttachmentType.Podcast),
                NewsFeedFilterIds.Links => HasAttachment(post, AttachmentType.Link) || ContainsAny(post.Text, "https://", "http://", "vk.cc/"),
                NewsFeedFilterIds.Rich => hasAttachments,
                NewsFeedFilterIds.Text => !String.IsNullOrWhiteSpace(post.Text) && !hasAttachments,
                _ => true
            };
        }

        private static bool HasAttachment(WallPost post, AttachmentType type) {
            return post.Attachments?.Any(a => a.Type == type) == true;
        }

        private static long ResolvePostSourceId(WallPost post) {
            if (post == null) return 0;
            if (post.OwnerOrToId != 0) return post.OwnerOrToId;
            if (post.FromId != 0) return post.FromId;
            return post.ToId;
        }

        private static WallPost TryParsePost(JsonElement item) {
            if (item.TryGetProperty("type", out JsonElement typeElement)
                && !String.Equals(typeElement.GetString(), "post", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            JsonObject node = JsonNode.Parse(item.GetRawText()) as JsonObject;
            if (node == null) return null;

            if (!node.ContainsKey("owner_id") && item.TryGetProperty("source_id", out JsonElement sourceId)) node["owner_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("from_id") && item.TryGetProperty("source_id", out sourceId)) node["from_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("to_id") && item.TryGetProperty("source_id", out sourceId)) node["to_id"] = sourceId.GetInt64();
            if (!node.ContainsKey("id") && item.TryGetProperty("post_id", out JsonElement postId)) node["id"] = postId.GetInt32();

            return (WallPost)JsonSerializer.Deserialize(node.ToJsonString(), typeof(WallPost), BuildInJsonContext.Default);
        }

        private bool IsAdLike(JsonElement item, WallPost post, out string reason) {
            if (post?.MarkedAsAds == 1 || HasIntFlag(item, "marked_as_ads")) {
                reason = "помечено VK";
                return true;
            }

            if (HasAnyIntFlag(item, "is_ad", "ads_easy_promote", "ad", "promoted", "promoted_post")) {
                reason = "promo-флаг";
                return true;
            }

            if (HasPromotionalJsonMarker(item, out reason)) return true;

            if (item.TryGetProperty("type", out JsonElement typeElement)) {
                string type = typeElement.GetString();
                if (ContainsAny(type, "ads", "ad", "promo", "promoted")) {
                    reason = "promo";
                    return true;
                }
            }

            string text = BuildAdScanText(item, post);
            if (ContainsAny(text, "erid:", "erid ", "erid=", "erid-", "#реклама", "реклама.", "sponsored", "adfox", "utm_source=ad", "utm_medium=cpc")) {
                reason = "рекламная метка";
                return true;
            }

            foreach (string keyword in _customAdKeywords) {
                if (!ContainsAny(text, keyword)) continue;

                reason = $"ключ: {keyword}";
                return true;
            }

            if (StrictAds && ContainsAny(text,
                "#ad",
                "#ads",
                "#sponsored",
                "промокод",
                "скидк",
                "акци",
                "купить",
                "маркет",
                "wildberries",
                "ozon",
                "casino",
                "казино",
                "ставк",
                "розыгрыш",
                "подписывай")) {
                reason = "строгий фильтр";
                return true;
            }

            reason = null;
            return false;
        }

        private static string BuildAdScanText(JsonElement item, WallPost post) {
            StringBuilder builder = StringBuilderCache.Acquire();
            AppendLimited(builder, post?.Text);
            AppendJsonScanText(item, builder, 0);
            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static void AppendJsonScanText(JsonElement element, StringBuilder builder, int depth) {
            if (builder.Length >= MaxAdScanTextLength || depth > MaxAdScanDepth) return;

            switch (element.ValueKind) {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject()) {
                        AppendLimited(builder, property.Name);
                        AppendJsonScanText(property.Value, builder, depth + 1);
                        if (builder.Length >= MaxAdScanTextLength) return;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray()) {
                        AppendJsonScanText(child, builder, depth + 1);
                        if (builder.Length >= MaxAdScanTextLength) return;
                    }
                    break;
                case JsonValueKind.String:
                    AppendLimited(builder, element.GetString());
                    break;
            }
        }

        private static void AppendLimited(StringBuilder builder, string value) {
            if (String.IsNullOrWhiteSpace(value) || builder.Length >= MaxAdScanTextLength) return;

            int remaining = MaxAdScanTextLength - builder.Length;
            builder.Append(' ');
            builder.Append(value.Length <= remaining ? value : value[..remaining]);
        }

        private static bool HasPromotionalJsonMarker(JsonElement element, out string reason) {
            reason = null;
            return HasPromotionalJsonMarkerCore(element, 0, out reason);
        }

        private static bool HasPromotionalJsonMarkerCore(JsonElement element, int depth, out string reason) {
            reason = null;
            if (depth > MaxAdScanDepth) return false;

            if (element.ValueKind == JsonValueKind.Object) {
                foreach (JsonProperty property in element.EnumerateObject()) {
                    string name = property.Name;
                    if (IsPromotionalPropertyName(name) && IsTruthyPromoValue(property.Value)) {
                        reason = $"json: {name}";
                        return true;
                    }

                    if (HasPromotionalJsonMarkerCore(property.Value, depth + 1, out reason)) return true;
                }
            } else if (element.ValueKind == JsonValueKind.Array) {
                foreach (JsonElement child in element.EnumerateArray()) {
                    if (HasPromotionalJsonMarkerCore(child, depth + 1, out reason)) return true;
                }
            }

            return false;
        }

        private static bool IsPromotionalPropertyName(string name) {
            return String.Equals(name, "ad", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "is_ad", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "marked_as_ads", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "ads_easy_promote", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "promoted", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "promoted_post", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "ad_data", StringComparison.OrdinalIgnoreCase)
                || String.Equals(name, "ads", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthyPromoValue(JsonElement value) {
            return value.ValueKind switch {
                JsonValueKind.True => true,
                JsonValueKind.Number => value.TryGetInt32(out int intValue) && intValue != 0,
                JsonValueKind.String => !String.IsNullOrWhiteSpace(value.GetString()),
                JsonValueKind.Object => true,
                JsonValueKind.Array => value.GetArrayLength() > 0,
                _ => false
            };
        }

        private static bool HasAnyIntFlag(JsonElement item, params string[] propertyNames) {
            return propertyNames.Any(propertyName => HasIntFlag(item, propertyName));
        }

        private static bool HasIntFlag(JsonElement item, string propertyName) {
            if (!item.TryGetProperty(propertyName, out JsonElement value)) return false;
            if (value.ValueKind == JsonValueKind.True) return true;

            return value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out int intValue)
                && intValue != 0;
        }

        private static bool ContainsAny(string text, params string[] patterns) {
            if (String.IsNullOrWhiteSpace(text)) return false;
            return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> ParseCustomAdKeywords(string keywords) {
            if (String.IsNullOrWhiteSpace(keywords)) return Array.Empty<string>();

            return keywords
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(keyword => keyword.Trim())
                .Where(keyword => keyword.Length >= 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .ToArray();
        }

        private TwoStringTuple ResolveCleaningMode() {
            string id = !_hideAds ? CleaningModeOff : _strictAds ? CleaningModeStrict : CleaningModeSoft;
            return CleaningModeOptions.FirstOrDefault(o => o.Item1 == id) ?? CleaningModeOptions[0];
        }

        private static HashSet<long> ParseBlockedSourceIds(string value) {
            HashSet<long> result = new HashSet<long>();
            if (String.IsNullOrWhiteSpace(value)) return result;

            foreach (string raw in value.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (TryParseBlockedSourceId(raw, out long sourceId)) result.Add(sourceId);
            }

            return result;
        }

        private static string NormalizeBlockedSources(string value) {
            HashSet<long> ids = ParseBlockedSourceIds(value);
            return FormatBlockedSources(ids);
        }

        private static string FormatBlockedSources(IEnumerable<long> sourceIds) {
            return String.Join("; ", sourceIds.Distinct().OrderBy(i => i));
        }

        private static bool TryParseBlockedSourceId(string value, out long sourceId) {
            sourceId = 0;
            if (String.IsNullOrWhiteSpace(value)) return false;

            string token = value.Trim().Trim('@').ToLowerInvariant();
            int slash = token.LastIndexOf('/');
            if (slash >= 0 && slash < token.Length - 1) token = token[(slash + 1)..];
            token = token.Trim();

            bool negative = token.StartsWith("club", StringComparison.Ordinal) || token.StartsWith("public", StringComparison.Ordinal);
            if (negative) {
                token = token.StartsWith("club", StringComparison.Ordinal) ? token[4..] : token[6..];
            } else if (token.StartsWith("id", StringComparison.Ordinal)) {
                token = token[2..];
            }

            if (!long.TryParse(token, out long id) || id == 0) return false;
            sourceId = negative ? -Math.Abs(id) : id;
            return true;
        }

        private static void CacheNewsOwners(JsonElement response) {
            if (response.TryGetProperty("profiles", out JsonElement profiles) && profiles.ValueKind == JsonValueKind.Array) {
                List<User> users = (List<User>)JsonSerializer.Deserialize(profiles.GetRawText(), typeof(List<User>), BuildInJsonContext.Default);
                if (users?.Count > 0) CacheManager.Add(users);
            }

            if (response.TryGetProperty("groups", out JsonElement groups) && groups.ValueKind == JsonValueKind.Array) {
                List<Group> communities = (List<Group>)JsonSerializer.Deserialize(groups.GetRawText(), typeof(List<Group>), BuildInJsonContext.Default);
                if (communities?.Count > 0) CacheManager.Add(communities);
            }
        }

        private void UpdateEmptyState() {
            if (Posts.Count > 0 || Placeholder != null) return;

            Placeholder = new PlaceholderViewModel {
                Icon = new VKIcon { Id = VKIconNames.Icon56InfoOutline },
                Header = "Лента пуста",
                Text = BlockedAdsCount > 0 ? "После фильтров и скрытия рекламы ничего не осталось." : "VK не вернул посты для выбранного фильтра.",
                ActionButton = "Обновить",
                ActionButtonFunc = new RelayCommand(_ => new System.Action(async () => await RefreshAsync())())
            };
        }

        private void SetLoading(bool value) {
            IsLoading = value;
            OnPropertyChanged(nameof(IsLoadingFirstPage));
            OnPropertyChanged(nameof(CanLoadMore));
        }

        private void RefreshPostState() {
            OnPropertyChanged(nameof(HasPosts));
            OnPropertyChanged(nameof(IsLoadingFirstPage));
            OnPropertyChanged(nameof(CanLoadMore));
            OnPropertyChanged(nameof(FeedCounterText));
        }
    }

    public sealed class NewsFeedRulesQaReport {
        public bool Passed { get; set; }
        public int HiddenPosts { get; set; }
        public int BlockedAds { get; set; }
        public bool HiddenHasPromos { get; set; }
        public int BlockedSourcePosts { get; set; }
        public bool BlockedSourceVisible { get; set; }
        public int VisiblePosts { get; set; }
        public int VisiblePromos { get; set; }
    }
}
