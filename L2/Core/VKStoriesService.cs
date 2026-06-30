using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public static class VKStoriesService {
        private const int DefaultLimit = 24;

        public static async Task<IReadOnlyList<Story>> LoadStoriesAsync(VKSession session, int limit = DefaultLimit) {
            if (session?.API == null || limit <= 0) return Array.Empty<Story>();

            using JsonDocument document = await session.API.CallMethodAsync("stories.get", new Dictionary<string, string> {
                { "extended", "1" },
                { "fields", "photo_50,photo_100,photo_200,screen_name" }
            });

            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)) {
                string message = error.TryGetProperty("error_msg", out JsonElement errorMessage)
                    ? errorMessage.GetString()
                    : "VK API вернул ошибку без текста.";
                throw new InvalidOperationException(message);
            }

            if (!root.TryGetProperty("response", out JsonElement response)) return Array.Empty<Story>();

            CacheStoryOwners(response);
            if (!response.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array) {
                return Array.Empty<Story>();
            }

            List<Story> stories = new List<Story>(Math.Min(limit, 64));
            foreach (JsonElement storyGroup in items.EnumerateArray()) {
                if (!storyGroup.TryGetProperty("stories", out JsonElement groupStories)
                    || groupStories.ValueKind != JsonValueKind.Array) {
                    continue;
                }

                foreach (JsonElement storyElement in groupStories.EnumerateArray()) {
                    Story story = (Story)JsonSerializer.Deserialize(
                        storyElement.GetRawText(),
                        typeof(Story),
                        ELOR.VKAPILib.BuildInJsonContext.Default);
                    if (story == null) continue;

                    stories.Add(story);
                    if (stories.Count >= limit) return stories;
                }
            }

            return stories;
        }

        private static void CacheStoryOwners(JsonElement response) {
            if (response.TryGetProperty("profiles", out JsonElement profiles) && profiles.ValueKind == JsonValueKind.Array) {
                List<User> users = (List<User>)JsonSerializer.Deserialize(
                    profiles.GetRawText(),
                    typeof(List<User>),
                    ELOR.VKAPILib.BuildInJsonContext.Default);
                if (users != null) CacheManager.Add(users);
            }

            if (response.TryGetProperty("groups", out JsonElement groups) && groups.ValueKind == JsonValueKind.Array) {
                List<Group> communities = (List<Group>)JsonSerializer.Deserialize(
                    groups.GetRawText(),
                    typeof(List<Group>),
                    ELOR.VKAPILib.BuildInJsonContext.Default);
                if (communities != null) CacheManager.Add(communities);
            }
        }
    }
}
