using System;
using System.Globalization;
using System.IO;
using irSidekick;

namespace irSidekickProfiles;

public class appCameraCar
{
	private const string CameraFileName = "car.cam";

	public string Profile { get; internal set; }

	public string CarPath { get; private set; }

	private string ProfileFileName => "car.cam" + ProfileName.Suffix(Profile);

	private string FullFileName => Path.Combine(iRacing.pathCarCameras(CarPath), ProfileFileName);

	private string ApplyFileName => Path.Combine(iRacing.pathCarCameras(CarPath), "car.cam");

	public bool CameraExists => File.Exists(FullFileName);

	public appCameraCar(string filename)
	{
		Profile = filename.getFileSuffix();
		if (Profile.ToLower(CultureInfo.InvariantCulture).Equals("ini"))
		{
			Profile = string.Empty;
		}
		CarPath = filename.getFileFolder();
	}

	public appCameraCar(string profile, string car)
	{
		Profile = profile;
		CarPath = car;
	}

	public void Apply(DateTime when)
	{
		if (!Profile.IsNullOrEmpty())
		{
			FullFileName.SafeFileCopy(ApplyFileName, when);
		}
	}

	public void Delete()
	{
		try
		{
			FullFileName.SafeFileDelete();
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Exception encountered deleting car specific camera: Profile '" + ProfileName.DisplayName(Profile) + "' Car '" + CarPath + "': " + ex.Message);
		}
	}

	public static appCameraCar CloneFrom(string target, appCameraCar source, DateTime when)
	{
		appCameraCar appCameraCar2 = new appCameraCar(target, source.CarPath);
		source.FullFileName.SafeFileCopy(appCameraCar2.FullFileName, when);
		return appCameraCar2;
	}
}
