using XRViewLab.UI;

static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
Require(OverlaySettingsCatalog.All.Count==6,"ordinary overlay catalogue count changed without migration coverage");
Require(OverlaySettingsCatalog.All.Values.Select(x=>x.Id).Distinct().Count()==6,"overlay identifiers are not unique");
for(int vk=OverlaySettingsCatalog.FirstFunctionKey;vk<=OverlaySettingsCatalog.LastFunctionKey;++vk)
{
    int index=OverlaySettingsCatalog.ComboIndexFromVirtualKey(vk);
    Require(OverlaySettingsCatalog.VirtualKeyFromComboIndex(index)==vk,$"hotkey round trip failed for {vk}");
}
Require(OverlaySettingsCatalog.ComboIndexFromVirtualKey(0)==0&&OverlaySettingsCatalog.VirtualKeyFromComboIndex(0)==0,"None hotkey does not round trip");
var sticky=OverlaySettingsCatalog.All["sticky"];
Require(sticky.LegacyToggleKey=="sticky_note_toggle_vk"&&sticky.DefaultToggleVirtualKey==118,"sticky hotkey migration contract changed");
foreach(var definition in OverlaySettingsCatalog.All.Values)
{
    Require(definition.DefaultX is >=-1 and <=1&&definition.DefaultY is >=-1 and <=1,$"{definition.Id} position default is invalid");
    Require(definition.DefaultScale>0&&definition.DefaultOpacity is >0 and <=1,$"{definition.Id} scale/opacity defaults are invalid");
}
var previewItem=new OverlayPreviewItem("test","TEST",.5,.5,.2,.1,1,.25,4,1,OverlayPreviewAnchor.Centre,OverlayPreviewStyle.System);
var fullArea=new System.Windows.Rect(0,0,1000,500);
var half=OverlayPreviewReplicaLayout.ResolveSize(previewItem with{Scale=.5},fullArea);
var normal=OverlayPreviewReplicaLayout.ResolveSize(previewItem,fullArea);
var doubleSize=OverlayPreviewReplicaLayout.ResolveSize(previewItem with{Scale=2},fullArea);
Require(Math.Abs(normal.Width-half.Width*2)<.001&&Math.Abs(normal.Height-half.Height*2)<.001,"preview half-scale is not uniform");
Require(Math.Abs(doubleSize.Width-normal.Width*2)<.001&&Math.Abs(doubleSize.Height-normal.Height*2)<.001,"preview double-scale is not uniform");
Require(Math.Abs(normal.Width/normal.Height-doubleSize.Width/doubleSize.Height)<.001,"preview scaling changed the replica aspect ratio");
var cropOnlyChanged=new System.Windows.Rect(300,150,400,200);
var fullReferenceSize=OverlayPreviewReplicaLayout.ResolveSize(previewItem,fullArea);
Require(Math.Abs(fullReferenceSize.Width-normal.Width)<.001&&Math.Abs(fullReferenceSize.Height-normal.Height)<.001&&cropOnlyChanged.Width<1000,
    "crop-only coverage state changed the full-reference overlay footprint");
Console.WriteLine("Overlay settings, migration and uniform preview replica fixtures passed.");
