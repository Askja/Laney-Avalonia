using ELOR.Laney.ViewModels.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace ELOR.Laney.Collections {
    public class MessagesCollection : ObservableCollection<MessageViewModel> {
        private int deferLevel;
        private bool hasDeferredChanges;

        public MessageViewModel First => this.FirstOrDefault();
        public MessageViewModel Last => this.LastOrDefault();

        public MessagesCollection(List<MessageViewModel> messages) {
            for (int i = 0; i < messages.Count; i++) {
                MessageViewModel message = messages[i];

                bool isPrevFromSameSender = false;
                bool isDateBetweenVisible = false;
                if (i == 0) {
                    isPrevFromSameSender = false;
                    isDateBetweenVisible = true;
                } else {
                    var prev = messages[i - 1];
                    isPrevFromSameSender = prev.SenderId == message.SenderId && prev.SentTime.Date == message.SentTime.Date && prev.Action == null;
                    isDateBetweenVisible = prev.SentTime.Date != message.SentTime.Date;
                }


                bool isNextFromSameSender = false;
                if (i == messages.Count - 1) {
                    isNextFromSameSender = false;
                } else {
                    var next = messages[i + 1];
                    isNextFromSameSender = next.SenderId == message.SenderId && next.SentTime.Date == message.SentTime.Date && next.Action == null;
                }

                message.UpdateSenderInfoView(isPrevFromSameSender, isNextFromSameSender);
                message.UpdateDateBetweenVisibility(isDateBetweenVisible);
                Items.Add(message);
            }
        }

        public void Insert(MessageViewModel message) {
            int idx = 0;

            var q = Items.Where(obj => obj is MessageViewModel msg && msg.ConversationMessageId == message.ConversationMessageId).FirstOrDefault();
            if (q != null && q is MessageViewModel old) {
                idx = Items.IndexOf(old);
                RemoveAt(idx);
            } else {
                idx = Items.ToList().BinarySearch(message);
                if (idx < 0) idx = ~idx;
            }

            Insert(idx, message);
            UpdateSenderInfoView(message);
        }

        public void InsertRange(List<MessageViewModel> messages) {
            if (messages == null || messages.Count == 0) return;

            using (DeferNotifications()) {
                foreach (var message in CollectionsMarshal.AsSpan<MessageViewModel>(messages)) {
                    Insert(message);
                }
            }
        }

        public IDisposable DeferNotifications() {
            deferLevel++;
            return new DeferredNotifications(this);
        }

        public new void Remove(MessageViewModel message) {
            int index = Items.IndexOf(message);
            if (index == -1) return;
            RemoveAt(index);
            if (Count == 0) return;
            if (index > 0) UpdateSenderInfoView(this.ElementAt(index - 1));
            if (Count > index) UpdateSenderInfoView(this.ElementAt(index));
        }

        private void UpdateSenderInfoView(MessageViewModel msg) {
            int index = IndexOf(msg);

            if (Count == 1) {
                msg.UpdateSenderInfoView(false, false);
                msg.UpdateDateBetweenVisibility(true);
            } else if (Count > 1) {
                bool isPrevFromSameSender = false;
                bool isNextFromSameSender = false;

                if (index > 0) {
                    var prev = this[index - 1];
                    isPrevFromSameSender = msg.SenderId == prev.SenderId && msg.SentTime.Date == prev.SentTime.Date;
                    prev.UpdateSenderInfoView(null, isPrevFromSameSender);
                    msg.UpdateDateBetweenVisibility(prev.SentTime.Date != msg.SentTime.Date);
                }
                if (index < Count - 1) {
                    var next = this[index + 1];
                    isNextFromSameSender = msg.SenderId == next.SenderId && msg.SentTime.Date == next.SentTime.Date;
                    next.UpdateSenderInfoView(isNextFromSameSender, null);
                    next.UpdateDateBetweenVisibility(next.SentTime.Date != msg.SentTime.Date);
                }
                msg.UpdateSenderInfoView(isPrevFromSameSender, isNextFromSameSender);
            }
        }

        public MessageViewModel GetById(int messageId) {
            return this.Where(m => m.ConversationMessageId == messageId).FirstOrDefault();
        }

        public void RemoveById(int messageId) {
            var message = GetById(messageId);
            if (message != null) Remove(message);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (deferLevel > 0) {
                hasDeferredChanges = true;
                return;
            }

            base.OnCollectionChanged(e);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (deferLevel > 0) return;
            base.OnPropertyChanged(e);
        }

        private void EndDefer() {
            if (deferLevel == 0) return;

            deferLevel--;
            if (deferLevel > 0 || !hasDeferredChanges) return;

            hasDeferredChanges = false;
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            base.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private sealed class DeferredNotifications : IDisposable {
            private MessagesCollection owner;

            public DeferredNotifications(MessagesCollection owner) {
                this.owner = owner;
            }

            public void Dispose() {
                MessagesCollection collection = owner;
                owner = null;
                collection?.EndDefer();
            }
        }
    }
}
