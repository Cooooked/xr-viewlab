using System;
using System.IO.MemoryMappedFiles;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XRViewLab.UI;

internal sealed class ObsRecordingProvider : IDisposable
{
	private const int Magic=0x314F4C56; // VLO1
	private readonly MemoryMappedFile _map=MemoryMappedFile.CreateOrOpen("Local\\XRViewLabObsRecordingState",16,MemoryMappedFileAccess.ReadWrite);
	private CancellationTokenSource? _cts; private int _generation;
	public void Update(bool enabled,string endpoint,string password)
	{
		Stop(); if(!enabled){Publish(ObsConnectionState.Disconnected);return;}_cts=new();Publish(ObsConnectionState.Connecting);_ = Task.Run(()=>RunAsync(endpoint,password,_cts.Token));
	}
	private async Task RunAsync(string endpoint,string password,CancellationToken token)
	{
		if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var endpointUri)||(endpointUri.Scheme!="ws"&&endpointUri.Scheme!="wss")){Publish(ObsConnectionState.Disconnected);return;}
		while(!token.IsCancellationRequested){bool authenticationRequired=false,identifiedOk=false;try{Publish(ObsConnectionState.Connecting);using var ws=new ClientWebSocket();await ws.ConnectAsync(endpointUri,token);JsonDocument hello=await Receive(ws,token);JsonElement hd=hello.RootElement.GetProperty("d");string? auth=null;
			if(hd.TryGetProperty("authentication",out var challenge)){authenticationRequired=true;string salt=challenge.GetProperty("salt").GetString()??"",nonce=challenge.GetProperty("challenge").GetString()??"";auth=ObsWebSocketProtocol.Authenticate(password,salt,nonce);}
			string identify=auth==null?"{\"op\":1,\"d\":{\"rpcVersion\":1}}":JsonSerializer.Serialize(new{op=1,d=new{rpcVersion=1,authentication=auth}});await Send(ws,identify,token);using var identified=await Receive(ws,token);
			identifiedOk=ObsWebSocketProtocol.IsIdentified(identified.RootElement);if(!identifiedOk)throw new WebSocketException("OBS identification failed");Publish(ObsConnectionState.Connected);
			while(ws.State==WebSocketState.Open&&!token.IsCancellationRequested){await Send(ws,"{\"op\":6,\"d\":{\"requestType\":\"GetRecordStatus\",\"requestId\":\"viewlab-record\"}}",token);using var response=await Receive(ws,token);bool active=ObsWebSocketProtocol.ParseRecordStatus(response.RootElement);Publish(active?ObsConnectionState.Recording:ObsConnectionState.Connected);await Task.Delay(750,token);}
		}catch(OperationCanceledException){}catch{Publish(authenticationRequired&&!identifiedOk?ObsConnectionState.AuthenticationFailed:ObsConnectionState.Disconnected);try{await Task.Delay(1500,token);}catch(OperationCanceledException){}}
		}
	}
	private static async Task Send(ClientWebSocket ws,string json,CancellationToken token){byte[] bytes=Encoding.UTF8.GetBytes(json);await ws.SendAsync(bytes,WebSocketMessageType.Text,true,token);}
	private static async Task<JsonDocument> Receive(ClientWebSocket ws,CancellationToken token){byte[] bytes=new byte[65536];int used=0;ValueWebSocketReceiveResult part;do{part=await ws.ReceiveAsync(bytes.AsMemory(used),token);used+=part.Count;if(part.MessageType==WebSocketMessageType.Close)throw new WebSocketException("OBS WebSocket closed");}while(!part.EndOfMessage&&used<bytes.Length);return JsonDocument.Parse(bytes.AsMemory(0,used));}
	private void Publish(ObsConnectionState state){using var view=_map.CreateViewAccessor(0,16,MemoryMappedFileAccess.Write);view.Write(0,Magic);view.Write(4,1);view.Write(12,(int)state);view.Write(8,++_generation);}
	private void Stop(){if(_cts==null)return;_cts.Cancel();_cts.Dispose();_cts=null;}
	public void Dispose(){Stop();_map.Dispose();}
}
