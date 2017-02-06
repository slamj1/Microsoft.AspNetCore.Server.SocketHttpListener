using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	public class HeaderDictionary : IHeaderDictionary
	{
		private readonly NameValueCollection _collection;

		public HeaderDictionary(NameValueCollection collection)
		{
			_collection = collection;
		}

		public int Count => _collection.Count;
		public bool IsReadOnly => false;

		public StringValues this[string key]
		{
			get { return _collection.GetValues(key); }
			set { _collection[key] = value; }
		}

		public ICollection<string> Keys => _collection.Keys.OfType<string>().ToList();

		public ICollection<StringValues> Values => _collection.Keys.OfType<string>()
			.Select(x => _collection.GetValues(x))
			.Cast<StringValues>()
			.ToList();

		public void Add(KeyValuePair<string, StringValues> item) => Add(item.Key, item.Value);

		public void Add(string key, StringValues value)
		{
			_collection.Add(key, value);
		}

		public void Clear()
		{
			_collection.Clear();
		}

		public bool Contains(KeyValuePair<string, StringValues> item) => ContainsKey(item.Key);

		public bool ContainsKey(string key)
		{
			return _collection.Keys.OfType<string>().Contains(key);
		}

		public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex)
		{
			for (var i = arrayIndex; i < array.Length - arrayIndex; i++)
			{
				var key = _collection.GetKey(i);
				var values = _collection.GetValues(i);
				array[i] = new KeyValuePair<string, StringValues>(key, values);
			}
		}

		public bool Remove(KeyValuePair<string, StringValues> item) => Remove(item.Key);

		public bool Remove(string key)
		{
			if (!ContainsKey(key))
				return false;

			_collection.Remove(key);
			return true;
		}

		public bool TryGetValue(string key, out StringValues value)
		{
			if (ContainsKey(key))
			{
				value = _collection.GetValues(key);
				return true;
			}

			value = default(StringValues);
			return false;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator()
		{
			foreach (string key in _collection.Keys)
				yield return new KeyValuePair<string, StringValues>(key, _collection.GetValues(key));
		}
	}
}