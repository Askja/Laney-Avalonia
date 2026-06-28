using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney;

public partial class ReactionsPicker : UserControl {
    private const int DefaultQuickReactionsLimit = 6;
    Control popupTarget;
    PopupFlyoutBase parentPopup;
    long peerId;
    int cmid;
    int selectedReactionId = 0;

    public ReactionsPicker() {
        InitializeComponent();
    }

    public ReactionsPicker(long peerId, int cmid, int pickedReactionId, Control target, PopupFlyoutBase parent) {
        InitializeComponent();
        this.peerId = peerId;
        this.cmid = cmid;
        selectedReactionId = pickedReactionId;
        popupTarget = target;
        parentPopup = parent;

        Command command = new Command(null, null, false, OnReactionClick);
        var entities = BuildReactionIds(peerId)
            .Select(r => new Entity(r, new Uri(CacheManager.GetStaticReactionUrl(r)), null, "Быстрая реакция", command))
            .ToList();
        ReactionsList.ItemsSource = entities;
    }

    private void OnReactionClick(object obj) {
        parentPopup?.Hide();

        if (DemoMode.IsEnabled) return;
        if (obj == null || obj is not long) return;

        var session = VKSession.GetByDataContext(popupTarget);
        int picked = Convert.ToInt32(obj);
        bool remove = selectedReactionId == picked;

        new Action(async () => {
            try {
                bool response = remove
                    ? await session.API.Messages.DeleteReactionAsync(session.GroupId, peerId, cmid)
                    : await session.API.Messages.SendReactionAsync(session.GroupId, peerId, cmid, picked);
                if (!remove && response) Settings.PromotePeerQuickReactionId(peerId, picked, GetDefaultQuickReactionIds());
            } catch (Exception ex) {
                string str = remove ? "remove" : "send";
                Log.Error(ex, $"Failed to {str} reaction to message {peerId}_{cmid}!");
                await ExceptionHelper.ShowErrorDialogAsync(session?.Window, ex, true);
            }
        })();
    }

    private void Button_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
        Button b = sender as Button;
        Entity entity = b.DataContext as Entity;
        if (entity == null) return;

        if (Convert.ToInt64(selectedReactionId) == entity.Id) b.Classes.Remove("Tertiary");
    }

    private static List<int> BuildReactionIds(long peerId) {
        List<int> available = CacheManager.AvailableReactions ?? new List<int>();
        HashSet<int> availableSet = available.ToHashSet();
        List<int> quick = Settings.GetPeerQuickReactionIds(peerId)
            .Where(availableSet.Contains)
            .ToList();

        if (quick.Count == 0) quick = GetDefaultQuickReactionIds();

        return quick
            .Concat(available)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static List<int> GetDefaultQuickReactionIds() {
        return (CacheManager.AvailableReactions ?? new List<int>())
            .Where(id => id > 0)
            .Take(DefaultQuickReactionsLimit)
            .ToList();
    }
}
