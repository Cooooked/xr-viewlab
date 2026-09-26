using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace irSidekickProfiles;

public class gfxGPUList : List<gfxGPUCard>
{
	public gfxGPUList()
	{
		foreach (gfxGPU value in Enum.GetValues(typeof(gfxGPU)))
		{
			Add(new gfxGPUCard(value));
		}
	}

	public void LoadCombo(ComboBox cbo)
	{
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			gfxGPUCard current = enumerator.Current;
			cbo.Items.Add(current.Name);
		}
	}

	public void SetComboIndex(ComboBox cbo, string GPUName)
	{
		cbo.SelectedIndex = GetGPUIndex(GPUName);
	}

	public int GetGPUIndex(string GPUName)
	{
		for (int i = 0; i < base.Count; i++)
		{
			if (base[i].Name.Equals(GPUName) || base[i].Name.Contains(GPUName))
			{
				return i;
			}
		}
		return -1;
	}

	public gfxGPUCard GetGPUCard(string GPUName)
	{
		int gPUIndex = GetGPUIndex(GPUName);
		if (gPUIndex >= 0)
		{
			return base[gPUIndex];
		}
		return null;
	}
}
