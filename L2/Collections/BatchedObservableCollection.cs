using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ELOR.Laney.Collections {
    public class BatchedObservableCollection<T> : ObservableCollection<T> {
        private int deferLevel;
        private bool hasDeferredChanges;

        public IDisposable DeferNotifications() {
            deferLevel++;
            return new DeferredNotifications(this);
        }

        public void AddRange(IEnumerable<T> items) {
            if (items == null) return;

            using (DeferNotifications()) {
                foreach (T item in items) Add(item);
            }
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
            private BatchedObservableCollection<T> owner;

            public DeferredNotifications(BatchedObservableCollection<T> owner) {
                this.owner = owner;
            }

            public void Dispose() {
                BatchedObservableCollection<T> collection = owner;
                owner = null;
                collection?.EndDefer();
            }
        }
    }
}
