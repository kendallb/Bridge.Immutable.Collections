﻿using System;
using System.Collections;
using System.Collections.Generic;
using Bridge;
using Newtonsoft.Json;

namespace ProductiveRage.Immutable
{
	// This backs onto the ImmutableJs library but it's not a direct binding because I want to favour a more C#-style interface and to use the ProductiveRage.Immutable "Optional" type for cases where we may or may
	// not be returning a value (from GetIfPresent, for example)
	public sealed class Map<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> // Implementing IEnumerable means that Json.NET serialises as we desire (as well as being handy for LINQ operations)
	{
		public static Map<TKey, TValue> Empty { get; } = new Map<TKey, TValue>(GetEmptyBackingMap());

		private readonly object _map;
		private Map(object map)
		{
			if (map == null)
				throw new ArgumentNullException(nameof(map));

			_map = map;
		}

		[JsonConstructor]
		private Map(IEnumerable<KeyValuePair<TKey, TValue>> source)
		{
			_map = GetEmptyBackingMap();
			foreach (var keyValuePair in source)
			{
				if (keyValuePair.Key == null)
					throw new ArgumentException($"Null Key encountered in {nameof(source)} data");
				TValue existingValue;
				if (TryGetValueFromBackingMap(_map, keyValuePair.Key, out existingValue))
					throw new ArgumentException($"Duplicate Key encountered in {nameof(source)} data: {keyValuePair.Key}");
				if (keyValuePair.Value == null)
					throw new ArgumentException($"Null Value encountered in {nameof(source)} data");
				_map = SetOnBackingMap(_map, keyValuePair.Key, keyValuePair.Value);
			}
		}

		public uint Count
		{
			get { return (uint)Script.Write<int>("{0}.size", _map); } // Cast from JavaScript number to uint so that we definitely get a uint returned in case anyone wants to do type checking on the value
		}

		/// <summary>
		/// This will throw an ArgumentNullException for a null key
		/// </summary>
		public bool Contains(TKey key)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			TValue value;
			return TryGetValueFromBackingMap(_map, key, out value);
		}

		/// <summary>
		/// This will return default of TValue if the key is not present, it will throw an ArgumentNullException for a null key
		/// </summary>
		public Optional<TValue> GetIfPresent(TKey key)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			TValue value;
			return TryGetValueFromBackingMap(_map, key, out value) ? value : Optional<TValue>.Missing;
		}

		/// <summary>
		/// If the specified key is already present then the value will be replaced and if the specified key is not already present in the data then a new entry will be created. This will throw an ArgumentNullException
		/// if either a null key or value are provided. If the key is already present and the value matches the existing value then the same instance will be returned.
		/// </summary>
		public Map<TKey, TValue> AddOrUpdate(TKey key, TValue value)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			// Although the ImmutableJs Map has the facility to return the same list if the key/value pair being added already exists, it only works with JavaScript referential equality and so we need to do a separate
			// get-and-compare-if-present check if we want to support C# custom equality / equality operator overloads
			TValue currentValue;
			if (TryGetValueFromBackingMap(_map, key, out currentValue) && value.Equals(currentValue))
				return this;

			return new Map<TKey, TValue>(Script.Write<object>("{0}.set({1}, {2})", _map, key, value));
		}

		/// <summary>
		/// If the specified key is not present then the current Map will be returned unchanged. This will throw an ArgumentNullException for a null key.
		/// </summary>
		public Map<TKey, TValue> RemoveIfPresent(TKey key)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			var newMap = Script.Write<object>("{0}.delete({1})", _map, key);
			return (newMap == _map) ? this : new Map<TKey, TValue>(newMap);
		}

		private static object GetEmptyBackingMap()
		{
			return Script.Write<object>("Immutable.Map()");
		}

		private static object SetOnBackingMap(object map, TKey key, TValue value)
		{
			return Script.Write<object>("{0}.set({1}, {2})", map, key, value);
		}
		
		/// <summary>
		/// If the key is not present then the returned value be default TValue
		/// </summary>
		private static bool TryGetValueFromBackingMap(object map, TKey key, out TValue value)
		{
			var valueIfPresent = Script.Write<TValue>("{0}.get({1})", map, key);
			var valueWasFound = Script.Write<bool>("typeof({0}) !== 'undefined'", valueIfPresent);
			value = valueWasFound ? valueIfPresent : default(TValue);
			return valueWasFound;
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			var keys = Script.Write<TKey[]>("{0}.keySeq().toArray()", _map);
			var values = Script.Write<TValue[]>("{0}.valueSeq().toArray()", _map);
			for (var i = 0; i < keys.Length; i++)
				yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
		}
		IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
	}
}
