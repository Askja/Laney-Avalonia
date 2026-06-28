using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class ScheduledMessageQueueItemViewModel {
        public string Id { get; init; }
        public long PeerId { get; init; }
        public string Text { get; init; }
        public string Title { get; init; }
        public string Subtitle { get; init; }
        public string RepeatText { get; init; }
        public RelayCommand CancelCommand { get; init; }
    }

    public sealed class AutomationViewModel : CommonViewModel {
        private RelayCommand _resetAutomationRulesCommand;
        private RelayCommand _resetAutoReplyRulesCommand;
        private RelayCommand _resetAutoStatusRulesCommand;
        private RelayCommand _refreshScheduledMessagesCommand;

        public ObservableCollection<TwoStringTuple> Hours { get; } = BuildHours();
        public ObservableCollection<TwoStringTuple> AutoStatuses { get; } = new ObservableCollection<TwoStringTuple>(
            AutoStatusManager.Modes.Select(mode => new TwoStringTuple(mode, AutoStatusManager.GetTitle(mode))));
        public ObservableCollection<TwoStringTuple> IdleMinuteOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("5", "5 мин"),
            new TwoStringTuple("10", "10 мин"),
            new TwoStringTuple("15", "15 мин"),
            new TwoStringTuple("30", "30 мин"),
            new TwoStringTuple("60", "1 час")
        };
        public ObservableCollection<ScheduledMessageQueueItemViewModel> ScheduledMessages { get; } = new ObservableCollection<ScheduledMessageQueueItemViewModel>();

        public bool DontAnnoyMeMode { get { return Settings.DontAnnoyMeMode; } set { Settings.DontAnnoyMeMode = value; OnPropertyChanged(); } }
        public bool DontAnnoyMeAllowMentions { get { return Settings.DontAnnoyMeAllowMentions; } set { Settings.DontAnnoyMeAllowMentions = value; OnPropertyChanged(); } }
        public bool DontAnnoyMeAllowImportant { get { return Settings.DontAnnoyMeAllowImportant; } set { Settings.DontAnnoyMeAllowImportant = value; OnPropertyChanged(); } }
        public string DontAnnoyMeKeywords { get { return Settings.DontAnnoyMeKeywords; } set { Settings.DontAnnoyMeKeywords = value; OnPropertyChanged(); } }
        public TwoStringTuple CurrentDontAnnoyMeStartHour { get { return GetHour(Settings.DontAnnoyMeStartHour); } set { ChangeDontAnnoyMeStartHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentDontAnnoyMeEndHour { get { return GetHour(Settings.DontAnnoyMeEndHour); } set { ChangeDontAnnoyMeEndHour(value); OnPropertyChanged(); } }
        public bool AutoStatusEnabled { get { return Settings.AutoStatusEnabled; } set { Settings.AutoStatusEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusSchedule)); OnPropertyChanged(nameof(CanConfigureAutoStatusIdle)); OnPropertyChanged(nameof(CanConfigureAutoStatusRules)); } }
        public TwoStringTuple CurrentAutoStatusMode { get { return GetAutoStatus(Settings.AutoStatusMode); } set { ChangeAutoStatusMode(value); OnPropertyChanged(); } }
        public bool AutoStatusScheduleEnabled { get { return Settings.AutoStatusScheduleEnabled; } set { Settings.AutoStatusScheduleEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusSchedule)); } }
        public TwoStringTuple CurrentAutoStatusScheduleStartHour { get { return GetHour(Settings.AutoStatusScheduleStartHour); } set { ChangeAutoStatusScheduleStartHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusScheduleEndHour { get { return GetHour(Settings.AutoStatusScheduleEndHour); } set { ChangeAutoStatusScheduleEndHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusScheduleMode { get { return GetAutoStatus(Settings.AutoStatusScheduleMode); } set { ChangeAutoStatusScheduleMode(value); OnPropertyChanged(); } }
        public bool AutoStatusIdleEnabled { get { return Settings.AutoStatusIdleEnabled; } set { Settings.AutoStatusIdleEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusIdle)); } }
        public TwoStringTuple CurrentAutoStatusIdleMinutes { get { return GetIdleMinutes(Settings.AutoStatusIdleMinutes); } set { ChangeAutoStatusIdleMinutes(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusIdleMode { get { return GetAutoStatus(Settings.AutoStatusIdleMode); } set { ChangeAutoStatusIdleMode(value); OnPropertyChanged(); } }
        public bool CanConfigureAutoStatusSchedule { get { return AutoStatusEnabled && AutoStatusScheduleEnabled; } }
        public bool CanConfigureAutoStatusIdle { get { return AutoStatusEnabled && AutoStatusIdleEnabled; } }
        public bool CanConfigureAutoStatusRules { get { return AutoStatusEnabled; } }
        public string AutoStatusRulesText { get { return AutoStatusRuleStore.GetRulesText(); } set { AutoStatusRuleStore.SetRulesText(value); OnPropertyChanged(); OnPropertyChanged(nameof(AutoStatusRulesSummary)); } }
        public string AutoStatusRulesSummary { get { return AutoStatusRuleStore.GetSummary(); } }
        public RelayCommand ResetAutoStatusRulesCommand { get { return _resetAutoStatusRulesCommand; } private set { _resetAutoStatusRulesCommand = value; OnPropertyChanged(); } }
        public string AutomationRulesText { get { return AutomationRuleStore.GetRulesText(); } set { AutomationRuleStore.SetRulesText(value); OnPropertyChanged(); OnPropertyChanged(nameof(AutomationRulesSummary)); } }
        public string AutomationRulesSummary { get { return AutomationRuleStore.GetSummary(); } }
        public string AutomationRulesShape { get { return "Кто: peer/sender/mention. Где: peer=* или peer id. Что: contains. Когда: duration. Действия: mute/tag/download/remind/archive/todo."; } }
        public string AutomationRulesExample { get { return "enabled=true; peer=*; sender=*; contains=инцидент,prod; action=tag; value=urgent; suppress=false"; } }
        public RelayCommand ResetAutomationRulesCommand { get { return _resetAutomationRulesCommand; } private set { _resetAutomationRulesCommand = value; OnPropertyChanged(); } }
        public string AutoReplyRulesText { get { return AutoReplyRuleStore.GetRulesText(); } set { AutoReplyRuleStore.SetRulesText(value); OnPropertyChanged(); OnPropertyChanged(nameof(AutoReplyRulesSummary)); } }
        public string AutoReplyRulesSummary { get { return AutoReplyRuleStore.GetSummary(); } }
        public RelayCommand ResetAutoReplyRulesCommand { get { return _resetAutoReplyRulesCommand; } private set { _resetAutoReplyRulesCommand = value; OnPropertyChanged(); } }
        public string ScheduledMessagesSummary => ScheduledMessages.Count == 0 ? "Очередь пуста" : $"{ScheduledMessages.Count} в очереди";
        public bool HasScheduledMessages => ScheduledMessages.Count > 0;
        public RelayCommand RefreshScheduledMessagesCommand { get { return _refreshScheduledMessagesCommand; } private set { _refreshScheduledMessagesCommand = value; OnPropertyChanged(); } }

        public AutomationViewModel() {
            ResetAutomationRulesCommand = new RelayCommand((o) => ResetAutomationRules());
            ResetAutoReplyRulesCommand = new RelayCommand((o) => ResetAutoReplyRules());
            ResetAutoStatusRulesCommand = new RelayCommand((o) => ResetAutoStatusRules());
            RefreshScheduledMessagesCommand = new RelayCommand((o) => ReloadScheduledMessages());
            ReloadScheduledMessages();
        }

        private static ObservableCollection<TwoStringTuple> BuildHours() {
            ObservableCollection<TwoStringTuple> hours = new ObservableCollection<TwoStringTuple>();
            for (int hour = 0; hour < 24; hour++) {
                string value = hour.ToString();
                hours.Add(new TwoStringTuple(value, $"{hour:00}:00"));
            }

            return hours;
        }

        private TwoStringTuple GetHour(int hour) {
            string id = hour.ToString();
            return Hours.Where(h => h.Item1 == id).FirstOrDefault();
        }

        private TwoStringTuple GetAutoStatus(string mode) {
            string id = AutoStatusModeIds.Normalize(mode);
            return AutoStatuses.Where(s => s.Item1 == id).FirstOrDefault();
        }

        private TwoStringTuple GetIdleMinutes(int minutes) {
            string id = minutes.ToString();
            return IdleMinuteOptions.Where(m => m.Item1 == id).FirstOrDefault() ?? IdleMinuteOptions.FirstOrDefault();
        }

        private void ChangeDontAnnoyMeStartHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.DontAnnoyMeStartHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentDontAnnoyMeStartHour));
        }

        private void ChangeDontAnnoyMeEndHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.DontAnnoyMeEndHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentDontAnnoyMeEndHour));
        }

        private void ChangeAutoStatusMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusMode));
        }

        private void ChangeAutoStatusScheduleStartHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleStartHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleStartHour));
        }

        private void ChangeAutoStatusScheduleEndHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleEndHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleEndHour));
        }

        private void ChangeAutoStatusScheduleMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleMode));
        }

        private void ChangeAutoStatusIdleMinutes(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusIdleMinutes = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusIdleMinutes));
        }

        private void ChangeAutoStatusIdleMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusIdleMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusIdleMode));
        }

        private void ResetAutomationRules() {
            AutomationRuleStore.ResetRulesText();
            OnPropertyChanged(nameof(AutomationRulesText));
            OnPropertyChanged(nameof(AutomationRulesSummary));
        }

        private void ResetAutoReplyRules() {
            AutoReplyRuleStore.ResetRulesText();
            OnPropertyChanged(nameof(AutoReplyRulesText));
            OnPropertyChanged(nameof(AutoReplyRulesSummary));
        }

        private void ResetAutoStatusRules() {
            AutoStatusRuleStore.ResetRulesText();
            OnPropertyChanged(nameof(AutoStatusRulesText));
            OnPropertyChanged(nameof(AutoStatusRulesSummary));
        }

        private void ReloadScheduledMessages() {
            ScheduledMessages.Clear();
            foreach (ScheduledMessageItem item in Settings.GetScheduledMessages().OrderBy(i => i.NextSendUnix).Take(64)) {
                DateTimeOffset nextSend = DateTimeOffset.FromUnixTimeSeconds(item.NextSendUnix).ToLocalTime();
                string text = item.Text?.Trim() ?? String.Empty;
                ScheduledMessages.Add(new ScheduledMessageQueueItemViewModel {
                    Id = item.Id,
                    PeerId = item.PeerId,
                    Text = text.Length <= 160 ? text : $"{text[..160]}...",
                    Title = $"peer {item.PeerId}",
                    Subtitle = $"Отправка: {nextSend:dd.MM.yyyy HH:mm}",
                    RepeatText = item.RepeatIntervalMinutes > 0 ? $"Повтор каждые {FormatRepeat(item.RepeatIntervalMinutes)}" : "Без повтора",
                    CancelCommand = new RelayCommand((o) => CancelScheduledMessage(item.Id))
                });
            }

            OnPropertyChanged(nameof(ScheduledMessagesSummary));
            OnPropertyChanged(nameof(HasScheduledMessages));
        }

        private void CancelScheduledMessage(string id) {
            Settings.RemoveScheduledMessage(id);
            ReloadScheduledMessages();
        }

        private static string FormatRepeat(int minutes) {
            if (minutes % (24 * 60) == 0) return $"{minutes / (24 * 60)} д";
            if (minutes % 60 == 0) return $"{minutes / 60} ч";
            return $"{minutes} мин";
        }
    }
}
