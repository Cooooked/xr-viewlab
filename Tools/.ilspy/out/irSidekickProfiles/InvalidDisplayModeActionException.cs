using System;

namespace irSidekickProfiles;

public class InvalidDisplayModeActionException : Exception
{
	public InvalidDisplayModeActionException()
		: base("Cannot perform 'Display Mode' actions against app.ini and core.ini")
	{
	}
}
