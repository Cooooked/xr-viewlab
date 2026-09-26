using System.IO;

namespace irSidekickProfiles;

internal class FileSystemEventArgsExtn : FileSystemEventArgs
{
	public FileSystemEventArgsExtn(WatcherChangeTypes typeChange, string namePath, string nameFile)
		: base(typeChange, namePath, nameFile)
	{
	}

	public FileSystemEventArgsExtn(FileSystemEventArgs args)
		: base(args.ChangeType, Path.GetDirectoryName(args.FullPath), args.Name)
	{
	}

	public bool IsSame(FileSystemEventArgs args)
	{
		if (base.ChangeType == args.ChangeType && base.FullPath == args.FullPath)
		{
			return base.Name == args.Name;
		}
		return false;
	}
}
