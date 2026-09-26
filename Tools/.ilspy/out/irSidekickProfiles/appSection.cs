using System.IO;

namespace irSidekickProfiles;

public class appSection : appSettingList
{
	public string Name = "";

	public string outLine => "[" + Name + "]";

	public appSection(string prefix, string name)
		: base(prefix + "." + name)
	{
		Name = name;
	}

	public appSection Clone()
	{
		appSection obj = new appSection(base.tagPrefix, Name);
		obj.CloneFrom(this);
		return obj;
	}

	public override void Save(StreamWriter iniWriter)
	{
		iniWriter.WriteLine(outLine);
		base.Save(iniWriter);
		iniWriter.WriteLine("");
	}

	public bool hasSetting(string name, string notValue)
	{
		if (!ContainsKey(name))
		{
			return false;
		}
		return !base[name].Value.Equals(notValue);
	}
}
