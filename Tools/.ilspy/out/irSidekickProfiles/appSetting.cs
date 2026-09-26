using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using irSidekick;

namespace irSidekickProfiles;

public class appSetting : INotifyPropertyChanged
{
	public bool Modified;

	public string tagPrefix = "";

	private string _Name = "";

	private string _Value = "";

	private string _Comment = "";

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (!_Name.Equals(value))
			{
				_Name = value;
				Modified = true;
				NotifyPropertyChanged("Name");
				NotifyPropertyChanged("tagFull");
			}
		}
	}

	public string Value
	{
		get
		{
			return _Value;
		}
		set
		{
			if (!_Value.IfNullOrEmpty(string.Empty).Equals(value.IfNullOrEmpty(string.Empty)))
			{
				_Value = value.IfNullOrEmpty(string.Empty);
				Modified = true;
				NotifyPropertyChanged("Value");
				NotifyPropertyChanged("outSetting");
			}
		}
	}

	public string Comment
	{
		get
		{
			return _Comment;
		}
		set
		{
			if (!_Comment.Equals(value))
			{
				_Comment = value;
				Modified = true;
				NotifyPropertyChanged("Comment");
			}
		}
	}

	public string tagFull => tagPrefix + "." + Name;

	public string outLine => outSetting + outFiller() + outComment.IfNullOrEmpty("");

	public string outSetting => Name + "=" + Value;

	public string outComment
	{
		get
		{
			if (Comment.IfNullOrEmpty("").Length <= 0)
			{
				return "";
			}
			return "\t; " + Comment;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public string outFiller()
	{
		int val = (tagPrefix.Equals("2") ? 48 : 40) - outSetting.Length;
		return new string(' ', val.Max(0));
	}

	public appSetting(string prefix, string name, string value, string comment = "", bool modified = false)
	{
		tagPrefix = prefix;
		_Name = name;
		_Value = value;
		_Comment = comment;
		Modified = modified;
	}

	public appSetting Clone()
	{
		return new appSetting(tagPrefix, _Name, _Value, _Comment, modified: true);
	}

	public void Save(StreamWriter iniWriter)
	{
		iniWriter.WriteLine(outLine);
	}
}
