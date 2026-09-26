#define TRACE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using irSidekick;

namespace irSidekickProfiles;

public class appCameraList : Dictionary<string, appCameraCar>
{
	public string Profile { get; private set; }

	public appCameraList(string profile)
	{
		Profile = profile;
	}

	public void Add(appCameraCar car)
	{
		Add(car.CarPath, car);
	}

	public void Load()
	{
		Clear();
		string path = iRacing.pathCarCameras();
		if (!File.Exists(path))
		{
			return;
		}
		foreach (string item in Directory.GetFiles(path, "car.cam" + ProfileName.Suffix(Profile), SearchOption.AllDirectories).ToList())
		{
			string fileFolder = item.getFileFolder();
			appCameraCar appCameraCar2 = new appCameraCar(Profile, fileFolder);
			if (base.Keys.Contains(appCameraCar2.CarPath))
			{
				TraceLog.Verbose("Skipping camera file '" + item + "'");
				continue;
			}
			TraceLog.Verbose("Loading camera file '" + fileFolder + "/" + item.getFileName() + "'");
			Add(appCameraCar2);
		}
	}

	public void Apply()
	{
		if (Profile.IsNullOrEmpty())
		{
			return;
		}
		DateTime now = DateTime.Now;
		foreach (appCameraCar value in base.Values)
		{
			value.Apply(now);
		}
	}

	public void Delete()
	{
		foreach (appCameraCar value in base.Values)
		{
			value.Delete();
		}
		Clear();
	}

	public void CloneFrom(appCameraList source)
	{
		Delete();
		DateTime now = DateTime.Now;
		foreach (appCameraCar value in source.Values)
		{
			Add(appCameraCar.CloneFrom(Profile, value, now));
		}
	}
}
