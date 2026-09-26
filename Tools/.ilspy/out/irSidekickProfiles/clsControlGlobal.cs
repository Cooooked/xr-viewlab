using System.Windows.Controls;

namespace irSidekickProfiles;

public static class clsControlGlobal
{
	public static clsControlList Items = new clsControlList();

	public static void Add(GroupBox box, Control control)
	{
		Items.Add(new clsControlItem(box, control));
	}

	public static void Add(string lbl, Control control)
	{
		Items.Add(new clsControlItem(lbl, control));
	}

	public static void Add(Label label, Control control)
	{
		Items.Add(new clsControlItem(label, control));
	}

	public static void Add(RadioButton control)
	{
		Items.Add(new clsControlItem(control));
	}

	public static void Add(CheckBox control)
	{
		Items.Add(new clsControlItem(control));
	}

	public static void Add(XSlider control)
	{
		Items.Add(new clsControlItem(control));
	}

	public static void Add(YSlider control)
	{
		Items.Add(new clsControlItem(control));
	}

	public static clsControlList Search(string input)
	{
		return Items.Search(input);
	}
}
