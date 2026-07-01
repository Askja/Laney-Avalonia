using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.VKAPILib.Objects;
using System;

namespace ELOR.Laney.ViewModels {
    public sealed class StoryRailItemViewModel : ViewModelBase {
        private string _stateText;
        private bool _isSeen;

        public StoryRailItemViewModel(Story story) {
            Story = story ?? throw new ArgumentNullException(nameof(story));
            OwnerId = story.OwnerId;
            PreviewUri = GetStoryPreviewUri(story);
            _isSeen = story.Seen == 1;
            IsVideo = story.Type == StoryType.Video;
            IsUnavailable = story.IsExpired || story.IsDeleted || story.CanSee == 0;

            Tuple<string, string, Uri> owner = CacheManager.GetNameAndAvatar(story.OwnerId);
            string title = BuildOwnerTitle(story, owner);
            Title = title;
            Initials = BuildInitials(title);
            AvatarUri = owner?.Item3;
            _stateText = BuildStateText(story);
        }

        private StoryRailItemViewModel(string title, Uri previewUri, Uri avatarUri, long ownerId, bool isSeen, bool isVideo, bool isUnavailable, string stateText) {
            Story = null;
            OwnerId = ownerId;
            PreviewUri = previewUri;
            AvatarUri = avatarUri;
            Title = String.IsNullOrWhiteSpace(title) ? "История" : title;
            Initials = BuildInitials(Title);
            _isSeen = isSeen;
            IsVideo = isVideo;
            IsUnavailable = isUnavailable;
            _stateText = stateText;
        }

        public Story Story { get; }
        public long OwnerId { get; }
        public Uri PreviewUri { get; }
        public Uri AvatarUri { get; }
        public string Title { get; }
        public string Initials { get; }
        public string StateText { get { return _stateText; } private set { _stateText = value; OnPropertyChanged(); } }
        public bool IsSeen { get { return _isSeen; } private set { _isSeen = value; OnPropertyChanged(); } }
        public bool IsVideo { get; }
        public bool IsUnavailable { get; }

        public static StoryRailItemViewModel CreateDemo(string title, string previewUri, long ownerId, bool isSeen = false, bool isVideo = false) {
            Uri preview = Uri.TryCreate(previewUri, UriKind.Absolute, out Uri parsedPreview) ? parsedPreview : null;
            string state = isVideo ? "demo-видео" : isSeen ? "demo-просмотрена" : "demo-новая";
            return new StoryRailItemViewModel(title, preview, null, ownerId, isSeen, isVideo, false, state);
        }

        public void MarkSeenLocally() {
            if (IsSeen || IsUnavailable) return;
            if (Story == null) {
                IsSeen = true;
                StateText = "demo-просмотрена";
                return;
            }

            Story.Seen = 1;
            IsSeen = true;
            StateText = BuildStateText(Story);
        }

        private static Uri GetStoryPreviewUri(Story story) {
            try {
                return story.Type switch {
                    StoryType.Photo => story.Photo?.GetSizeAndUriForThumbnail(160, 220).Uri,
                    StoryType.Video => story.Video?.FirstFrameForStory?.Uri,
                    _ => null
                };
            } catch {
                return null;
            }
        }

        private static string BuildOwnerTitle(Story story, Tuple<string, string, Uri> owner) {
            if (Settings.StreamerMode) return "История";

            if (owner != null) {
                string fullName = $"{owner.Item1} {owner.Item2}".Trim();
                if (!String.IsNullOrWhiteSpace(fullName)) return fullName;
            }

            return story.OwnerId < 0 ? $"Сообщество {-story.OwnerId}" : $"Пользователь {story.OwnerId}";
        }

        private static string BuildInitials(string title) {
            if (String.IsNullOrWhiteSpace(title)) return "И";
            return title.Trim()[0].ToString().ToUpperInvariant();
        }

        private static string BuildStateText(Story story) {
            if (story.IsDeleted) return "удалена";
            if (story.IsExpired) return "истекла";
            if (story.CanSee == 0) return "закрыта";
            if (story.Type == StoryType.Video) return "видео";
            return story.Seen == 1 ? "просмотрена" : "новая";
        }
    }
}
