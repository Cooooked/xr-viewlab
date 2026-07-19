using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XRViewLab.UI;
internal enum ObsConnectionState { Disconnected=0, Connected=1, Recording=2, Connecting=3, AuthenticationFailed=4 }
internal static class ObsWebSocketProtocol
{
	public static string Authenticate(string password,string salt,string challenge)
	{
		string secret=Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password+salt)));
		return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret+challenge)));
	}
	public static bool ParseRecordStatus(JsonElement root)
	{
		JsonElement d=root.GetProperty("d");return d.GetProperty("requestStatus").GetProperty("result").GetBoolean()&&d.GetProperty("responseData").GetProperty("outputActive").GetBoolean();
	}
	public static bool IsIdentified(JsonElement root) => root.TryGetProperty("op",out JsonElement op)&&op.GetInt32()==2;
	public static bool IsRecording(ObsConnectionState state) => state==ObsConnectionState.Recording;
	public static string StatusText(ObsConnectionState state) => state switch
	{
		ObsConnectionState.Connecting=>"Connecting",
		ObsConnectionState.Connected or ObsConnectionState.Recording=>"Connected",
		ObsConnectionState.AuthenticationFailed=>"Authentication failed",
		_=>"Disconnected"
	};
}
