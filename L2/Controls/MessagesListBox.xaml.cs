// https://github.com/Elorucov/MessagesListBox.Avalonia

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ELOR.Laney.Core;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.VisualTree;

namespace ELOR.Laney.Controls {
    public interface IMessageListItem {
        int Id { get; }
    }

    public interface IMessagesListHolder {
        long Id { get; }
        event EventHandler<IMessageListItem> ScrollToMessageRequested;

        Task LoadPreviousMessagesAsync(CancellationToken? cancellationToken);
        Task LoadNextMessagesAsync(CancellationToken? cancellationToken);
    }

    public struct ScrollInfo {
        public double Height { get; private set; }
        public double Offset { get; private set; }
        public ScrollInfo(double height, double offset) {
            Height = height;
            Offset = offset;
        }
    }

    public class MessagesListBox : ListBox {
        private Dictionary<long, ScrollInfo> _lastPositions = new Dictionary<long, ScrollInfo>();
        private long _controlHolderId1 = 0;
        private long _controlHolderId2 = 0;
        private bool _canChangeScroll = true;

        private IMessagesListHolder _currentHolder = null;
        private CancellationTokenSource _cts = null;

        private bool _isPreviousMessagesLoadTriggered = false;
        private bool _isNextMessagesLoadTriggered = false;
        private long _lastScrollSaveTicks = 0;
        private const double BottomStickTolerance = 96;
        private const double BottomPinnedTolerance = 6;
        private const double ScrollDirectionTolerance = 0.5;
        private const byte RestoreRequiredStableFrames = 3;
        private const byte RestorePreviousLoadAttempts = 90;
        private const byte RestorePostAnchorAttempts = 60;
        private const double PreviousLoadStaleMs = 5000;
        private const double IncrementalLoadSuppressMs = 400;
        private const double LayoutAnchorGuardMs = 1800;
        private const double PreviousLayoutAnchorGuardMs = 5000;
        private const double BottomStickGuardMs = 1200;
        private const double BottomStickManualSuppressMs = 1600;
        private double _lastScrollOffset = Double.NaN;
        private double _restoreScrollLastHeight = Double.NaN;
        private byte _restoreScrollStableFrames = 0;
        private ScrollAnchor _activePreviousRestoreAnchor;
        private ScrollAnchor _layoutAnchorGuard;
        private long _previousRestoreTriggerTicks = 0;
        private long _previousLoadGeneration = 0;
        private long _suppressIncrementalLoadUntilTicks = 0;
        private long _suppressPreviousRestoreManualDetectionUntilTicks = 0;
        private long _layoutAnchorGuardUntilTicks = 0;
        private long _bottomStickGuardUntilTicks = 0;
        private long _bottomStickSuppressedUntilTicks = 0;
        private long _manualScrollGeneration = 0;
        private long _scrollToBottomGeneration = 0;
        private bool _isRestoringLayoutAnchor = false;
        private bool _isApplyingBottomStick = false;
        private bool _isRestoringPreviousLoad = false;
        private double _previousLoadSnapshotOffset = Double.NaN;
        private double _previousLoadUserOffsetDelta = 0;
        private long _previousRestoreManualGeneration = 0;
        private bool _previousRestoreUserInterrupted = false;

        private ScrollViewer ScrollViewer => Scroll as ScrollViewer;
        public double LastPreviousRestoreDrift { get; private set; } = Double.NaN;
        public double LastPreviousRestoreOldOffset { get; private set; } = Double.NaN;
        public double LastPreviousRestoreOldHeight { get; private set; } = Double.NaN;
        public double LastPreviousRestoreFinalOffset { get; private set; } = Double.NaN;
        public double LastPreviousRestoreFinalHeight { get; private set; } = Double.NaN;
        public double LastPreviousLoadUserOffsetDelta { get; private set; } = 0;
        public int LastPreviousRestoreAnchorId { get; private set; } = 0;
        public string LastPreviousTriggerSkipReason { get; private set; } = String.Empty;
        public bool IsHolderReady => _controlHolderId1 != 0 && _controlHolderId1 == _controlHolderId2;
        public bool HasCurrentHolder => _currentHolder != null;
        public bool CanChangeScroll => _canChangeScroll;
        public bool IsPreviousMessagesLoadTriggered => _isPreviousMessagesLoadTriggered;
        public bool IsNextMessagesLoadTriggered => _isNextMessagesLoadTriggered;
        public bool IsScrollOperationInProgress => !_canChangeScroll || _isPreviousMessagesLoadTriggered || _isNextMessagesLoadTriggered;

        public void SuppressIncrementalLoadingFor(double milliseconds) {
            if (milliseconds <= 0) return;
            _suppressIncrementalLoadUntilTicks = GetFutureTimestamp(milliseconds);
        }

        private readonly struct ScrollAnchor {
            public IMessageListItem Item { get; }
            public double Top { get; }
            public bool IsValid => Item != null;

            public ScrollAnchor(IMessageListItem item, double top) {
                Item = item;
                Top = top;
            }
        }

        private readonly struct ScrollSnapshot {
            public ScrollAnchor Anchor { get; }
            public double Height { get; }
            public double Offset { get; }

            public ScrollSnapshot(ScrollAnchor anchor, double height, double offset) {
                Anchor = anchor;
                Height = height;
                Offset = offset;
            }
        }

        private readonly struct ItemsState {
            public int Count { get; }
            public int FirstId { get; }

            public ItemsState(int count, int firstId) {
                Count = count;
                FirstId = firstId;
            }

            public bool HasVisiblePrependComparedTo(ItemsState previous) {
                if (Count <= previous.Count) return false;
                if (previous.FirstId == 0) return true;
                return FirstId != previous.FirstId;
            }
        }

        public T GetFirstVisibleItem<T>() where T : class {
            return GetVisibleItem<T>(false);
        }

        public T GetLastVisibleItem<T>() where T : class {
            return GetVisibleItem<T>(true);
        }

        public double? GetItemTopInViewport(IMessageListItem item) {
            if (item == null || ScrollViewer == null) return null;

            Control container = FindRealizedControlForItem(item);
            Point? currentTop = container?.TranslatePoint(new Point(0, 0), ScrollViewer);
            return currentTop?.Y;
        }

        public bool TriggerPreviousPageLoadForCurrentViewport(bool force = false) {
            LastPreviousTriggerSkipReason = String.Empty;
            ClearStalePreviousLoadIfNeeded();

            if (_currentHolder == null) {
                LastPreviousTriggerSkipReason = "no_holder";
                return false;
            }

            if (_isPreviousMessagesLoadTriggered) {
                if (!force) {
                    LastPreviousTriggerSkipReason = "already_loading_previous";
                    return false;
                }

                _previousLoadGeneration++;
                AbortPreviousMessagesLoad("force_restart");
            }

            TriggerLoadPreviousMessages();
            return true;
        }

        public async Task ScrollToBottomStableAsync(IMessageListItem target = null, int maxAttempts = 14) {
            if (ScrollViewer == null) return;

            long operationGeneration = ++_scrollToBottomGeneration;
            long manualGeneration = _manualScrollGeneration;
            bool cancelledByUser = false;
            _canChangeScroll = false;
            double lastExtent = -1;
            int settledFrames = 0;

            try {
                for (int attempt = 0; attempt < maxAttempts; attempt++) {
                    if (IsScrollToBottomCancelled(operationGeneration, manualGeneration)) {
                        cancelledByUser = true;
                        break;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() => {
                        if (target != null) ScrollIntoView(target);
                        ScrollViewer.ScrollToEnd();
                    }, DispatcherPriority.Render);

                    await Task.Delay(attempt < 4 ? 16 : 32);

                    if (IsScrollToBottomCancelled(operationGeneration, manualGeneration)) {
                        cancelledByUser = true;
                        break;
                    }

                    (double remaining, double extent) = await Dispatcher.UIThread.InvokeAsync(() => {
                        double viewportHeight = ScrollViewer.Viewport.Height;
                        if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;

                        double maxOffset = Math.Max(0, ScrollViewer.Extent.Height - viewportHeight);
                        ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, maxOffset);
                        return (Math.Max(0, maxOffset - ScrollViewer.Offset.Y), ScrollViewer.Extent.Height);
                    }, DispatcherPriority.Render);

                    bool extentSettled = Math.Abs(extent - lastExtent) < 1;
                    if (remaining <= 2 && extentSettled) {
                        settledFrames++;
                        if (settledFrames >= 2) break;
                    } else {
                        settledFrames = 0;
                    }

                    lastExtent = extent;
                }
            } finally {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    _canChangeScroll = true;
                    if (cancelledByUser || IsScrollToBottomCancelled(operationGeneration, manualGeneration)) {
                        SuppressBottomStick(BottomStickManualSuppressMs);
                    } else {
                        RefreshBottomStickGuard(BottomStickGuardMs);
                    }

                    SaveScrollPosition(true);
                }, DispatcherPriority.Render);
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            DataContextChanged += MessagesListBox_DataContextChanged;
            ScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            ScrollViewer.PointerWheelChanged += ScrollViewer_PointerWheelChanged;
            CheckDataContext();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);
            DataContextChanged -= MessagesListBox_DataContextChanged;

            if (ScrollViewer != null) {
                ScrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                ScrollViewer.PointerWheelChanged -= ScrollViewer_PointerWheelChanged;
            }

            if (_currentHolder != null) {
                _currentHolder.ScrollToMessageRequested -= ScrollToMessage;
                _currentHolder = null;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void MessagesListBox_DataContextChanged(object sender, EventArgs e) {
            CheckDataContext();
        }

        private void CheckDataContext() {
            if (_currentHolder != null) {
                _currentHolder.ScrollToMessageRequested -= ScrollToMessage;
            }
            _previousLoadGeneration++;
            _cts?.Cancel();
            _cts?.Dispose();

            _controlHolderId1 = 0;
            _controlHolderId2 = 0;
            _lastScrollOffset = Double.NaN;
            IMessagesListHolder holder = DataContext as IMessagesListHolder;
            if (holder == null) return;

            _controlHolderId1 = holder.Id;
            bool shouldRestoreSavedPosition = Settings.ChatOpenBehavior != ChatOpenBehaviorIds.Bottom && _lastPositions.ContainsKey(holder.Id);
            if (shouldRestoreSavedPosition) {
                Debug.WriteLine($"Restoring scroll for {holder.Id}...");
                RestoreScroll(_lastPositions[holder.Id]);
            } else {
                _controlHolderId2 = holder.Id;
            }
            holder.ScrollToMessageRequested += ScrollToMessage;
            _currentHolder = holder;
            _cts = new CancellationTokenSource();
        }

        private void RestoreScroll(ScrollInfo scrollInfo, double ph = -1, byte attempts = 6) {
            double h = Scroll.Extent.Height;
            if (h == 0) return;
            bool heightChanged = false;
            if (ph < 0) {
                ph = h;
            } else if (ph != h) {
                heightChanged = true;
            }
            if (h != scrollInfo.Height) {
                if (!heightChanged && attempts > 0) {
                    if (Settings.ShowDebugCounters) Debug.WriteLine($"Cannot restore scroll because height is different. Height: {h}; saved height: {scrollInfo.Height}. Trying in next frame...");
                    RequestNextFrame(() => RestoreScroll(scrollInfo, h, (byte)(attempts - 1)));
                    return;
                } else {
                    // высота равна приблизительно сохранённому значению, ок, восстановим скролл тогда, мало ли какое-то сообщение поменялось...
                    if (Settings.ShowDebugCounters) Debug.WriteLine($"Cannot restore scroll. Height is changed, but STILL WRONG!. Height: {h}; saved height: {scrollInfo.Height}.");
                    //if (h > scrollInfo.Height - 100 && h < scrollInfo.Height + 100) {

                    //} else {
                    //    TopLevel.GetTopLevel(this).RequestAnimationFrame((t) => RestoreScroll(scrollInfo, h));
                    //    return;
                    //}
                }
            }

            Scroll.Offset = new Vector(Scroll.Offset.X, scrollInfo.Offset);

            double o = Scroll.Offset.Y;
            double oDiff = o - scrollInfo.Offset;
            if ((oDiff > 4 || oDiff < -4) && attempts > 0) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"Cannot restore scroll because offset is different. Offset: {o}; saved offset: {scrollInfo.Offset}. Trying in next frame...");
                RequestNextFrame(() => RestoreScroll(scrollInfo, -1, (byte)(attempts - 1)));
                return;
            }

            _controlHolderId2 = _controlHolderId1;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            ClearStalePreviousLoadIfNeeded();
            if (_isRestoringLayoutAnchor || _isApplyingBottomStick || _isRestoringPreviousLoad) return;

            double o = Scroll.Offset.Y;
            double previousOffset = _lastScrollOffset;
            _lastScrollOffset = o;
            bool hasScrollDirection = !Double.IsNaN(previousOffset);
            bool isMovingUp = hasScrollDirection && o < previousOffset - ScrollDirectionTolerance;
            bool isMovingDown = hasScrollDirection && o > previousOffset + ScrollDirectionTolerance;
            bool isOk = _controlHolderId1 != 0 && _controlHolderId1 == _controlHolderId2 && _canChangeScroll;
            bool offsetChanged = Math.Abs(e.OffsetDelta.Y) > ScrollDirectionTolerance;
            bool extentChanged = Math.Abs(e.ExtentDelta.Y) > 1;
            bool isUserScroll = offsetChanged && (!extentChanged || IsBottomStickSuppressed());

            if (_isPreviousMessagesLoadTriggered
                && _previousRestoreTriggerTicks != 0
                && offsetChanged
                && !extentChanged
                && !IsPreviousRestoreManualDetectionSuppressed()) {
                RegisterManualScrollIntent();
                return;
            }

            if (_canChangeScroll && offsetChanged && isMovingUp && !extentChanged) RegisterManualScrollIntent();

            if (IsPreviousLoadAwaitingData()) {
                TrackPreviousLoadPendingScroll(e, o);
                return;
            }

            if (isOk) {
                if (offsetChanged && isMovingUp) SuppressBottomStick(BottomStickManualSuppressMs);
                if (IsNearBottom(BottomPinnedTolerance) && (!isUserScroll || isMovingDown || !hasScrollDirection)) RefreshBottomStickGuard(BottomStickGuardMs);

                if (isUserScroll) {
                    SaveScrollPosition();
                    TryTriggerIncrementalLoading(o, Scroll.Viewport.Height, Scroll.Extent.Height, isMovingUp, isMovingDown);
                    if (!IsBottomStickGuardActive() && !IsNearBottom(BottomStickTolerance)) RefreshLayoutAnchorGuard(LayoutAnchorGuardMs);
                    return;
                }

                if (e.ExtentDelta.Y > 1 && TryApplyBottomStickGuard(offsetChanged)) {
                    SaveScrollPosition(true);
                    return;
                }

                if (extentChanged && TryApplyLayoutAnchorGuard()) {
                    SaveScrollPosition(true);
                    return;
                }

                // Saving scroll
                SaveScrollPosition();
            }
        }

        private void ScrollViewer_PointerWheelChanged(object sender, PointerWheelEventArgs e) {
            if (Math.Abs(e.Delta.Y) < 0.01) return;

            RegisterManualScrollIntent();
            if (!IsNearBottom(BottomPinnedTolerance)) RefreshLayoutAnchorGuard(LayoutAnchorGuardMs);
        }

        private void SaveScrollPosition(bool force = false) {
            long now = Stopwatch.GetTimestamp();
            if (!force && _lastScrollSaveTicks != 0) {
                double elapsedMs = (now - _lastScrollSaveTicks) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < 80) return;
            }

            _lastScrollSaveTicks = now;
            if (Settings.ShowDebugCounters) Debug.WriteLine($"Saving scroll for {_controlHolderId1}: {Scroll.Offset.Y}/{Scroll.Extent.Height}");

            ScrollInfo scrollInfo = new ScrollInfo(Scroll.Extent.Height, Scroll.Offset.Y);
            if (_lastPositions.ContainsKey(_controlHolderId1)) {
                _lastPositions[_controlHolderId1] = scrollInfo;
            } else {
                _lastPositions.Add(_controlHolderId1, scrollInfo);
            }
        }

        private void TryTriggerIncrementalLoading(double offset, double viewportHeight, double extentHeight, bool isMovingUp, bool isMovingDown) {
            if (extentHeight <= viewportHeight * 2 || IsIncrementalLoadSuppressed()) return;

            if (offset < viewportHeight && !_isPreviousMessagesLoadTriggered && isMovingUp) {
                if (Settings.ShowDebugCounters) Debug.WriteLine("Load previous");
                TriggerLoadPreviousMessages();
            } else if (offset > extentHeight - viewportHeight - viewportHeight && !_isPreviousMessagesLoadTriggered && !_isNextMessagesLoadTriggered && isMovingDown) {
                if (Settings.ShowDebugCounters) Debug.WriteLine("Load next");
                TriggerLoadNextMessages();
            }
        }

        private void TriggerLoadPreviousMessages() {
            _ = TriggerLoadPreviousMessagesAsync();
        }

        private async Task TriggerLoadPreviousMessagesAsync() {
            IMessagesListHolder holder = _currentHolder;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;
            if (holder == null || token.IsCancellationRequested) return;

            long operationId = ++_previousLoadGeneration;
            bool restoreScheduled = false;

            _isPreviousMessagesLoadTriggered = true;
            _canChangeScroll = true;
            _previousRestoreTriggerTicks = 0;
            _previousLoadSnapshotOffset = Double.NaN;
            _previousLoadUserOffsetDelta = 0;
            _previousRestoreManualGeneration = 0;
            _previousRestoreUserInterrupted = false;
            LastPreviousLoadUserOffsetDelta = 0;
            LastPreviousTriggerSkipReason = String.Empty;
            SuppressBottomStick(PreviousLoadStaleMs + 1000);

            try {
                ScrollSnapshot snapshot = await CaptureStableScrollSnapshotAsync();
                if (!IsCurrentPreviousLoadOperation(operationId, holder, token)) {
                    if (operationId == _previousLoadGeneration) AbortPreviousMessagesLoad("cancelled");
                    return;
                }

                ScrollAnchor anchor = snapshot.Anchor;
                double oldHeight = snapshot.Height;
                double oldOffset = snapshot.Offset;
                ItemsState oldItemsState = CaptureItemsState();

                _previousLoadSnapshotOffset = oldOffset;
                _activePreviousRestoreAnchor = anchor;
                LastPreviousRestoreDrift = Double.NaN;
                LastPreviousRestoreOldOffset = oldOffset;
                LastPreviousRestoreOldHeight = oldHeight;
                LastPreviousRestoreFinalOffset = Double.NaN;
                LastPreviousRestoreFinalHeight = Double.NaN;
                LastPreviousRestoreAnchorId = anchor.Item?.Id ?? 0;

                await holder.LoadPreviousMessagesAsync(token);
                if (!IsCurrentPreviousLoadOperation(operationId, holder, token)) {
                    if (operationId == _previousLoadGeneration) AbortPreviousMessagesLoad("cancelled");
                    return;
                }

                ItemsState newItemsState = CaptureItemsState();
                if (!newItemsState.HasVisiblePrependComparedTo(oldItemsState)) {
                    AbortPreviousMessagesLoad($"no_prepend:{oldItemsState.Count}/{oldItemsState.FirstId}->{newItemsState.Count}/{newItemsState.FirstId}", true);
                    return;
                }

                ScrollAnchor adjustedAnchor = ApplyPendingUserScrollToAnchor(anchor);
                _activePreviousRestoreAnchor = adjustedAnchor;
                LastPreviousRestoreAnchorId = adjustedAnchor.Item?.Id ?? 0;
                LastPreviousLoadUserOffsetDelta = _previousLoadUserOffsetDelta;

                _restoreScrollAttempts = RestorePreviousLoadAttempts;
                _restoreScrollLastHeight = Double.NaN;
                _restoreScrollStableFrames = 0;
                _previousRestoreTriggerTicks = Stopwatch.GetTimestamp();
                _previousRestoreManualGeneration = _manualScrollGeneration;
                _previousRestoreUserInterrupted = false;
                _canChangeScroll = false;
                restoreScheduled = true;
                TryRestoreScroll(adjustedAnchor, oldHeight, oldOffset);
            } catch (OperationCanceledException) {
                if (operationId == _previousLoadGeneration) AbortPreviousMessagesLoad("cancelled");
            } catch (Exception ex) {
                Log.Error(ex, "Previous messages load/restore failed.");
                if (operationId == _previousLoadGeneration) AbortPreviousMessagesLoad("exception", true);
            } finally {
                if (!restoreScheduled && _isPreviousMessagesLoadTriggered && operationId == _previousLoadGeneration) {
                    AbortPreviousMessagesLoad(String.IsNullOrWhiteSpace(LastPreviousTriggerSkipReason) ? "aborted" : LastPreviousTriggerSkipReason, true);
                }
            }
        }

        byte _restoreScrollAttempts = 10;
        private void TryRestoreScroll(ScrollAnchor anchor, double oldHeight, double oldOffset) {
            if (!_isPreviousMessagesLoadTriggered) return;

            if (WasPreviousRestoreInterruptedByUser()) {
                LastPreviousTriggerSkipReason = "manual_scroll_during_previous_restore";
                FinishPreviousMessagesLoadRestore(false);
                return;
            }

            if (_restoreScrollAttempts == 0 || IsPreviousLoadStale()) {
                string reason = _restoreScrollAttempts == 0 ? "restore_attempts_exhausted" : "restore_timeout";
                if (TryFinishPreviousRestoreWithAnchor(anchor, reason)) return;

                double fallbackHeight = Scroll.Extent.Height;
                double fallbackDiff = Math.Max(0, fallbackHeight - oldHeight);
                if (fallbackDiff > 0) EnsurePreviousRestoreApproximateOffset(oldOffset, fallbackDiff);
                LastPreviousTriggerSkipReason = reason;
                FinishPreviousMessagesLoadRestore(false);
                return;
            }
            _restoreScrollAttempts--;

            _canChangeScroll = false;
            double newHeight = Scroll.Extent.Height;
            double diff = newHeight - oldHeight;
            if (Settings.ShowDebugCounters) Debug.WriteLine($"Trying to restore scroll position after previous messages loaded. Old height: {oldHeight}, new height: {newHeight}, diff: {diff}");
            if (diff > 0) EnsurePreviousRestoreApproximateOffset(oldOffset, diff);

            bool heightStable = !Double.IsNaN(_restoreScrollLastHeight) && Math.Abs(newHeight - _restoreScrollLastHeight) <= 1;
            _restoreScrollLastHeight = newHeight;
            _restoreScrollStableFrames = heightStable
                ? (byte)Math.Min(_restoreScrollStableFrames + 1, RestoreRequiredStableFrames)
                : (byte)0;

            if (diff <= 0) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"Extent is not increased after previous messages load. Trying in next frame, attempts: {_restoreScrollAttempts}.");
                RequestNextFrame(() => TryRestoreScroll(anchor, oldHeight, oldOffset));
                return;
            }

            if (anchor.IsValid) {
                if (TryRestorePreviousAnchor(anchor)) {
                    if (Settings.ShowDebugCounters) Debug.WriteLine("Scroll anchor successfully restored.");
                    _restoreScrollAttempts = RestorePostAnchorAttempts;
                    _restoreScrollLastHeight = Double.NaN;
                    _restoreScrollStableFrames = 0;
                    StabilizeRestoredAnchor(anchor, oldHeight, oldOffset);
                    return;
                }

                if (_restoreScrollStableFrames < RestoreRequiredStableFrames) {
                    if (TryFinishPreviousRestoreByHeightDiff(oldOffset, diff, "height_diff_restored")) return;

                    if (Settings.ShowDebugCounters) Debug.WriteLine($"Extent is not stable after previous messages load. Trying in next frame, attempts: {_restoreScrollAttempts}.");
                    RequestNextFrame(() => TryRestoreScroll(anchor, oldHeight, oldOffset));
                    return;
                }

                if (Settings.ShowDebugCounters) Debug.WriteLine("Anchor container is not reliable after previous messages load. Finishing by height diff.");
                RestoreByHeightDiff(oldOffset, newHeight, diff);
                return;
            }

            RestoreByHeightDiff(oldOffset, newHeight, diff);
        }

        private void StabilizeRestoredAnchor(ScrollAnchor anchor, double oldHeight, double oldOffset) {
            if (!_isPreviousMessagesLoadTriggered) return;

            if (WasPreviousRestoreInterruptedByUser()) {
                LastPreviousTriggerSkipReason = "manual_scroll_during_previous_restore";
                FinishPreviousMessagesLoadRestore(false);
                return;
            }

            if (_restoreScrollAttempts == 0 || IsPreviousLoadStale()) {
                LastPreviousTriggerSkipReason = _restoreScrollAttempts == 0 ? "stabilize_attempts_exhausted" : "stabilize_timeout";
                TryRestorePreviousAnchor(anchor);
                FinishPreviousMessagesLoadRestore();
                return;
            }
            _restoreScrollAttempts--;

            _canChangeScroll = false;
            double newHeight = Scroll.Extent.Height;
            _restoreScrollLastHeight = newHeight;

            bool anchorStable = TryRestorePreviousAnchor(anchor);
            _restoreScrollStableFrames = anchorStable
                ? (byte)Math.Min(_restoreScrollStableFrames + 1, RestoreRequiredStableFrames)
                : (byte)0;

            if (_restoreScrollStableFrames >= RestoreRequiredStableFrames) {
                FinishPreviousMessagesLoadRestore();
                return;
            }

            double diff = Math.Max(0, newHeight - oldHeight);
            if (!anchorStable && diff > 0) {
                if (TryFinishPreviousRestoreByHeightDiff(oldOffset, diff, "height_diff_restored")) return;
            }

            RequestNextFrame(() => StabilizeRestoredAnchor(anchor, oldHeight, oldOffset));
        }

        private bool TryFinishPreviousRestoreByHeightDiff(double oldOffset, double heightDiff, string reason) {
            if (heightDiff <= 0) return false;

            EnsurePreviousRestoreApproximateOffset(oldOffset, heightDiff);
            UpdatePreviousRestoreDiagnosticsByHeightDiff();
            if (Double.IsNaN(LastPreviousRestoreDrift) || Math.Abs(LastPreviousRestoreDrift) > 3) return false;

            LastPreviousTriggerSkipReason = reason;
            FinishPreviousMessagesLoadRestore(false);
            return true;
        }

        private void TriggerLoadNextMessages() {
            _ = TriggerLoadNextMessagesAsync();
        }

        private async Task TriggerLoadNextMessagesAsync() {
            IMessagesListHolder holder = _currentHolder;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;
            if (holder == null || token.IsCancellationRequested) return;

            _isNextMessagesLoadTriggered = true;
            bool shouldStickToBottom = IsNearBottom(BottomPinnedTolerance);
            long manualGeneration = _manualScrollGeneration;

            try {
                await holder.LoadNextMessagesAsync(token);
                if (!token.IsCancellationRequested
                    && ReferenceEquals(holder, _currentHolder)
                    && shouldStickToBottom
                    && manualGeneration == _manualScrollGeneration
                    && IsNearBottom(BottomStickTolerance)) {
                    await ScrollToBottomStableAsync(null, 10);
                }
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Error(ex, "Next messages load/restore failed.");
            } finally {
                _isNextMessagesLoadTriggered = false;
            }
        }

        private void ScrollToMessage(object sender, IMessageListItem e) {
            if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage requested. Message ID: {e.Id}");
            ScrollToMessage(e);
        }

        private void ScrollToMessage(IMessageListItem e) {
            if (IsLastItem(e)) {
                _ = ScrollToBottomStableAsync(e, 12);
                return;
            }

            _canChangeScroll = false;
            ScrollIntoView(e);
            TryFinishScrollToMessage(e, 10);
        }

        private void TryFinishScrollToMessage(IMessageListItem e, byte attempts) {
            Control item = ContainerFromItem(e);

            if (item == null) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage: UI for message {e.Id} not created yet.");
                if (attempts > 0) {
                    RequestNextFrame(() => TryFinishScrollToMessage(e, (byte)(attempts - 1)));
                } else {
                    _canChangeScroll = true;
                }
                return;
            }

            if (!item.IsLoaded) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage: UI for message {e.Id} not loaded yet! Trying in another frame...");
                if (attempts > 0) {
                    RequestNextFrame(() => TryFinishScrollToMessage(e, (byte)(attempts - 1)));
                } else {
                    _canChangeScroll = true;
                }
                return;
            }

            item.BringIntoView();
            _canChangeScroll = true;
        }

        private ScrollAnchor CaptureTopAnchor() {
            if (ScrollViewer == null) return default;

            ScrollAnchor containerAnchor = CaptureTopAnchorFromRealizedContainers();
            if (containerAnchor.IsValid) return containerAnchor;

            double viewportHeight = ScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;
            double maxProbe = Math.Min(Math.Max(48, viewportHeight), 240);

            for (double y = 4; y <= maxProbe; y += 24) {
                foreach (double x in GetProbeXPositions()) {
                    if (ScrollViewer.GetVisualAt(new Point(x, y)) is not Control visual) continue;
                    if (FindDataContext<IMessageListItem>(visual) is not IMessageListItem item) continue;

                    Control container = FindRealizedControlForItem(item);
                    double top = container?.TranslatePoint(new Point(0, 0), ScrollViewer)?.Y ?? y;
                    return new ScrollAnchor(item, top);
                }
            }

            return default;
        }

        private async Task<ScrollSnapshot> CaptureStableScrollSnapshotAsync() {
            ScrollSnapshot snapshot = default;
            double lastHeight = Double.NaN;
            double lastOffset = Double.NaN;
            byte stableFrames = 0;

            for (byte attempt = 0; attempt < 8; attempt++) {
                snapshot = await Dispatcher.UIThread.InvokeAsync(() => {
                    if (ScrollViewer == null) return default;
                    return new ScrollSnapshot(CaptureTopAnchor(), ScrollViewer.Extent.Height, ScrollViewer.Offset.Y);
                }, DispatcherPriority.Render);

                bool stable = !Double.IsNaN(lastHeight)
                    && Math.Abs(snapshot.Height - lastHeight) <= 1
                    && Math.Abs(snapshot.Offset - lastOffset) <= 1;

                if (stable) {
                    stableFrames++;
                    if (stableFrames >= 2) break;
                } else {
                    stableFrames = 0;
                }

                lastHeight = snapshot.Height;
                lastOffset = snapshot.Offset;
                await Task.Delay(16);
            }

            return snapshot;
        }

        private bool IsCurrentPreviousLoadOperation(long operationId, IMessagesListHolder holder, CancellationToken token) {
            return operationId == _previousLoadGeneration
                && ReferenceEquals(holder, _currentHolder)
                && !token.IsCancellationRequested;
        }

        private bool IsPreviousLoadAwaitingData() {
            return _isPreviousMessagesLoadTriggered && _previousRestoreTriggerTicks == 0;
        }

        private void TrackPreviousLoadPendingScroll(ScrollChangedEventArgs e, double currentOffset) {
            if (Double.IsNaN(_previousLoadSnapshotOffset)) return;

            bool userOffsetChanged = Math.Abs(e.OffsetDelta.Y) > ScrollDirectionTolerance;
            bool contentExtentStable = Math.Abs(e.ExtentDelta.Y) <= 1;
            if (!userOffsetChanged || !contentExtentStable) return;

            _previousLoadUserOffsetDelta = currentOffset - _previousLoadSnapshotOffset;
        }

        private ScrollAnchor ApplyPendingUserScrollToAnchor(ScrollAnchor anchor) {
            if (!anchor.IsValid || Math.Abs(_previousLoadUserOffsetDelta) <= ScrollDirectionTolerance) return anchor;

            double viewportHeight = ScrollViewer?.Viewport.Height ?? 0;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer?.Bounds.Height ?? 0;
            if (viewportHeight <= 0) viewportHeight = 600;

            double adjustedTop = anchor.Top - _previousLoadUserOffsetDelta;
            adjustedTop = Math.Clamp(adjustedTop, -viewportHeight * 0.5, viewportHeight * 1.5);
            return new ScrollAnchor(anchor.Item, adjustedTop);
        }

        private bool WasPreviousRestoreInterruptedByUser() {
            return _isPreviousMessagesLoadTriggered
                && _previousRestoreTriggerTicks != 0
                && (_previousRestoreUserInterrupted || _manualScrollGeneration != _previousRestoreManualGeneration);
        }

        private bool TryRestoreAnchor(ScrollAnchor anchor, double maxDelta = Double.PositiveInfinity) {
            if (!anchor.IsValid || ScrollViewer == null) return false;

            Control container = FindRealizedControlForItem(anchor.Item);
            if (container == null) return false;

            Point? currentTop = container.TranslatePoint(new Point(0, 0), ScrollViewer);
            if (currentTop == null) return false;

            double delta = currentTop.Value.Y - anchor.Top;
            if (Math.Abs(delta) <= 1) return true;
            if (Math.Abs(delta) > maxDelta) return false;

            double maxOffset = Math.Max(0, ScrollViewer.Extent.Height - ScrollViewer.Viewport.Height);
            double nextOffset = Math.Clamp(ScrollViewer.Offset.Y + delta, 0, maxOffset);
            ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, nextOffset);

            Control updatedContainer = FindRealizedControlForItem(anchor.Item);
            Point? updatedTop = updatedContainer?.TranslatePoint(new Point(0, 0), ScrollViewer);
            if (updatedTop == null) return false;

            return Math.Abs(updatedTop.Value.Y - anchor.Top) <= 3;
        }

        private ScrollAnchor CaptureTopAnchorFromRealizedContainers() {
            if (ScrollViewer == null || ItemsSource is not IEnumerable source) return default;

            double viewportHeight = ScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;
            if (viewportHeight <= 0) return default;

            ScrollAnchor best = default;
            double bestScore = Double.MaxValue;

            foreach (object value in source) {
                if (value is not IMessageListItem item) continue;
                if (!TryGetContainerVerticalBounds(value, out double top, out double bottom)) continue;
                if (bottom <= 0 || top >= viewportHeight) continue;

                double score = top >= 0 ? top : viewportHeight + Math.Abs(top);
                if (score >= bestScore) continue;

                best = new ScrollAnchor(item, top);
                bestScore = score;
            }

            return best;
        }

        private T GetVisibleItem<T>(bool last) where T : class {
            if (ScrollViewer == null || ItemsSource is not IEnumerable source) return default;

            double viewportHeight = ScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;
            if (viewportHeight <= 0) return default;

            T best = default;
            double bestScore = Double.MaxValue;

            foreach (object value in source) {
                if (value is not T typed) continue;
                if (!TryGetContainerVerticalBounds(value, out double top, out double bottom)) continue;
                if (bottom <= 0 || top >= viewportHeight) continue;

                double score = last
                    ? bottom <= viewportHeight ? viewportHeight - bottom : viewportHeight + (bottom - viewportHeight)
                    : top >= 0 ? top : viewportHeight + Math.Abs(top);

                if (score >= bestScore) continue;
                best = typed;
                bestScore = score;
            }

            return best;
        }

        private bool TryGetContainerVerticalBounds(object item, out double top, out double bottom) {
            top = 0;
            bottom = 0;

            Control container = FindRealizedControlForItem(item);
            if (container == null || container.Bounds.Height <= 0) return false;

            Point? translated = container.TranslatePoint(new Point(0, 0), ScrollViewer);
            if (translated == null) return false;

            top = translated.Value.Y;
            bottom = top + container.Bounds.Height;
            return true;
        }

        private Control FindRealizedControlForItem(object item) {
            if (item == null) return null;

            Control container = ContainerFromItem(item);
            if (container != null) return container;

            if (item is IMessageListItem) {
                object currentItem = FindCurrentItemById(item);
                if (currentItem != null) {
                    container = ContainerFromItem(currentItem);
                    if (container != null) return container;
                }
            }

            foreach (var descendant in this.GetVisualDescendants()) {
                if (descendant is not Control control) continue;
                if (!IsSameListItem(control.DataContext, item)) continue;

                Control current = control;
                while (current?.Parent is Control parent && !ReferenceEquals(parent, this)) {
                    if (parent is ListBoxItem) return parent;
                    current = parent;
                }

                return current;
            }

            return null;
        }

        private object FindCurrentItemById(object item) {
            if (ItemsSource is not IEnumerable source) return null;

            foreach (object candidate in source) {
                if (IsSameListItem(candidate, item)) return candidate;
            }

            return null;
        }

        private static bool IsSameListItem(object candidate, object target) {
            if (candidate == null || target == null) return false;
            if (ReferenceEquals(candidate, target)) return true;

            return candidate is IMessageListItem candidateItem
                && target is IMessageListItem targetItem
                && candidateItem.Id != 0
                && candidateItem.Id == targetItem.Id;
        }

        private IEnumerable<double> GetProbeXPositions() {
            double width = ScrollViewer?.Bounds.Width ?? Bounds.Width;
            if (width <= 0) width = Bounds.Width;
            if (width <= 0) {
                yield return 64;
                yield break;
            }

            yield return Math.Min(64, Math.Max(8, width - 8));
            yield return Math.Clamp(width * 0.5, 8, Math.Max(8, width - 8));
            yield return Math.Clamp(width - 64, 8, Math.Max(8, width - 8));
        }

        private static T FindDataContext<T>(Control visual) where T : class {
            Control current = visual;
            while (current != null) {
                if (current.DataContext is T target) return target;
                current = current.Parent as Control;
            }

            return null;
        }

        private void EnsureApproximateOffset(double oldOffset, double heightDiff) {
            if (ScrollViewer == null || heightDiff <= 0) return;

            double restoredOffset = GetHeightDiffRestoredOffset(oldOffset, heightDiff);
            if (Math.Abs(ScrollViewer.Offset.Y - restoredOffset) <= 1) return;

            ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, restoredOffset);
        }

        private void EnsurePreviousRestoreApproximateOffset(double oldOffset, double heightDiff) {
            SuppressPreviousRestoreManualDetection(120);
            _isRestoringPreviousLoad = true;
            try {
                EnsureApproximateOffset(oldOffset, heightDiff);
            } finally {
                _isRestoringPreviousLoad = false;
                SuppressPreviousRestoreManualDetection(120);
            }
        }

        private void RestoreByHeightDiff(double oldOffset, double newHeight, double heightDiff) {
            RestoreByHeightDiff(oldOffset, newHeight, heightDiff, true);
        }

        private void RestoreByHeightDiff(double oldOffset, double newHeight, double heightDiff, bool finishRestore) {
            double restoredOffset = GetHeightDiffRestoredOffset(oldOffset, heightDiff);
            SuppressPreviousRestoreManualDetection(120);
            _isRestoringPreviousLoad = true;
            try {
                Scroll.Offset = new Vector(Scroll.Offset.X, restoredOffset);
            } finally {
                _isRestoringPreviousLoad = false;
                SuppressPreviousRestoreManualDetection(120);
            }

            if (_controlHolderId1 != 0) {
                _lastPositions[_controlHolderId1] = new ScrollInfo(newHeight, restoredOffset);
            }

            if (Settings.ShowDebugCounters) Debug.WriteLine("Scroll successfully restored by height diff.");
            if (finishRestore) FinishPreviousMessagesLoadRestore();
        }

        private double GetHeightDiffRestoredOffset(double oldOffset, double heightDiff) {
            double viewportHeight = ScrollViewer?.Viewport.Height ?? 0;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer?.Bounds.Height ?? 0;
            double maxOffset = Math.Max(0, (ScrollViewer?.Extent.Height ?? Scroll.Extent.Height) - viewportHeight);
            return Math.Clamp(oldOffset + Math.Max(0, heightDiff), 0, maxOffset);
        }

        private void FinishPreviousMessagesLoadRestore() {
            FinishPreviousMessagesLoadRestore(true);
        }

        private void FinishPreviousMessagesLoadRestore(bool preserveRestoredAnchor) {
            ScrollAnchor guardAnchor = preserveRestoredAnchor ? _activePreviousRestoreAnchor : CaptureTopAnchor();
            if (preserveRestoredAnchor) TryApplyFinalPreviousRestoreAnchor();
            _isPreviousMessagesLoadTriggered = false;
            _canChangeScroll = true;
            _previousRestoreTriggerTicks = 0;
            _previousLoadSnapshotOffset = Double.NaN;
            _previousLoadUserOffsetDelta = 0;
            _previousRestoreManualGeneration = 0;
            _previousRestoreUserInterrupted = false;
            _suppressIncrementalLoadUntilTicks = GetFutureTimestamp(IncrementalLoadSuppressMs);
            _lastScrollOffset = ScrollViewer?.Offset.Y ?? _lastScrollOffset;
            LastPreviousRestoreFinalOffset = ScrollViewer?.Offset.Y ?? Double.NaN;
            LastPreviousRestoreFinalHeight = ScrollViewer?.Extent.Height ?? Double.NaN;
            if (preserveRestoredAnchor) {
                UpdatePreviousRestoreDiagnostics();
                RequestNextFrame(UpdatePreviousRestoreDiagnostics);
            } else {
                UpdatePreviousRestoreDiagnosticsByHeightDiff();
                RequestNextFrame(UpdatePreviousRestoreDiagnosticsByHeightDiff);
            }
            RefreshLayoutAnchorGuard(preserveRestoredAnchor ? PreviousLayoutAnchorGuardMs : LayoutAnchorGuardMs, guardAnchor);
            if (preserveRestoredAnchor) SchedulePostLayoutPreviousRestore(_manualScrollGeneration, 6);
            else SchedulePostLayoutHeightDiffRestore(_manualScrollGeneration, 45);
            SaveScrollPosition(true);
        }

        private bool TryFinishPreviousRestoreWithAnchor(ScrollAnchor anchor, string exhaustedReason) {
            if (!anchor.IsValid || WasPreviousRestoreInterruptedByUser()) return false;
            if (!TryRestorePreviousAnchor(anchor)) return false;

            LastPreviousTriggerSkipReason = exhaustedReason == "restore_timeout" || exhaustedReason == "stabilize_timeout"
                ? "final_anchor_restored_after_timeout"
                : "final_anchor_restored_after_attempts";
            FinishPreviousMessagesLoadRestore(true);
            return true;
        }

        private bool TryRestorePreviousAnchor(ScrollAnchor anchor) {
            if (!anchor.IsValid || ScrollViewer == null) return false;

            double viewportHeight = ScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;
            double maxDelta = Math.Max(240, viewportHeight * 0.9);

            SuppressPreviousRestoreManualDetection(120);
            _isRestoringLayoutAnchor = true;
            try {
                return TryRestoreAnchor(anchor, maxDelta);
            } finally {
                _isRestoringLayoutAnchor = false;
                SuppressPreviousRestoreManualDetection(120);
            }
        }

        private void TryApplyFinalPreviousRestoreAnchor() {
            if (!_activePreviousRestoreAnchor.IsValid || ScrollViewer == null) return;

            TryRestorePreviousAnchor(_activePreviousRestoreAnchor);
        }

        private void SchedulePostLayoutPreviousRestore(long manualGeneration, byte attempts) {
            if (!_activePreviousRestoreAnchor.IsValid || attempts == 0 || ScrollViewer == null) return;

            RequestNextFrame(() => {
                if (!_activePreviousRestoreAnchor.IsValid
                    || ScrollViewer == null
                    || manualGeneration != _manualScrollGeneration) {
                    return;
                }

                _isRestoringLayoutAnchor = true;
                try {
                    TryRestoreAnchor(_activePreviousRestoreAnchor);
                    _lastScrollOffset = ScrollViewer.Offset.Y;
                } finally {
                    _isRestoringLayoutAnchor = false;
                }

                UpdatePreviousRestoreDiagnostics();
                SaveScrollPosition(true);

                if (!Double.IsNaN(LastPreviousRestoreDrift) && Math.Abs(LastPreviousRestoreDrift) <= 3) return;
                SchedulePostLayoutPreviousRestore(manualGeneration, (byte)(attempts - 1));
            });
        }

        private void SchedulePostLayoutHeightDiffRestore(long manualGeneration, byte attempts) {
            if (attempts == 0
                || ScrollViewer == null
                || Double.IsNaN(LastPreviousRestoreOldOffset)
                || Double.IsNaN(LastPreviousRestoreOldHeight)) {
                return;
            }

            RequestNextFrame(() => {
                if (ScrollViewer == null
                    || manualGeneration != _manualScrollGeneration) {
                    return;
                }

                _isRestoringLayoutAnchor = true;
                try {
                    double heightDiff = Math.Max(0, ScrollViewer.Extent.Height - LastPreviousRestoreOldHeight);
                    EnsureApproximateOffset(LastPreviousRestoreOldOffset, heightDiff);
                    _lastScrollOffset = ScrollViewer.Offset.Y;
                    LastPreviousRestoreFinalOffset = ScrollViewer.Offset.Y;
                    LastPreviousRestoreFinalHeight = ScrollViewer.Extent.Height;
                } finally {
                    _isRestoringLayoutAnchor = false;
                }

                UpdatePreviousRestoreDiagnosticsByHeightDiff();
                SaveScrollPosition(true);
                if (!Double.IsNaN(LastPreviousRestoreDrift) && Math.Abs(LastPreviousRestoreDrift) <= 3) return;
                SchedulePostLayoutHeightDiffRestore(manualGeneration, (byte)(attempts - 1));
            });
        }

        private void AbortPreviousMessagesLoad(string reason, bool saveScroll = false) {
            LastPreviousTriggerSkipReason = reason;
            _isPreviousMessagesLoadTriggered = false;
            _canChangeScroll = true;
            _previousRestoreTriggerTicks = 0;
            _previousLoadSnapshotOffset = Double.NaN;
            _previousLoadUserOffsetDelta = 0;
            _previousRestoreManualGeneration = 0;
            _previousRestoreUserInterrupted = false;
            _lastScrollOffset = ScrollViewer?.Offset.Y ?? _lastScrollOffset;
            if (saveScroll) SaveScrollPosition(true);
        }

        private bool IsPreviousLoadStale() {
            if (!_isPreviousMessagesLoadTriggered || _previousRestoreTriggerTicks == 0) return false;

            double elapsedMs = (Stopwatch.GetTimestamp() - _previousRestoreTriggerTicks) * 1000.0 / Stopwatch.Frequency;
            return elapsedMs >= PreviousLoadStaleMs;
        }

        private void ClearStalePreviousLoadIfNeeded() {
            if (!IsPreviousLoadStale()) return;

            LastPreviousTriggerSkipReason = "stale_previous_load_cleared";
            if (_previousRestoreTriggerTicks != 0) {
                FinishStalePreviousRestore();
                return;
            }

            AbortPreviousMessagesLoad("stale_previous_load_cleared", true);
        }

        private void FinishStalePreviousRestore() {
            if (ScrollViewer == null) {
                AbortPreviousMessagesLoad("stale_previous_load_no_scrollviewer", true);
                return;
            }

            if (TryFinishPreviousRestoreWithAnchor(_activePreviousRestoreAnchor, "restore_timeout")) return;

            double heightDiff = Math.Max(0, ScrollViewer.Extent.Height - LastPreviousRestoreOldHeight);
            if (!Double.IsNaN(LastPreviousRestoreOldOffset) && heightDiff > 0) {
                EnsureApproximateOffset(LastPreviousRestoreOldOffset, heightDiff);
            }

            FinishPreviousMessagesLoadRestore(false);
        }

        private bool IsIncrementalLoadSuppressed() {
            return _suppressIncrementalLoadUntilTicks != 0 && Stopwatch.GetTimestamp() < _suppressIncrementalLoadUntilTicks;
        }

        private void SuppressPreviousRestoreManualDetection(double milliseconds) {
            if (milliseconds <= 0) return;

            _suppressPreviousRestoreManualDetectionUntilTicks = GetFutureTimestamp(milliseconds);
        }

        private bool IsPreviousRestoreManualDetectionSuppressed() {
            if (_suppressPreviousRestoreManualDetectionUntilTicks == 0) return false;

            if (Stopwatch.GetTimestamp() <= _suppressPreviousRestoreManualDetectionUntilTicks) return true;

            _suppressPreviousRestoreManualDetectionUntilTicks = 0;
            return false;
        }

        private static long GetFutureTimestamp(double delayMs) {
            return Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * delayMs / 1000.0);
        }

        private bool IsBottomStickGuardActive() {
            if (IsBottomStickSuppressed()) return false;
            if (_bottomStickGuardUntilTicks == 0) return false;

            if (Stopwatch.GetTimestamp() <= _bottomStickGuardUntilTicks) return true;

            ClearBottomStickGuard();
            return false;
        }

        private void RefreshBottomStickGuard(double milliseconds) {
            if (milliseconds <= 0 || ScrollViewer == null) {
                ClearBottomStickGuard();
                return;
            }

            if (IsBottomStickSuppressed()) {
                ClearBottomStickGuard();
                return;
            }

            if (!IsNearBottom(BottomPinnedTolerance)) {
                ClearBottomStickGuard();
                return;
            }

            _bottomStickGuardUntilTicks = GetFutureTimestamp(milliseconds);
            ClearLayoutAnchorGuard();
        }

        private void ClearBottomStickGuard() {
            _bottomStickGuardUntilTicks = 0;
        }

        private bool IsBottomStickSuppressed() {
            if (_bottomStickSuppressedUntilTicks == 0) return false;

            if (Stopwatch.GetTimestamp() <= _bottomStickSuppressedUntilTicks) return true;

            _bottomStickSuppressedUntilTicks = 0;
            return false;
        }

        private void SuppressBottomStick(double milliseconds) {
            ClearBottomStickGuard();
            if (milliseconds <= 0) return;

            _bottomStickSuppressedUntilTicks = GetFutureTimestamp(milliseconds);
        }

        private void RegisterManualScrollIntent() {
            _manualScrollGeneration++;
            _scrollToBottomGeneration++;
            SuppressBottomStick(BottomStickManualSuppressMs);
            if (_isPreviousMessagesLoadTriggered && _previousRestoreTriggerTicks != 0) {
                CancelPreviousRestoreForManualScroll();
            }
        }

        private void CancelPreviousRestoreForManualScroll() {
            _previousRestoreUserInterrupted = true;
            LastPreviousTriggerSkipReason = "manual_scroll_during_previous_restore";
            _isPreviousMessagesLoadTriggered = false;
            _canChangeScroll = true;
            _previousRestoreTriggerTicks = 0;
            _previousLoadSnapshotOffset = Double.NaN;
            _previousLoadUserOffsetDelta = 0;
            _previousRestoreManualGeneration = 0;
            _suppressIncrementalLoadUntilTicks = GetFutureTimestamp(IncrementalLoadSuppressMs);
            _lastScrollOffset = ScrollViewer?.Offset.Y ?? _lastScrollOffset;
            LastPreviousRestoreFinalOffset = ScrollViewer?.Offset.Y ?? Double.NaN;
            LastPreviousRestoreFinalHeight = ScrollViewer?.Extent.Height ?? Double.NaN;
            UpdatePreviousRestoreDiagnostics();
            ClearLayoutAnchorGuard();
            RefreshLayoutAnchorGuard(LayoutAnchorGuardMs, CaptureTopAnchor());
            SaveScrollPosition(true);
        }

        private bool IsScrollToBottomCancelled(long operationGeneration, long manualGeneration) {
            return _scrollToBottomGeneration != operationGeneration
                || _manualScrollGeneration != manualGeneration;
        }

        private double GetMaxOffset() {
            if (ScrollViewer == null) return 0;

            double viewportHeight = ScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = ScrollViewer.Bounds.Height;

            return Math.Max(0, ScrollViewer.Extent.Height - viewportHeight);
        }

        private bool TryApplyBottomStickGuard(bool offsetChanged) {
            if (ScrollViewer == null || !IsBottomStickGuardActive() || _isPreviousMessagesLoadTriggered) return false;
            if (offsetChanged && !IsNearBottom(BottomPinnedTolerance)) {
                ClearBottomStickGuard();
                return false;
            }

            double maxOffset = GetMaxOffset();
            if (maxOffset < 0) return false;
            if (Math.Abs(ScrollViewer.Offset.Y - maxOffset) <= 1) return false;

            _isApplyingBottomStick = true;
            try {
                ScrollViewer.Offset = new Vector(ScrollViewer.Offset.X, maxOffset);
                _lastScrollOffset = ScrollViewer.Offset.Y;
                _bottomStickGuardUntilTicks = GetFutureTimestamp(BottomStickGuardMs);
                ClearLayoutAnchorGuard();
                return true;
            } finally {
                _isApplyingBottomStick = false;
            }
        }

        private void RefreshLayoutAnchorGuard(double milliseconds, ScrollAnchor anchor = default) {
            if (milliseconds <= 0 || ScrollViewer == null || IsBottomStickGuardActive() || IsNearBottom(BottomStickTolerance)) {
                ClearLayoutAnchorGuard();
                return;
            }

            ScrollAnchor nextAnchor = anchor.IsValid ? anchor : CaptureTopAnchor();
            if (!nextAnchor.IsValid) {
                ClearLayoutAnchorGuard();
                return;
            }

            _layoutAnchorGuard = nextAnchor;
            _layoutAnchorGuardUntilTicks = GetFutureTimestamp(milliseconds);
        }

        private void ClearLayoutAnchorGuard() {
            _layoutAnchorGuard = default;
            _layoutAnchorGuardUntilTicks = 0;
        }

        private bool TryApplyLayoutAnchorGuard() {
            if (!_layoutAnchorGuard.IsValid || _layoutAnchorGuardUntilTicks == 0 || ScrollViewer == null) return false;

            if (Stopwatch.GetTimestamp() > _layoutAnchorGuardUntilTicks || IsBottomStickGuardActive() || IsNearBottom(BottomStickTolerance)) {
                ClearLayoutAnchorGuard();
                return false;
            }

            if (FindRealizedControlForItem(_layoutAnchorGuard.Item) == null) return false;

            _isRestoringLayoutAnchor = true;
            try {
                bool restored = TryRestoreAnchor(_layoutAnchorGuard);
                if (restored) {
                    _lastScrollOffset = ScrollViewer.Offset.Y;
                    _layoutAnchorGuardUntilTicks = GetFutureTimestamp(LayoutAnchorGuardMs);
                }

                return restored;
            } finally {
                _isRestoringLayoutAnchor = false;
            }
        }

        private void UpdatePreviousRestoreDiagnostics() {
            if (!_activePreviousRestoreAnchor.IsValid) return;

            double? currentTop = GetItemTopInViewport(_activePreviousRestoreAnchor.Item);
            LastPreviousRestoreAnchorId = _activePreviousRestoreAnchor.Item.Id;
            LastPreviousRestoreFinalOffset = ScrollViewer?.Offset.Y ?? LastPreviousRestoreFinalOffset;
            LastPreviousRestoreFinalHeight = ScrollViewer?.Extent.Height ?? LastPreviousRestoreFinalHeight;
            if (currentTop == null) {
                UpdatePreviousRestoreDiagnosticsByHeightDiff();
                return;
            }

            LastPreviousRestoreDrift = currentTop.Value - _activePreviousRestoreAnchor.Top;
            if (Math.Abs(LastPreviousRestoreDrift) <= 6 && IsPreviousRestoreExhaustedReason()) {
                LastPreviousTriggerSkipReason = "final_anchor_restored";
            }
        }

        private void UpdatePreviousRestoreDiagnosticsByHeightDiff() {
            if (ScrollViewer == null
                || Double.IsNaN(LastPreviousRestoreOldOffset)
                || Double.IsNaN(LastPreviousRestoreOldHeight)) {
                return;
            }

            LastPreviousRestoreFinalOffset = ScrollViewer.Offset.Y;
            LastPreviousRestoreFinalHeight = ScrollViewer.Extent.Height;
            double heightDiff = Math.Max(0, LastPreviousRestoreFinalHeight - LastPreviousRestoreOldHeight);
            double expectedOffset = GetHeightDiffRestoredOffset(LastPreviousRestoreOldOffset, heightDiff);
            LastPreviousRestoreDrift = ScrollViewer.Offset.Y - expectedOffset;
            if (Math.Abs(LastPreviousRestoreDrift) <= 6 && IsPreviousRestoreExhaustedReason()) {
                LastPreviousTriggerSkipReason = "final_height_diff_restored";
            }
        }

        private bool IsPreviousRestoreExhaustedReason() {
            return LastPreviousTriggerSkipReason == "restore_attempts_exhausted"
                || LastPreviousTriggerSkipReason == "stabilize_attempts_exhausted"
                || LastPreviousTriggerSkipReason == "restore_timeout"
                || LastPreviousTriggerSkipReason == "stabilize_timeout";
        }

        private bool IsNearBottom(double tolerance) {
            if (ScrollViewer == null) return false;

            double remaining = GetMaxOffset() - ScrollViewer.Offset.Y;
            return remaining <= tolerance;
        }

        private bool IsLastItem(IMessageListItem item) {
            if (item == null || ItemsSource is not IEnumerable source) return false;

            object last = null;
            foreach (object value in source) {
                last = value;
            }

            return IsSameListItem(last, item);
        }

        private ItemsState CaptureItemsState() {
            if (ItemsSource is not IEnumerable source) return new ItemsState(0, 0);

            int count = 0;
            int firstId = 0;
            foreach (object value in source) {
                if (value is IMessageListItem item) {
                    if (firstId == 0) firstId = item.Id;
                    count++;
                }
            }

            return new ItemsState(count, firstId);
        }

        private void RequestNextFrame(Action action) {
            Dispatcher.UIThread.Post(async () => {
                await Task.Delay(16);
                action();
            }, DispatcherPriority.Render);
        }
    }
}
