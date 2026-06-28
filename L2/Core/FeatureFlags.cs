using System;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney.Core {
    public sealed class FeatureFlagDefinition {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool DefaultValue { get; set; }
        public bool RequiresRestart { get; set; }
    }

    public static class FeatureFlags {
        public const string HealthCheckNetworkProbe = "diagnostics.health.network_probe";
        public const string ApiDebugLiveWindow = "diagnostics.api_debug.live_window";
        public const string TelegramTransitions = "ui.telegram.transitions";
        public const string VirtualizedChatListV2 = "ui.chat_list.virtualized_v2";
        public const string LazyAnimatedStickers = "media.stickers.lazy_animation";
        public const string StrictShadowBan = "privacy.shadowban.strict_local";
        public const string PreloadNextChat = "performance.chat.preload_next";

        private static readonly List<FeatureFlagDefinition> Registry = new List<FeatureFlagDefinition> {
            new FeatureFlagDefinition {
                Id = HealthCheckNetworkProbe,
                Title = "Health-check network probe",
                Description = "Пинговать VK CDN/favicon при health-check. Выключай, если диагностике нельзя трогать сеть лишний раз.",
                DefaultValue = true
            },
            new FeatureFlagDefinition {
                Id = ApiDebugLiveWindow,
                Title = "Live API debug window",
                Description = "Окно API debug подписывается на новые вызовы без ручного refresh.",
                DefaultValue = true
            },
            new FeatureFlagDefinition {
                Id = TelegramTransitions,
                Title = "Telegram-like transitions",
                Description = "Preview-гейт под более быстрые и короткие анимации переходов.",
                DefaultValue = false
            },
            new FeatureFlagDefinition {
                Id = VirtualizedChatListV2,
                Title = "Chat list virtualization v2",
                Description = "Preview-гейт под новый список чатов с более жесткой виртуализацией.",
                DefaultValue = false,
                RequiresRestart = true
            },
            new FeatureFlagDefinition {
                Id = LazyAnimatedStickers,
                Title = "Lazy animated stickers",
                Description = "Preview-гейт под отложенный старт тяжелых анимированных стикеров.",
                DefaultValue = false
            },
            new FeatureFlagDefinition {
                Id = StrictShadowBan,
                Title = "Strict local shadow-ban",
                Description = "Preview-гейт под более жесткую локальную фильтрацию скрытых отправителей.",
                DefaultValue = false
            },
            new FeatureFlagDefinition {
                Id = PreloadNextChat,
                Title = "Preload next chat",
                Description = "Preview-гейт под предзагрузку вероятного следующего чата. Может жрать сеть и RAM.",
                DefaultValue = false
            }
        };

        public static IReadOnlyList<FeatureFlagDefinition> Definitions => Registry;

        public static bool IsEnabled(string id) {
            FeatureFlagDefinition definition = Registry.FirstOrDefault(f => String.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
            return Settings.GetFeatureFlag(id, definition?.DefaultValue ?? false);
        }

        public static void SetEnabled(string id, bool enabled) {
            Settings.SetFeatureFlag(id, enabled);
        }
    }
}
