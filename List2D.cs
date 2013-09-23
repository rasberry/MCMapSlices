using System;
using System.Collections.Generic;


namespace mcmappy
{
	public class List2D<T>
	{
		private Dictionary<int,Dictionary<int,T>> _store;
		
		public List2D()
		{
			_store = new Dictionary<int,Dictionary<int,T>>();
		}
		
		public T this[int r, int c] { get {
			if (_store.ContainsKey(r) && _store[r].ContainsKey(c)) {
				if (c >= Columns) { Columns = c+1; }
				return _store[r][c];
			}
			return default(T);
		} set {
			if (!_store.ContainsKey(r)) {
				_store[r] = new Dictionary<int, T>();
			}
			_store[r][c] = value;
		}}
		
		public int Rows { get { return _store.Count; }}
		public int Columns { get; private set; }
	}
}

