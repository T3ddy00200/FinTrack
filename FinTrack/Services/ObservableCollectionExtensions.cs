using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FinTrack.Services
{
    public static class ObservableCollectionExtensions
    {
        public static void ReplaceRange<T>(this ObservableCollection<T> collection, IEnumerable<T> newItems)
        {
            collection.Clear();
            foreach (var item in newItems)
                collection.Add(item);
        }
    }
}
