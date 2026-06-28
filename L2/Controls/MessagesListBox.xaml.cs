// https://github.com/Elorucov/MessagesListBox.Avalonia

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ELOR.Laney.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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

        private ScrollViewer ScrollViewer => Scroll as ScrollViewer;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            DataContextChanged += MessagesListBox_DataContextChanged;
            ScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            CheckDataContext();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);
            DataContextChanged -= MessagesListBox_DataContextChanged;

            if (ScrollViewer != null) {
                ScrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
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
            _cts?.Cancel();
            _cts?.Dispose();

            _controlHolderId1 = 0;
            _controlHolderId2 = 0;
            IMessagesListHolder holder = DataContext as IMessagesListHolder;
            if (holder == null) return;

            _controlHolderId1 = holder.Id;
            if (_lastPositions.ContainsKey(holder.Id)) {
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
                    TopLevel.GetTopLevel(this).RequestAnimationFrame((t) => RestoreScroll(scrollInfo, h, (byte)(attempts - 1)));
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
                TopLevel.GetTopLevel(this).RequestAnimationFrame((t) => RestoreScroll(scrollInfo, -1, (byte)(attempts - 1)));
                return;
            }

            _controlHolderId2 = _controlHolderId1;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            bool isOk = _controlHolderId1 != 0 && _controlHolderId1 == _controlHolderId2 && _canChangeScroll;

            if (isOk) {
                // Saving scroll
                SaveScrollPosition();

                // Incremental loading
                double v = Scroll.Viewport.Height;
                double h = Scroll.Extent.Height;
                double o = Scroll.Offset.Y;
                if (h > v * 2) // To trigger incremental loading correctly, scrollable height should be 3 times larger than display height.
                {
                    if (o < v && !_isPreviousMessagesLoadTriggered) // Load previous
                    {
                        if (Settings.ShowDebugCounters) Debug.WriteLine("Load previous");
                        TriggerLoadPreviousMessages();
                    } else if (o > h - v - v && !_isNextMessagesLoadTriggered) // Load next
                      {
                        if (Settings.ShowDebugCounters) Debug.WriteLine("Load next");
                        TriggerLoadNextMessages();
                    }
                }
            }
        }

        private void SaveScrollPosition() {
            long now = Stopwatch.GetTimestamp();
            if (_lastScrollSaveTicks != 0) {
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

        private void TriggerLoadPreviousMessages() {
            new Action(async () => {
                _isPreviousMessagesLoadTriggered = true;

                await _currentHolder.LoadPreviousMessagesAsync(_cts.Token);
                if (_cts.IsCancellationRequested) {
                    _isPreviousMessagesLoadTriggered = false;
                    return;
                }

                // Immediately after the task is completed, the Scroll.Extent.Height value is still old.
                double oldHeight = Scroll.Extent.Height;
                double oldOffset = Scroll.Offset.Y;

                _restoreScrollAttempts = 10;
                TryRestoreScroll(oldHeight, oldOffset);
            })();
        }

        byte _restoreScrollAttempts = 10;
        private void TryRestoreScroll(double oldHeight, double oldOffset) {
            if (_restoreScrollAttempts == 0) {
                _isPreviousMessagesLoadTriggered = false;
                _canChangeScroll = true;
                return;
            }
            _restoreScrollAttempts--;

            _canChangeScroll = false;
            double newHeight = Scroll.Extent.Height;
            double diff = newHeight - oldHeight;
            if (Settings.ShowDebugCounters) Debug.WriteLine($"Trying to restore scroll position after previous messages loaded. Old height: {oldHeight}, new height: {newHeight}, diff: {diff}");

            if (diff > 0) // height increased
            {
                Scroll.Offset = new Vector(Scroll.Offset.X, oldOffset + diff);
                if (Settings.ShowDebugCounters) Debug.WriteLine($"Scroll successfully restored.");
                _isPreviousMessagesLoadTriggered = false;
                _canChangeScroll = true;
            } else {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"Height not changed after load messages command executed. Trying in next frame, attemps: {_restoreScrollAttempts}.");
                TopLevel.GetTopLevel(this).RequestAnimationFrame((t) => TryRestoreScroll(oldHeight, oldOffset));
            }
        }

        private void TriggerLoadNextMessages() {
            new Action(async () => {
                _isNextMessagesLoadTriggered = true;
                await _currentHolder.LoadNextMessagesAsync(_cts.Token);
                _isNextMessagesLoadTriggered = false;
            })();
        }

        private void ScrollToMessage(object sender, IMessageListItem e) {
            if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage requested. Message ID: {e.Id}");
            ScrollToMessage(e);
        }

        private void ScrollToMessage(IMessageListItem e) {
            _canChangeScroll = false;
            ScrollIntoView(e);
            TryFinishScrollToMessage(e, 10);
        }

        private void TryFinishScrollToMessage(IMessageListItem e, byte attempts) {
            Control item = ContainerFromItem(e);

            if (item == null) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage: UI for message {e.Id} not created yet.");
                if (attempts > 0) {
                    TopLevel.GetTopLevel(this)?.RequestAnimationFrame((t) => TryFinishScrollToMessage(e, (byte)(attempts - 1)));
                } else {
                    _canChangeScroll = true;
                }
                return;
            }

            if (!item.IsLoaded) {
                if (Settings.ShowDebugCounters) Debug.WriteLine($"ScrollToMessage: UI for message {e.Id} not loaded yet! Trying in another frame...");
                if (attempts > 0) {
                    TopLevel.GetTopLevel(this)?.RequestAnimationFrame((t) => TryFinishScrollToMessage(e, (byte)(attempts - 1)));
                } else {
                    _canChangeScroll = true;
                }
                return;
            }

            item.BringIntoView();
            _canChangeScroll = true;
        }
    }
}
