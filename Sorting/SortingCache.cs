﻿using MagicStorage.CrossMod;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

#nullable enable

namespace MagicStorage.Sorting;

/// <summary>
///     A helper class that stores a cache of sorting order for every item ID
/// </summary>
public static class SortingCache
{
	public static readonly SortingCacheDictionary dictionary = new();
}

public class SortingCacheDictionary
{
	private class Entry
	{
		public int[]? IndexByType { get; }

		public Entry(int[]? indexByType = null)
		{
			IndexByType = indexByType;
		}

		public int FindIndex(int itemType)
		{
			if (IndexByType is null || itemType < 0 || itemType >= IndexByType.Length)
				return -1;

			return IndexByType[itemType];
		}
	}

	private readonly Dictionary<int, Entry> cache = new();

	public int FindIndex(int mode, int itemType) => cache.TryGetValue(mode, out var entry) ? entry.FindIndex(itemType) : -1;

	public void Fill()
	{
		cache.Clear();

		foreach (var option in SortingOptionLoader.Options) {
			try {
				Create(option);
			} catch (Exception ex) {
				throw new InvalidOperationException($"SortingOption.Sorter for type \"{option.GetType().GetSimplifiedGenericTypeName()}\" was invalid" +
					(option == SortingOptionLoader.Definitions.Default ? $"\nDefinitions.Default most recent class: {SortClassList.actualException_class ?? "none"}" : ""),
					ex);
			}
		}
	}

	private void Create(SortingOption option)
	{
		if (!option.CacheFuzzySorting) {
			cache[option.Type] = new Entry();
			return;
		}

		var sorter = option.Sorter.AsSafe(x => $"{x.Name} | ID: {x.type} | Mod: {x.ModItem?.Mod.Name ?? "Terraria"}");

		var items = ContentSamples.ItemsByType
			.Select((pair, i) => (item: pair.Value, type: i))
			.OrderByDescending(x => x.item, sorter)
			.ToArray();

		int[] indices = new int[items.Length];

		for (int i = 0; i < items.Length; i++)
		{
			(Item item, int type) = items[i];

			indices[type] = item.IsAir ? -1 : i;
		}

		var entry = new Entry(indices);

		cache[option.Type] = entry;
	}

	/// <summary>
	///     Sorts the items based on the cached item order
	/// </summary>
	/// <returns>A sorted collection</returns>
	public IOrderedEnumerable<Item> SortFuzzy(IEnumerable<Item> items, int mode)
	{
		Entry entry = cache[mode];

		if (items is null)
			return Array.Empty<Item>().OrderByDescending(_ => 0); //Failsafe - a pointless collection

		if (entry?.IndexByType is null)
			return items.OrderByDescending(_ => 0); //Preserve item order, likely because the sorter uses runtime values

		return items.Where(i => i is not null).OrderByDescending(i => entry.FindIndex(i.type));
	}
}
