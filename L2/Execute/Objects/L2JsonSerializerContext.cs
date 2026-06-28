using ELOR.Laney.DataModels;
using ELOR.Laney.DataModels.VKQueue;
using ELOR.Laney.Core;
using ELOR.Laney.Helpers;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ELOR.Laney.Execute.Objects {

    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(AlbumLite))]
    [JsonSerializable(typeof(List<AlbumLite>))]
    [JsonSerializable(typeof(StartSessionResponse))]
    [JsonSerializable(typeof(MessagesHistoryEx))]
    [JsonSerializable(typeof(StickerPickerData))]
    [JsonSerializable(typeof(UserEx))]
    [JsonSerializable(typeof(GroupEx))]
    [JsonSerializable(typeof(ChatInfoEx))]
    [JsonSerializable(typeof(DemoModeData))]
    [JsonSerializable(typeof(long[]))]
    [JsonSerializable(typeof(LongPollPushNotificationData))]
    [JsonSerializable(typeof(LongPollCallbackResponse))]
    [JsonSerializable(typeof(OnlineEvent))]
    [JsonSerializable(typeof(HistoryStatisticsState))]
    [JsonSerializable(typeof(List<OfflineDownloadedAttachmentRecord>))]
    [JsonSerializable(typeof(OfflineCacheStore.OfflineChatSnapshot))]
    public partial class L2JsonSerializerContext : JsonSerializerContext {
    }
}
