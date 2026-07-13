using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace XRViewLab.UI;

// Optional, dependency-free reader for the public iRacing SDK shared-memory layout. Raw simulator
// values stop here; consumers receive only generic ViewLabEvent contracts.
internal sealed class IRacingTelemetryProvider : IViewLabEventProvider
{
    private const string MapName = "Local\\IRSDKMemMapFileName";
    private readonly object _gate=new();
    private Thread? _thread; private CancellationTokenSource? _stop;
    private MemoryMappedFile? _map; private MemoryMappedViewAccessor? _view;
    private readonly Dictionary<string,(int type,int offset)> _vars=new(StringComparer.OrdinalIgnoreCase);
    private int _lastCar=-1,_lastLap=-1; private uint _lastFlags=uint.MaxValue; private long _samples; private long _rateTick;
    public string Name => "iRacing SDK";
    public bool IsConnected { get; private set; }
    public string Status { get; private set; }="Disconnected";
    public string Diagnostics { get; private set; }="No telemetry received.";
    public event EventHandler<ViewLabEvent>? EventPublished;
    public event Action? DiagnosticsChanged;

    public void Start(){lock(_gate){if(_thread!=null)return;_stop=new();_thread=new Thread(()=>Run(_stop.Token)){IsBackground=true,Name="ViewLab iRacing telemetry"};_thread.Start();}}
    public void Stop(){Thread? t;lock(_gate){_stop?.Cancel();t=_thread;_thread=null;}t?.Join(500);Disconnect("Disconnected");}

    private void Run(CancellationToken token)
    {
        _rateTick=Environment.TickCount64;
        while(!token.IsCancellationRequested)
        {
            try
            {
                if(_view==null && !Connect()){SetStatus("Waiting for iRacing","SDK mapping not present.");Thread.Sleep(1000);continue;}
                int status=_view!.ReadInt32(4); if((status&1)==0){SetStatus("Connected","SDK mapping present; telemetry not active.");Thread.Sleep(250);continue;}
                int numVars=_view.ReadInt32(24),varHeaderOffset=_view.ReadInt32(28),numBuf=Math.Clamp(_view.ReadInt32(32),1,4);
                if(_vars.Count==0) ReadHeaders(numVars,varHeaderOffset);
                int bestTick=int.MinValue,bestOffset=0; for(int i=0;i<numBuf;i++){int b=48+i*16,tick=_view.ReadInt32(b),off=_view.ReadInt32(b+4);if(tick>bestTick){bestTick=tick;bestOffset=off;}}
                Poll(bestOffset); IsConnected=true; _samples++;
                long now=Environment.TickCount64;if(now-_rateTick>=1000){double hz=_samples*1000.0/(now-_rateTick);_samples=0;_rateTick=now;SetStatus("Telemetry active",$"{hz:0.0} Hz; {Diagnostics}");}
                Thread.Sleep(16);
            }
            catch(Exception ex){Disconnect($"Disconnected: {ex.GetType().Name}: {ex.Message}");Thread.Sleep(1000);}
        }
    }

    private bool Connect(){try{_map=MemoryMappedFile.OpenExisting(MapName,MemoryMappedFileRights.Read);_view=_map.CreateViewAccessor(0,0,MemoryMappedFileAccess.Read);_vars.Clear();SetStatus("Connected","SDK shared memory opened.");return true;}catch(FileNotFoundException){return false;}}
    private void ReadHeaders(int count,int offset){for(int i=0;i<count;i++){int p=offset+i*144,type=_view!.ReadInt32(p),dataOffset=_view.ReadInt32(p+4);byte[] name=new byte[32];_view.ReadArray(p+16,name,0,32);string n=Encoding.ASCII.GetString(name).TrimEnd('\0');if(n.Length>0)_vars[n]=(type,dataOffset);}}
    private double ReadValue(string name,int buf){if(!_vars.TryGetValue(name,out var v))return double.NaN;long p=buf+v.offset;return v.type switch{0=>_view!.ReadByte(p),1=>_view!.ReadByte(p)!=0?1:0,2=>_view!.ReadInt32(p),3=>_view!.ReadUInt32(p),4=>_view!.ReadSingle(p),5=>_view!.ReadDouble(p),_=>double.NaN};}
    private void Poll(int buf)
    {
        int car=(int)ReadValue("CarLeftRight",buf),lap=(int)ReadValue("LapCompleted",buf);double last=ReadValue("LapLastLapTime",buf);uint flags=(uint)ReadValue("SessionFlags",buf);
        Diagnostics=$"raw CarLeftRight={car}, LapCompleted={lap}, LapLastLapTime={last:0.000}, SessionFlags=0x{flags:X}; normalized spotter={NormalizeSpotter(car)}, flag={NormalizeFlag(flags)}";
        if(car!=_lastCar){_lastCar=car;PublishSpotter(NormalizeSpotter(car));}
        if(_lastLap>=0&&lap>_lastLap)Publish(new(){Kind=ViewLabEventKind.LapTime,Title=$"Lap {lap}",Body=double.IsFinite(last)?TimeSpan.FromSeconds(last).ToString(@"m\:ss\.fff"):"--",Value=last,TimestampUtc=DateTimeOffset.UtcNow});_lastLap=lap;
        if(flags!=_lastFlags){_lastFlags=flags;string f=NormalizeFlag(flags);Publish(new(){Kind=ViewLabEventKind.FlagState,Title=f,Color=f=="Yellow"?0xFFFF00u:f=="Blue"?0x0080FFu:0u,TimestampUtc=DateTimeOffset.UtcNow});}
    }
    private static string NormalizeSpotter(int raw)=>raw switch{2=>"Left",3=>"Right",4 or 5 or 6=>"Both",_=>"Clear"};
    private static string NormalizeFlag(uint raw)=>(raw&0x8)!=0?"Yellow":(raw&0x20)!=0?"Blue":"Clear";
    private void PublishSpotter(string s)=>Publish(new(){Kind=ViewLabEventKind.SpotterGlow,Title=s,Value=s=="Left"?-1:s=="Right"?1:s=="Both"?2:0,TimestampUtc=DateTimeOffset.UtcNow});
    private void Publish(ViewLabEvent e)=>EventPublished?.Invoke(this,e);
    public void Simulate(string kind){switch(kind){case"Left":PublishSpotter("Left");break;case"Right":PublishSpotter("Right");break;case"Both":PublishSpotter("Both");break;case"Clear":PublishSpotter("Clear");Publish(new(){Kind=ViewLabEventKind.FlagState,Title="Clear",Color=0,TimestampUtc=DateTimeOffset.UtcNow});break;case"Lap":Publish(new(){Kind=ViewLabEventKind.LapTime,Title="Lap 12",Body="1:34.221",Value=94.221,TimestampUtc=DateTimeOffset.UtcNow});break;case"Yellow":Publish(new(){Kind=ViewLabEventKind.FlagState,Title="Yellow",Color=0xFFFF00,TimestampUtc=DateTimeOffset.UtcNow});break;case"Blue":Publish(new(){Kind=ViewLabEventKind.FlagState,Title="Blue",Color=0x0080FF,TimestampUtc=DateTimeOffset.UtcNow});break;}Diagnostics="Simulated generic event: "+kind;SetStatus("Telemetry active",Diagnostics);}
    private void SetStatus(string status,string detail){Status=status;Diagnostics=detail;DiagnosticsChanged?.Invoke();}
    private void Disconnect(string status){IsConnected=false;_view?.Dispose();_map?.Dispose();_view=null;_map=null;_vars.Clear();SetStatus(status,"No active SDK mapping.");}
    public void Dispose(){Stop();_stop?.Dispose();}
}
