using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace StateMachines
{
    public class ListFragment<T> : IReadOnlyList<T>
    {
        public ListFragment(IEnumerable<T> items, bool moreDataPending)
        {
            Items = new List<T>(items);
            MoreDataPending = moreDataPending;
        }

        public ListFragment(IEnumerable<T> items, int capacity)
        {
            Items = new List<T>(items);
            MoreDataPending = Items.Count >= capacity;
        }

        public bool MoreDataPending { get; }

        private IReadOnlyList<T> Items { get; }

        public int Count => Items.Count;

        public T this[int index] => Items[index];

        public IEnumerator<T> GetEnumerator()
        {
            return Items.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Items.GetEnumerator();
        }
    }

    public static class ListFragmentExtensions
    {
        public static ListFragment<TActor> ToFragment<TActor>(this IEnumerable<TActor> items, bool moreData)
        {
            return new ListFragment<TActor>(items, moreData);
        }
        public static ListFragment<TActor> ToFragment<TActor>(this IEnumerable<TActor> items, int capacity)
        {
            return new ListFragment<TActor>(items, capacity);
        }

    }
}
