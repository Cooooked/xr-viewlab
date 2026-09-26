namespace irSidekickProfiles;

public class SelectItem
{
	public bool IsSelected { get; set; }

	public string ProfileName { get; set; }

	public SelectItem(string profile)
	{
		IsSelected = false;
		ProfileName = profile;
	}
}
