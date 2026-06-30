using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.VKAPILib.Objects;
using System;

namespace ELOR.Laney.ViewModels {
    public sealed class StoryRailItemViewModel : ViewModelBase {
        public StoryRailItemViewModel(Story story) {
            Story = story ?? throw new ArgumentNullException(nameof(story));
            OwnerId = story.OwnerId;
            PreviewUri = GetStoryPreviewUri(story);
            IsSeen = story.Seen == 1;
            IsVideo = story.Type == StoryType.Video;
            IsUnavailable = story.IsExpired || story.IsDeleted || story.CanSee == 0;

            Tuple<string, string, Uri> owner = CacheManager.GetNameAndAvatar(story.OwnerId);
            string title = BuildOwnerTitle(story, owner);
            Title = title;
            Initials = BuildInitials(title);
            AvatarUri = owner?.Item3;
            StateText = BuildStateText(story);
        }

        public Story Story { get; }
        public long OwnerId { get; }
        public Uri PreviewUri { get; }
        public Uri AvatarUri { get; }
        public string Title { get; }
        public string Initials { get; }
        public string StateText { get; }
        public bool IsSeen { get; }
        public bool IsVideo { get; }
        public bool IsUnavailable { get; }

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
