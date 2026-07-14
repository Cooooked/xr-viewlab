using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XRViewLab.UI;
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
}
