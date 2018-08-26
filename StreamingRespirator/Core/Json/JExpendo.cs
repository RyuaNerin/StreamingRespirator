using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace StreamingRespirator.Core.Json
{
    internal class JExpendo : IDictionary<string, object>,
                              IDictionary,
                              ICollection<KeyValuePair<string, object>>,
                              ICollection,
                              IEnumerable<KeyValuePair<string, object>>,
                              IEnumerable
    {
        private Dictionary<string, object> Dic { get; } = new Dictionary<string, object>();

        /// <summary>** nullable **</summary>
        public object this[string key]
            => this.Dic.TryGetValue(key, out var value) ? value : null;

        #region IDictionary<string, object>
        [EditorBrowsable(EditorBrowsableState.Never)]
        object IDictionary<string, object>.this[string key]
        {
            get => this.Dic[key];
            set => this.Dic[key] = value;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        ICollection<string> IDictionary<string, object>.Keys
            => this.Dic.Keys;

        [EditorBrowsable(EditorBrowsableState.Never)]
        ICollection<object> IDictionary<string, object>.Values
            => this.Dic.Values;

        [EditorBrowsable(EditorBrowsableState.Never)]
        void IDictionary<string, object>.Add(string key, object value)
            => this.Dic.Add(key, value);

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary<string, object>.ContainsKey(string key)
            => this.Dic.ContainsKey(key);

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary<string, object>.Remove(string key)
            => this.Dic.Remove(key);

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary<string, object>.TryGetValue(string key, out object value)
            => this.Dic.TryGetValue(key, out value);
        #endregion

        #region IDictionary
        object IDictionary.this[object key]
        {
            get => ((IDictionary)this.Dic)[key];
            set => ((IDictionary)this.Dic)[key] = value;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        ICollection IDictionary.Keys
            => ((IDictionary)this.Dic).Keys;

        [EditorBrowsable(EditorBrowsableState.Never)]
        ICollection IDictionary.Values
            => ((IDictionary)this.Dic).Keys;

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary.IsReadOnly
            => ((IDictionary)this.Dic).IsReadOnly;

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary.IsFixedSize
            => ((IDictionary)this.Dic).IsFixedSize;

        [EditorBrowsable(EditorBrowsableState.Never)]
        void IDictionary.Add(object key, object value)
            => ((IDictionary)this.Dic).Add(key, value);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void IDictionary.Clear()
            => this.Dic.Clear();

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool IDictionary.Contains(object key)
            => ((IDictionary)this.Dic).Contains(key);

        [EditorBrowsable(EditorBrowsableState.Never)]
        IDictionaryEnumerator IDictionary.GetEnumerator()
            => ((IDictionary)this.Dic).GetEnumerator();

        [EditorBrowsable(EditorBrowsableState.Never)]
        void IDictionary.Remove(object key)
            => ((IDictionary)this.Dic).Remove(key);
        #endregion

        #region ICollection<KeyValuePair<string, object>>
        [EditorBrowsable(EditorBrowsableState.Never)]
        int ICollection<KeyValuePair<string, object>>.Count
            => this.Dic.Count;

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool ICollection<KeyValuePair<string, object>>.IsReadOnly
            => ((ICollection<KeyValuePair<string, object>>)this.Dic).IsReadOnly;

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            => ((ICollection<KeyValuePair<string, object>>)this.Dic).Add(item);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection<KeyValuePair<string, object>>.Clear()
            => this.Dic.Clear();

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
            => ((ICollection<KeyValuePair<string, object>>)this.Dic).Contains(item);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<string, object>>)this.Dic).CopyTo(array, arrayIndex);

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
            => ((ICollection<KeyValuePair<string, object>>)this.Dic).Remove(item);
        #endregion

        #region ICollection
        [EditorBrowsable(EditorBrowsableState.Never)]
        int ICollection.Count
            => this.Dic.Count;

        [EditorBrowsable(EditorBrowsableState.Never)]
        object ICollection.SyncRoot
            => ((ICollection)this.Dic).SyncRoot;

        [EditorBrowsable(EditorBrowsableState.Never)]
        bool ICollection.IsSynchronized
            => ((ICollection)this.Dic).IsSynchronized;

        [EditorBrowsable(EditorBrowsableState.Never)]
        void ICollection.CopyTo(Array array, int index)
            => ((ICollection)this.Dic).CopyTo(array, index);
        #endregion

        #region IEnumerable<KeyValuePair<string, object>>
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            => this.Dic.GetEnumerator();
        #endregion

        #region IEnumerable
        [EditorBrowsable(EditorBrowsableState.Never)]
        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable)this.Dic).GetEnumerator();
        #endregion
    }
}
