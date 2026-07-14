using System;
using System.Text.Json;
using XRViewLab.UI;
string auth=ObsWebSocketProtocol.Authenticate("password","salt","challenge");
if(auth!="zTM5ki6L2vVvBQiTG9ckH1Lh64AbnCf6XZ226UmnkIA=")throw new Exception("OBS v5 authentication formula changed: "+auth);
using var active=JsonDocument.Parse("{\"op\":7,\"d\":{\"requestStatus\":{\"result\":true},\"responseData\":{\"outputActive\":true}}}");
using var idle=JsonDocument.Parse("{\"op\":7,\"d\":{\"requestStatus\":{\"result\":true},\"responseData\":{\"outputActive\":false}}}");
if(!ObsWebSocketProtocol.ParseRecordStatus(active.RootElement)||ObsWebSocketProtocol.ParseRecordStatus(idle.RootElement))throw new Exception("GetRecordStatus parsing changed");
Console.WriteLine("PASS: OBS v5 authentication and real recording-state response parsing");
