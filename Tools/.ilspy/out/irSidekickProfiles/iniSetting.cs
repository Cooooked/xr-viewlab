using System.Collections.Generic;
using System.Text;
using irSidekick;

namespace irSidekickProfiles;

public class iniSetting
{
	public iniConfig Config { get; set; }

	public string Section { get; set; }

	public string Name { get; set; }

	public string Comment { get; set; }

	public string TagID => Config.TagID() + "." + Section + "." + Name;

	public string TagName => Config.TagName() + "." + Section + "." + Name;

	public bool ValueIsDouble { get; set; }

	public override string ToString()
	{
		return TagID;
	}

	public iniSetting()
	{
	}

	public iniSetting(string tag)
	{
		UnpackTag(tag);
	}

	public iniSetting(string tag, string comment)
	{
		UnpackTag(tag);
		Comment = comment;
	}

	public appSetting AsAppSetting(string value)
	{
		return new appSetting(Config.TagID(), Name, value, Comment.IfNullOrEmpty(""));
	}

	public static iniSetting Find(string tag)
	{
		if (tag.IsNullOrEmpty())
		{
			return null;
		}
		return tag[0] switch
		{
			'0' => iniApp.Find(tag), 
			'1' => iniCore.Find(tag), 
			'2' => iniDX11.Find(tag), 
			_ => null, 
		};
	}

	private void UnpackTag(string tag)
	{
		string[] array = tag.Split('.');
		if (array.Length == 3)
		{
			Config = (iniConfig)array[0].ToInt();
			Section = array[1];
			Name = array[2];
		}
	}

	public string DisplayValue(string value)
	{
		return value;
	}

	public string DisplayLODValue(appConfig dx11)
	{
		return string.Empty;
	}

	private int LineMinLength()
	{
		if (Config != iniConfig.DX11)
		{
			return 40;
		}
		return 48;
	}

	private string LinePadding(int length)
	{
		int val = LineMinLength() - length;
		return new string(' ', val.Max(0));
	}

	public string LineOutput(string value)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Name + "=" + value);
		stringBuilder.Append(LinePadding(stringBuilder.Length));
		stringBuilder.Append("\t");
		stringBuilder.Append(Comment);
		return stringBuilder.ToString();
	}

	public void DictionaryAdd(Dictionary<string, iniSetting> dictionary)
	{
		try
		{
			dictionary.Add(TagID, this);
		}
		catch
		{
		}
	}
}
