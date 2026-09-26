using System.Collections.Generic;
using irSidekick;

namespace irSidekickProfiles;

public class clsControlList : List<clsControlItem>
{
	public clsControlList Clone()
	{
		clsControlList clsControlList2 = new clsControlList();
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			clsControlItem current = enumerator.Current;
			clsControlList2.Add(current.Clone());
		}
		return clsControlList2;
	}

	public clsControlList Search(string input)
	{
		if (input.IsNullOrEmpty())
		{
			return Clone();
		}
		clsControlList clsControlList2 = new clsControlList();
		string[] words = input.ToLower().Split(' ');
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				clsControlItem current = enumerator.Current;
				if (current.Search(words) > 0)
				{
					clsControlList2.Add(current);
				}
			}
		}
		clsControlList2.Sort(CompareBySearchScore);
		return clsControlList2;
	}

	public int CompareBySearchScore(clsControlItem x, clsControlItem y)
	{
		return y.SearchScore.CompareTo(x.SearchScore);
	}
}
