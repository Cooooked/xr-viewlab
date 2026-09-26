#define TRACE
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Media;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using irSidekick;
using irSidekickProfiles.Properties;

namespace irSidekickProfiles;

public class winMain : MetroWindow, IComponentConnector, IStyleConnector
{
	private bool NotifyAlignmentError = true;

	private DispatcherTimer TimerFOV = new DispatcherTimer();

	private static Color irPitLineColor = Color.FromRgb(48, 140, byte.MaxValue);

	private static Color irRaceLineFastColor = Color.FromRgb(99, 232, 79);

	private static Color irRaceLineSameColor = Color.FromRgb(232, 232, 232);

	private static Color irRaceLineSlowColor = Color.FromRgb(249, 8, 37);

	private List<Color> irLineColors = new List<Color> { irPitLineColor, irRaceLineFastColor, irRaceLineSameColor, irRaceLineSlowColor };

	private FileSystemEventArgsExtn changePrevious = new FileSystemEventArgsExtn(WatcherChangeTypes.Created, "", "");

	private string iRacingFolder = appExtensions.getIRacingFolder();

	private const string oculusFileName = "rendererDX11Oculus.ini";

	private const string openvrFileName = "rendererDX11OpenVR.ini";

	private const string openxrFileName = "rendererDX11OpenXR.ini";

	private FileSystemWatcher appWatcher;

	private FileSystemWatcher coreWatcher;

	private FileSystemWatcher dx11Watcher;

	private FileSystemWatcher oculusWatcher;

	private FileSystemWatcher openvrWatcher;

	private FileSystemWatcher openxrWatcher;

	private object SyncLock = new object();

	private AppUpdate appUpdate = new AppUpdate();

	private Regex rxGPU = new Regex("[0-9]{4}");

	public bool IsClosing;

	public bool DataLoading;

	public string Options = Ini.GetProductOptions();

	public string MemberID = IniMember.ReadMemberID(Settings.Default.ALTEXE);

	public bool ConfirmActions = Ini.ReadKey("ConfirmActions", "Options").ParseBool();

	public bool ShowBackupProfile = Ini.ReadKey("ShowBackupProfile", "Options").ParseBool();

	public bool ShowMonitorProfile = Ini.ReadKey("ShowMonitorProfile", "Options").ParseBool(def: true);

	public bool ShowOculusProfile = Ini.ReadKey("ShowOculusProfile", "Options").ParseBool();

	public bool ShowOpenVRProfile = Ini.ReadKey("ShowOpenVRProfile", "Options").ParseBool();

	public bool ShowOpenXRProfile = Ini.ReadKey("ShowOpenXRProfile", "Options").ParseBool();

	public bool PreserveMainVolume = Ini.ReadKey("PreserveMainVolume", "Options").ParseBool();

	public bool AutoExitOnLaunch = Ini.ReadKey("AutoExitOnLaunch", "Options").ParseBool();

	public bool GSyncHack = Ini.ReadKey("GSyncHack", "Options").ParseBool();

	public bool MismatchedMonitorKluge = Ini.ReadKey("MismatchedMonitorKludge", "Options").ParseBool();

	public bool ShowTripleCurvedWarning = Ini.ReadKey("ShowTripleCurvedWarning", "Options").ParseBool(def: true);

	public appProfile ProfileView;

	public appProfileList ProfileList = new appProfileList();

	private clsMonitorList MonitorList = new clsMonitorList();

	internal Label lblCopySettings;

	internal ComboBox cboTargetProfile;

	internal TextBlock txtDisplayMode;

	internal MetroTabControl tabSections;

	internal MetroTabItem tabView;

	internal MetroTabControl tabMonitorDisplay;

	internal MetroTabItem tabMonitor;

	internal Label lblNumMonitors;

	internal ComboBox cboNumMonitors;

	internal clsRainbow bowNumScreens;

	internal Label lblMonitorWidth;

	internal NumericUpDown numMonitorWidth;

	internal clsRainbow bowMonitorWidth;

	internal Label lblMonitorVisible;

	internal NumericUpDown numMonitorVisible;

	internal clsRainbow bowMonitorVisible;

	internal Label lblDiagonalWidth;

	internal NumericUpDown numDiagonalWidth;

	internal clsRainbow bowDiagonalWidth;

	internal Label lblScreenAngle;

	internal NumericUpDown numScreenAngle;

	internal clsRainbow bowScreenAngle;

	internal Label lblViewDistance;

	internal NumericUpDown numViewDistance;

	internal clsRainbow bowViewDistance;

	internal Label lblFOV;

	internal NumericUpDown numFOV;

	internal clsRainbow bowFOV;

	internal GroupBox grpFOV;

	internal Button btnScreenAngle;

	internal Button btnViewDistance;

	internal Button btnFOV;

	internal Label lblRadius;

	internal ComboBox cboRadius;

	internal clsRainbow bowRadius;

	internal Label lblReflex;

	internal ComboBox cboReflex;

	internal clsRainbow bowReflex;

	internal Label lblResolution;

	internal ComboBox cboResolution;

	internal clsRainbow bowResolution;

	internal Label lblRefreshRate;

	internal NumericUpDown numRefreshRate;

	internal clsRainbow bowRefreshRate;

	internal CheckBox chkMultiProjection;

	internal CheckBox chkSMP;

	internal clsRainbow bowMultiProjection;

	internal CheckBox chkAutoConfigDone;

	internal clsRainbow bowAutoConfigDone;

	internal System.Windows.Controls.Image imgMonitorWidth;

	internal System.Windows.Controls.Image imgVisibleWidth;

	internal System.Windows.Controls.Image imgDiagonalWidth;

	internal System.Windows.Controls.Image imgDistance;

	internal System.Windows.Controls.Image imgAngle;

	internal System.Windows.Controls.Image imgFOV;

	internal Grid gridMonitors;

	internal MetroTabItem tabDisplay;

	internal Label lblResScale;

	internal ComboBox cboResScale;

	internal clsRainbow bowResScale;

	internal Label lblFSRSharpness;

	internal NumericUpDown numFSRSharpness;

	internal clsRainbow bowFSRSharpness;

	internal Label lblDeviceIdx;

	internal NumericUpDown numDeviceIdx;

	internal clsRainbow bowDeviceIdx;

	internal CheckBox chkFullScreen;

	internal clsRainbow bowFullScreen;

	internal CheckBox chkBorder;

	internal clsRainbow bowBorder;

	internal CheckBox chkHDRFormat;

	internal clsRainbow bowHDRFormat;

	internal Label lblAlign;

	internal ComboBox cboAlign;

	internal clsRainbow bowAlign;

	internal Label lblGamma;

	internal NumericUpDown numGamma;

	internal clsRainbow bowGamma;

	internal Label lblBrightness;

	internal NumericUpDown numBrightness;

	internal clsRainbow bowBrightness;

	internal Label lblContrast;

	internal NumericUpDown numContrast;

	internal clsRainbow bowContrast;

	internal XSlider sliderUIScale;

	internal clsRainbow bowUIScale;

	internal XSlider sliderBezelProtect;

	internal clsRainbow bowBezelProtect;

	internal XSlider sliderBottomOffset;

	internal clsRainbow bowBottomOffset;

	internal CheckBox chkDriveUIFullScreen;

	internal clsRainbow bowDriveUIFullScreen;

	internal Label lblDriveUITransparency;

	internal NumericUpDown numDriveUITransparency;

	internal clsRainbow bowDriveUITransparency;

	internal CheckBox chkSessionUIFullScreen;

	internal clsRainbow bowSessionUIFullScreen;

	internal Label lblSessionUITransparency;

	internal NumericUpDown numSessionUITransparency;

	internal clsRainbow bowSessionUITransparency;

	internal MetroTabItem tabVR;

	internal CheckBox chkOculusWait4Sync;

	internal CheckBox chkOpenVRWait4Sync;

	internal clsRainbow bowWait4Sync;

	internal Label lblVRMode;

	internal ComboBox cboVRMode;

	internal clsRainbow bowVRMode;

	internal CheckBox chkOculusEnabled;

	internal clsRainbow bowOculusEnabled;

	internal CheckBox chkOpenVREnabled;

	internal clsRainbow bowOpenVREnabled;

	internal CheckBox chkOpenXREnabled;

	internal clsRainbow bowOpenXREnabled;

	internal Label lblUIScreenWidthCM;

	internal NumericUpDown numUIScreenWidthCM;

	internal clsRainbow bowUIScreenWidthCM;

	internal Label lblUIScreenDistCM;

	internal NumericUpDown numUIScreenDistCM;

	internal clsRainbow bowUIScreenDistCM;

	internal Label lblFoveatedOuterPctRes;

	internal NumericUpDown numFoveatedOuterPctRes;

	internal clsRainbow bowFoveatedOuterPctRes;

	internal Label lblFoveatedInsetWidthPct;

	internal NumericUpDown numFoveatedInsetWidthPct;

	internal clsRainbow bowFoveatedInsetWidthPct;

	internal Label lblResolutionScalePct;

	internal NumericUpDown numResolutionScalePct;

	internal clsRainbow bowResolutionScalePct;

	internal MetroTabItem tabGraphics;

	internal Label lblSky;

	internal ComboBox cboSky;

	internal clsRainbow bowSky;

	internal Label lblCars;

	internal ComboBox cboCars;

	internal clsRainbow bowCars;

	internal Label lblPits;

	internal ComboBox cboPits;

	internal clsRainbow bowPits;

	internal Label lblEvent;

	internal ComboBox cboEvent;

	internal clsRainbow bowEvent;

	internal Label lblGrandstands;

	internal ComboBox cboGrandstands;

	internal clsRainbow bowGrandstands;

	internal Label lblCrowds;

	internal ComboBox cboCrowds;

	internal clsRainbow bowCrowds;

	internal Label lblObjects;

	internal ComboBox cboObjects;

	internal clsRainbow bowObjects;

	internal Label lblFoliage;

	internal ComboBox cboFoliage;

	internal clsRainbow bowFoliage;

	internal Label lblParticles;

	internal ComboBox cboParticles;

	internal clsRainbow bowParticles;

	internal CheckBox chkFullRes;

	internal clsRainbow bowFullRes;

	internal Label lblMaxCars;

	internal NumericUpDown numMaxCars;

	internal ComboBox cboDrawCars;

	internal clsRainbow bowDrawCars;

	internal ComboBox cboDrawPits;

	internal clsRainbow bowDrawPits;

	internal NumericUpDown numLODFPS;

	internal Label lblLODFPS;

	internal NumericUpDown numLODBias;

	internal Label lblLODBias;

	internal ComboBox cboLODWorld;

	internal clsRainbow bowLODWorld;

	internal ComboBox cboLODCar;

	internal clsRainbow bowLODCar;

	internal GroupBox boxFrameRate;

	internal RadioButton rbFPSNoLimit;

	internal RadioButton rbFPSVSync;

	internal RadioButton rbFPSLimit;

	internal NumericUpDown numFPSLimit;

	internal clsRainbow bowFPSFrameRate;

	internal Label lblMaxPrerenderedFrames;

	internal NumericUpDown numMaxPrerenderedFrames;

	internal clsRainbow bowMaxPrerenderedFrames;

	internal Label lblVisibilityFrameDelay;

	internal NumericUpDown numVisibilityFrameDelay;

	internal clsRainbow bowVisibilityFrameDelay;

	internal Label lblGpuMem;

	internal NumericUpDown numGpuMem;

	internal clsRainbow bowGpuMem;

	internal Label lblSysMem;

	internal NumericUpDown numSysMem;

	internal clsRainbow bowSysMem;

	internal Label lblAntiAliasMethod;

	internal ComboBox cboAntiAliasMethod;

	internal clsRainbow bowAntiAliasMethod;

	internal Label lblMSAASamples;

	internal ComboBox cboMSAASamples;

	internal clsRainbow bowMSAASamples;

	internal Label lblMSAAUseFilter;

	internal ComboBox cboMSAAUseFilter;

	internal clsRainbow bowMSAAUseFilter;

	internal CheckBox chkShadowMapsDay;

	internal clsRainbow bowShadowMapsDay;

	internal CheckBox chkObjSelfShadow;

	internal clsRainbow bowObjSelfShadow;

	internal Label lblObjDynamic;

	internal ComboBox cboObjDynamic;

	internal clsRainbow bowObjDynamic;

	internal CheckBox chkShadowMapsNight;

	internal CheckBox chkShadowWalls;

	internal clsRainbow bowShadowMapsNight;

	internal Label lblLights;

	internal NumericUpDown numLights;

	internal clsRainbow bowNumLights;

	internal Label lblShadowNightFilter;

	internal ComboBox cboShadowNightFilter;

	internal clsRainbow bowShadowNightFilter;

	internal Label lblDynCubeMaps;

	internal NumericUpDown numDynCubeMaps;

	internal clsRainbow bowDynCubeMaps;

	internal Label lblFixCubeMaps;

	internal NumericUpDown numFixCubeMaps;

	internal clsRainbow bowFixCubeMaps;

	internal Label lblShaderQuality;

	internal ComboBox cboShaderQuality;

	internal clsRainbow bowShaderQuality;

	internal Label lblDynShadowRes;

	internal ComboBox cboDynShadowRes;

	internal clsRainbow bowDynShadowRes;

	internal Label lblStaticShadowRes;

	internal ComboBox cboStaticShadowRes;

	internal clsRainbow bowStaticShadowRes;

	internal Label lblStaticShadowCount;

	internal NumericUpDown numStaticShadowCount;

	internal clsRainbow bowStaticShadowCount;

	internal CheckBox chkTrackDisplacement;

	internal CheckBox chkParallelSorting;

	internal clsRainbow bowTrackDisplacement;

	internal Label lblObstructions;

	internal ComboBox cboObstructions;

	internal clsRainbow bowObstructions;

	internal Label lblSteerWheel;

	internal ComboBox cboSteerWheel;

	internal clsRainbow bowSteerWheel;

	internal CheckBox chkTrees2Pass;

	internal clsRainbow bowTrees2Pass;

	internal CheckBox chkTreesHighQ;

	internal clsRainbow bowTreesHighQ;

	internal Label lblMirrors;

	internal ComboBox cboMirrors;

	internal clsRainbow bowMirrors;

	internal CheckBox chkMirrorHighQ;

	internal clsRainbow bowMirrorHighQ;

	internal Label lblHeadlights;

	internal ComboBox cboHeadlights;

	internal clsRainbow bowHeadlights;

	internal CheckBox chkHeadlightsTrackMirror;

	internal clsRainbow bowHeadlightsTrackMirror;

	internal CheckBox chkVMirror;

	internal ComboBox cboVMirrorSize;

	internal NumericUpDown numVMirrorFOV;

	internal clsRainbow bowVMirrorSize;

	internal Label lblMotionBlur;

	internal ComboBox cboMotionBlur;

	internal clsRainbow bowMotionBlur;

	internal Label lblSSR;

	internal ComboBox cboSSR;

	internal clsRainbow bowSSR;

	internal CheckBox chkSharpening;

	internal CheckBox chkDistortion;

	internal clsRainbow bowDistortion;

	internal CheckBox chkHDR;

	internal CheckBox chkAutoExposure;

	internal clsRainbow bowHDR;

	internal CheckBox chkSSAO;

	internal CheckBox chkMonoHeadlights;

	internal clsRainbow bowMonoHeadlights;

	internal CheckBox chkGPUMemSwap;

	internal clsRainbow bowGPUMemSwap;

	internal CheckBox chkCar2048;

	internal CheckBox chkHeatHaze;

	internal clsRainbow bowCar2048;

	internal CheckBox chkNumCustom;

	internal CheckBox chkZBuffer;

	internal clsRainbow bowZBuffer;

	internal Label lblFXAAEdge;

	internal ComboBox cboFXAAEdge;

	internal clsRainbow bowFXAAEdge;

	internal Label lblFXAASubPix;

	internal ComboBox cboFXAASubPix;

	internal clsRainbow bowFXAASubPix;

	internal MetroTabItem tabReplay;

	internal Label lblSkyReplay;

	internal ComboBox cboSkyReplay;

	internal clsRainbow bowSkyReplay;

	internal Label lblCarsReplay;

	internal ComboBox cboCarsReplay;

	internal clsRainbow bowCarsReplay;

	internal Label lblPitsReplay;

	internal ComboBox cboPitsReplay;

	internal clsRainbow bowPitsReplay;

	internal Label lblEventReplay;

	internal ComboBox cboEventReplay;

	internal clsRainbow bowEventReplay;

	internal Label lblGrandstandsReplay;

	internal ComboBox cboGrandstandsReplay;

	internal clsRainbow bowGrandstandsReplay;

	internal Label lblCrowdsReplay;

	internal ComboBox cboCrowdsReplay;

	internal clsRainbow bowCrowdsReplay;

	internal Label lblObjectsReplay;

	internal ComboBox cboObjectsReplay;

	internal clsRainbow bowObjectsReplay;

	internal Label lblFoliageReplay;

	internal ComboBox cboFoliageReplay;

	internal clsRainbow bowFoliageReplay;

	internal Label lblParticlesReplay;

	internal ComboBox cboParticlesReplay;

	internal clsRainbow bowParticlesReplay;

	internal CheckBox chkFullResReplay;

	internal clsRainbow bowFullResReplay;

	internal ComboBox cboDrawCarsReplay;

	internal clsRainbow bowDrawCarsReplay;

	internal ComboBox cboDrawPitsReplay;

	internal clsRainbow bowDrawPitsReplay;

	internal NumericUpDown numLODFPSReplay;

	internal Label lblLODFPSReplay;

	internal ComboBox cboLODWorldReplay;

	internal clsRainbow bowLODWorldReplay;

	internal ComboBox cboLODCarReplay;

	internal clsRainbow bowLODCarReplay;

	internal CheckBox chkShadowMapsDayReplay;

	internal clsRainbow bowShadowMapsDayReplay;

	internal CheckBox chkObjSelfShadowReplay;

	internal clsRainbow bowObjSelfShadowReplay;

	internal Label lblObjDynamicReplay;

	internal ComboBox cboObjDynamicReplay;

	internal clsRainbow bowObjDynamicReplay;

	internal CheckBox chkShadowMapsNightReplay;

	internal CheckBox chkShadowWallsReplay;

	internal clsRainbow bowShadowMapsNightReplay;

	internal clsRainbow bowShadowHeadlightsReplay;

	internal Label lblLightsReplay;

	internal NumericUpDown numLightsReplay;

	internal clsRainbow bowNumLightsReplay;

	internal Label lblShadowNightFilterReplay;

	internal ComboBox cboShadowNightFilterReplay;

	internal clsRainbow bowShadowNightFilterReplay;

	internal Label lblDynCubeMapsReplay;

	internal NumericUpDown numDynCubeMapsReplay;

	internal clsRainbow bowDynCubeMapsReplay;

	internal Label lblFixCubeMapsReplay;

	internal NumericUpDown numFixCubeMapsReplay;

	internal clsRainbow bowFixCubeMapsReplay;

	internal Label lblObstructionsReplay;

	internal ComboBox cboObstructionsReplay;

	internal clsRainbow bowObstructionsReplay;

	internal Label lblSteerWheelReplay;

	internal ComboBox cboSteerWheelReplay;

	internal clsRainbow bowSteerWheelReplay;

	internal CheckBox chkTrees2PassReplay;

	internal clsRainbow bowTrees2PassReplay;

	internal Label lblMirrorsReplay;

	internal ComboBox cboMirrorsReplay;

	internal clsRainbow bowMirrorsReplay;

	internal CheckBox chkMirrorHighQReplay;

	internal clsRainbow bowMirrorHighQReplay;

	internal Label lblMotionBlurReplay;

	internal ComboBox cboMotionBlurReplay;

	internal clsRainbow bowMotionBlurReplay;

	internal CheckBox chkMotionBlurCarCams;

	internal CheckBox chkMotionBlurBroadcast;

	internal clsRainbow bowMotionBlurBroadcast;

	internal Label lblSSRReplay;

	internal ComboBox cboSSRReplay;

	internal clsRainbow bowSSRReplay;

	internal CheckBox chkSharpeningReplay;

	internal CheckBox chkDistortionReplay;

	internal clsRainbow bowDistortionReplay;

	internal CheckBox chkSSAOReplay;

	internal clsRainbow bowSSAOReplay;

	internal CheckBox chkDOFReplay;

	internal CheckBox chkHeatHazeReplay;

	internal clsRainbow bowHeatHazeReplay;

	internal CheckBox chkRenderReplay;

	internal clsRainbow bowRenderReplay;

	internal Label lblFXAAEdgeReplay;

	internal ComboBox cboFXAAEdgeReplay;

	internal clsRainbow bowFXAAEdgeReplay;

	internal Label lblFXAASubPixReplay;

	internal ComboBox cboFXAASubPixReplay;

	internal clsRainbow bowFXAASubPixReplay;

	internal MetroTabItem tabExtras;

	internal CheckBox chkTicker;

	internal clsRainbow bowTicker;

	internal CheckBox chkConnectSockets;

	internal clsRainbow bowConnectSockets;

	internal CheckBox chkSDKEnableMem;

	internal clsRainbow bowSDKEnableMem;

	internal CheckBox chkAutoTelemetry;

	internal clsRainbow bowAutoTelemetry;

	internal CheckBox chkAskSaveReplay;

	internal clsRainbow bowAskSaveReplay;

	internal CheckBox chkAutoResetFastRepair;

	internal clsRainbow bowAutoResetFastRepair;

	internal CheckBox chkAutoResetPitBox;

	internal clsRainbow bowAutoResetPitBox;

	internal CheckBox chkAutoFuel;

	internal clsRainbow bowAutoFuel;

	internal Label lblAutoFuelLapMargin;

	internal NumericUpDown numAutoFuelLapMargin;

	internal clsRainbow bowAutoFuelLapMargin;

	internal CheckBox chkForceCrowdVisible;

	internal clsRainbow bowForceCrowdVisible;

	internal CheckBox chkForceControlsVisible;

	internal clsRainbow bowForceControlsVisible;

	internal CheckBox chkShowMsgUsr;

	internal clsRainbow bowShowMsgUsr;

	internal CheckBox chkShowMsgSys;

	internal clsRainbow bowShowMsgSys;

	internal CheckBox chkShowMsgInc;

	internal clsRainbow bowShowMsgInc;

	internal CheckBox chkShowMsgJoin;

	internal clsRainbow bowShowMsgJoin;

	internal CheckBox chkLoadPaintDriving;

	internal clsRainbow bowLoadPaintDriving;

	internal Label lblDriveBlackBox;

	internal ComboBox cboDriveBlackBox;

	internal clsRainbow bowDriveBlackBox;

	internal Label lblPitBlackBox;

	internal ComboBox cboPitBlackBox;

	internal clsRainbow bowPitBlackBox;

	internal CheckBox chkDisableSplitsRaceStart;

	internal clsRainbow bowDisableSplitsRaceStart;

	internal CheckBox chkFadeGhost;

	internal clsRainbow bowFadeGhost;

	internal Label lblGhostOffset;

	internal NumericUpDown numGhostOffset;

	internal clsRainbow bowGhostOffset;

	internal Label lblGhostOpacity;

	internal NumericUpDown numGhostOpacity;

	internal clsRainbow bowGhostOpacity;

	internal Label lblWorkerThreads;

	internal NumericUpDown numWorkerThreads;

	internal clsRainbow bowWorkerThreads;

	internal Label lblDamageThreads;

	internal NumericUpDown numDamageThreads;

	internal clsRainbow bowDamageThreads;

	internal Label lblTestPitLocation;

	internal NumericUpDown numTestPitLocation;

	internal clsRainbow bowTestPitLocation;

	internal Label lblDriveHeightAdj;

	internal NumericUpDown numDriveHeightAdj;

	internal clsRainbow bowDriveHeightAdj;

	internal Label lblDriveVanishY;

	internal NumericUpDown numDriveVanishY;

	internal clsRainbow bowDriveVanishY;

	internal Label lblLookInstant;

	internal ComboBox cboLookInstant;

	internal clsRainbow bowLookInstant;

	internal Label lblLookSideAngle;

	internal NumericUpDown numLookSideAngle;

	internal clsRainbow bowLookSideAngle;

	internal Label lblLookUpAngle;

	internal NumericUpDown numLookUpAngle;

	internal clsRainbow bowLookUpAngle;

	internal Label lblLookDownAngle;

	internal NumericUpDown numLookDownAngle;

	internal clsRainbow bowLookDownAngle;

	internal CheckBox chkCompressVertices;

	internal clsRainbow bowCompressVertices;

	internal CheckBox chkCompressCars;

	internal clsRainbow bowCompressCars;

	internal CheckBox chkCompressSuits;

	internal clsRainbow bowCompressSuits;

	internal CheckBox chkCompressHelmets;

	internal clsRainbow bowCompressHelmets;

	internal CheckBox chkPauseReplayOnExit;

	internal clsRainbow bowPauseReplayOnExit;

	internal Label lblReplaySecondsNormal;

	internal NumericUpDown numReplaySecondsNormal;

	internal clsRainbow bowReplaySecondsNormal;

	internal Label lblReplaySecondsTeam;

	internal NumericUpDown numReplaySecondsTeam;

	internal clsRainbow bowReplaySecondsTeam;

	internal CheckBox chkCaptureEnabled;

	internal clsRainbow bowCaptureEnabled;

	internal Label lblScreenshotFmt;

	internal ComboBox cboScreenshotFmt;

	internal clsRainbow bowScreenshotFmt;

	internal Label lblScreenshotWidth;

	internal NumericUpDown numScreenshotWidth;

	internal clsRainbow bowScreenshotWidth;

	internal Label lblScreenshotHeight;

	internal NumericUpDown numScreenshotHeight;

	internal clsRainbow bowScreenshotHeight;

	internal Label lblVideoFmt;

	internal ComboBox cboVideoFmt;

	internal clsRainbow bowVideoFmt;

	internal Label lblVideoFrameRate;

	internal ComboBox cboVideoFrameRate;

	internal clsRainbow bowVideoFrameRate;

	internal Label lblVideoImgSize;

	internal ComboBox cboVideoImgSize;

	internal clsRainbow bowVideoImgSize;

	internal CheckBox chkHiContrastCursor;

	internal clsRainbow bowHiContrastCursor;

	internal CheckBox chkPitLineVisible;

	internal clsRainbow bowPitLineVisible;

	internal Label lblRaceLineWidth;

	internal NumericUpDown numRaceLineWidth;

	internal clsRainbow bowRaceLineWidth;

	internal Label lblPitLine;

	internal ColorPicker colPitLine;

	internal clsRainbow bowPitLine;

	internal Label lblFastLine;

	internal ColorPicker colFastLine;

	internal clsRainbow bowFastLine;

	internal Label lblSameLine;

	internal ColorPicker colSameLine;

	internal clsRainbow bowSameLine;

	internal Label lblSlowLine;

	internal ColorPicker colSlowLine;

	internal clsRainbow bowSlowLine;

	internal MetroTabItem tabFFB;

	internal CheckBox chkWheelDisplay;

	internal clsRainbow bowWheelDisplay;

	internal CheckBox chkWheelDisplayBlink;

	internal clsRainbow bowWheelDisplayBlink;

	internal CheckBox chkVibratePedalWheel;

	internal clsRainbow bowVibratePedalWheel;

	internal CheckBox chkFFB360Hz;

	internal clsRainbow bowFFB360Hz;

	internal CheckBox chkAsetekAPI;

	internal clsRainbow bowAsetekAPI;

	internal CheckBox chkConspitAPI;

	internal clsRainbow bowConspitAPI;

	internal CheckBox chkFanatecAPI;

	internal clsRainbow bowFanatecAPI;

	internal CheckBox chkMozaAPI;

	internal clsRainbow bowMozaAPI;

	internal CheckBox chkSimagicAPI;

	internal clsRainbow bowSimagicAPI;

	internal CheckBox chkSimuCubeAPI;

	internal clsRainbow bowSimuCubeAPI;

	internal CheckBox chkVRSAPI;

	internal clsRainbow bowVRSAPI;

	internal Label lblFFBScaling;

	internal NumericUpDown numFFBScaling;

	internal clsRainbow bowFFBScaling;

	internal Label lblFFBSmoothing;

	internal ComboBox cboFFBSmoothing;

	internal clsRainbow bowFFBSmoothing;

	internal Label lblFFBClutchLaunchMode;

	internal ComboBox cboFFBClutchLaunchMode;

	internal clsRainbow bowFFBClutchLaunchMode;

	internal CheckBox chkTrueForceAPI;

	internal clsRainbow bowTrueForceAPI;

	internal CheckBox chkTrueForceVibe;

	internal clsRainbow bowTrueForceVibe;

	internal CheckBox chkForceVibePhysics;

	internal clsRainbow bowForceVibePhysics;

	internal XSlider sliderTrueForceDamper;

	internal clsRainbow bowTrueForceDamper;

	internal XSlider sliderTrueForceMaster;

	internal clsRainbow bowTrueForceMaster;

	internal XSlider sliderTrueForceCarBody;

	internal clsRainbow bowTrueForceCarBody;

	internal XSlider sliderTrueForceDriveShaft;

	internal clsRainbow bowTrueForceDriveShaft;

	internal XSlider sliderTrueForceEngineRPM;

	internal clsRainbow bowTrueForceEngineRPM;

	internal XSlider sliderTrueForceGearChange;

	internal clsRainbow bowTrueForceGearChange;

	internal XSlider sliderTrueForceRevLimit;

	internal clsRainbow bowTrueForceRevLimit;

	internal XSlider sliderTrueForceRoadTexture;

	internal clsRainbow bowTrueForceRoadTexture;

	internal XSlider sliderTrueForceRumbleStrip;

	internal clsRainbow bowTrueForceRumbleStrip;

	internal XSlider sliderTrueForceWheelSlip;

	internal clsRainbow bowTrueForceWheelSlip;

	internal MetroTabItem tabMacros;

	internal clsRainbow bowMacroEnable;

	internal clsRainbow bowMacro01;

	internal clsRainbow bowMacro02;

	internal clsRainbow bowMacro03;

	internal clsRainbow bowMacro04;

	internal clsRainbow bowMacro05;

	internal clsRainbow bowMacro06;

	internal clsRainbow bowMacro07;

	internal clsRainbow bowMacro08;

	internal clsRainbow bowMacro09;

	internal clsRainbow bowMacro10;

	internal clsRainbow bowMacro11;

	internal clsRainbow bowMacro12;

	internal clsRainbow bowMacro13;

	internal clsRainbow bowMacro14;

	internal clsRainbow bowMacro15;

	internal CheckBox chkMacroEnable;

	internal TextBox txtMacro01;

	internal TextBox txtMacro02;

	internal TextBox txtMacro03;

	internal TextBox txtMacro04;

	internal TextBox txtMacro05;

	internal TextBox txtMacro06;

	internal TextBox txtMacro07;

	internal TextBox txtMacro08;

	internal TextBox txtMacro09;

	internal TextBox txtMacro10;

	internal TextBox txtMacro11;

	internal TextBox txtMacro12;

	internal TextBox txtMacro13;

	internal TextBox txtMacro14;

	internal TextBox txtMacro15;

	internal MetroTabItem tabSound;

	internal Label lblSoundDevice;

	internal clsRainbow bowSoundDevice;

	internal CheckBox chkAmbientMusic;

	internal clsRainbow bowAmbientMusic;

	internal Label lblDimensions;

	internal ComboBox cboDimensions;

	internal clsRainbow bowDimensions;

	internal Label lblNotification;

	internal ComboBox cboNotification;

	internal clsRainbow bowNotification;

	internal CheckBox chkDownshiftAlert;

	internal clsRainbow bowDownshiftAlert;

	internal CheckBox chkRotateVR;

	internal clsRainbow bowRotateVR;

	internal XSlider sliderLoudMaster;

	internal clsRainbow bowLoudMaster;

	internal XSlider sliderLoudEngines;

	internal clsRainbow bowLoudEngines;

	internal XSlider sliderLoudTyres;

	internal clsRainbow bowLoudTyres;

	internal XSlider sliderLoudCrashes;

	internal clsRainbow bowLoudCrashes;

	internal XSlider sliderLoudWind;

	internal clsRainbow bowLoudWind;

	internal XSlider sliderLoudRain;

	internal clsRainbow bowLoudRain;

	internal XSlider sliderLoudInCar;

	internal clsRainbow bowLoudInCar;

	internal XSlider sliderLoudAmbient;

	internal clsRainbow bowLoudAmbient;

	internal XSlider sliderLoudSpotter;

	internal clsRainbow bowLoudSpotter;

	internal XSlider sliderLoudChat;

	internal clsRainbow bowLoudChat;

	internal XSlider sliderLoudReplay;

	internal clsRainbow bowLoudReplay;

	internal Label lblEarProtection;

	internal ComboBox cboEarProtection;

	internal clsRainbow bowEarProtection;

	internal Label lblCompressorReplay;

	internal ComboBox cboCompressorReplay;

	internal clsRainbow bowCompressorReplay;

	internal Label lblChatSpeaker;

	internal clsRainbow bowChatSpeaker;

	internal Label lblChatMike;

	internal clsRainbow bowChatMike;

	internal CheckBox chkEnableChat;

	internal CheckBox chkChatMuted;

	internal clsRainbow bowEnableChat;

	internal CheckBox chkChatWhileDriving;

	internal clsRainbow bowChatWhileDriving;

	internal Label lblLFEDevice;

	internal clsRainbow bowLFEDevice;

	internal CheckBox chkEnableLFE;

	internal clsRainbow bowEnableLFE;

	internal CheckBox chk10dbCut;

	internal clsRainbow bow10dbCut;

	internal XSlider sliderLFEMaster;

	internal clsRainbow bowLFEMaster;

	internal XSlider sliderLFEGame;

	internal clsRainbow bowLFEGame;

	internal XSlider sliderLFEImpact;

	internal clsRainbow bowLFEImpact;

	internal XSlider sliderLFEEngine;

	internal clsRainbow bowLFEEngine;

	internal XSlider sliderLFEGear;

	internal clsRainbow bowLFEGear;

	internal XSlider sliderLFERevLimiter;

	internal clsRainbow bowLFERevLimiter;

	internal XSlider sliderLFERumble;

	internal clsRainbow bowLFERumble;

	internal XSlider sliderLFEWheels;

	internal clsRainbow bowLFEWheels;

	internal XSlider sliderLFERoadTexture;

	internal clsRainbow bowLFERoadTexture;

	internal XSlider sliderLFELowPassFreq;

	internal clsRainbow bowLFELowPassFreq;

	internal CheckBox chkUiControlsInDb;

	internal clsRainbow bowUiControlsInDb;

	internal MetroTabItem tabSpotter;

	internal Label lblSpotDevice;

	internal clsRainbow bowSpotDevice;

	internal CheckBox chkSpotEnable;

	internal clsRainbow bowSpotEnable;

	internal CheckBox chkSpotMuteIfLive;

	internal clsRainbow bowSpotMuteIfLive;

	internal CheckBox chkSpotReduceIfLive;

	internal clsRainbow bowSpotReduceIfLive;

	internal CheckBox chkShowSpotterUIForSpectators;

	internal clsRainbow bowShowSpotterUIForSpectators;

	internal Label lblSpotDisplay;

	internal ComboBox cboSpotDisplay;

	internal clsRainbow bowSpotDisplay;

	internal Label lblSpotVoice;

	internal ComboBox cboSpotVoice;

	internal clsRainbow bowSpotVoice;

	internal Label lblSpotChatty;

	internal ComboBox cboSpotChatty;

	internal clsRainbow bowSpotChatty;

	internal Label lblHushDuration;

	internal NumericUpDown numHushDuration;

	internal clsRainbow bowHushDuration;

	internal CheckBox chkSpotHiLoStart;

	internal clsRainbow bowSpotHiLoStart;

	internal Label lblSpotHiLowPadding;

	internal NumericUpDown numSpotHiLowPadding;

	internal clsRainbow bowSpotHiLowPadding;

	internal CheckBox chkLeaderChanged;

	internal clsRainbow bowLeaderChanged;

	internal CheckBox chkLeaderTimes;

	internal clsRainbow bowLeaderTimes;

	internal CheckBox chkReportGap;

	internal clsRainbow bowReportGap;

	internal CheckBox chkLappingTraffic;

	internal clsRainbow bowLappingTraffic;

	internal CheckBox chkPitboxCountdown;

	internal clsRainbow bowPitboxCountdown;

	internal CheckBox chkPitNotify;

	internal clsRainbow bowPitNotify;

	internal CheckBox chkReportLaps;

	internal CheckBox chkReportMinute;

	internal clsRainbow bowReportLaps;

	internal Label lblSpotPrecision;

	internal NumericUpDown numSpotPrecision;

	internal clsRainbow bowSpotPrecision;

	internal Label lblSpotReporting;

	internal ComboBox cboSpotReporting;

	internal clsRainbow bowSpotReporting;

	internal XSlider sliderSPCCTextFactor;

	internal clsRainbow bowSPCCTextFactor;

	internal CheckBox chkReportFuel;

	internal clsRainbow bowReportFuel;

	internal CheckBox chkNewClassBest;

	internal clsRainbow bowNewClassBest;

	internal CheckBox chkNewClassBestTime;

	internal clsRainbow bowNewClassBestTime;

	internal CheckBox chkNewPersonalBestRace;

	internal clsRainbow bowNewPersonalBestRace;

	internal CheckBox chkNewPersonalBest;

	internal clsRainbow bowNewPersonalBest;

	internal MetroTabItem tabNotes;

	internal TextBox txtNotes;

	internal MetroTabControl tabPanel;

	internal MetroTabItem panelProfiles;

	internal ToolBar barProfiles;

	internal Grid gridValues;

	internal MetroTabControl tabProfiles;

	internal MetroTabItem panelGFXWizard;

	internal StackPanel stackGFXWizard;

	internal StackPanel stackGFXCard;

	internal ToolBar barGFXWizard;

	internal ComboBox cboGPUCard;

	internal Label lblGFXPreset;

	internal Button btnGFXPreset;

	internal ImageBrush imgGFXPreset;

	internal Button btnCloseWizard;

	internal ImageBrush imgCloseWizard;

	internal TextBlock txtGFXCard;

	internal ItemsControl itemsGFXWizard;

	internal MetroTabItem panelSearch;

	internal Button btnCloseSearch;

	internal ImageBrush imgCloseSearch;

	internal TextBox txtSearch;

	internal Button btnSearch;

	internal ImageBrush imgSearch;

	internal ListView lstControls;

	private bool _contentLoaded;

	private void InitTabAutoChat()
	{
		bowMacroEnable.InitControl(this, chkMacroEnable);
		bowMacro01.InitControl(this, txtMacro01);
		bowMacro02.InitControl(this, txtMacro02);
		bowMacro03.InitControl(this, txtMacro03);
		bowMacro04.InitControl(this, txtMacro04);
		bowMacro05.InitControl(this, txtMacro05);
		bowMacro06.InitControl(this, txtMacro06);
		bowMacro07.InitControl(this, txtMacro07);
		bowMacro08.InitControl(this, txtMacro08);
		bowMacro09.InitControl(this, txtMacro09);
		bowMacro10.InitControl(this, txtMacro10);
		bowMacro11.InitControl(this, txtMacro11);
		bowMacro12.InitControl(this, txtMacro12);
		bowMacro13.InitControl(this, txtMacro13);
		bowMacro14.InitControl(this, txtMacro14);
		bowMacro15.InitControl(this, txtMacro15);
	}

	private void DataLoadTabAutoChat()
	{
		loadCheckbox(chkMacroEnable, bowMacroEnable);
		loadTextbox(txtMacro01, bowMacro01);
		loadTextbox(txtMacro02, bowMacro02);
		loadTextbox(txtMacro03, bowMacro03);
		loadTextbox(txtMacro04, bowMacro04);
		loadTextbox(txtMacro05, bowMacro05);
		loadTextbox(txtMacro06, bowMacro06);
		loadTextbox(txtMacro07, bowMacro07);
		loadTextbox(txtMacro08, bowMacro08);
		loadTextbox(txtMacro09, bowMacro09);
		loadTextbox(txtMacro10, bowMacro10);
		loadTextbox(txtMacro11, bowMacro11);
		loadTextbox(txtMacro12, bowMacro12);
		loadTextbox(txtMacro13, bowMacro13);
		loadTextbox(txtMacro14, bowMacro14);
		loadTextbox(txtMacro15, bowMacro15);
	}

	private void InitTabDisplay()
	{
		TimerFOV.Interval = new TimeSpan(0, 0, 5);
		TimerFOV.Tick += TimerFOV_Tick;
		bowResolution.InitBasic(this, cboResolution, lblResolution);
		bowRefreshRate.InitControl(this, numRefreshRate, lblRefreshRate);
		bowResScale.InitComboIndex(this, cboResScale, lblResScale);
		bowFSRSharpness.InitControl(this, numFSRSharpness, lblFSRSharpness);
		bowDeviceIdx.InitControl(this, numDeviceIdx, lblDeviceIdx);
		bowNumScreens.InitBasic(this, cboNumMonitors, lblNumMonitors);
		bowDrawCars.tagAdd(iniDX11.MonitorType.TagID);
		bowRadius.InitComboValue(this, cboRadius, lblRadius);
		bowReflex.InitComboIndex(this, cboReflex, lblReflex);
		bowMultiProjection.InitControl(this, chkMultiProjection);
		bowMultiProjection.InitControl(this, chkSMP);
		bowAutoConfigDone.InitControl(this, chkAutoConfigDone);
		bowHDRFormat.InitControl(this, chkHDRFormat);
		bowFullScreen.InitControl(this, chkFullScreen);
		bowBorder.InitControl(this, chkBorder);
		bowAlign.InitComboIndex(this, cboAlign, lblAlign);
		bowGamma.InitControl(this, numGamma, lblGamma);
		bowContrast.InitControl(this, numContrast, lblContrast);
		bowBrightness.InitControl(this, numBrightness, lblBrightness);
		bowMonitorWidth.InitControl(this, numMonitorWidth, lblMonitorWidth);
		bowMonitorVisible.InitControl(this, numMonitorVisible, lblMonitorVisible);
		bowDiagonalWidth.InitControl(this, numDiagonalWidth, lblDiagonalWidth);
		bowScreenAngle.InitControl(this, numScreenAngle, lblScreenAngle);
		bowViewDistance.InitControl(this, numViewDistance, lblViewDistance);
		bowFOV.InitControl(this, numFOV, lblFOV);
		bowUIScale.InitControl(this, sliderUIScale);
		bowBezelProtect.InitControl(this, sliderBezelProtect);
		bowBottomOffset.InitControl(this, sliderBottomOffset);
		bowDriveUIFullScreen.InitControl(this, chkDriveUIFullScreen);
		bowDriveUITransparency.InitDecimal(this, numDriveUITransparency, lblDriveUITransparency);
		bowSessionUIFullScreen.InitControl(this, chkSessionUIFullScreen);
		bowSessionUITransparency.InitDecimal(this, numSessionUITransparency, lblSessionUITransparency);
		cboNumMonitors.SelectionChanged += CboNumScreens_SelectionChanged;
		bowWait4Sync.InitControl(this, chkOculusWait4Sync);
		bowWait4Sync.InitControl(this, chkOpenVRWait4Sync);
		bowVRMode.InitComboIndex(this, cboVRMode, lblVRMode);
		bowOculusEnabled.InitControl(this, chkOculusEnabled);
		bowOpenVREnabled.InitControl(this, chkOpenVREnabled);
		bowOpenXREnabled.InitControl(this, chkOpenXREnabled);
		bowUIScreenDistCM.InitControl(this, numUIScreenDistCM, lblUIScreenDistCM);
		bowUIScreenWidthCM.InitControl(this, numUIScreenWidthCM, lblUIScreenWidthCM);
		bowFoveatedOuterPctRes.InitControl(this, numFoveatedOuterPctRes, lblFoveatedOuterPctRes);
		bowFoveatedInsetWidthPct.InitControl(this, numFoveatedInsetWidthPct, lblFoveatedInsetWidthPct);
		bowResolutionScalePct.InitControl(this, numResolutionScalePct, lblResolutionScalePct);
	}

	private void DataLoadTabDisplay()
	{
		txtDisplayMode.Text = ProfileView.DisplayMode.ToString();
		loadNumber(numRefreshRate, bowRefreshRate);
		loadNumber(numDeviceIdx, bowDeviceIdx);
		loadComboIndex(cboReflex, bowReflex);
		if (ProfileView.DisplayMode != iniDisplayMode.Monitor)
		{
			sliderBezelProtect.Visibility = Visibility.Hidden;
			bowBezelProtect.Visibility = Visibility.Hidden;
			lblRadius.Visibility = Visibility.Hidden;
			cboRadius.Visibility = Visibility.Hidden;
			bowRadius.Visibility = Visibility.Hidden;
			chkMultiProjection.Visibility = Visibility.Hidden;
			chkSMP.Visibility = Visibility.Hidden;
			bowMultiProjection.Visibility = Visibility.Hidden;
			lblMonitorWidth.Visibility = Visibility.Hidden;
			numMonitorWidth.Visibility = Visibility.Hidden;
			bowMonitorWidth.Visibility = Visibility.Hidden;
			lblMonitorVisible.Visibility = Visibility.Hidden;
			numMonitorVisible.Visibility = Visibility.Hidden;
			bowMonitorVisible.Visibility = Visibility.Hidden;
			lblDiagonalWidth.Visibility = Visibility.Hidden;
			numDiagonalWidth.Visibility = Visibility.Hidden;
			bowDiagonalWidth.Visibility = Visibility.Hidden;
			lblScreenAngle.Visibility = Visibility.Hidden;
			numScreenAngle.Visibility = Visibility.Hidden;
			bowScreenAngle.Visibility = Visibility.Hidden;
			btnScreenAngle.Visibility = Visibility.Hidden;
			lblViewDistance.Visibility = Visibility.Hidden;
			numViewDistance.Visibility = Visibility.Hidden;
			bowViewDistance.Visibility = Visibility.Hidden;
			btnViewDistance.Visibility = Visibility.Hidden;
			lblFOV.Visibility = Visibility.Hidden;
			numFOV.Visibility = Visibility.Hidden;
			bowFOV.Visibility = Visibility.Hidden;
			btnFOV.Visibility = Visibility.Hidden;
			grpFOV.Visibility = Visibility.Hidden;
			lblNumMonitors.Visibility = Visibility.Hidden;
			cboNumMonitors.Visibility = Visibility.Hidden;
			bowNumScreens.Visibility = Visibility.Hidden;
			lblResolution.Visibility = Visibility.Hidden;
			cboResolution.Visibility = Visibility.Hidden;
			bowResolution.Visibility = Visibility.Hidden;
			MonitorList.SetVisible(Visibility.Hidden);
			tabVR.Visibility = Visibility.Visible;
			loadComboIndex(cboVRMode, bowVRMode);
			switch (ProfileView.DisplayMode)
			{
			case iniDisplayMode.Oculus:
				chkOculusWait4Sync.Visibility = Visibility.Visible;
				bowWait4Sync.Visibility = Visibility.Visible;
				loadCheckbox(chkOculusEnabled, bowOculusEnabled);
				chkOculusEnabled.Visibility = Visibility.Visible;
				bowOculusEnabled.Visibility = Visibility.Visible;
				chkOpenVREnabled.Visibility = Visibility.Hidden;
				bowOpenVREnabled.Visibility = Visibility.Hidden;
				chkOpenXREnabled.Visibility = Visibility.Hidden;
				bowOpenXREnabled.Visibility = Visibility.Hidden;
				break;
			case iniDisplayMode.OpenVR:
				chkOpenVRWait4Sync.Visibility = Visibility.Visible;
				bowWait4Sync.Visibility = Visibility.Visible;
				loadCheckbox(chkOpenVREnabled, bowOpenVREnabled);
				chkOculusEnabled.Visibility = Visibility.Hidden;
				bowOculusEnabled.Visibility = Visibility.Hidden;
				chkOpenVREnabled.Visibility = Visibility.Visible;
				bowOpenVREnabled.Visibility = Visibility.Visible;
				chkOpenXREnabled.Visibility = Visibility.Hidden;
				bowOpenXREnabled.Visibility = Visibility.Hidden;
				break;
			case iniDisplayMode.OpenXR:
				chkOculusWait4Sync.Visibility = Visibility.Hidden;
				chkOpenVRWait4Sync.Visibility = Visibility.Hidden;
				bowWait4Sync.Visibility = Visibility.Hidden;
				loadCheckbox(chkOpenXREnabled, bowOpenXREnabled);
				chkOculusEnabled.Visibility = Visibility.Hidden;
				bowOculusEnabled.Visibility = Visibility.Hidden;
				chkOpenVREnabled.Visibility = Visibility.Hidden;
				bowOpenVREnabled.Visibility = Visibility.Hidden;
				chkOpenXREnabled.Visibility = Visibility.Visible;
				bowOpenXREnabled.Visibility = Visibility.Visible;
				loadNumber(numUIScreenDistCM, bowUIScreenDistCM);
				loadNumber(numUIScreenWidthCM, bowUIScreenWidthCM);
				loadNumber(numFoveatedOuterPctRes, bowFoveatedOuterPctRes);
				loadNumber(numFoveatedInsetWidthPct, bowFoveatedInsetWidthPct);
				loadNumber(numResolutionScalePct, bowResolutionScalePct);
				break;
			}
		}
		else
		{
			sliderBezelProtect.Visibility = Visibility.Visible;
			bowBezelProtect.Visibility = Visibility.Visible;
			lblRadius.Visibility = Visibility.Visible;
			cboRadius.Visibility = Visibility.Visible;
			bowRadius.Visibility = Visibility.Visible;
			chkMultiProjection.Visibility = Visibility.Visible;
			bowMultiProjection.Visibility = Visibility.Visible;
			loadSlider(sliderBezelProtect, bowBezelProtect);
			loadComboValue(cboRadius, bowRadius);
			loadCheckbox(chkMultiProjection, null);
			loadCheckbox(chkSMP, bowMultiProjection);
			lblMonitorWidth.Visibility = Visibility.Visible;
			numMonitorWidth.Visibility = Visibility.Visible;
			bowMonitorWidth.Visibility = Visibility.Visible;
			lblMonitorVisible.Visibility = Visibility.Visible;
			numMonitorVisible.Visibility = Visibility.Visible;
			bowMonitorVisible.Visibility = Visibility.Visible;
			lblDiagonalWidth.Visibility = Visibility.Visible;
			numDiagonalWidth.Visibility = Visibility.Visible;
			bowDiagonalWidth.Visibility = Visibility.Visible;
			lblScreenAngle.Visibility = Visibility.Visible;
			numScreenAngle.Visibility = Visibility.Visible;
			bowScreenAngle.Visibility = Visibility.Visible;
			btnScreenAngle.Visibility = Visibility.Visible;
			lblViewDistance.Visibility = Visibility.Visible;
			numViewDistance.Visibility = Visibility.Visible;
			btnViewDistance.Visibility = Visibility.Visible;
			bowViewDistance.Visibility = Visibility.Visible;
			lblFOV.Visibility = Visibility.Visible;
			numFOV.Visibility = Visibility.Visible;
			bowFOV.Visibility = Visibility.Visible;
			btnFOV.Visibility = Visibility.Visible;
			loadNumber(numMonitorWidth, bowMonitorWidth);
			loadNumber(numMonitorVisible, bowMonitorVisible);
			loadNumber(numDiagonalWidth, bowDiagonalWidth);
			loadNumber(numScreenAngle, bowScreenAngle);
			loadNumber(numViewDistance, bowViewDistance);
			grpFOV.Visibility = Visibility.Visible;
			loadNumber(numFOV, bowFOV);
			MonitorList.SetSelected(ProfileView.getDisplayBounds());
			CheckSelectedAlignment();
			lblNumMonitors.Visibility = Visibility.Visible;
			cboNumMonitors.Visibility = Visibility.Visible;
			bowNumScreens.Visibility = Visibility.Visible;
			lblResolution.Visibility = Visibility.Visible;
			cboResolution.Visibility = Visibility.Visible;
			bowResolution.Visibility = Visibility.Visible;
			loadResolution();
			loadNumScreens();
			MonitorCalculations();
			tabVR.Visibility = Visibility.Hidden;
			chkOculusWait4Sync.Visibility = Visibility.Hidden;
			chkOpenVRWait4Sync.Visibility = Visibility.Hidden;
			bowWait4Sync.Visibility = Visibility.Hidden;
			lblResScale.Visibility = Visibility.Visible;
			cboResScale.Visibility = Visibility.Visible;
			bowResScale.Visibility = Visibility.Visible;
			lblFSRSharpness.Visibility = Visibility.Visible;
			numFSRSharpness.Visibility = Visibility.Visible;
			bowFSRSharpness.Visibility = Visibility.Visible;
		}
		loadComboIndex(cboResScale, bowResScale);
		loadNumber(numFSRSharpness, bowFSRSharpness);
		loadCheckbox(chkAutoConfigDone, bowAutoConfigDone);
		loadCheckbox(chkHDRFormat, bowHDRFormat);
		loadCheckbox(chkFullScreen, bowFullScreen);
		loadCheckbox(chkBorder, bowBorder);
		loadComboIndex(cboAlign, bowAlign);
		loadNumber(numGamma, bowGamma);
		loadNumber(numContrast, bowContrast);
		loadNumber(numBrightness, bowBrightness);
		loadSlider(sliderUIScale, bowUIScale);
		loadSlider(sliderBottomOffset, bowBottomOffset);
		loadCheckbox(chkDriveUIFullScreen, bowDriveUIFullScreen);
		loadDecimal(numDriveUITransparency, bowDriveUITransparency);
		loadCheckbox(chkSessionUIFullScreen, bowSessionUIFullScreen);
		loadDecimal(numSessionUITransparency, bowSessionUITransparency);
	}

	private void CheckSelectedAlignment()
	{
		if (NotifyAlignmentError && MonitorList.SelectedCount() == 3 && !MonitorList.SelectedAlignmentIsOk())
		{
			string messageBoxText = "The selected monitors are not correctly aligned within 'Windows Display Settings'.\rThis will create visual alignment problems within iRacing.\r\rClose this program and correct the Windows monitor alignment by dragging the monitor boxes into alignment with each other.\r\rThen come back to this program and reselect the monitors to obtain their corrected co-ordinates.";
			MessageBox.Show(this, messageBoxText, "Windows Monitor Alignment", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			NotifyAlignmentError = false;
		}
	}

	private void loadResolution()
	{
		string tag = cboResolution.GetTag();
		string text = ProfileView.tagGetValue(tag);
		string text2 = ProfileView.tagGetValue(cboNumMonitors.GetTag());
		if (text2 == "1" || text2 == "3")
		{
			text = text.Replace('*', 'x');
		}
		cboResolution.SelectionChanged -= cboResolution_Changed;
		bool flag = false;
		foreach (ComboBoxItem item in (IEnumerable)cboResolution.Items)
		{
			if ((item.Content as string).Equals(text))
			{
				cboResolution.SelectedItem = item;
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			ComboBoxItem comboBoxItem2 = new ComboBoxItem();
			comboBoxItem2.Content = text;
			cboResolution.Items.Insert(0, comboBoxItem2);
			cboResolution.SelectedIndex = 0;
		}
		cboResolution.SelectionChanged += cboResolution_Changed;
		bowResolution.tagCompare();
	}

	private void loadNumScreens()
	{
		switch (ProfileView.tagGetValue(cboNumMonitors.GetTag()) + "." + ProfileView.tagGetValue(iniDX11.MonitorType.TagID))
		{
		case "1.0":
			cboNumMonitors.SelectedIndex = 0;
			break;
		case "3.0":
			cboNumMonitors.SelectedIndex = 1;
			break;
		case "1.1":
			cboNumMonitors.SelectedIndex = 2;
			break;
		case "3.1":
			cboNumMonitors.SelectedIndex = 3;
			break;
		}
		bowNumScreens.tagCompare();
	}

	private void cboResolution_Changed(object sender, SelectionChangedEventArgs e)
	{
		saveResolution();
		CheckFOV();
	}

	private void cboResScale_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!DataLoading && cboResolution.SelectedIndex >= 1 && cboMSAASamples.SelectedIndex <= 0)
		{
			ProfileView.tagSetValue(iniDX11.Graphic_MSAASamples.TagID, "4");
			loadMSAASamples(cboMSAASamples.Tag as string);
		}
	}

	private void cboNumScreens_Changed(object sender, RoutedEventArgs e)
	{
		saveNumMonitors();
		CheckFOV();
		MonitorCalculations();
	}

	private void CheckFOV()
	{
	}

	private void TimerFOV_Tick(object sender, EventArgs e)
	{
		TimerFOV.Stop();
		numFOV.Background = new SolidColorBrush(Color.FromRgb(37, 37, 37));
	}

	private void Monitor_Changed(object sender, RoutedPropertyChangedEventArgs<double?> e)
	{
		if (!DataLoading && ProfileView != null)
		{
			if (sender == numDiagonalWidth)
			{
				ProfileView.tagSetValue(iniDX11.DiagonalWidth.TagID, numDiagonalWidth.Value?.ToIntStr());
				loadNumber(numScreenAngle, bowScreenAngle);
			}
			if (sender == numScreenAngle)
			{
				ProfileView.tagSetValue(iniDX11.ScreenAngles.TagID, numScreenAngle.Value?.ToIntStr());
				loadNumber(numDiagonalWidth, bowDiagonalWidth);
			}
			if (sender != numFOV)
			{
				MonitorCalculations();
			}
		}
	}

	private void Graphic_GotFocus(object sender, RoutedEventArgs e)
	{
		if (sender == numMonitorWidth)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Visible;
			imgVisibleWidth.Visibility = Visibility.Hidden;
			imgDiagonalWidth.Visibility = Visibility.Hidden;
			imgDistance.Visibility = Visibility.Hidden;
			imgAngle.Visibility = Visibility.Hidden;
			imgFOV.Visibility = Visibility.Hidden;
		}
		else if (sender == numMonitorVisible)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Hidden;
			imgVisibleWidth.Visibility = Visibility.Visible;
			imgDiagonalWidth.Visibility = Visibility.Hidden;
			imgDistance.Visibility = Visibility.Hidden;
			imgAngle.Visibility = Visibility.Hidden;
			imgFOV.Visibility = Visibility.Hidden;
		}
		else if (sender == numDiagonalWidth)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Hidden;
			imgVisibleWidth.Visibility = Visibility.Hidden;
			imgDiagonalWidth.Visibility = Visibility.Visible;
			imgDistance.Visibility = Visibility.Hidden;
			imgAngle.Visibility = Visibility.Hidden;
			imgFOV.Visibility = Visibility.Hidden;
		}
		else if (sender == numViewDistance)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Hidden;
			imgVisibleWidth.Visibility = Visibility.Hidden;
			imgDiagonalWidth.Visibility = Visibility.Hidden;
			imgDistance.Visibility = Visibility.Visible;
			imgAngle.Visibility = Visibility.Hidden;
			imgFOV.Visibility = Visibility.Hidden;
		}
		else if (sender == numScreenAngle)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Hidden;
			imgVisibleWidth.Visibility = Visibility.Hidden;
			imgDiagonalWidth.Visibility = Visibility.Hidden;
			imgDistance.Visibility = Visibility.Hidden;
			imgAngle.Visibility = Visibility.Visible;
			imgFOV.Visibility = Visibility.Hidden;
		}
		else if (sender == numFOV)
		{
			gridMonitors.Visibility = Visibility.Hidden;
			imgMonitorWidth.Visibility = Visibility.Hidden;
			imgVisibleWidth.Visibility = Visibility.Hidden;
			imgDiagonalWidth.Visibility = Visibility.Hidden;
			imgDistance.Visibility = Visibility.Hidden;
			imgAngle.Visibility = Visibility.Hidden;
			imgFOV.Visibility = Visibility.Visible;
		}
	}

	private void Graphic_LostFocus(object sender, RoutedEventArgs e)
	{
		gridMonitors.Visibility = Visibility.Visible;
		imgMonitorWidth.Visibility = Visibility.Hidden;
		imgVisibleWidth.Visibility = Visibility.Hidden;
		imgDiagonalWidth.Visibility = Visibility.Hidden;
		imgDistance.Visibility = Visibility.Hidden;
		imgAngle.Visibility = Visibility.Hidden;
		imgFOV.Visibility = Visibility.Hidden;
	}

	public void MonitorCalculations()
	{
		if (!base.IsVisible)
		{
			return;
		}
		int num = 1;
		switch (cboNumMonitors.SelectedIndex)
		{
		case 0:
		case 2:
			num = 1;
			break;
		case 1:
		case 3:
			num = 3;
			break;
		default:
			num = 1;
			break;
		}
		double widthMonitor = (numMonitorWidth.Value.HasValue ? numMonitorWidth.Value.Value : 1.0);
		double num2 = (numViewDistance.Value.HasValue ? numViewDistance.Value.Value : 0.0);
		double num3 = (numScreenAngle.Value.HasValue ? numScreenAngle.Value.Value : 0.0);
		if (num < 3)
		{
			btnViewDistance.Tag = null;
			btnViewDistance.Content = "N/A";
			btnViewDistance.IsEnabled = false;
		}
		else if (num3 == 0.0)
		{
			btnViewDistance.Tag = null;
			btnViewDistance.Content = "Angle?";
			btnViewDistance.IsEnabled = false;
		}
		else
		{
			double num4 = 0.0;
			try
			{
				num4 = clsFOV.calcDistanceViewing(num3, widthMonitor);
			}
			catch
			{
				num4 = 0.0;
			}
			btnViewDistance.Tag = num4.ToString4Display(0);
			btnViewDistance.Content = "Set to " + btnViewDistance.GetTag();
			btnViewDistance.IsEnabled = true;
		}
		double num5 = 0.0;
		if (num < 3)
		{
			btnScreenAngle.Tag = null;
			btnScreenAngle.Content = "N/A";
			btnScreenAngle.IsEnabled = false;
		}
		else if (num2 == 0.0)
		{
			btnScreenAngle.Tag = null;
			btnScreenAngle.Content = "View Distance?";
			btnScreenAngle.IsEnabled = false;
		}
		else
		{
			try
			{
				num5 = clsFOV.calcAngleMonitors(num2, widthMonitor);
			}
			catch
			{
				num5 = 0.0;
			}
			if (num5 < 30.0 || num5 > 90.0)
			{
				btnScreenAngle.Tag = null;
				btnScreenAngle.Content = "Out of range";
				btnScreenAngle.IsEnabled = false;
			}
			else
			{
				btnScreenAngle.Tag = num5.ToString4Display(0);
				btnScreenAngle.Content = "Set to " + btnScreenAngle.GetTag();
				btnScreenAngle.IsEnabled = true;
			}
		}
		double num6 = 0.0;
		if (num2 == 0.0)
		{
			btnFOV.Tag = null;
			btnFOV.Content = "View Distance?";
			btnFOV.IsEnabled = false;
			return;
		}
		try
		{
			num6 = clsFOV.calcHorizontalFOV(num2, widthMonitor, num);
		}
		catch
		{
			num6 = 0.0;
		}
		if (num6 < 30.0 || num6 > 200.0)
		{
			btnFOV.Tag = null;
			btnFOV.Content = "Out of range";
			btnFOV.IsEnabled = false;
		}
		else
		{
			btnFOV.Tag = num6.ToString4Display(0);
			btnFOV.Content = "Set to " + btnFOV.GetTag();
			btnFOV.IsEnabled = true;
		}
	}

	private void btnViewDistance_Click(object sender, RoutedEventArgs e)
	{
		if (btnViewDistance.Tag != null)
		{
			numViewDistance.Value = btnViewDistance.GetTag().ToDouble();
		}
	}

	private void btnScreenAngle_Click(object sender, RoutedEventArgs e)
	{
		if (btnScreenAngle.Tag != null)
		{
			numScreenAngle.Value = btnScreenAngle.GetTag().ToDouble();
		}
	}

	private void btnFOV_Click(object sender, RoutedEventArgs e)
	{
		if (btnFOV.Tag != null)
		{
			numFOV.Value = btnFOV.GetTag().ToDouble();
		}
	}

	private void CboNumScreens_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (cboNumMonitors.SelectedIndex == 3 && ShowTripleCurvedWarning)
		{
			winTripleCurved obj = new winTripleCurved();
			obj.Owner = this;
			obj.ShowDialog();
		}
	}

	public void Monitor_Click(object sender, RoutedEventArgs e)
	{
		if (MonitorList.SetProfile(ProfileView, MismatchedMonitorKluge, GSyncHack))
		{
			loadResolution();
			loadNumScreens();
			loadNumber(numDeviceIdx, bowDeviceIdx);
			loadNumber(numRefreshRate, bowRefreshRate);
			loadCheckbox(chkMultiProjection, bowMultiProjection);
			loadCheckbox(chkFullScreen, bowFullScreen);
			loadComboIndex(cboAlign, bowAlign);
			loadNumber(numFPSLimit, bowFPSFrameRate);
			ProfileView.IsModified = true;
			CheckFOV();
			CheckSelectedAlignment();
		}
	}

	public void PhysicalMonitors_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.AllScreens);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void SamsungG9_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.SamsungG9);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Single1920_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Single1920);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Single2560_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Single2560);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Single3440_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Single3440);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Triple1920_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Triple1920);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Triple2560_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Triple2560);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Triple3440_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Triple3440);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Triple4k_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Triple4k);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Surround1920_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Surround1920);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Surround2560_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Surround2560);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Surround3440_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Surround3440);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Quad1920Above_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Quad1920Above);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Quad2560Above_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Quad2560Above);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void Quad3440Above_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.Quad3440Above);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void TripleMismatched_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.TripleMismatched);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	public void TripleVertical_Click(object sender, RoutedEventArgs e)
	{
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.TripleVertical);
		MonitorList.SetSelected(ProfileView.getDisplayBounds());
	}

	private void InitTabExtras()
	{
		ColorHelper.ColorNamesDictionary.Add(irPitLineColor, "Pit Lines");
		ColorHelper.ColorNamesDictionary.Add(irRaceLineFastColor, "Faster");
		ColorHelper.ColorNamesDictionary.Add(irRaceLineSameColor, "Coasting");
		ColorHelper.ColorNamesDictionary.Add(irRaceLineSlowColor, "Slowdown");
		colPitLine.CustomColorPalette01ItemsSource = irLineColors;
		colFastLine.CustomColorPalette01ItemsSource = irLineColors;
		colSameLine.CustomColorPalette01ItemsSource = irLineColors;
		colSlowLine.CustomColorPalette01ItemsSource = irLineColors;
		bowTicker.InitControl(this, chkTicker);
		bowConnectSockets.InitControl(this, chkConnectSockets);
		bowSDKEnableMem.InitControl(this, chkSDKEnableMem);
		bowAutoTelemetry.InitControl(this, chkAutoTelemetry);
		bowAskSaveReplay.InitControl(this, chkAskSaveReplay);
		bowAutoResetFastRepair.InitControl(this, chkAutoResetFastRepair);
		bowAutoResetPitBox.InitControl(this, chkAutoResetPitBox);
		bowReportFuel.InitControl(this, chkReportFuel);
		bowAutoFuel.InitControl(this, chkAutoFuel);
		bowAutoFuelLapMargin.InitNumber(this, numAutoFuelLapMargin, lblAutoFuelLapMargin);
		bowForceCrowdVisible.InitControl(this, chkForceCrowdVisible);
		bowForceControlsVisible.InitControl(this, chkForceControlsVisible);
		bowShowMsgJoin.InitControl(this, chkShowMsgJoin);
		bowShowMsgUsr.InitControl(this, chkShowMsgUsr);
		bowLoadPaintDriving.InitControl(this, chkLoadPaintDriving);
		bowShowMsgSys.InitControl(this, chkShowMsgSys);
		bowShowMsgInc.InitControl(this, chkShowMsgInc);
		bowDriveBlackBox.InitComboIndex(this, cboDriveBlackBox, lblDriveBlackBox);
		bowPitBlackBox.InitComboIndex(this, cboPitBlackBox, lblPitBlackBox);
		bowDisableSplitsRaceStart.InitControl(this, chkDisableSplitsRaceStart);
		bowFadeGhost.InitControl(this, chkFadeGhost);
		bowGhostOffset.InitDecimal(this, numGhostOffset, lblGhostOffset);
		bowGhostOpacity.InitDecimal(this, numGhostOpacity, lblGhostOpacity);
		bowDriveHeightAdj.InitDecimal(this, numDriveHeightAdj, lblDriveHeightAdj);
		bowDriveVanishY.InitDecimal(this, numDriveVanishY, lblDriveVanishY);
		bowLookInstant.InitComboIndex(this, cboLookInstant, lblLookInstant);
		bowLookSideAngle.InitNumber(this, numLookSideAngle, lblLookSideAngle);
		bowLookUpAngle.InitNumber(this, numLookUpAngle, lblLookUpAngle);
		bowLookDownAngle.InitNumber(this, numLookDownAngle, lblLookDownAngle);
		bowTestPitLocation.InitNumber(this, numTestPitLocation, lblTestPitLocation);
		bowWorkerThreads.InitNumber(this, numWorkerThreads, lblWorkerThreads);
		bowDamageThreads.InitNumber(this, numDamageThreads, lblDamageThreads);
		bowCompressVertices.InitControl(this, chkCompressVertices);
		bowCompressCars.InitControl(this, chkCompressCars);
		bowCompressSuits.InitControl(this, chkCompressSuits);
		bowCompressHelmets.InitControl(this, chkCompressHelmets);
		bowPauseReplayOnExit.InitControl(this, chkPauseReplayOnExit);
		bowCaptureEnabled.InitControl(this, chkCaptureEnabled);
		bowScreenshotFmt.InitComboIndex(this, cboScreenshotFmt, lblScreenshotFmt);
		bowScreenshotWidth.InitControl(this, numScreenshotWidth, lblScreenshotWidth);
		bowScreenshotHeight.InitControl(this, numScreenshotHeight, lblScreenshotHeight);
		bowVideoFmt.InitComboIndex(this, cboVideoFmt, lblVideoFmt);
		bowVideoFrameRate.InitComboIndex(this, cboVideoFrameRate, lblVideoFrameRate);
		bowVideoImgSize.InitComboIndex(this, cboVideoImgSize, lblVideoImgSize);
		bowHiContrastCursor.InitControl(this, chkHiContrastCursor);
		bowPitLineVisible.InitControl(this, chkPitLineVisible);
		bowRaceLineWidth.InitDecimal(this, numRaceLineWidth, lblRaceLineWidth);
		bowPitLine.InitControl(this, colPitLine, lblPitLine);
		bowFastLine.InitControl(this, colFastLine, lblFastLine);
		bowSameLine.InitControl(this, colSameLine, lblSameLine);
		bowSlowLine.InitControl(this, colSlowLine, lblSlowLine);
		bowReplaySecondsNormal.InitNumber(this, numReplaySecondsNormal, lblReplaySecondsNormal);
		bowReplaySecondsTeam.InitNumber(this, numReplaySecondsTeam, lblReplaySecondsTeam);
	}

	private void DataLoadTabExtras()
	{
		loadCheckbox(chkTicker, bowTicker);
		loadCheckbox(chkConnectSockets, bowConnectSockets);
		loadCheckbox(chkSDKEnableMem, bowSDKEnableMem);
		loadCheckbox(chkAutoTelemetry, bowAutoTelemetry);
		loadCheckbox(chkAskSaveReplay, bowAskSaveReplay);
		loadCheckbox(chkAutoResetFastRepair, bowAutoResetFastRepair);
		loadCheckbox(chkAutoResetPitBox, bowAutoResetPitBox);
		loadCheckbox(chkReportFuel, bowReportFuel);
		loadCheckbox(chkAutoFuel, bowAutoFuel);
		loadDecimal(numAutoFuelLapMargin, bowAutoFuelLapMargin);
		loadCheckbox(chkForceCrowdVisible, bowForceCrowdVisible);
		loadCheckbox(chkForceControlsVisible, bowForceControlsVisible);
		loadCheckbox(chkShowMsgJoin, bowShowMsgJoin);
		loadCheckbox(chkLoadPaintDriving, bowLoadPaintDriving);
		loadCheckbox(chkShowMsgUsr, bowShowMsgUsr);
		loadCheckbox(chkShowMsgSys, bowShowMsgSys);
		loadCheckbox(chkShowMsgInc, bowShowMsgInc);
		loadComboIndex(cboDriveBlackBox, bowDriveBlackBox);
		loadComboIndex(cboPitBlackBox, bowPitBlackBox);
		loadCheckbox(chkDisableSplitsRaceStart, bowDisableSplitsRaceStart);
		loadCheckbox(chkFadeGhost, bowFadeGhost);
		loadDecimal(numGhostOffset, bowGhostOffset);
		loadDecimal(numGhostOpacity, bowGhostOpacity);
		loadDecimal(numDriveHeightAdj, bowDriveHeightAdj);
		loadDecimal(numDriveVanishY, bowDriveVanishY);
		loadComboIndex(cboLookInstant, bowLookInstant);
		loadNumber(numLookSideAngle, bowLookSideAngle);
		loadNumber(numLookUpAngle, bowLookUpAngle);
		loadNumber(numLookDownAngle, bowLookDownAngle);
		loadNumber(numTestPitLocation, bowTestPitLocation);
		loadNumber(numWorkerThreads, bowWorkerThreads);
		loadNumber(numDamageThreads, bowDamageThreads);
		loadCheckbox(chkCompressVertices, bowCompressVertices);
		loadCheckbox(chkCompressCars, bowCompressCars);
		loadCheckbox(chkCompressSuits, bowCompressSuits);
		loadCheckbox(chkCompressHelmets, bowCompressHelmets);
		loadCheckbox(chkPauseReplayOnExit, bowPauseReplayOnExit);
		loadCheckbox(chkCaptureEnabled, bowCaptureEnabled);
		loadComboIndex(cboScreenshotFmt, bowScreenshotFmt);
		loadNumber(numScreenshotWidth, bowScreenshotWidth);
		loadNumber(numScreenshotHeight, bowScreenshotHeight);
		loadComboIndex(cboVideoFmt, bowVideoFmt);
		loadComboIndex(cboVideoFrameRate, bowVideoFrameRate);
		loadComboIndex(cboVideoImgSize, bowVideoImgSize);
		loadCheckbox(chkHiContrastCursor, bowHiContrastCursor);
		loadCheckbox(chkPitLineVisible, bowPitLineVisible);
		loadDecimal(numRaceLineWidth, bowRaceLineWidth);
		loadColorPicker(colPitLine, bowPitLine);
		loadColorPicker(colFastLine, bowFastLine);
		loadColorPicker(colSameLine, bowSameLine);
		loadColorPicker(colSlowLine, bowSlowLine);
		loadNumber(numReplaySecondsNormal, bowReplaySecondsNormal);
		loadNumber(numReplaySecondsTeam, bowReplaySecondsTeam);
	}

	private void SS2X_Click(object sender, RoutedEventArgs e)
	{
		setScreenshotResolution(2);
	}

	private void SS3X_Click(object sender, RoutedEventArgs e)
	{
		setScreenshotResolution(3);
	}

	private void SS4X_Click(object sender, RoutedEventArgs e)
	{
		setScreenshotResolution(4);
	}

	private void setScreenshotResolution(int xFactor)
	{
		int num = 1980;
		int num2 = 1024;
		try
		{
			string text = ProfileView.irDX11.tagGetValue("Display", "fullScreen");
			if (!(text == "0"))
			{
				if (text == "1")
				{
					num = ProfileView.irDX11.tagGetValue("Display", "fullScreenWidth").ToInt();
					num2 = ProfileView.irDX11.tagGetValue("Display", "fullScreenHeight").ToInt();
				}
			}
			else
			{
				num = ProfileView.irDX11.tagGetValue("Display", "windowedWidth").ToInt();
				num2 = ProfileView.irDX11.tagGetValue("Display", "windowedHeight").ToInt();
			}
		}
		catch
		{
		}
		num2 *= xFactor;
		ProfileView.tagSetValue(iniApp.xHeight.TagID, num2.ToIntStr());
		loadNumber(numScreenshotHeight, bowScreenshotHeight);
		num *= xFactor;
		ProfileView.tagSetValue(iniApp.xWidth.TagID, num.ToIntStr());
		loadNumber(numScreenshotWidth, bowScreenshotWidth);
		ProfileView.tagSetValue(iniApp.xTilesAcross.TagID, "1");
	}

	private void InitTabGraphics()
	{
		bowSky.InitComboIndex(this, cboSky, lblSky);
		bowCars.InitComboIndex(this, cboCars, lblCars);
		bowPits.InitComboIndex(this, cboPits, lblPits);
		bowEvent.InitComboIndex(this, cboEvent, lblEvent);
		bowGrandstands.InitComboIndex(this, cboGrandstands, lblGrandstands);
		bowCrowds.InitComboIndex(this, cboCrowds, lblCrowds);
		bowObjects.InitComboIndex(this, cboObjects, lblObjects);
		bowFoliage.InitComboIndex(this, cboFoliage, lblFoliage);
		bowParticles.InitComboIndex(this, cboParticles, lblParticles);
		bowFullRes.InitControl(this, chkFullRes);
		bowDrawCars.InitBasic(this, cboDrawCars);
		bowDrawCars.tagAdd("2.Graphics Options.MaxCarsToDrawInMirrors");
		bowDrawPits.InitControl(this, numMaxCars, lblMaxCars);
		bowDrawPits.InitBasic(this, cboDrawPits);
		bowDrawPits.tagAdd("2.Graphics Options.MaxPitObjsToDrawInMirrors");
		bowLODWorld.InitControl(this, numLODFPS, lblLODFPS);
		bowLODWorld.InitBasic(this, cboLODWorld);
		bowLODWorld.tagAdd("2.Graphics Options.LODPctMin");
		bowLODCar.InitControl(this, numLODBias, lblLODBias);
		bowLODCar.InitBasic(this, cboLODCar);
		bowLODCar.tagAdd("2.Graphics Options.LODPctDynoMin");
		bowFPSFrameRate.InitBasic(this, rbFPSLimit, boxFrameRate);
		bowFPSFrameRate.InitBasic(this, rbFPSNoLimit, boxFrameRate);
		bowFPSFrameRate.InitBasic(this, rbFPSVSync, boxFrameRate);
		bowFPSFrameRate.InitControl(this, numFPSLimit, boxFrameRate);
		bowFPSFrameRate.tagAdd("2.Graphics Options.LimitFrameRate");
		bowMaxPrerenderedFrames.InitControl(this, numMaxPrerenderedFrames, lblMaxPrerenderedFrames);
		bowVisibilityFrameDelay.InitControl(this, numVisibilityFrameDelay, lblVisibilityFrameDelay);
		bowAntiAliasMethod.InitComboIndex(this, cboAntiAliasMethod, lblAntiAliasMethod);
		bowMSAASamples.InitBasic(this, cboMSAASamples, lblMSAASamples);
		bowMSAAUseFilter.InitComboIndex(this, cboMSAAUseFilter, lblMSAAUseFilter);
		bowShadowMapsDay.InitControl(this, chkShadowMapsDay);
		bowObjSelfShadow.InitControl(this, chkObjSelfShadow);
		bowObjDynamic.InitComboIndex(this, cboObjDynamic, lblObjDynamic);
		bowShadowMapsNight.InitControl(this, chkShadowMapsNight);
		bowShadowMapsNight.InitControl(this, chkShadowWalls);
		bowNumLights.InitControl(this, numLights, lblLights);
		bowShadowNightFilter.InitComboIndex(this, cboShadowNightFilter, lblShadowNightFilter);
		bowDynCubeMaps.InitControl(this, numDynCubeMaps, lblDynCubeMaps);
		bowFixCubeMaps.InitControl(this, numFixCubeMaps, lblFixCubeMaps);
		bowShaderQuality.InitComboIndex(this, cboShaderQuality, lblShaderQuality);
		bowObstructions.InitComboIndex(this, cboObstructions, lblObstructions);
		bowDynShadowRes.InitComboIndex(this, cboDynShadowRes, lblDynShadowRes);
		bowStaticShadowRes.InitComboIndex(this, cboStaticShadowRes, lblStaticShadowRes);
		bowStaticShadowCount.InitControl(this, numStaticShadowCount, lblStaticShadowCount);
		bowTrackDisplacement.InitControl(this, chkTrackDisplacement);
		bowTrackDisplacement.InitControl(this, chkParallelSorting);
		bowSysMem.InitControl(this, numSysMem, lblSysMem);
		bowGpuMem.InitControl(this, numGpuMem, lblGpuMem);
		bowSteerWheel.InitBasic(this, cboSteerWheel, lblSteerWheel);
		bowSteerWheel.tagAdd("2.Graphics Options.DriverHands");
		bowTrees2Pass.InitControl(this, chkTrees2Pass);
		bowTreesHighQ.InitControl(this, chkTreesHighQ);
		bowMirrors.InitComboIndex(this, cboMirrors);
		bowMirrorHighQ.InitControl(this, chkMirrorHighQ);
		bowHeadlights.InitComboIndex(this, cboHeadlights, lblHeadlights);
		bowHeadlightsTrackMirror.InitControl(this, chkHeadlightsTrackMirror);
		bowVMirrorSize.InitControl(this, chkVMirror);
		bowVMirrorSize.InitComboIndex(this, cboVMirrorSize);
		bowVMirrorSize.InitControl(this, numVMirrorFOV);
		bowMotionBlur.InitComboIndex(this, cboMotionBlur, lblMotionBlur);
		bowMotionBlurBroadcast.InitControl(this, chkMotionBlurCarCams);
		bowMotionBlurBroadcast.InitControl(this, chkMotionBlurBroadcast);
		bowSSR.InitBasic(this, cboSSR, lblSSR);
		bowDistortion.InitControl(this, chkSharpening);
		bowDistortion.InitControl(this, chkDistortion);
		bowHDR.InitControl(this, chkHDR);
		bowHDR.InitControl(this, chkAutoExposure);
		bowMonoHeadlights.InitControl(this, chkSSAO);
		bowMonoHeadlights.InitControl(this, chkMonoHeadlights);
		bowGPUMemSwap.InitControl(this, chkGPUMemSwap);
		bowCar2048.InitControl(this, chkCar2048);
		bowCar2048.InitControl(this, chkHeatHaze);
		bowZBuffer.InitControl(this, chkNumCustom);
		bowZBuffer.InitControl(this, chkZBuffer);
		bowFXAAEdge.InitBasic(this, cboFXAAEdge, lblFXAAEdge);
		bowFXAASubPix.InitBasic(this, cboFXAASubPix, lblFXAASubPix);
	}

	private void DataLoadTabGraphics(string tagLoad = null)
	{
		loadComboIndex(cboSky, bowSky, tagLoad);
		loadComboIndex(cboCars, bowCars, tagLoad);
		loadComboIndex(cboPits, bowPits, tagLoad);
		loadComboIndex(cboEvent, bowEvent, tagLoad);
		loadComboIndex(cboGrandstands, bowGrandstands, tagLoad);
		loadComboIndex(cboCrowds, bowCrowds, tagLoad);
		loadComboIndex(cboObjects, bowObjects, tagLoad);
		loadComboIndex(cboFoliage, bowFoliage, tagLoad);
		loadComboIndex(cboParticles, bowParticles, tagLoad);
		loadCheckbox(chkFullRes, bowFullRes, tagLoad);
		loadDrawCars(cboDrawCars, iniDX11.Graphic_MaxCarsToDrawInMirrors.TagID, bowDrawCars);
		loadDrawPits(cboDrawPits, iniDX11.Graphic_MaxPitObjsToDrawInMirrors.TagID, bowDrawPits);
		loadNumber(numMaxCars, bowDrawPits, tagLoad);
		loadNumber(numLODFPS, bowLODWorld, tagLoad);
		loadLOD(cboLODWorld, bowLODWorld);
		loadNumber(numLODBias, bowLODCar, tagLoad);
		loadLOD(cboLODCar, bowLODCar);
		loadNumber(numMaxPrerenderedFrames, bowMaxPrerenderedFrames, tagLoad);
		loadNumber(numVisibilityFrameDelay, bowVisibilityFrameDelay, tagLoad);
		loadFPSOptions(tagLoad);
		loadComboIndex(cboAntiAliasMethod, bowAntiAliasMethod, tagLoad);
		loadMSAASamples(tagLoad);
		loadComboIndex(cboMSAAUseFilter, bowMSAAUseFilter, tagLoad);
		loadCheckbox(chkShadowMapsDay, bowShadowMapsDay, tagLoad);
		loadCheckbox(chkObjSelfShadow, bowObjSelfShadow, tagLoad);
		loadComboIndex(cboObjDynamic, bowObjDynamic, tagLoad);
		guiShadowMaps();
		loadCheckbox(chkShadowMapsNight, null, tagLoad);
		loadCheckbox(chkShadowWalls, bowShadowMapsNight, tagLoad);
		loadNumber(numLights, bowNumLights, tagLoad);
		loadComboIndex(cboShadowNightFilter, bowShadowNightFilter, tagLoad);
		loadNumber(numDynCubeMaps, bowDynCubeMaps, tagLoad);
		loadNumber(numFixCubeMaps, bowFixCubeMaps, tagLoad);
		loadComboIndex(cboShaderQuality, bowShaderQuality, tagLoad);
		loadComboIndex(cboObstructions, bowObstructions, tagLoad);
		loadComboIndex(cboDynShadowRes, bowDynShadowRes, tagLoad);
		loadComboIndex(cboStaticShadowRes, bowStaticShadowRes, tagLoad);
		loadNumber(numStaticShadowCount, bowStaticShadowCount, tagLoad);
		loadCheckbox(chkTrackDisplacement, null, tagLoad);
		loadCheckbox(chkParallelSorting, bowTrackDisplacement, tagLoad);
		LoadSteeringWheel(cboSteerWheel, iniDX11.Graphic_DriverHands.TagID, bowSteerWheel);
		loadCheckbox(chkTrees2Pass, bowTrees2Pass, tagLoad);
		loadCheckbox(chkTreesHighQ, bowTreesHighQ, tagLoad);
		loadComboIndex(cboMirrors, bowMirrors, tagLoad);
		loadCheckbox(chkMirrorHighQ, bowMirrorHighQ, tagLoad);
		loadComboIndex(cboHeadlights, bowHeadlights, tagLoad);
		loadCheckbox(chkHeadlightsTrackMirror, bowHeadlightsTrackMirror, tagLoad);
		loadCheckbox(chkVMirror, null, tagLoad);
		loadComboIndex(cboVMirrorSize, null, tagLoad);
		loadNumber(numVMirrorFOV, bowVMirrorSize, tagLoad);
		loadComboIndex(cboMotionBlur, bowMotionBlur, tagLoad);
		loadCheckbox(chkMotionBlurCarCams, null, tagLoad);
		loadCheckbox(chkMotionBlurBroadcast, bowMotionBlurBroadcast, tagLoad);
		loadSSR(cboSSR, bowSSR);
		loadCheckbox(chkSharpening, null, tagLoad);
		loadCheckbox(chkDistortion, bowDistortion, tagLoad);
		loadCheckbox(chkHDR, null, tagLoad);
		loadCheckbox(chkAutoExposure, bowHDR, tagLoad);
		loadCheckbox(chkSSAO, null, tagLoad);
		loadCheckbox(chkMonoHeadlights, bowMonoHeadlights, tagLoad);
		loadCheckbox(chkGPUMemSwap, bowGPUMemSwap, tagLoad);
		loadCheckbox(chkCar2048, null, tagLoad);
		loadCheckbox(chkHeatHaze, bowCar2048, tagLoad);
		loadCheckbox(chkNumCustom, null, tagLoad);
		loadCheckbox(chkZBuffer, bowZBuffer, tagLoad);
		loadFXAAEdge(cboFXAAEdge, bowFXAAEdge, tagLoad);
		loadFXAAEdgeSubPix(cboFXAASubPix, bowFXAASubPix, tagLoad);
		loadNumber(numGpuMem, bowGpuMem, tagLoad);
		loadNumber(numSysMem, bowSysMem, tagLoad);
	}

	public void guiShadowMaps()
	{
		if (chkShadowMapsDay.IsChecked.Value)
		{
			chkObjSelfShadow.IsEnabled = true;
			cboObjDynamic.IsEnabled = true;
			return;
		}
		chkObjSelfShadow.IsEnabled = false;
		chkObjSelfShadow.IsChecked = false;
		bowObjSelfShadow.CheckBox_Click(chkObjSelfShadow, null);
		cboObjDynamic.IsEnabled = false;
		cboObjDynamic.SelectedIndex = 0;
		bowObjDynamic.ComboIndex_Changed(cboObjDynamic, null);
	}

	public void loadFPSOptions(string tagLoad = null)
	{
		bool flag = tagLoad?.Contains("SetFrameRateToRefreshRate") ?? false;
		string tagID = iniDX11.Graphic_LimitFrameRate.TagID;
		if (!flag && tagID.tagIgnore(tagLoad))
		{
			return;
		}
		string text = ProfileView.tagGetValue(tagID);
		if (!(text == "0"))
		{
			if (text == "1")
			{
				rbFPSLimit.IsChecked = true;
			}
		}
		else
		{
			rbFPSNoLimit.IsChecked = true;
		}
		loadNumber(numFPSLimit, bowFPSFrameRate, flag ? (numFPSLimit.Tag as string) : tagLoad);
	}

	public void loadMSAASamples(string tagLoad = null)
	{
		string tag = cboMSAASamples.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			switch (ProfileView.tagGetValue(tag))
			{
			case "2":
				cboMSAASamples.SelectedIndex = 0;
				break;
			case "4":
				cboMSAASamples.SelectedIndex = 1;
				break;
			case "8":
				cboMSAASamples.SelectedIndex = 2;
				break;
			}
			bowMSAASamples.tagCompare();
		}
	}

	private void loadDrawCars(ComboBox cbo, string tagMirrors, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()) + "." + ProfileView.tagGetValue(tagMirrors))
		{
		case "64.64":
			cbo.SelectedIndex = 0;
			break;
		case "64.30":
			cbo.SelectedIndex = 1;
			break;
		case "64.16":
			cbo.SelectedIndex = 2;
			break;
		case "64.8":
			cbo.SelectedIndex = 3;
			break;
		case "40.20":
			cbo.SelectedIndex = 4;
			break;
		case "40.12":
			cbo.SelectedIndex = 5;
			break;
		case "40.6":
			cbo.SelectedIndex = 6;
			break;
		case "30.12":
			cbo.SelectedIndex = 7;
			break;
		case "30.8":
			cbo.SelectedIndex = 8;
			break;
		case "30.4":
			cbo.SelectedIndex = 9;
			break;
		case "20.12":
			cbo.SelectedIndex = 10;
			break;
		case "20.8":
			cbo.SelectedIndex = 11;
			break;
		case "20.4":
			cbo.SelectedIndex = 12;
			break;
		case "10.4":
			cbo.SelectedIndex = 13;
			break;
		default:
			cbo.SelectedIndex = 14;
			break;
		}
		bow.tagCompare();
	}

	private void loadDrawPits(ComboBox cbo, string tagMirrors, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()) + "." + ProfileView.tagGetValue(tagMirrors))
		{
		case "64.64":
			cbo.SelectedIndex = 0;
			break;
		case "64.30":
			cbo.SelectedIndex = 1;
			break;
		case "64.16":
			cbo.SelectedIndex = 2;
			break;
		case "64.8":
			cbo.SelectedIndex = 3;
			break;
		case "40.20":
			cbo.SelectedIndex = 4;
			break;
		case "40.12":
			cbo.SelectedIndex = 5;
			break;
		case "40.6":
			cbo.SelectedIndex = 6;
			break;
		case "30.12":
			cbo.SelectedIndex = 7;
			break;
		case "30.8":
			cbo.SelectedIndex = 8;
			break;
		case "30.4":
			cbo.SelectedIndex = 9;
			break;
		case "20.12":
			cbo.SelectedIndex = 10;
			break;
		case "20.8":
			cbo.SelectedIndex = 11;
			break;
		case "20.4":
			cbo.SelectedIndex = 12;
			break;
		case "4.0":
			cbo.SelectedIndex = 13;
			break;
		case "0.0":
			cbo.SelectedIndex = 14;
			break;
		default:
			cbo.SelectedIndex = 15;
			break;
		}
		bow.tagCompare();
	}

	public void loadAA(ComboBox cbo, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()))
		{
		case "0.0":
			cbo.SelectedIndex = 0;
			break;
		case "1.0":
			cbo.SelectedIndex = 1;
			break;
		case "0.1":
			cbo.SelectedIndex = 2;
			break;
		default:
			cbo.SelectedIndex = 0;
			break;
		}
		bow?.tagCompare();
	}

	public void loadLOD(ComboBox cbo, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()))
		{
		case "400.25":
			cbo.SelectedIndex = 0;
			break;
		case "300.50":
			cbo.SelectedIndex = 1;
			break;
		case "200.75":
			cbo.SelectedIndex = 2;
			break;
		case "400.100":
			cbo.SelectedIndex = 3;
			break;
		case "100.25":
			cbo.SelectedIndex = 4;
			break;
		case "50.50":
			cbo.SelectedIndex = 5;
			break;
		case "100.100":
			cbo.SelectedIndex = 6;
			break;
		default:
			cbo.SelectedIndex = 7;
			break;
		}
		bow.tagCompare();
	}

	public void loadSSR(ComboBox cbo, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()))
		{
		case "0.0":
			cbo.SelectedIndex = 0;
			break;
		case "1.1":
			cbo.SelectedIndex = 1;
			break;
		case "1.2":
			cbo.SelectedIndex = 2;
			break;
		case "0.1":
			cbo.SelectedIndex = 3;
			break;
		case "0.2":
			cbo.SelectedIndex = 4;
			break;
		default:
			cbo.SelectedIndex = 0;
			break;
		}
		bow?.tagCompare();
	}

	private void LoadSteeringWheel(ComboBox cbo, string tagHands, clsRainbow bow)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()) + "." + ProfileView.tagGetValue(tagHands))
		{
		case "0.0":
			cbo.SelectedIndex = 0;
			break;
		case "2.0":
			cbo.SelectedIndex = 1;
			break;
		case "1.0":
			cbo.SelectedIndex = 2;
			break;
		case "1.1":
			cbo.SelectedIndex = 3;
			break;
		case "3.0":
			cbo.SelectedIndex = 4;
			break;
		}
		bow.tagCompare();
	}

	private void cboSteerWheel_Changed(object sender, RoutedEventArgs e)
	{
		saveSteeringWheel(cboSteerWheel, iniDX11.Graphic_DriverHands.TagID, bowSteerWheel);
	}

	public void loadHeadlights()
	{
		switch (ProfileView.tagGetValue(cboHeadlights.GetTag()))
		{
		case "-1":
			cboHeadlights.SelectedIndex = 0;
			break;
		case "0":
			cboHeadlights.SelectedIndex = 1;
			break;
		case "1":
			cboHeadlights.SelectedIndex = 2;
			break;
		case "2":
			cboHeadlights.SelectedIndex = 3;
			break;
		}
		bowHeadlights.tagCompare();
	}

	public void loadFXAAEdge(ComboBox cbo, clsRainbow bow, string tagLoad = null)
	{
		string tag = cbo.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			switch (ProfileView.tagGetValue(tag))
			{
			case "333":
				cbo.SelectedIndex = 0;
				break;
			case "250":
				cbo.SelectedIndex = 1;
				break;
			case "166":
				cbo.SelectedIndex = 2;
				break;
			case "125":
				cbo.SelectedIndex = 3;
				break;
			case "63":
				cbo.SelectedIndex = 4;
				break;
			}
			bow.tagCompare();
		}
	}

	private void cboFXAAEdge_Changed(object sender, RoutedEventArgs e)
	{
		saveFXAAEdge(cboFXAAEdge, bowFXAAEdge);
	}

	public void loadFXAAEdgeSubPix(ComboBox cbo, clsRainbow bow, string tagLoad = null)
	{
		switch (ProfileView.tagGetValue(cbo.GetTag()))
		{
		case "100":
			cbo.SelectedIndex = 0;
			break;
		case "75":
			cbo.SelectedIndex = 1;
			break;
		case "50":
			cbo.SelectedIndex = 2;
			break;
		case "25":
			cbo.SelectedIndex = 3;
			break;
		case "0":
			cbo.SelectedIndex = 4;
			break;
		}
		bow.tagCompare();
	}

	private void cboFXAASubPix_Changed(object sender, RoutedEventArgs e)
	{
		saveFXAASubPix(cboFXAASubPix, bowFXAASubPix);
	}

	private void cboDrawCars_Changed(object sender, RoutedEventArgs e)
	{
		saveDrawCars(cboDrawCars.SelectedIndex, iniDX11.Graphic_MaxCarsToDraw.TagID, iniDX11.Graphic_MaxCarsToDrawInMirrors.TagID, bowDrawCars);
	}

	private void cboDrawPits_Changed(object sender, RoutedEventArgs e)
	{
		saveDrawPits(cboDrawPits.SelectedIndex, iniDX11.Graphic_MaxPitObjsToDraw.TagID, iniDX11.Graphic_MaxPitObjsToDrawInMirrors.TagID, bowDrawPits);
	}

	private void cboLODWorld_Changed(object sender, RoutedEventArgs e)
	{
		saveLOD(cboLODWorld, bowLODWorld);
	}

	private void cboLODCar_Changed(object sender, RoutedEventArgs e)
	{
		saveLOD(cboLODCar, bowLODCar);
	}

	private void cboSSR_Changed(object sender, RoutedEventArgs e)
	{
		saveSSR(cboSSR, bowSSR);
	}

	private void rbFPSNoLimit_Change(object sender, RoutedEventArgs e)
	{
		saveFPSOptions();
	}

	private void rbFPSLimit_Change(object sender, RoutedEventArgs e)
	{
		saveFPSOptions();
	}

	private void rbFPSVSync_Change(object sender, RoutedEventArgs e)
	{
		saveFPSOptions();
	}

	private void cboMSAASamples_Changed(object sender, RoutedEventArgs e)
	{
		saveMSAASamples();
	}

	private void chkShadowMapsDay_Click(object sender, RoutedEventArgs e)
	{
		guiShadowMaps();
	}

	private void InitTabReplay()
	{
		bowSkyReplay.InitComboIndex(this, cboSkyReplay, lblSkyReplay);
		bowCarsReplay.InitComboIndex(this, cboCarsReplay, lblCarsReplay);
		bowPitsReplay.InitComboIndex(this, cboPitsReplay, lblPitsReplay);
		bowEventReplay.InitComboIndex(this, cboEventReplay, lblEventReplay);
		bowGrandstandsReplay.InitComboIndex(this, cboGrandstandsReplay, lblGrandstandsReplay);
		bowCrowdsReplay.InitComboIndex(this, cboCrowdsReplay, lblCrowdsReplay);
		bowObjectsReplay.InitComboIndex(this, cboObjectsReplay, lblObjectsReplay);
		bowFoliageReplay.InitComboIndex(this, cboFoliageReplay, lblFoliageReplay);
		bowParticlesReplay.InitComboIndex(this, cboParticlesReplay, lblParticlesReplay);
		bowFullResReplay.InitControl(this, chkFullResReplay);
		bowDrawCarsReplay.InitBasic(this, cboDrawCarsReplay);
		bowDrawCarsReplay.tagAdd(iniDX11.Replay_MaxCarsToDrawInMirrors.TagID);
		bowDrawPitsReplay.InitBasic(this, cboDrawPitsReplay);
		bowDrawPitsReplay.tagAdd(iniDX11.Replay_MaxPitObjsToDrawInMirrors.TagID);
		bowLODCarReplay.InitBasic(this, cboLODCarReplay);
		bowLODWorldReplay.InitBasic(this, cboLODWorldReplay);
		bowLODWorldReplay.tagAdd(iniDX11.Replay_LODPctMin.TagID);
		bowLODCarReplay.InitControl(this, numLODFPSReplay, lblLODFPS);
		bowLODCarReplay.tagAdd(iniDX11.Replay_LODPctDynoMin.TagID);
		bowShadowMapsDayReplay.InitControl(this, chkShadowMapsDayReplay);
		bowObjSelfShadowReplay.InitControl(this, chkObjSelfShadowReplay);
		bowObjDynamicReplay.InitComboIndex(this, cboObjDynamicReplay, lblObjDynamicReplay);
		bowObstructionsReplay.InitComboIndex(this, cboObstructionsReplay, lblObstructionsReplay);
		bowShadowMapsNightReplay.InitControl(this, chkShadowMapsNightReplay);
		bowShadowHeadlightsReplay.InitControl(this, chkShadowWallsReplay);
		bowNumLightsReplay.InitControl(this, numLightsReplay, lblLightsReplay);
		bowShadowNightFilterReplay.InitComboIndex(this, cboShadowNightFilterReplay, lblShadowNightFilterReplay);
		bowDynCubeMapsReplay.InitControl(this, numDynCubeMapsReplay, lblDynCubeMapsReplay);
		bowFixCubeMapsReplay.InitControl(this, numFixCubeMapsReplay, lblFixCubeMapsReplay);
		bowSteerWheelReplay.InitBasic(this, cboSteerWheelReplay, lblSteerWheelReplay);
		bowSteerWheelReplay.tagAdd(iniDX11.Replay_DriverHands.TagID);
		bowTrees2PassReplay.InitControl(this, chkTrees2PassReplay);
		bowMirrorsReplay.InitComboIndex(this, cboMirrorsReplay, lblMirrorsReplay);
		bowMirrorHighQReplay.InitControl(this, chkMirrorHighQReplay);
		bowMotionBlurReplay.InitComboIndex(this, cboMotionBlurReplay, lblMotionBlurReplay);
		bowSSRReplay.InitControl(this, cboSSRReplay, lblSSRReplay);
		bowDistortionReplay.InitControl(this, chkSharpeningReplay);
		bowDistortionReplay.InitControl(this, chkDistortionReplay);
		bowSSAOReplay.InitControl(this, chkSSAOReplay);
		bowHeatHazeReplay.InitControl(this, chkHeatHazeReplay);
		bowHeatHazeReplay.InitControl(this, chkDOFReplay);
		bowRenderReplay.InitControl(this, chkRenderReplay);
		bowFXAAEdgeReplay.InitBasic(this, cboFXAAEdgeReplay, lblFXAAEdgeReplay);
		bowFXAASubPixReplay.InitBasic(this, cboFXAASubPixReplay, lblFXAASubPixReplay);
	}

	private void DataLoadTabReplay()
	{
		loadComboIndex(cboSkyReplay, bowSkyReplay);
		loadComboIndex(cboCarsReplay, bowCarsReplay);
		loadComboIndex(cboPitsReplay, bowPitsReplay);
		loadComboIndex(cboEventReplay, bowEventReplay);
		loadComboIndex(cboGrandstandsReplay, bowGrandstandsReplay);
		loadComboIndex(cboCrowdsReplay, bowCrowdsReplay);
		loadComboIndex(cboObjectsReplay, bowObjectsReplay);
		loadComboIndex(cboFoliageReplay, bowFoliageReplay);
		loadComboIndex(cboParticlesReplay, bowParticlesReplay);
		loadCheckbox(chkFullResReplay, bowFullResReplay);
		loadDrawCars(cboDrawCarsReplay, iniDX11.Replay_MaxCarsToDrawInMirrors.TagID, bowDrawCarsReplay);
		loadDrawPits(cboDrawPitsReplay, iniDX11.Replay_MaxPitObjsToDrawInMirrors.TagID, bowDrawPitsReplay);
		loadNumber(numLODFPSReplay, bowLODCarReplay);
		loadLOD(cboLODWorldReplay, bowLODWorldReplay);
		loadLOD(cboLODCarReplay, bowLODCarReplay);
		loadCheckbox(chkShadowMapsDayReplay, bowShadowMapsDayReplay);
		loadCheckbox(chkObjSelfShadowReplay, bowObjSelfShadowReplay);
		loadComboIndex(cboObjDynamicReplay, bowObjDynamicReplay);
		guiShadowMapsReplay();
		loadCheckbox(chkShadowMapsNightReplay, bowShadowMapsNightReplay);
		loadCheckbox(chkShadowWallsReplay, null);
		loadNumber(numLightsReplay, bowNumLightsReplay);
		loadComboIndex(cboShadowNightFilterReplay, bowShadowNightFilterReplay);
		loadNumber(numDynCubeMapsReplay, bowDynCubeMapsReplay);
		loadNumber(numFixCubeMapsReplay, bowFixCubeMapsReplay);
		loadComboIndex(cboObstructionsReplay, bowObstructionsReplay);
		LoadSteeringWheel(cboSteerWheelReplay, iniDX11.Replay_DriverHands.TagID, bowSteerWheelReplay);
		loadCheckbox(chkTrees2PassReplay, bowTrees2PassReplay);
		loadComboIndex(cboMirrorsReplay, bowMirrorsReplay);
		loadCheckbox(chkMirrorHighQReplay, bowMirrorHighQReplay);
		loadComboIndex(cboMotionBlurReplay, bowMotionBlurReplay);
		loadSSR(cboSSRReplay, bowSSRReplay);
		loadCheckbox(chkSharpeningReplay, null);
		loadCheckbox(chkDistortionReplay, bowDistortionReplay);
		loadCheckbox(chkSSAOReplay, bowSSAOReplay);
		loadCheckbox(chkDOFReplay, null);
		loadCheckbox(chkHeatHazeReplay, bowHeatHazeReplay);
		loadCheckbox(chkRenderReplay, bowRenderReplay);
		loadFXAAEdge(cboFXAAEdgeReplay, bowFXAAEdgeReplay);
		loadFXAAEdgeSubPix(cboFXAASubPixReplay, bowFXAASubPixReplay);
	}

	public void guiShadowMapsReplay()
	{
		if (chkShadowMapsDayReplay.IsChecked.Value)
		{
			chkObjSelfShadowReplay.IsEnabled = true;
			cboObjDynamicReplay.IsEnabled = true;
			return;
		}
		chkObjSelfShadowReplay.IsEnabled = false;
		chkObjSelfShadowReplay.IsChecked = false;
		bowObjSelfShadowReplay.CheckBox_Click(chkObjSelfShadowReplay, null);
		cboObjDynamicReplay.IsEnabled = false;
		cboObjDynamicReplay.SelectedIndex = 0;
		bowObjDynamicReplay.ComboIndex_Changed(cboObjDynamicReplay, null);
	}

	private void cboDrawCarsReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveDrawCars(cboDrawCarsReplay.SelectedIndex, iniDX11.Replay_MaxCarsToDraw.TagID, iniDX11.Replay_MaxCarsToDrawInMirrors.TagID, bowDrawCarsReplay);
	}

	private void cboDrawPitsReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveDrawPits(cboDrawPitsReplay.SelectedIndex, iniDX11.Replay_MaxPitObjsToDraw.TagID, iniDX11.Replay_MaxPitObjsToDrawInMirrors.TagID, bowDrawPitsReplay);
	}

	private void cboLODWorldReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveLOD(cboLODWorldReplay, bowLODWorldReplay);
	}

	private void cboLODCarReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveLOD(cboLODCarReplay, bowLODCarReplay);
	}

	private void cboSSRReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveSSR(cboSSRReplay, bowSSRReplay);
	}

	private void chkShadowMapsDayReplay_Click(object sender, RoutedEventArgs e)
	{
		guiShadowMapsReplay();
	}

	private void cboSteerWheelReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveSteeringWheel(cboSteerWheelReplay, iniDX11.Replay_DriverHands.TagID, bowSteerWheelReplay);
	}

	private void cboFXAAEdgeReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveFXAAEdge(cboFXAAEdgeReplay, bowFXAAEdgeReplay);
	}

	private void cboFXAASubPixReplay_Changed(object sender, RoutedEventArgs e)
	{
		saveFXAASubPix(cboFXAASubPixReplay, bowFXAASubPixReplay);
	}

	private void InitTabSound()
	{
		bowSoundDevice.InitControl(this, lblSoundDevice);
		bowDimensions.InitComboIndex(this, cboDimensions, lblDimensions);
		bowNotification.InitComboIndex(this, cboNotification, lblNotification);
		bowAmbientMusic.InitControl(this, chkAmbientMusic);
		bowDownshiftAlert.InitControl(this, chkDownshiftAlert);
		bowRotateVR.InitControl(this, chkRotateVR);
		bowLoudMaster.InitControl(this, sliderLoudMaster);
		bowLoudEngines.InitControl(this, sliderLoudEngines);
		bowLoudTyres.InitControl(this, sliderLoudTyres);
		bowLoudCrashes.InitControl(this, sliderLoudCrashes);
		bowLoudWind.InitControl(this, sliderLoudWind);
		bowLoudRain.InitControl(this, sliderLoudRain);
		bowLoudInCar.InitControl(this, sliderLoudInCar);
		bowLoudAmbient.InitControl(this, sliderLoudAmbient);
		bowLoudSpotter.InitControl(this, sliderLoudSpotter);
		bowLoudChat.InitControl(this, sliderLoudChat);
		bowLoudReplay.InitControl(this, sliderLoudReplay);
		bowEarProtection.InitComboIndex(this, cboEarProtection, lblEarProtection);
		bowCompressorReplay.InitComboIndex(this, cboCompressorReplay, lblCompressorReplay);
		bowLFEDevice.InitControl(this, lblLFEDevice);
		bow10dbCut.InitControl(this, chk10dbCut);
		bowEnableLFE.InitControl(this, chkEnableLFE);
		bowLFEMaster.InitControl(this, sliderLFEMaster);
		bowLFEGame.InitControl(this, sliderLFEGame);
		bowLFEImpact.InitControl(this, sliderLFEImpact);
		bowLFEEngine.InitControl(this, sliderLFEEngine);
		bowLFEGear.InitControl(this, sliderLFEGear);
		bowLFERevLimiter.InitControl(this, sliderLFERevLimiter);
		bowLFERumble.InitControl(this, sliderLFERumble);
		bowLFEWheels.InitControl(this, sliderLFEWheels);
		bowLFERoadTexture.InitControl(this, sliderLFERoadTexture);
		bowLFELowPassFreq.InitControl(this, sliderLFELowPassFreq);
		bowChatSpeaker.InitControl(this, lblChatSpeaker);
		bowChatMike.InitControl(this, lblChatMike);
		bowEnableChat.InitControl(this, chkEnableChat);
		bowEnableChat.InitControl(this, chkChatMuted);
		bowChatWhileDriving.InitControl(this, chkChatWhileDriving);
		bowUiControlsInDb.InitControl(this, chkUiControlsInDb);
	}

	private void DataLoadTabSound()
	{
		loadLabel(lblSoundDevice, bowSoundDevice);
		loadComboIndex(cboDimensions, bowDimensions);
		loadComboIndex(cboNotification, bowNotification);
		loadCheckbox(chkAmbientMusic, bowAmbientMusic);
		loadCheckbox(chkDownshiftAlert, bowDownshiftAlert);
		loadCheckbox(chkRotateVR, bowRotateVR);
		loadSlider(sliderLoudMaster, bowLoudMaster);
		loadSlider(sliderLoudEngines, bowLoudEngines);
		loadSlider(sliderLoudTyres, bowLoudTyres);
		loadSlider(sliderLoudCrashes, bowLoudCrashes);
		loadSlider(sliderLoudWind, bowLoudWind);
		loadSlider(sliderLoudRain, bowLoudRain);
		loadSlider(sliderLoudInCar, bowLoudInCar);
		loadSlider(sliderLoudAmbient, bowLoudAmbient);
		loadSlider(sliderLoudSpotter, bowLoudSpotter);
		loadSlider(sliderLoudChat, bowLoudChat);
		loadSlider(sliderLoudReplay, bowLoudReplay);
		loadComboIndex(cboEarProtection, bowEarProtection);
		loadComboIndex(cboCompressorReplay, bowCompressorReplay);
		loadLabel(lblLFEDevice, bowLFEDevice);
		loadCheckbox(chk10dbCut, bow10dbCut);
		loadCheckbox(chkEnableLFE, bowEnableLFE);
		loadSlider(sliderLFEMaster, bowLFEMaster);
		loadSlider(sliderLFEGame, bowLFEGame);
		loadSlider(sliderLFEImpact, bowLFEImpact);
		loadSlider(sliderLFEEngine, bowLFEEngine);
		loadSlider(sliderLFEGear, bowLFEGear);
		loadSlider(sliderLFERevLimiter, bowLFERevLimiter);
		loadSlider(sliderLFERumble, bowLFERumble);
		loadSlider(sliderLFEWheels, bowLFEWheels);
		loadSlider(sliderLFERoadTexture, bowLFERoadTexture);
		loadSlider(sliderLFELowPassFreq, bowLFELowPassFreq);
		loadLabel(lblChatSpeaker, bowChatSpeaker);
		loadLabel(lblChatMike, bowChatMike);
		loadCheckbox(chkEnableChat, bowEnableChat);
		loadCheckbox(chkChatMuted, bowEnableChat);
		loadCheckbox(chkChatWhileDriving, bowChatWhileDriving);
		loadCheckbox(chkUiControlsInDb, bowUiControlsInDb);
	}

	private void InitTabFFB()
	{
		bowTrueForceAPI.InitControl(this, chkTrueForceAPI);
		bowTrueForceVibe.InitControl(this, chkTrueForceVibe);
		bowForceVibePhysics.InitControl(this, chkForceVibePhysics);
		bowTrueForceDamper.InitControl(this, sliderTrueForceDamper);
		bowTrueForceMaster.InitControl(this, sliderTrueForceMaster);
		bowTrueForceCarBody.InitControl(this, sliderTrueForceCarBody);
		bowTrueForceDriveShaft.InitControl(this, sliderTrueForceDriveShaft);
		bowTrueForceEngineRPM.InitControl(this, sliderTrueForceEngineRPM);
		bowTrueForceGearChange.InitControl(this, sliderTrueForceGearChange);
		bowTrueForceRevLimit.InitControl(this, sliderTrueForceRevLimit);
		bowTrueForceRoadTexture.InitControl(this, sliderTrueForceRoadTexture);
		bowTrueForceRumbleStrip.InitControl(this, sliderTrueForceRumbleStrip);
		bowTrueForceWheelSlip.InitControl(this, sliderTrueForceWheelSlip);
		bowAsetekAPI.InitControl(this, chkAsetekAPI);
		bowConspitAPI.InitControl(this, chkConspitAPI);
		bowFanatecAPI.InitControl(this, chkFanatecAPI);
		bowMozaAPI.InitControl(this, chkMozaAPI);
		bowSimagicAPI.InitControl(this, chkSimagicAPI);
		bowSimuCubeAPI.InitControl(this, chkSimuCubeAPI);
		bowVRSAPI.InitControl(this, chkVRSAPI);
		bowFFB360Hz.InitControl(this, chkFFB360Hz);
		bowWheelDisplay.InitControl(this, chkWheelDisplay);
		bowWheelDisplayBlink.InitControl(this, chkWheelDisplayBlink);
		bowFFBScaling.InitDecimal(this, numFFBScaling, lblFFBScaling);
		bowFFBSmoothing.InitComboIndex(this, cboFFBSmoothing, lblFFBSmoothing);
		bowFFBClutchLaunchMode.InitComboIndex(this, cboFFBClutchLaunchMode, lblFFBClutchLaunchMode);
		bowVibratePedalWheel.InitControl(this, chkVibratePedalWheel);
	}

	private void DataLoadTabFFB()
	{
		loadCheckbox(chkTrueForceAPI, bowTrueForceAPI);
		loadCheckbox(chkTrueForceVibe, bowTrueForceVibe);
		loadCheckbox(chkForceVibePhysics, bowForceVibePhysics);
		loadSlider(sliderTrueForceDamper, bowTrueForceDamper);
		loadSlider(sliderTrueForceMaster, bowTrueForceMaster);
		loadSlider(sliderTrueForceCarBody, bowTrueForceCarBody);
		loadSlider(sliderTrueForceDriveShaft, bowTrueForceDriveShaft);
		loadSlider(sliderTrueForceEngineRPM, bowTrueForceEngineRPM);
		loadSlider(sliderTrueForceGearChange, bowTrueForceGearChange);
		loadSlider(sliderTrueForceRevLimit, bowTrueForceRevLimit);
		loadSlider(sliderTrueForceRoadTexture, bowTrueForceRoadTexture);
		loadSlider(sliderTrueForceRumbleStrip, bowTrueForceRumbleStrip);
		loadSlider(sliderTrueForceWheelSlip, bowTrueForceWheelSlip);
		loadCheckbox(chkAsetekAPI, bowAsetekAPI);
		loadCheckbox(chkConspitAPI, bowConspitAPI);
		loadCheckbox(chkFanatecAPI, bowFanatecAPI);
		loadCheckbox(chkMozaAPI, bowMozaAPI);
		loadCheckbox(chkSimagicAPI, bowSimagicAPI);
		loadCheckbox(chkSimuCubeAPI, bowSimuCubeAPI);
		loadCheckbox(chkVRSAPI, bowVRSAPI);
		loadCheckbox(chkFFB360Hz, bowFFB360Hz);
		loadCheckbox(chkWheelDisplay, bowWheelDisplay);
		loadCheckbox(chkWheelDisplayBlink, bowWheelDisplayBlink);
		loadDecimal(numFFBScaling, bowFFBScaling);
		loadComboIndex(cboFFBSmoothing, bowFFBSmoothing);
		loadComboIndex(cboFFBClutchLaunchMode, bowFFBClutchLaunchMode);
		loadCheckbox(chkVibratePedalWheel, bowVibratePedalWheel);
	}

	private void chkFFBAPI_Click(object sender, RoutedEventArgs e)
	{
		if (!DataLoading && (sender as CheckBox).IsChecked.Value && !chkFFB360Hz.IsChecked.Value)
		{
			ProfileView.tagSetValue(iniApp.enableFFB360HzInterpolated.TagID, "1");
			loadCheckbox(chkFFB360Hz, bowFFB360Hz);
		}
	}

	private void InitTabSpotter()
	{
		bowSpotDevice.InitControl(this, lblSpotDevice);
		bowSpotEnable.InitControl(this, chkSpotEnable);
		bowSpotMuteIfLive.InitControl(this, chkSpotMuteIfLive);
		bowSpotReduceIfLive.InitControl(this, chkSpotReduceIfLive);
		bowShowSpotterUIForSpectators.InitControl(this, chkShowSpotterUIForSpectators);
		bowSpotDisplay.InitComboIndex(this, cboSpotDisplay, lblSpotDisplay);
		bowSpotChatty.InitComboIndex(this, cboSpotChatty, lblSpotChatty);
		bowSpotVoice.InitBasic(this, cboSpotVoice, lblSpotVoice);
		bowHushDuration.InitControl(this, numHushDuration, lblHushDuration);
		bowSpotHiLoStart.InitControl(this, chkSpotHiLoStart);
		bowSpotHiLowPadding.InitDecimal(this, numSpotHiLowPadding, lblSpotHiLowPadding);
		bowReportLaps.InitControl(this, chkReportLaps);
		bowReportLaps.InitControl(this, chkReportMinute);
		bowSpotPrecision.InitControl(this, numSpotPrecision, lblSpotPrecision);
		bowSpotReporting.InitComboIndex(this, cboSpotReporting, lblSpotReporting);
		bowSPCCTextFactor.InitControl(this, sliderSPCCTextFactor);
		bowLeaderChanged.InitControl(this, chkLeaderChanged);
		bowLeaderTimes.InitControl(this, chkLeaderTimes);
		bowReportGap.InitControl(this, chkReportGap);
		bowLappingTraffic.InitControl(this, chkLappingTraffic);
		bowPitboxCountdown.InitControl(this, chkPitboxCountdown);
		bowPitNotify.InitControl(this, chkPitNotify);
		bowNewClassBest.InitControl(this, chkNewClassBest);
		bowNewClassBestTime.InitControl(this, chkNewClassBestTime);
		bowNewPersonalBestRace.InitControl(this, chkNewPersonalBestRace);
		bowNewPersonalBest.InitControl(this, chkNewPersonalBest);
	}

	private void DataLoadTabSpotter()
	{
		loadLabel(lblSpotDevice, bowSpotDevice);
		loadCheckbox(chkSpotEnable, bowSpotEnable);
		loadCheckbox(chkSpotMuteIfLive, bowSpotMuteIfLive);
		loadCheckbox(chkSpotHiLoStart, bowSpotHiLoStart);
		loadCheckbox(chkShowSpotterUIForSpectators, bowShowSpotterUIForSpectators);
		loadDecimal(numHushDuration, bowHushDuration);
		loadDecimal(numSpotHiLowPadding, bowSpotHiLowPadding);
		loadComboIndex(cboSpotDisplay, bowSpotDisplay);
		loadSpotVoice();
		loadCheckbox(chkSpotReduceIfLive, bowSpotReduceIfLive);
		loadComboIndex(cboSpotChatty, bowSpotChatty);
		loadCheckbox(chkReportLaps, bowReportLaps);
		loadCheckbox(chkReportMinute, bowReportLaps);
		loadNumber(numSpotPrecision, bowSpotPrecision);
		loadComboIndex(cboSpotReporting, bowSpotReporting);
		loadSlider(sliderSPCCTextFactor, bowSPCCTextFactor);
		loadCheckbox(chkLeaderChanged, bowLeaderChanged);
		loadCheckbox(chkLeaderTimes, bowLeaderTimes);
		loadCheckbox(chkReportGap, bowReportGap);
		loadCheckbox(chkLappingTraffic, bowLappingTraffic);
		loadCheckbox(chkPitboxCountdown, bowPitboxCountdown);
		loadCheckbox(chkPitNotify, bowPitNotify);
		loadCheckbox(chkNewClassBest, bowNewClassBest);
		loadCheckbox(chkNewClassBestTime, bowNewClassBestTime);
		loadCheckbox(chkNewPersonalBestRace, bowNewPersonalBestRace);
		loadCheckbox(chkNewPersonalBest, bowNewPersonalBest);
	}

	private void loadSpotVoice()
	{
		string text = ProfileView.tagGetValue(cboSpotVoice.GetTag());
		if (text.Length == 0)
		{
			cboSpotVoice.SelectedIndex = 0;
		}
		else
		{
			cboSpotVoice.SelectedIndex = cboSpotVoice.Items.IndexOf(text);
		}
		bowSpotVoice.tagCompare();
	}

	private void cboSpotVoice_Changed(object sender, RoutedEventArgs e)
	{
		saveSpotVoice(cboSpotVoice, bowSpotVoice);
	}

	private void InitTabNotes()
	{
	}

	private void DataLoadNotes()
	{
		txtNotes.Text = string.Join(Environment.NewLine, ProfileView.Notes);
		txtNotes.Tag = null;
	}

	private void DataSaveNotes()
	{
		if (ProfileView == null || txtNotes.Tag == null)
		{
			return;
		}
		ProfileView.Notes.Clear();
		if (txtNotes.Text.Trim().Length > 0)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < txtNotes.LineCount; i++)
			{
				list.Add(txtNotes.GetLineText(i).Replace(Environment.NewLine, ""));
			}
			ProfileView.NotesUpdate(list);
		}
	}

	private void txtNotes_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (ProfileView != null && !DataLoading)
		{
			ProfileView.IsModified = true;
			txtNotes.Tag = string.Empty;
		}
	}

	private void iniWatchInit()
	{
		appWatcher = newWatcher("app.ini");
		coreWatcher = newWatcher("core.ini");
		dx11Watcher = newWatcher("rendererDX11Monitor.ini");
		if (iniExists("rendererDX11Oculus.ini"))
		{
			oculusWatcher = newWatcher("rendererDX11Oculus.ini");
		}
		if (iniExists("rendererDX11OpenVR.ini"))
		{
			openvrWatcher = newWatcher("rendererDX11OpenVR.ini");
		}
		if (iniExists("rendererDX11OpenXR.ini"))
		{
			openxrWatcher = newWatcher("rendererDX11OpenXR.ini");
		}
		iniWatchEnable(enable: true);
		FileSystemWatcher newWatcher(string filename)
		{
			FileSystemWatcher fileSystemWatcher = new FileSystemWatcher(iRacingFolder, filename.ToLower());
			fileSystemWatcher.Changed += iniChanged;
			fileSystemWatcher.Renamed += iniRenamed;
			fileSystemWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
			fileSystemWatcher.IncludeSubdirectories = false;
			return fileSystemWatcher;
		}
	}

	private bool iniExists(string filename)
	{
		return File.Exists(Path.Combine(iRacingFolder, filename));
	}

	private void iniWatchEnable(bool enable)
	{
		appWatcher.EnableRaisingEvents = enable;
		coreWatcher.EnableRaisingEvents = enable;
		dx11Watcher.EnableRaisingEvents = enable;
		if (oculusWatcher != null)
		{
			oculusWatcher.EnableRaisingEvents = enable;
		}
		if (openvrWatcher != null)
		{
			openvrWatcher.EnableRaisingEvents = enable;
		}
		if (openxrWatcher != null)
		{
			openxrWatcher.EnableRaisingEvents = enable;
		}
	}

	private void iniChangeEvent(FileSystemWatcher watcher)
	{
		lock (SyncLock)
		{
			appProfile appProfile2 = null;
			try
			{
				if (watcher == oculusWatcher)
				{
					appProfile2 = dx11Reload(ProfileList.Oculus);
				}
				else if (watcher == openvrWatcher)
				{
					appProfile2 = dx11Reload(ProfileList.OpenVR);
				}
				else if (watcher == openxrWatcher)
				{
					appProfile2 = dx11Reload(ProfileList.OpenXR);
				}
				else if (watcher == appWatcher)
				{
					appProfile2 = appReload(ProfileList.Monitor);
				}
				else if (watcher == coreWatcher)
				{
					appProfile2 = coreReload(ProfileList.Monitor);
				}
				else if (watcher == dx11Watcher)
				{
					appProfile2 = dx11Reload(ProfileList.Monitor);
				}
				if (appProfile2 == null)
				{
					return;
				}
			}
			catch (Exception ex)
			{
				TraceLog.Exception(ex, "iniChangeEvent failed to resolve the file watcher to a profile");
				return;
			}
			if (ProfileView.IsMonitor || ProfileView.IsIRacingVR)
			{
				try
				{
					loadControlValues();
					return;
				}
				catch (Exception ex2)
				{
					TraceLog.Exception(ex2, "loadControlValues encountered an exception");
					return;
				}
			}
		}
	}

	private appProfile appReload(appProfile profile)
	{
		TraceLog.Info("Windows file watcher triggered app.ini reload for profile '" + profile.DisplayName + "'");
		if (FileIsEmpty(profile))
		{
			return null;
		}
		try
		{
			profile.irApp.Load(profile.Name, Fixup: false);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "App load failure");
			return null;
		}
		try
		{
			profile.CarCameras.Load();
		}
		catch (Exception ex2)
		{
			TraceLog.Exception(ex2, "CarCamers load failure");
			return null;
		}
		profile.LastChanged = profile.irApp.LastChanged;
		return profile;
	}

	private appProfile coreReload(appProfile profile)
	{
		TraceLog.Info("Windows file watcher triggered core.ini reload for profile '" + profile.DisplayName + "'");
		if (FileIsEmpty(profile))
		{
			return null;
		}
		try
		{
			profile.irCore.Load(profile.Name, Fixup: false);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Core load failure");
			return null;
		}
		profile.LastChanged = profile.irCore.LastChanged;
		return profile;
	}

	private appProfile dx11Reload(appProfile profile)
	{
		TraceLog.Info("Windows file watcher triggered dx11.ini reload for profile '" + profile.DisplayName + "'");
		if (FileIsEmpty(profile))
		{
			return null;
		}
		try
		{
			profile.irDX11.Load(profile.Name, Fixup: false);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "DX11 load failure");
			return null;
		}
		profile.LastChanged = profile.irDX11.LastChanged;
		return profile;
	}

	private bool FileIsEmpty(appProfile profile)
	{
		string fullFileName = iniConfig.App.getFullFileName(profile.Name);
		if (!File.Exists(fullFileName))
		{
			return true;
		}
		if (new FileInfo(fullFileName).Length < 5)
		{
			Thread.Sleep(1000);
			if (new FileInfo(fullFileName).Length < 5)
			{
				TraceLog.Warn("Empty iRacing ini file, load aborted: " + fullFileName);
				return true;
			}
		}
		return false;
	}

	protected void iniChanged(object source, FileSystemEventArgs changeEvent)
	{
		TraceLog.Info("Windows file change notification: " + changeEvent.ChangeType.ToString() + " " + changeEvent.Name);
		try
		{
			Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				iniChangeEvent(source as FileSystemWatcher);
			});
		}
		catch
		{
		}
	}

	protected void iniRenamed(object source, RenamedEventArgs renameEvent)
	{
		TraceLog.Info("Windows file rename notification: " + renameEvent.ChangeType.ToString() + " " + renameEvent.OldName + " to " + renameEvent.Name);
		if (!renameEvent.Name.ToLower().EndsWith(".ini"))
		{
			return;
		}
		try
		{
			Application.Current.Dispatcher.BeginInvoke((Action)delegate
			{
				iniChangeEvent(source as FileSystemWatcher);
			});
		}
		catch
		{
		}
	}

	private void iniAudioNotify()
	{
		new SoundPlayer(Assembly.GetExecutingAssembly().GetManifestResourceStream("irSidekickProfiles.Audio.iniModified.wav")).Play();
	}

	private void saveResolution()
	{
		if (!DataLoading && cboResolution.SelectedIndex != -1)
		{
			string obj = (cboResolution.SelectedItem as ComboBoxItem).Content as string;
			bool flag = obj.Contains("*");
			string[] array = obj.Split(flag ? '*' : 'x');
			int num = cboNumMonitors.SelectedIndex * 2 + 1;
			double num2 = array[0].ToDouble();
			string value = (flag ? array[0] : (num2 * (double)num).ToIntStr());
			ProfileView.tagSetValue(iniDX11.windowedWidth.TagID, value);
			ProfileView.tagSetValue(iniDX11.windowedHeight.TagID, array[1]);
			ProfileView.tagSetValue(iniDX11.fullScreenWidth.TagID, value);
			ProfileView.tagSetValue(iniDX11.fullScreenHeight.TagID, array[1]);
			ProfileView.tagSetValue(iniApp.browserWindowedXPos.TagID, "0");
			ProfileView.tagSetValue(iniApp.browserWindowedYPos.TagID, "0");
			ProfileView.tagSetValue(iniApp.browserWindowedWidth.TagID, "1920");
			ProfileView.tagSetValue(iniApp.browserWindowedHeight.TagID, "1080");
			if (!flag && num == 3)
			{
				loadResolution();
			}
			else
			{
				bowResolution.tagCompare();
			}
		}
	}

	private void saveNumMonitors()
	{
		if (DataLoading || cboNumMonitors == null)
		{
			return;
		}
		int selectedIndex = cboNumMonitors.SelectedIndex;
		if (selectedIndex < 0 || selectedIndex > 3)
		{
			return;
		}
		int[] array = new int[4] { 1, 3, 1, 3 };
		int[] obj = new int[4] { 0, 0, 1, 1 };
		int value = array[selectedIndex];
		int value2 = obj[selectedIndex];
		try
		{
			if (ProfileView.tagSetValue(cboNumMonitors.GetTag(), value.ToIntStr()) | ProfileView.tagSetValue(iniDX11.MonitorType.TagID, value2.ToIntStr()))
			{
				bowNumScreens.tagCompare();
			}
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Exception encountered saving number of monitors.");
		}
	}

	private void saveDrawCars(int index, string tagCars, string tagMirrors, clsRainbow bow)
	{
		if (!DataLoading)
		{
			switch (index)
			{
			case 0:
				ProfileView.tagSetValue(tagCars, "64");
				ProfileView.tagSetValue(tagMirrors, "64");
				break;
			case 1:
				ProfileView.tagSetValue(tagCars, "64");
				ProfileView.tagSetValue(tagMirrors, "30");
				break;
			case 2:
				ProfileView.tagSetValue(tagCars, "64");
				ProfileView.tagSetValue(tagMirrors, "16");
				break;
			case 3:
				ProfileView.tagSetValue(tagCars, "64");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 4:
				ProfileView.tagSetValue(tagCars, "40");
				ProfileView.tagSetValue(tagMirrors, "20");
				break;
			case 5:
				ProfileView.tagSetValue(tagCars, "40");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 6:
				ProfileView.tagSetValue(tagCars, "40");
				ProfileView.tagSetValue(tagMirrors, "6");
				break;
			case 7:
				ProfileView.tagSetValue(tagCars, "30");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 8:
				ProfileView.tagSetValue(tagCars, "30");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 9:
				ProfileView.tagSetValue(tagCars, "30");
				ProfileView.tagSetValue(tagMirrors, "4");
				break;
			case 10:
				ProfileView.tagSetValue(tagCars, "20");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 11:
				ProfileView.tagSetValue(tagCars, "20");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 12:
				ProfileView.tagSetValue(tagCars, "20");
				ProfileView.tagSetValue(tagMirrors, "4");
				break;
			case 13:
				ProfileView.tagSetValue(tagCars, "10");
				ProfileView.tagSetValue(tagMirrors, "4");
				break;
			}
			bow.tagCompare();
		}
	}

	private void saveDrawPits(int index, string tagPits, string tagMirrors, clsRainbow bow)
	{
		if (!DataLoading)
		{
			switch (index)
			{
			case 0:
				ProfileView.tagSetValue(tagPits, "64");
				ProfileView.tagSetValue(tagMirrors, "64");
				break;
			case 1:
				ProfileView.tagSetValue(tagPits, "64");
				ProfileView.tagSetValue(tagMirrors, "30");
				break;
			case 2:
				ProfileView.tagSetValue(tagPits, "64");
				ProfileView.tagSetValue(tagMirrors, "16");
				break;
			case 3:
				ProfileView.tagSetValue(tagPits, "64");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 4:
				ProfileView.tagSetValue(tagPits, "40");
				ProfileView.tagSetValue(tagMirrors, "20");
				break;
			case 5:
				ProfileView.tagSetValue(tagPits, "40");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 6:
				ProfileView.tagSetValue(tagPits, "40");
				ProfileView.tagSetValue(tagMirrors, "6");
				break;
			case 7:
				ProfileView.tagSetValue(tagPits, "30");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 8:
				ProfileView.tagSetValue(tagPits, "30");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 9:
				ProfileView.tagSetValue(tagPits, "30");
				ProfileView.tagSetValue(tagMirrors, "4");
				break;
			case 10:
				ProfileView.tagSetValue(tagPits, "20");
				ProfileView.tagSetValue(tagMirrors, "12");
				break;
			case 11:
				ProfileView.tagSetValue(tagPits, "20");
				ProfileView.tagSetValue(tagMirrors, "8");
				break;
			case 12:
				ProfileView.tagSetValue(tagPits, "20");
				ProfileView.tagSetValue(tagMirrors, "4");
				break;
			case 13:
				ProfileView.tagSetValue(tagPits, "4");
				ProfileView.tagSetValue(tagMirrors, "0");
				break;
			case 14:
				ProfileView.tagSetValue(tagPits, "0");
				ProfileView.tagSetValue(tagMirrors, "0");
				break;
			}
			bow.tagCompare();
		}
	}

	public void saveLOD(ComboBox cbo, clsRainbow bow)
	{
		if (!DataLoading && cbo.SelectedIndex >= 0)
		{
			string tag = cbo.GetTag();
			string tag2 = (cbo.SelectedItem as ComboBoxItem).GetTag();
			ProfileView.tagSetValue(tag, tag2);
			bow.tagCompare();
		}
	}

	public void saveSSR(ComboBox cbo, clsRainbow bow)
	{
		if (!DataLoading && cbo.SelectedIndex >= 0)
		{
			string tag = cbo.GetTag();
			string tag2 = (cbo.SelectedItem as ComboBoxItem).GetTag();
			ProfileView.tagSetValue(tag, tag2);
			bow.tagCompare();
		}
	}

	public void saveFPSOptions()
	{
		if (!DataLoading)
		{
			string tagID = iniDX11.Graphic_LimitFrameRate.TagID;
			string value = "0";
			if (rbFPSNoLimit.IsChecked == true)
			{
				value = "0";
			}
			if (rbFPSLimit.IsChecked == true)
			{
				value = "1";
			}
			if (ProfileView.tagSetValue(tagID, value))
			{
				bowFPSFrameRate.tagCompare();
			}
		}
	}

	public void saveMSAASamples()
	{
		if (!DataLoading)
		{
			bool flag = false;
			string tag = cboMSAASamples.Tag.ToString();
			if (cboMSAASamples.SelectedIndex switch
			{
				0 => ProfileView.tagSetValue(tag, "2"), 
				1 => ProfileView.tagSetValue(tag, "4"), 
				2 => ProfileView.tagSetValue(tag, "8"), 
				_ => ProfileView.tagSetValue(tag, "4"), 
			})
			{
				bowMSAASamples.tagCompare();
			}
		}
	}

	private void saveSteeringWheel(ComboBox cbo, string tagHands, clsRainbow bow)
	{
		string tag = cbo.GetTag();
		if (!DataLoading)
		{
			switch (cbo.SelectedIndex)
			{
			case 4:
				ProfileView.tagSetValue(tag, "3");
				ProfileView.tagSetValue(tagHands, "0");
				break;
			case 3:
				ProfileView.tagSetValue(tag, "1");
				ProfileView.tagSetValue(tagHands, "1");
				break;
			case 2:
				ProfileView.tagSetValue(tag, "1");
				ProfileView.tagSetValue(tagHands, "0");
				break;
			case 1:
				ProfileView.tagSetValue(tag, "2");
				ProfileView.tagSetValue(tagHands, "0");
				break;
			default:
				ProfileView.tagSetValue(tag, "0");
				ProfileView.tagSetValue(tagHands, "0");
				break;
			}
			bow.tagCompare();
		}
	}

	public void saveFXAAEdge(ComboBox cbo, clsRainbow bow)
	{
		if (!DataLoading)
		{
			switch (cbo.SelectedIndex)
			{
			case 0:
				ProfileView.tagSetValue(cbo.GetTag(), "333");
				break;
			case 1:
				ProfileView.tagSetValue(cbo.GetTag(), "250");
				break;
			case 2:
				ProfileView.tagSetValue(cbo.GetTag(), "166");
				break;
			case 3:
				ProfileView.tagSetValue(cbo.GetTag(), "125");
				break;
			case 4:
				ProfileView.tagSetValue(cbo.GetTag(), "63");
				break;
			}
			bow.tagCompare();
		}
	}

	public void saveFXAASubPix(ComboBox cbo, clsRainbow bow)
	{
		if (!DataLoading)
		{
			switch (cbo.SelectedIndex)
			{
			case 0:
				ProfileView.tagSetValue(cbo.GetTag(), "100");
				break;
			case 1:
				ProfileView.tagSetValue(cbo.GetTag(), "75");
				break;
			case 2:
				ProfileView.tagSetValue(cbo.GetTag(), "50");
				break;
			case 3:
				ProfileView.tagSetValue(cbo.GetTag(), "25");
				break;
			case 4:
				ProfileView.tagSetValue(cbo.GetTag(), "0");
				break;
			}
			bow.tagCompare();
		}
	}

	private void saveSpotVoice(ComboBox cbo, clsRainbow bow)
	{
		if (!DataLoading)
		{
			string value = ((cbo.SelectedIndex == 0) ? "" : cbo.SelectedItem.ToString());
			if (ProfileView.tagSetValue(cbo.GetTag(), value))
			{
				bow.tagCompare();
			}
		}
	}

	private void ConfigMem()
	{
		SetSYSMem();
		string text = Ini.ReadKey("GPU", "Hardware");
		if (text.Length > 0)
		{
			gfxGPUCard gPUCard = gfxConfig.GPUList.GetGPUCard(text);
			if (gPUCard != null)
			{
				SetGPUMem(gPUCard.gbMem);
			}
		}
	}

	private void SetSYSMem()
	{
		ProfileView.tagSetValue("2.Graphics Options.SysMemToUseMB", CalcSYSMem());
	}

	public string CalcSYSMem()
	{
		double val = Math.Round((double)ExtnProcess.GetTotalMemoryInBytes() * 0.85 / 1024000.0, 0);
		val = Math.Max(1024.0, Math.Min(val, 20480.0));
		return val.ToIntStr();
	}

	private void SetGPUMem(int hasGB)
	{
		ProfileView.tagSetValue("2.Graphics Options.VidMemToUseMB", CalcGPUMem(hasGB));
	}

	public string CalcGPUMem(int hasGB)
	{
		double val = Math.Round((double)hasGB * 850.0, 0);
		val = Math.Max(512.0, Math.Min(val, 16384.0));
		return val.ToIntStr();
	}

	private void canSave(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ProfileList.Count > 0 && ProfileList.IsModified();
	}

	private void cmdSave(object sender, ExecutedRoutedEventArgs e)
	{
		(((Selector)(object)tabSections).SelectedItem as TabItem).Focus();
		if (ProfileList.Monitor.IsModified && ExtnProcess.isIRacingRunning())
		{
			MessageBox.Show("The iRacing profile has been modified but cannot be saved while iRacing is running, close iRacing then press the save button.", "Cannot save iRacing profile");
		}
		if (ConfirmAction("This action will save all settings for all profiles into the ini files.", "Confirm Save Action") && doSave(warn: false))
		{
			loadControlValues();
		}
	}

	private bool doSave(bool warn)
	{
		DataSaveNotes();
		if (!ProfileList.IsModified())
		{
			return false;
		}
		if (warn && MessageBoxResult.No == MessageBox.Show("Some profiles have been modified, do you want to save your changes?", "Changes NOT saved", MessageBoxButton.YesNo))
		{
			return false;
		}
		try
		{
			iniWatchEnable(enable: false);
			return ProfileList.Save();
		}
		finally
		{
			iniWatchEnable(enable: true);
		}
	}

	private void canBackup(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ProfileView != null;
	}

	private void cmdBackup(object sender, ExecutedRoutedEventArgs e)
	{
		appProfile backup = ProfileList.Backup;
		backup.CloneFrom(ProfileView);
		backup.Save();
		loadControlValues();
		Ini.WriteKey(backup.DisplayName, ProfileView.DisplayName, "Origin");
		backup.UpdateToolTip();
	}

	private void canRestore(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = !ExtnProcess.isIRacingRunning() && ProfileView != null;
	}

	private void cmdRestore(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("This action will restore " + ProfileView.DisplayName + " settings from backup", "Confirm Restore Action"))
		{
			ProfileView.CloneFrom(ProfileList.Backup);
			try
			{
				iniWatchEnable(enable: false);
				ProfileView.Save();
				Ini.WriteKey(ProfileView.DisplayName, ProfileList.Backup.DisplayName, "Origin");
				ProfileView.UpdateToolTip();
			}
			finally
			{
				iniWatchEnable(enable: true);
			}
			loadControlValues();
		}
	}

	private void canHistory(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = appHistory.HistoryAvailable && (!ExtnProcess.isIRacingRunning() || !ProfileView.IsMonitor);
	}

	private void cmdHistory(object sender, ExecutedRoutedEventArgs e)
	{
		winHistoryList.SelectRestore(this);
	}

	private void canUpdateFromIRacing(object sender, CanExecuteRoutedEventArgs e)
	{
		if (ProfileList == null || ProfileList.Count == 0 || ProfileView == null)
		{
			e.CanExecute = false;
		}
		else if (ProfileView.IsBackup || ProfileView.IsMonitor)
		{
			e.CanExecute = false;
		}
		else
		{
			e.CanExecute = true;
		}
	}

	private void cmdUpdateFromIRacing(object sender, ExecutedRoutedEventArgs e)
	{
		if (ProfileList == null || ProfileList.Count == 0 || ProfileView == null || ProfileView.IsBackup || ProfileView.IsMonitor)
		{
			return;
		}
		if (ProfileView.IsIRacingVR)
		{
			switch (ProfileView.DisplayName)
			{
			case "Oculus":
				Oculus_Update(sender, e);
				break;
			case "OpenVR":
				OpenVR_Update(sender, e);
				break;
			case "OpenXR":
				OpenXR_Update(sender, e);
				break;
			}
			return;
		}
		appProfile appProfile2;
		switch (ProfileView.DisplayMode)
		{
		default:
			return;
		case iniDisplayMode.Monitor:
			appProfile2 = ProfileList.Monitor;
			break;
		case iniDisplayMode.Oculus:
			appProfile2 = ProfileList.Oculus;
			break;
		case iniDisplayMode.OpenVR:
			appProfile2 = ProfileList.OpenVR;
			break;
		case iniDisplayMode.OpenXR:
			appProfile2 = ProfileList.OpenXR;
			break;
		}
		try
		{
			iniWatchEnable(enable: false);
			if (appProfile2.IsIRacingVR)
			{
				appProfile monitor = ProfileList.Monitor;
				appProfile2.irApp.CloneFrom(monitor.irApp);
			}
			ProfileView.CloneFrom(appProfile2);
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Update from iRacing encountered an exception");
		}
		finally
		{
			iniWatchEnable(enable: true);
		}
		loadControlValues();
	}

	private void canApply(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdApply(object sender, ExecutedRoutedEventArgs e)
	{
		e.Handled = false;
		if (ProfileList == null || ProfileList.Count == 0 || ProfileView == null)
		{
			return;
		}
		if (ExtnProcess.isIRacingRunning())
		{
			MessageBox.Show(this, "Cannot apply profile settings to iRacing when iRacing is running", "Apply profile settings", MessageBoxButton.OK, MessageBoxImage.Hand);
			return;
		}
		if (ProfileView.IsBackup)
		{
			MessageBox.Show(this, "The Backup settings cannot be applied to iRacing.\r\rUse restore iRacing from backup instead.", "Apply profile settings", MessageBoxButton.OK, MessageBoxImage.Hand);
			return;
		}
		if (ProfileView.IsMonitor)
		{
			MessageBox.Show(this, "The Monitor settings are the iRacing settings.\r\rThere is no need to use the apply button.\r\rJust use the white save button.", "Apply profile settings", MessageBoxButton.OK, MessageBoxImage.Hand);
			return;
		}
		if (ProfileView.IsModified)
		{
			try
			{
				iniWatchEnable(enable: false);
				ProfileView.Save();
			}
			finally
			{
				iniWatchEnable(enable: true);
			}
		}
		if (ProfileView.IsBackup || ProfileView.IsMonitor)
		{
			return;
		}
		string[] vals;
		string[] tags;
		if (ProfileView.IsIRacingVR)
		{
			switch (ProfileView.DisplayName)
			{
			case "Oculus":
				Oculus_Apply(sender, e);
				break;
			case "OpenVR":
				OpenVR_Apply(sender, e);
				break;
			case "OpenXR":
				OpenXR_Apply(sender, e);
				break;
			}
			e.Handled = true;
		}
		else if (base.Tag == null || !(base.Tag as OptionsContainer).Option[2])
		{
			MessageBox.Show(this, "A licence is required to apply custom profiles to iRacing.\r\rLicensing is explained on the irSidekick Discord server.", "Apply profile settings", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		else
		{
			if (base.Tag == null || !(base.Tag as OptionsContainer).Option[2])
			{
				return;
			}
			e.Handled = true;
			appProfile appProfile2;
			switch (ProfileView.DisplayMode)
			{
			default:
				return;
			case iniDisplayMode.Monitor:
				appProfile2 = ProfileList.Monitor;
				break;
			case iniDisplayMode.Oculus:
				appProfile2 = ProfileList.Oculus;
				break;
			case iniDisplayMode.OpenVR:
				appProfile2 = ProfileList.OpenVR;
				break;
			case iniDisplayMode.OpenXR:
				appProfile2 = ProfileList.OpenXR;
				break;
			}
			Ini.WriteKey(appProfile2.DisplayName, ProfileView.DisplayName, "Origin");
			appProfile2.UpdateToolTip();
			vals = new string[3];
			tags = new string[3] { "0.SplitsDeltas.comparisonLapFileName", null, null };
			if (PreserveMainVolume && appProfile2.IsMonitor)
			{
				tags[1] = "0.Audio.masterVolumedB";
				tags[2] = "0.Audio.loudnessLFE";
			}
			try
			{
				iniWatchEnable(enable: false);
				tagGetValues(appProfile2);
				appProfile2.CloneFrom(ProfileView);
				tagSetValues(appProfile2);
				if (appProfile2.IsIRacingVR)
				{
					appProfile monitor = ProfileList.Monitor;
					monitor.irApp.CloneFrom(ProfileView.irApp);
					CopyTagValue(ProfileView, monitor, iniDX11.Graphic_VRMode);
					tagSetValues(monitor);
					monitor.Save();
					ProfileView.CarCameras.Apply();
				}
				appProfile2.Save();
			}
			catch (Exception ex)
			{
				TraceLog.Exception(ex, "Save to iRacing encountered an exception");
			}
			finally
			{
				iniWatchEnable(enable: true);
			}
			loadControlValues();
		}
		void tagGetValues(appProfile profile)
		{
			for (int i = 0; i < tags.Length; i++)
			{
				if (!tags[i].IsNullOrEmpty())
				{
					vals[i] = profile.tagGetValue(tags[i]);
				}
			}
		}
		void tagSetValues(appProfile profile)
		{
			for (int i = 0; i < tags.Length; i++)
			{
				if (!tags[i].IsNullOrEmpty())
				{
					profile.tagSetValue(tags[i], vals[i]);
				}
			}
		}
	}

	private void canCopy(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ProfileView != null;
	}

	private void cmdCopy(object sender, ExecutedRoutedEventArgs e)
	{
		string name = winProfileName.getName(this, ProfileList);
		if (name.Length != 0)
		{
			appProfile obj = ProfileList.addProfile(name);
			obj.CloneFrom(ProfileView);
			obj.Notes.Clear();
			List<string> list = new List<string>(ProfileView.Notes);
			list.Insert(0, "Copied from profile " + ProfileView.DisplayName);
			obj.NotesUpdate(list);
			addProfileControls();
			ProfileList[name].Tab.IsSelected = true;
		}
	}

	private void canRename(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
	}

	private void cmdRename(object sender, ExecutedRoutedEventArgs e)
	{
		MessageBox.Show("Not implemented yet...", "Rename Profile");
	}

	private void canDelete(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ProfileList.Count > 2 && ProfileView != null && !ProfileView.IsMonitor && !ProfileView.IsBackup;
	}

	private void cmdDelete(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("This action will delete the " + ProfileView.DisplayName + " profile.", "Confirm Delete Action"))
		{
			ProfileView.Delete();
			ProfileList.Remove(ProfileView.Name);
			addProfileControls();
			ProfileList.Monitor.Tab.IsSelected = true;
		}
	}

	private void canExport(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ProfileList.Count > 0;
	}

	private void cmdExport(object sender, ExecutedRoutedEventArgs e)
	{
		ProfileList.Export((base.Tag as OptionsContainer).Option[2]);
	}

	private void canImport(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdImport(object sender, ExecutedRoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.InitialDirectory = iRacing.pathSidekick();
		openFileDialog.Filter = "Profile Export|irSidekick.Profiles.*.zip";
		openFileDialog.Multiselect = false;
		if (openFileDialog.ShowDialog(this).Value)
		{
			fileImport(openFileDialog.FileName);
		}
	}

	private void fileImport(string filename)
	{
		if (!filename.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase))
		{
			return;
		}
		string text = "";
		List<string> list = new List<string>();
		try
		{
			if (!File.Exists(filename))
			{
				MessageBox.Show("The zip file no longer exists.\n\n" + filename, "Zip file not found");
				return;
			}
			string[] array = filename.Split('.');
			text = ((array.Length != 5) ? "External" : array[2]);
			ZipArchive val = ZipFile.OpenRead(filename);
			try
			{
				foreach (ZipArchiveEntry entry in val.Entries)
				{
					string[] array2 = entry.Name.Split('.');
					if (array2.Length == 3 && array2[0].Equals("app", StringComparison.CurrentCultureIgnoreCase) && array2[1].Equals("ini", StringComparison.CurrentCultureIgnoreCase) && !list.Contains(array2[2]))
					{
						list.Add(array2[2]);
					}
				}
				list.Sort();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (Exception ex)
		{
			TraceLog.Error("Error reading zip " + filename, ex);
			MessageBox.Show(ex.Message, "Error reading zip file");
			return;
		}
		bool flag = false;
		string text2 = Ini.ReadKey("MemberID", "UserInfo", iniSidekick: true);
		string text3 = Ini.ReadKey("Name", "UserInfo", iniSidekick: true);
		if (text2.Length > 0)
		{
			flag = filename.Contains(text2);
		}
		else if (text3.Length > 0)
		{
			flag = text.Contains(text3);
		}
		string path = iRacing.pathRoot();
		string text4 = winImportList.SelectProfiles(this, text, list);
		if (text4.Length == 0 || !ConfirmAction("The following profiles will be imported.\n\n" + text4.Replace(",", "\n"), "Confirm Import Profiles Action"))
		{
			return;
		}
		if (!File.Exists(filename))
		{
			MessageBox.Show("The zip file no longer exists.\n\n" + filename, "Zip file not found");
			return;
		}
		ZipArchive val2 = ZipFile.OpenRead(filename);
		try
		{
			foreach (ZipArchiveEntry entry2 in val2.Entries)
			{
				string[] array3 = entry2.Name.Split('.');
				if (array3.Length != 3 || !text4.Contains(array3[2]))
				{
					continue;
				}
				string tempFileName = Path.GetTempFileName();
				string text5 = Path.Combine(path, entry2.Name);
				if (flag && text3.Length > 0 && entry2.Name.Contains(text3))
				{
					text5 = Regex.Replace(text5, "(.*\\x2Eini\\x2E)(.*!)(.*)", "$1$3");
					if (text5.EndsWith(".iracing", StringComparison.CurrentCultureIgnoreCase))
					{
						text5 = text5.Substring(0, text5.Length - 7);
					}
				}
				try
				{
					TraceLog.Info("Import " + entry2.Name + " from " + filename);
					entry2.ExtractToFile(tempFileName, overwrite: true);
					File.Copy(tempFileName, text5, overwrite: true);
					File.Delete(tempFileName);
				}
				catch (Exception ex2)
				{
					if (File.Exists(tempFileName))
					{
						File.Delete(tempFileName);
					}
					TraceLog.Exception(ex2);
					MessageBox.Show(ex2.Message, "Error extracting zip file");
					return;
				}
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		LoadProfiles();
	}

	private void canRun(object sender, CanExecuteRoutedEventArgs e)
	{
		if (ProfileList.Count == 0 || ExtnProcess.isIRacingRunning() || ExtnProcess.isIRacingUIRunning())
		{
			e.CanExecute = false;
		}
		else if (ProfileView != null && !ProfileView.IsBackup)
		{
			e.CanExecute = true;
		}
		else
		{
			e.CanExecute = false;
		}
	}

	private void cmdRun(object sender, ExecutedRoutedEventArgs e)
	{
		if (ProfileView.IsMonitor)
		{
			if (ProfileView.IsModified)
			{
				cmdSave(sender, e);
			}
		}
		else
		{
			cmdApply(sender, e);
			if (!e.Handled)
			{
				return;
			}
		}
		e.Handled = false;
		try
		{
			string iRacingEXE = appExtensions.getIRacingEXE();
			try
			{
				Process.Start(iRacingEXE);
				e.Handled = true;
				if (AutoExitOnLaunch && !ProfileList.IsModified())
				{
					cmdExit();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error launching iRacing");
				TraceLog.Error("Error launching iRacing: " + ex.Message);
			}
		}
		catch (Exception ex2)
		{
			MessageBox.Show(ex2.Message, "Error reading registy");
			TraceLog.Error("Error reading registry: " + ex2.Message);
		}
	}

	private void canSettings(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdSettings(object sender, ExecutedRoutedEventArgs e)
	{
		winSettings obj = new winSettings();
		obj.Owner = this;
		obj.ShowDialog();
	}

	private void canUpdate(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = appUpdate.UpdateIsAvailable;
	}

	private void cmdUpdate(object sender, ExecutedRoutedEventArgs e)
	{
		doSave(warn: false);
		base.WindowState = WindowState.Minimized;
		appUpdate.RunSoftwareUpdate();
	}

	private void canForum(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdForum(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			Process.Start("https://forums.iracing.com/discussion/11701/irsidekick-profiles-manage-app-ini-rendererdx11monitor-ini#latest");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error launching browser");
			TraceLog.Error("Error launching browser: " + ex.Message);
		}
	}

	private void canDiscord(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdDiscord(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			Process.Start("https://discord.com/channels/899776203777536041/899776203777536044");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error launching browser");
			TraceLog.Error("Error launching browser: " + ex.Message);
		}
	}

	private void canHelp(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdHelp(object sender, ExecutedRoutedEventArgs e)
	{
		try
		{
			Process.Start("https://youtu.be/WgNnKkcEFlM");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "Error launching browser");
			TraceLog.Error("Error launching browser: " + ex.Message);
		}
	}

	private void canClose(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = true;
	}

	private void cmdClose(object sender, ExecutedRoutedEventArgs e)
	{
		cmdExit();
	}

	private void cmdExit()
	{
		if (Application.Current != null)
		{
			Application.Current.Shutdown();
		}
	}

	private void canGFXWizard(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && ProfileView != null && (!ProfileView.IsBackup & !ProfileView.IsDisplayModeVR) && tabGraphics.IsSelected)
		{
			e.CanExecute = true;
		}
	}

	private void cmdGFXWizard(object sender, ExecutedRoutedEventArgs e)
	{
		if (panelGFXWizard.IsSelected)
		{
			HideGFXWizard();
		}
		else
		{
			ShowGFXWizard();
		}
	}

	private void canSettingSearch(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && ProfileView != null)
		{
			e.CanExecute = true;
		}
	}

	private void cmdSettingSearch(object sender, ExecutedRoutedEventArgs e)
	{
		if (panelSearch.IsSelected)
		{
			HideSettingSearch();
		}
		else
		{
			ShowSettingSearch();
		}
	}

	private void canGFXVRR(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && ProfileView != null && !ProfileView.IsBackup && tabGraphics.IsSelected)
		{
			e.CanExecute = true;
		}
	}

	private void cmdGFXVRR(object sender, ExecutedRoutedEventArgs e)
	{
		double num = ProfileView.irDX11.tagGetValue(iniDX11.RefreshRate).ToInt();
		if (num > 60.0)
		{
			double num2 = Math.Floor(1000.0 / Math.Ceiling(1000.0 / num + 1.0));
			double value = Math.Ceiling(num2 * 0.85);
			ProfileView.irDX11.tagSetValue(iniDX11.Graphic_DesiredFPSLimit, num2.ToIntStr());
			ProfileView.irDX11.tagSetValue(iniDX11.Graphic_LODMinFPSTarget, value.ToIntStr());
			ProfileView.irDX11.tagSetValue(iniDX11.Graphic_NvReflexMode, "0");
			ProfileView.IsModified = true;
		}
		DataLoadTabDisplay();
		DataLoadTabGraphics();
	}

	private void canGFXPreset(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && ProfileView != null && !ProfileView.IsBackup && tabGraphics.IsSelected)
		{
			e.CanExecute = true;
		}
	}

	private void cmdGFXLow(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("Set profile GFX settings to LOW.\n\nThis GFX preset should only be used\nwhen experiencing very poor performance.", "Confirm LOW Graphic Preset Action"))
		{
			ConfigMem();
			ProfileView.IsModified = true;
			ProfileView.irDX11.allSetGFXPreset(gfxPreset.Low);
			DataLoadTabGraphics();
			DataLoadTabReplay();
		}
	}

	private void cmdGFXFaster(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("Set profile GFX settings to FASTER.\n\nThis GFX preset is used on tracks that return lower FPS\nor when the computer is unable to deliver good FPS.", "Confirm FASTER Graphic Preset Action"))
		{
			ConfigMem();
			ProfileView.IsModified = true;
			ProfileView.irDX11.allSetGFXPreset(gfxPreset.Faster);
			DataLoadTabGraphics();
			DataLoadTabReplay();
		}
	}

	private void cmdGFXStandard(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("Set profile GFX settings to STANDARD.\n\nThis GFX preset is a sensible starting point.", "Confirm STANDARD Graphic Preset Action"))
		{
			ConfigMem();
			ProfileView.IsModified = true;
			ProfileView.irDX11.allSetGFXPreset(gfxPreset.Standard);
			DataLoadTabGraphics();
			DataLoadTabReplay();
		}
	}

	private void cmdGFXPretty(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("Set profile GFX settings to PRETTY.\n\nThis GFX preset provides higher GFX settings\nwhich are pretty but need a capable computer.", "Confirm PRETTY Graphic Preset Action"))
		{
			ConfigMem();
			ProfileView.IsModified = true;
			ProfileView.irDX11.allSetGFXPreset(gfxPreset.Pretty);
			DataLoadTabGraphics();
			DataLoadTabReplay();
		}
	}

	private void cmdGFXHigh(object sender, ExecutedRoutedEventArgs e)
	{
		if (ConfirmAction("Set profile GFX settings to HIGH.\n\nThis GFX preset is used on single screen\nor triple screen with a high end computer.", "Confirm HIGH Graphic Preset Action"))
		{
			ConfigMem();
			ProfileView.IsModified = true;
			ProfileView.irDX11.allSetGFXPreset(gfxPreset.High);
			DataLoadTabGraphics();
			DataLoadTabReplay();
		}
	}

	private void canGFXSync(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && ProfileView != null && !ProfileView.IsBackup && tabReplay.IsSelected)
		{
			e.CanExecute = true;
		}
	}

	private void cmdGFXSync(object sender, ExecutedRoutedEventArgs e)
	{
		ProfileView.CopyGFX2Replay();
		DataLoadTabReplay();
	}

	private void canCopySettings(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		if (ProfileList.Count > 1 && cboTargetProfile.SelectedIndex >= 0 && cboTargetProfile.SelectedItem != null && ProfileView != null && (cboTargetProfile.SelectedItem as ComboBoxItem).Tag.ToString() != ProfileView.Name && ProfileList.ContainsKey(cboTargetProfile.Text))
		{
			e.CanExecute = true;
		}
	}

	private void cmdCopySettings(object sender, ExecutedRoutedEventArgs e)
	{
		appProfile appProfile2;
		if (cboTargetProfile.Text.Equals("Monitor"))
		{
			appProfile2 = ProfileList.Monitor;
		}
		else
		{
			if (!ProfileList.ContainsKey(cboTargetProfile.Text))
			{
				return;
			}
			appProfile2 = ProfileList[cboTargetProfile.Text];
		}
		string text = ((Selector)(object)tabSections).SelectedIndex switch
		{
			0 => "Display", 
			1 => "Graphics", 
			2 => "Replay", 
			3 => "Extra", 
			4 => "FFB", 
			5 => "Auto Chat", 
			6 => "Sound", 
			7 => "Spotter", 
			8 => "Notes", 
			_ => "?", 
		};
		if (ConfirmActions && !ConfirmAction("This action will copy all " + text + " settings\nfrom the " + ProfileView.DisplayName + " profile to the " + appProfile2.DisplayName + " profile.", "Confirm Copy Settings Action"))
		{
			return;
		}
		TraceLog.Info("Copy " + text + " settings from " + ProfileView.DisplayName + " to " + appProfile2.DisplayName);
		switch (((Selector)(object)tabSections).SelectedIndex)
		{
		case 0:
			cmdCopyDisplay(appProfile2);
			break;
		case 1:
			cmdCopyGraphics(appProfile2);
			break;
		case 2:
			cmdCopyReplay(appProfile2);
			break;
		case 3:
			cmdCopyExtra(appProfile2);
			break;
		case 4:
			cmdCopyFFB(appProfile2);
			break;
		case 5:
			cmdCopyAutoChat(appProfile2);
			break;
		case 6:
			cmdCopySound(appProfile2);
			break;
		case 7:
			cmdCopySpotter(appProfile2);
			break;
		case 8:
			cmdCopyNotes(appProfile2);
			break;
		}
		appProfile2.IsModified = true;
		try
		{
			iniWatchEnable(enable: false);
			appProfile2.Save();
		}
		catch (Exception ex)
		{
			TraceLog.Exception(ex, "Exception encountered saving profile " + appProfile2.DisplayName + " due to " + text + " settings being copied");
		}
		finally
		{
			iniWatchEnable(enable: true);
		}
	}

	private void cmdCopyDisplay(appProfile ProfileTarget)
	{
		if (ProfileView.IsDisplayModeVR && ProfileView.DisplayMode == ProfileTarget.DisplayMode)
		{
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_VRMode);
			if (ProfileView.IsOculus)
			{
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OculusEnabled);
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.Oculus_FullyWaitForSync);
			}
			if (ProfileView.IsOpenVR)
			{
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OpenVREnabled);
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OpenVR_FullyWaitForSync);
			}
			if (ProfileView.IsOpenVR)
			{
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OpenXREnabled);
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OpenXRFoveatedOuterPctRes);
				CopyTagValue(ProfileView, ProfileTarget, iniDX11.OpenXRFoveatedInsetWidthPct);
			}
		}
		if (ProfileView.IsDisplayModeMonitor && ProfileView.DisplayMode == ProfileTarget.DisplayMode)
		{
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.MonitorType);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.RadiusOfCurvature);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.MonitorWidth);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.ScreenWidth);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.ViewingDist);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.ScreenAngles);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.RenderViewPerMonitor);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.EnableSMPSurround);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.NumMonitors);
			CopyTagValue(ProfileView, ProfileTarget, iniDX11.BezelProtectionPct);
		}
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.AutoCfgCompleted);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.drivingCamFOV);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.HDRFormat);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.RefreshRate);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.deviceIdx);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.fullScreen);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.fullScreenDepth);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.fullScreenWidth);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.fullScreenHeight);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.border);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedWidth);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedHeight);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedXPos);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedYPos);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedAlignment);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.windowedMaximized);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_FSRSharpness);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_NvReflexMode);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_NumMultiGPUs);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_UIScalePct);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_GammaAdj);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ContrastAdj);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_BrightnessAdj);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ResolutionScaling);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.BezelProtectionPct);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.UIOffsetBottomPct);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.DriveUIFullScreen);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.DriveUITransparency);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.SessionUIFullScreen);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.SessionUITransparency);
		ProfileTarget.CarCameras.CloneFrom(ProfileView.CarCameras);
		DataLoadTabDisplay();
	}

	private void cmdCopyGraphics(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniApp.serverTransmitMaxCars);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.virtualMirrorFOV);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.VirtualMirrorSize);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.hideCarNum);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_EnableGPUParticles);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SkyRefreshRate);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CarDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_PitObjectDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_WeekendDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_GrandstandDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CrowdDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ObjectDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_FoliageDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ParticleDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ParticlesFullRes);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxCarsToDraw);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxCarsToDrawInMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxPitObjsToDraw);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxPitObjsToDrawInMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MipLODBias);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LODPctMax);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LODPctMin);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LODPctDynoMax);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LODPctDynoMin);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LODMinFPSTarget);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DesiredFPSLimit);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LimitFrameRate);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_AntiAliasMethod);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MSAASamples);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MSAAUseFilter);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ShadowMapType);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_AllowTSOSelfShadows);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DynamicShadowMaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DNSMEnable);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DNSMWallsCastShadows);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DNSMNumLights);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DNSMFilter);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_NumDynamicCubemaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_NumFixedCubemaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ShaderQuality);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_HideCockpitObstructions);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DynamicShadowRes);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_StaticShadowRes);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_StaticShadowCount);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxPrerenderedFrames);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_VisibilityFrameDelay);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SteeringWheel);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_DriverHands);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_TwoPassTrees);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LowQualityTrees);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MaxCockpitMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MirrorDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_HeadlightLevel);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_HeadlightsInMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_VirtualMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MotionBlurStrength);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_HeatHaze);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SSAO);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_FXAAQualitySubPix);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_FXAAQualityEdgeThreshold);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SSRRainOnly);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SSRLevel);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_Sharpening);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_Distortion);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_EnableHDR);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_AutoExposure);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CacheSwap3HighResCars);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CarPaint2048x2048);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_MonochromeHeadlights);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ZBuffer32Bits);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_TrackDisplacementEnable);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_ParallelSorting);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_VidMemToUseMB);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_SysMemToUseMB);
		DataLoadTabGraphics();
	}

	private void cmdCopyReplay(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_SkyRefreshRate);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_CarDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_PitObjectDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_WeekendDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_GrandstandDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_CrowdDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ObjectDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_FoliageDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ParticleDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ParticlesFullRes);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ShadowMapType);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_AllowTSOSelfShadows);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DynamicShadowMaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DynamicShadowFilters);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DNSMEnable);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DNSMWallsCastShadows);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DNSMNumLights);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DNSMFilter);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_NumDynamicCubemaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_NumFixedCubemaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_HideCockpitObstructions);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_TwoPassTrees);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MaxCockpitMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MirrorDetail);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MotionBlurStrength);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MotionBlurDrivingCams);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MotionBlurBroadcastCams);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_HeatHaze);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_SSAO);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_FXAAQualitySubPix);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_FXAAQualityEdgeThreshold);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_SSRRainOnly);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_SSRLevel);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_Sharpening);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_Distortion);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_DepthOfField);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ReplayRenderModes);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_ShaderQuality);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_SteeringWheel);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MaxCarsToDraw);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MaxCarsToDrawInMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MaxPitObjsToDraw);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_MaxPitObjsToDrawInMirrors);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_LODPctMax);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_LODPctMin);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_LODPctDynoMax);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_LODPctDynoMin);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Replay_LODMinFPSTarget);
		DataLoadTabReplay();
	}

	private void cmdCopyExtra(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniApp.EnableTicker);
		CopyTagValue(ProfileView, ProfileTarget, iniCore.connect_sockets);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.askToSaveOnQuit);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.irsdkEnableMem);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.irsdkAutoLogDisk);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.autoResetFastRepair);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.autoResetPitBox);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.autoFuelDefaultEnable);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.autoFuelDefaultMarginLaps);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.forceCrowdVisible);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.ForceVisibleWhenMove);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.showUserMessagesWhileDriving);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.showSysMessagesWhileDriving);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.showIncidentMessagesWhileDriving);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.showJoinLeave);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_LoadTexturesWhenDriving);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.blackBox);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.blackBoxPitStop);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.disableAtRaceStart);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.fadeGhostCarWhenClose);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.ghostCarOffsetSec);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.ghostCarTransp);
		CopyTagValue(ProfileView, ProfileTarget, iniCore.max_num_default_worker_threads);
		CopyTagValue(ProfileView, ProfileTarget, iniCore.num_processors_to_use_for_new_damage);
		CopyTagValue(ProfileView, ProfileTarget, iniCore.customTestSessionStallLocation);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.drivingCamFOV);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.driverHeightAdj);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.DrivingVanishY);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.cockpitLookInstant);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.cockpitLookAngle);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.cockpitLookUpAngle);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.cockpitLookDownAngle);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CompressedVertices);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CompressTexturesCars);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CompressTexturesSuits);
		CopyTagValue(ProfileView, ProfileTarget, iniDX11.Graphic_CompressTexturesHelmets);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.PauseReplayOnExit);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.vidCaptureEnable);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.screenshotFileFormat);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.xWidth);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.xHeight);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.videoFileFrmt);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.videoFramerate);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.videoImgSize);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.highContrastCursor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.pitLineAlwaysVisible);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.raceLineWidth);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.pitlineColor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.racelineFastColor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.racelineSameColor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.racelineSlowColor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.playSecondsFromReplayEndOnCarExitNonTeam);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.playSecondsFromReplayEndOnCarExitTeamEvt);
		DataLoadTabExtras();
	}

	private void cmdCopyFFB(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadTrueForceAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.enableTrueForceVibe);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.trueForceVibePhysics);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.trueForceDamper);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volTrueForcePhysMaster);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysCarBodyAccel_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volTrueForcePhysDriveShaft);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysEngineRPM_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysGearChange_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRevLimit_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRoadTextures_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRumbleStrip_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volTrueForcePhysWheelSlip);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadAsetekAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadConspitAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadFanatecAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadMozaAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadSimagicAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadSimucubeAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loadVRSAPI);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.enableFFB360HzInterpolated);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.EnableWheelDisplay);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.enableWheelDisplayBlink);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.autoForceFactor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.smoothingFilterType);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.clutchLaunchMode);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.VibratePedal);
		DataLoadTabFFB();
	}

	private void cmdCopyAutoChat(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniApp.radioScriptsEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr1);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr2);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr3);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr4);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr5);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr6);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr7);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr8);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr9);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr10);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr11);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr12);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr13);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr14);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.AutoChatStr15);
		DataLoadTabAutoChat();
	}

	private void cmdCopySound(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devLFEId");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devLFEAPI");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devLFEDefault");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devLFEName");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devSpeakerId");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devSPCCAPI");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devSpeakerAPI");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devSpeakerDefault");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devSpeakerName");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devMicrophoneId");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devMicrophoneAPI");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devMicrophoneDefault");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devMicrophoneName");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devVoiceChatId");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devVoiceChatAPI");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devVoiceChatDefault");
		CopyTagValue(ProfileView, ProfileTarget, "0.Audio.devVoiceChatName");
		CopyTagValue(ProfileView, ProfileTarget, iniApp.dimensions);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.voiceChatNotificationStyle);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.ambientMusicDisabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.rotateWithHeadset);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.masterVolumedB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessEngine);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessTires);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessCrash);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessWind);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessRain);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessIncar);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessAmbient);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessSPCC);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessVoiceChat);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessReplay);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.earProtection);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.compressorReplay);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.voiceChatEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.voiceChatMuted);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.voiceChatEnabledWhileDriving);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.downshiftProtectionAlert);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.LFEEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.enableLFEBKAmpCut);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.loudnessLFE);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volGameMaster_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysCarBodyAccel_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysEngineRPM_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysGearChange_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRevLimit_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRumbleStrip_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysWheelSlip_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.volPhysRoadTextures_dB);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.freqGameLowpass_Hz);
		DataLoadTabSound();
	}

	private void cmdCopySpotter(appProfile ProfileTarget)
	{
		CopyTagValue(ProfileView, ProfileTarget, iniApp.devSPCCId);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.devSPCCName);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.enabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.muteSpotterIfLive);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.ShowSpotterUIForSpectators);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.carLowHiAtStart);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.carLowHiPadding);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.display);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.voicePack);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.verbosity);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reduceVerbosityIfLive);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsMode_n);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsPrecision);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsMinute);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.textDurationFactor);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.hushDuration);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportFuelData);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportNewLeaderEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsLeaderLapTimeEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportGainingLosingEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportFasterCarBehindEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportPitboxCount);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportCompetitorPitEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsNewBestEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsNewBestLapTimeEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsNewPersonalBestEnabled);
		CopyTagValue(ProfileView, ProfileTarget, iniApp.reportLapsNewPersonalBestInRaceEnabled);
		DataLoadTabSpotter();
	}

	private void cmdCopyNotes(appProfile ProfileTarget)
	{
		DataSaveNotes();
		List<string> notes = new List<string>(ProfileView.Notes);
		ProfileTarget.Notes = notes;
		ProfileTarget.IsModified = true;
		DataLoadNotes();
	}

	private void CopyTagValue(appProfile source, appProfile target, string tag)
	{
		if (tag.IsNullOrEmpty())
		{
			return;
		}
		string[] array = tag.Split('.');
		if (array.Length != 3)
		{
			return;
		}
		appConfig appConfig2;
		appConfig appConfig3;
		switch (array[0])
		{
		default:
			return;
		case "0":
			appConfig2 = source.irApp;
			appConfig3 = target.irApp;
			break;
		case "1":
			appConfig2 = source.irCore;
			appConfig3 = target.irCore;
			break;
		case "2":
			appConfig2 = source.irDX11;
			appConfig3 = target.irDX11;
			break;
		}
		string value;
		try
		{
			value = appConfig2.tagGetValue(array[1], array[2]);
		}
		catch
		{
			return;
		}
		try
		{
			appConfig3.ensureSection(array[1]).ensureSetting(array[2], value);
			appConfig3.tagSetValue(array[1], array[2], value);
		}
		catch
		{
		}
	}

	private void CopyTagValue(appProfile source, appProfile target, iniSetting setting)
	{
		if (setting == null || setting.Name.IsNullOrEmpty())
		{
			return;
		}
		appConfig appConfig2;
		appConfig appConfig3;
		switch (setting.Config)
		{
		default:
			return;
		case iniConfig.App:
			appConfig2 = source.irApp;
			appConfig3 = target.irApp;
			break;
		case iniConfig.Core:
			appConfig2 = source.irCore;
			appConfig3 = target.irCore;
			break;
		case iniConfig.DX11:
			appConfig2 = source.irDX11;
			appConfig3 = target.irDX11;
			break;
		}
		string value;
		try
		{
			value = appConfig2.tagGetValue(setting.Section, setting.Name);
		}
		catch
		{
			return;
		}
		try
		{
			appConfig3.ensureSection(setting.Section).ensureSetting(setting.Name, value, setting.Comment);
			appConfig3.tagSetValue(setting.Section, setting.Name, value);
		}
		catch
		{
		}
	}

	public void loadControlValues()
	{
		if (IsClosing)
		{
			return;
		}
		try
		{
			DataLoading = true;
			try
			{
				DataLoadTabDisplay();
			}
			catch (Exception ex)
			{
				TraceLog.Exception(ex, "loadControlVales.DataLoadTabDisplay method encountered an exception");
				MessageBox.Show(ex.Message, "Error loading display tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabGraphics();
			}
			catch (Exception ex2)
			{
				TraceLog.Exception(ex2, "loadControlVales.DataLoadTabGraphics method encountered an exception");
				MessageBox.Show(ex2.Message, "Error loading graphics tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabReplay();
			}
			catch (Exception ex3)
			{
				TraceLog.Exception(ex3, "loadControlVales.DataLoadTabReplay method encountered an exception");
				MessageBox.Show(ex3.Message, "Error loading replay tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabExtras();
			}
			catch (Exception ex4)
			{
				TraceLog.Exception(ex4, "loadControlVales.DataLoadTabExtras method encountered an exception");
				MessageBox.Show(ex4.Message, "Error loading extras tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabFFB();
			}
			catch (Exception ex5)
			{
				TraceLog.Exception(ex5, "loadControlVales.DataLoadTabFFB method encountered an exception");
				MessageBox.Show(ex5.Message, "Error loading FFB tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabAutoChat();
			}
			catch (Exception ex6)
			{
				TraceLog.Exception(ex6, "loadControlVales.DataLoadTabAutoChat method encountered an exception");
				MessageBox.Show(ex6.Message, "Error loading autochat tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabSound();
			}
			catch (Exception ex7)
			{
				TraceLog.Exception(ex7, "loadControlVales.DataLoadTabSound method encountered an exception");
				MessageBox.Show(ex7.Message, "Error loading sound tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadTabSpotter();
			}
			catch (Exception ex8)
			{
				TraceLog.Exception(ex8, "loadControlVales.DataLoadTabSpotter method encountered an exception");
				MessageBox.Show(ex8.Message, "Error loading spotter tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			try
			{
				DataLoadNotes();
			}
			catch (Exception ex9)
			{
				TraceLog.Exception(ex9, "loadControlVales.DataLoadNotes method encountered an exception");
				MessageBox.Show(ex9.Message, "Error loading notes tab", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		finally
		{
			DataLoading = false;
		}
	}

	public void loadLabel(Label lbl, clsRainbow bow)
	{
		object content = (lbl.ToolTip = ProfileView.tagGetValue(lbl.GetTag()));
		lbl.Content = content;
		bow?.tagCompare();
	}

	private void loadDecimal(NumericUpDown num, clsRainbow bow, string tagLoad = null)
	{
		string tag = num.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			num.Value = ProfileView.tagGetValue(tag).ToDouble().Round2();
			bow?.tagCompare();
		}
	}

	private void loadNumber(NumericUpDown num, clsRainbow bow, string tagLoad = null)
	{
		string tag = num.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			num.Value = ProfileView.tagGetValue(tag).ToInt();
			bow?.tagCompare();
		}
	}

	private void loadTextbox(TextBox txt, clsRainbow bow, string tagLoad = null)
	{
		string tag = txt.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			txt.Text = ProfileView.tagGetValue(tag);
			bow?.tagCompare();
		}
	}

	private void loadCheckbox(CheckBox cb, clsRainbow bow, string tagLoad = null)
	{
		string tag = cb.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			cb.IsChecked = ProfileView.tagGetValue(tag).ToBool();
			bow?.tagCompare();
		}
	}

	private void loadColorPicker(ColorPicker picker, clsRainbow bow, string tagLoad = null)
	{
		string tag = picker.GetTag();
		if (tag.tagIgnore(tagLoad))
		{
			return;
		}
		string text = ProfileView.tagGetValue(tag).Replace("#", "#FF");
		if (!text.IsNullOrEmpty())
		{
			try
			{
				picker.SelectedColor = (Color)ColorConverter.ConvertFromString(text);
			}
			catch
			{
				return;
			}
			bow?.tagCompare();
		}
	}

	private void loadComboValue(ComboBox cbo, clsRainbow bow, string tagLoad = null)
	{
		string tag = cbo.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			cbo.SelectedValue = ProfileView.tagGetValue(tag);
			bow?.tagCompare();
		}
	}

	private void loadComboIndex(ComboBox cbo, clsRainbow bow, string tagLoad = null)
	{
		string tag = cbo.GetTag();
		if (!tag.tagIgnore(tagLoad))
		{
			int num = ProfileView.tagGetValue(tag).ToInt();
			int num2 = 0;
			if (iniApp.blackBox.TagID.Equals(tag) || iniApp.blackBoxPitStop.TagID.Equals(tag) || iniDX11.Graphic_HeadlightLevel.TagID.Equals(tag))
			{
				num2 = 1;
			}
			if (iniApp.dimensions.TagID.Equals(tag))
			{
				num2 = -1;
			}
			cbo.SelectedIndex = num + num2;
			bow?.tagCompare();
		}
	}

	private void loadSlider(Slider slider, clsRainbow bow, string tagLoad = null)
	{
		string tag = slider.GetTag();
		if (!tag.IsNullOrEmpty() && !tag.tagIgnore(tagLoad))
		{
			slider.Value = ProfileView.tagGetValue(tag).ToDouble();
			bow?.tagCompare();
		}
	}

	private void loadSlider(XSlider slider, clsRainbow bow, string tagLoad = null)
	{
		string tag = slider.GetTag();
		if (!tag.IsNullOrEmpty() && !tag.tagIgnore(tagLoad))
		{
			slider.Value = ProfileView.tagGetValue(tag).ToDouble();
			bow?.tagCompare();
		}
	}

	private void loadSlider(YSlider slider, clsRainbow bow, string tagLoad = null)
	{
		string tag = slider.GetTag();
		if (!tag.IsNullOrEmpty() && !tag.tagIgnore(tagLoad))
		{
			slider.Value = ProfileView.tagGetValue(tag).ToDouble();
			bow?.tagCompare();
		}
	}

	public void InitGFXWizard()
	{
		MetroTabItem metroTabItem = panelProfiles;
		MetroTabItem metroTabItem2 = panelGFXWizard;
		Visibility visibility = (panelSearch.Visibility = Visibility.Hidden);
		Visibility visibility3 = (metroTabItem2.Visibility = visibility);
		metroTabItem.Visibility = visibility3;
		gfxConfig.GPUList.LoadCombo(cboGPUCard);
	}

	public void HideGFXWizard()
	{
		panelProfiles.IsSelected = true;
		ToolBar toolBar = barProfiles;
		bool flag = (barGFXWizard.IsEnabled = false);
		toolBar.IsEnabled = !flag;
	}

	public void ShowGFXWizard()
	{
		itemsGFXWizard.Items.Clear();
		panelGFXWizard.IsSelected = true;
		ToolBar toolBar = barProfiles;
		bool flag = (barGFXWizard.IsEnabled = true);
		toolBar.IsEnabled = !flag;
		Label label = lblGFXPreset;
		Visibility visibility = (btnGFXPreset.Visibility = Visibility.Hidden);
		label.Visibility = visibility;
		string text = Ini.ReadKey("GPU", "Hardware");
		if (text.IsNullOrEmpty())
		{
			clsMonitorItem clsMonitorItem2 = MonitorList.ReferenceMonitor();
			if (clsMonitorItem2 != null && !clsMonitorItem2.GraphicCard.IsNullOrEmpty())
			{
				Match match = rxGPU.Match(clsMonitorItem2.GraphicCard);
				if (match.Success)
				{
					text = match.Value;
				}
			}
		}
		if (!text.IsNullOrEmpty())
		{
			if (text == (string)cboGPUCard.SelectedValue)
			{
				cboGPUCard_SelectionChanged(cboGPUCard, null);
			}
			else
			{
				gfxConfig.GPUList.SetComboIndex(cboGPUCard, text);
			}
		}
		MessageBox.Show(this, "This feature identifies settings that can improve FPS performance.\n\nSelect your GPU Card then accept or dismiss each proposed setting.", "GFX Wizard");
	}

	private void cboGPUCard_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		gfxGPUCard gfxGPUCard2 = gfxConfig.GPUList[cboGPUCard.SelectedIndex];
		Ini.WriteKey("GPU", gfxGPUCard2.Name, "Hardware");
		txtGFXCard.Text = gfxGPUCard2.Info;
		int monitorCount = ProfileView.tagGetValue(cboNumMonitors.GetTag()).ToInt();
		int monitorWidth = ProfileView.tagGetValue(iniDX11.windowedWidth.TagID).ToInt();
		int monitorHeight = ProfileView.tagGetValue(iniDX11.windowedHeight.TagID).ToInt();
		gfxResolution gfxResolution2 = gfxConfig.WhichResolution(monitorCount, monitorWidth, monitorHeight);
		gfxPreset gfxPreset2 = gfxGPUCard2.Presets[(int)gfxResolution2];
		btnGFXPreset.Tag = gfxPreset2;
		switch (gfxPreset2)
		{
		case gfxPreset.Low:
			imgGFXPreset.ImageSource = "Icons/thumb-down.png".LoadImageResource();
			btnGFXPreset.ToolTip = "Click to apply GFX Preset LOW";
			break;
		case gfxPreset.Faster:
			imgGFXPreset.ImageSource = "Icons/speed-slow.png".LoadImageResource();
			btnGFXPreset.ToolTip = "Click to apply GFX Preset FASTER";
			break;
		default:
			imgGFXPreset.ImageSource = "Icons/speed-medium.png".LoadImageResource();
			btnGFXPreset.ToolTip = "Click to apply GFX Preset STANDARD";
			break;
		case gfxPreset.Pretty:
			imgGFXPreset.ImageSource = "Icons/speed-fast.png".LoadImageResource();
			btnGFXPreset.ToolTip = "Click to apply GFX Preset PRETTY";
			break;
		case gfxPreset.High:
			imgGFXPreset.ImageSource = "Icons/thumb-up.png".LoadImageResource();
			btnGFXPreset.ToolTip = "Click to apply GFX Preset HIGH";
			break;
		}
		Label label = lblGFXPreset;
		Visibility visibility = (btnGFXPreset.Visibility = Visibility.Visible);
		label.Visibility = visibility;
		DoWizardMagic(gfxGPUCard2);
	}

	private void btnCloseWizard_Click(object sender, RoutedEventArgs e)
	{
		HideGFXWizard();
	}

	private void btnGFXPreset_Click(object sender, RoutedEventArgs e)
	{
		ConfigMem();
		ProfileView.irDX11.allSetGFXPreset((gfxPreset)btnGFXPreset.Tag);
		loadControlValues();
		HideGFXWizard();
	}

	private void btnDismiss_Click(object sender, RoutedEventArgs e)
	{
		gfxSetting removeItem = (e.Source as Button).DataContext as gfxSetting;
		itemsGFXWizard.Items.Remove(removeItem);
		if (itemsGFXWizard.Items.Count == 0)
		{
			HideGFXWizard();
		}
	}

	private void btnApply_Click(object sender, RoutedEventArgs e)
	{
		gfxSetting gfxSetting2 = (e.Source as Button).DataContext as gfxSetting;
		ProfileView.tagSetValue(gfxSetting2.Tag, gfxSetting2.Value);
		if (gfxSetting2.Tag.Contains("Compress"))
		{
			DataLoadTabExtras();
		}
		else
		{
			DataLoadTabGraphics(gfxSetting2.Tag);
		}
		itemsGFXWizard.Items.Remove(gfxSetting2);
		if (itemsGFXWizard.Items.Count == 0)
		{
			HideGFXWizard();
		}
	}

	private void DoWizardMagic(gfxGPUCard gpuCard)
	{
		itemsGFXWizard.Items.Clear();
		gfxPreset preset = (gfxPreset)btnGFXPreset.Tag;
		foreach (string item in gfxSetting.tagSettings())
		{
			string value = ProfileView.tagGetValue(item);
			string text = ProfileView.tagGetGFXPreset(preset, item);
			iniSetting obj = iniDX11.Find(item);
			if (obj == iniDX11.Graphic_VidMemToUseMB)
			{
				text = CalcGPUMem(gpuCard.gbMem);
			}
			if (obj == iniDX11.Graphic_SysMemToUseMB)
			{
				text = CalcSYSMem();
			}
			if (gfxSetting.tagValueGTPreset(item, value, text))
			{
				string show = ProfileView.tagGetDisplay(item, text);
				itemsGFXWizard.Items.Add(new gfxSetting(item, text, show));
			}
		}
	}

	public void HideSettingSearch()
	{
		panelProfiles.IsSelected = true;
		barProfiles.IsEnabled = true;
	}

	public void ShowSettingSearch()
	{
		panelSearch.IsSelected = true;
		barProfiles.IsEnabled = false;
		if (lstControls.DataContext == null)
		{
			lstControls.DataContext = clsControlGlobal.Search("");
		}
		Application.Current.Dispatcher.BeginInvoke((Action)delegate
		{
			txtSearch.Focus();
		}, DispatcherPriority.Render);
	}

	private void btnCloseSearch_Click(object sender, RoutedEventArgs e)
	{
		HideSettingSearch();
	}

	private void btnSearch_Click(object sender, RoutedEventArgs e)
	{
		lstControls.DataContext = clsControlGlobal.Search(txtSearch.Text);
	}

	private void txtSearch_KeyDown(object sender, KeyEventArgs e)
	{
		if (!txtSearch.Text.IsNullOrEmpty() && e.Key == Key.Return)
		{
			e.Handled = true;
			lstControls.DataContext = clsControlGlobal.Search(txtSearch.Text);
		}
	}

	private void lstControls_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (lstControls.SelectedItems.Count != 0 && (lstControls.SelectedItem as clsControlItem).Highlight())
		{
			lstControls.SelectedItem = null;
		}
	}

	public winMain()
	{
		InitializeComponent();
	}

	private void winClosed(object sender, EventArgs e)
	{
		doSave(warn: true);
		this.IniSave();
		IsClosing = true;
	}

	private void winLoaded(object sender, RoutedEventArgs e)
	{
		iRacing.Validate();
		this.IniLoad();
		this.AddTitleVersion();
	}

	public void winContentRendered(object sender, EventArgs e)
	{
		InitGFXWizard();
		InitTabDisplay();
		InitTabGraphics();
		InitTabReplay();
		InitTabExtras();
		InitTabFFB();
		InitTabAutoChat();
		InitTabSound();
		InitTabSpotter();
		MonitorList.CreateMonitorScreens(this, gridMonitors, WpfScreen.AllScreens);
		string[] iRacingSpotters = appExtensions.getIRacingSpotters();
		foreach (string newItem in iRacingSpotters)
		{
			cboSpotVoice.Items.Add(newItem);
		}
		TraceLog.Info("iRacing folder location: " + iRacing.pathRoot());
		OneDrive oneDrive = new OneDrive();
		TraceLog.Info($"OneDrive found: {oneDrive.IsValid}");
		LoadProfiles();
		appHistory.GetHistory(appHistoryLoaded);
		OptionsContainer optionsContainer = new OptionsContainer();
		try
		{
			if (!MemberID.IsNullOrEmpty() && !Options.IsNullOrEmpty())
			{
				optionsContainer.FromString(Letter.Open(Options));
				if (!optionsContainer.Number.Contains(MemberID))
				{
					optionsContainer = new OptionsContainer();
				}
			}
		}
		catch
		{
		}
		base.Tag = optionsContainer;
		iniWatchInit();
		appUpdate.Check4Update(SoftwareUpdateNotify);
	}

	private void SoftwareUpdateNotify()
	{
		appCommands.InvalidateRequerySuggested();
	}

	public void LoadProfiles()
	{
		try
		{
			ProfileList.Discover();
			ProfileList.Load();
			ProfileList.CreateBackupIfNone();
			addProfileControls();
		}
		catch (Exception ex)
		{
			TraceLog.Error("Loading ini files failed: " + ex.Message);
			TraceLog.Error(ex.StackTrace);
		}
		if (ProfileList.Monitor.irDX11.Invalid)
		{
			throw new Exception("The rendererDX11Monitor.ini file is missing critical settings, please run the iRacing Graphic Config");
		}
	}

	private void addProfileControls()
	{
		((ItemsControl)(object)tabProfiles).Items.Clear();
		cboTargetProfile.Items.Clear();
		gridValues.Children.Clear();
		addProfileTab(ProfileList.Backup);
		addProfileTab(ProfileList.Monitor);
		foreach (KeyValuePair<string, appProfile> profile in ProfileList)
		{
			if (!profile.Value.IsBackup && !profile.Value.IsMonitor)
			{
				addProfileTab(profile.Value);
			}
		}
		cboTargetProfile.SelectedIndex = 0;
		((Selector)(object)tabSections).SelectionChanged += tabSections_Change;
		((Selector)(object)tabSections).SelectedIndex = 0;
		((Selector)(object)tabProfiles).SelectionChanged += tabProfiles_Change;
		for (int i = 1; i < ((ItemsControl)(object)tabProfiles).Items.Count; i++)
		{
			if ((((ItemsControl)(object)tabProfiles).Items[i] as MetroTabItem).Visibility == Visibility.Visible)
			{
				((Selector)(object)tabProfiles).SelectedIndex = i;
				break;
			}
		}
	}

	private void addProfileTab(appProfile profile)
	{
		if (((ItemsControl)(object)tabProfiles).Items.Count >= 16)
		{
			return;
		}
		profile.Tab.Width = ((FrameworkElement)(object)tabProfiles).Width;
		((ItemsControl)(object)tabProfiles).Items.Add(profile.Tab);
		if (profile.IsIRacingVR)
		{
			if (profile.IsOculus)
			{
				profile.Tab.ContextMenu = FindResource("OculusContextMenu") as ContextMenu;
			}
			else if (profile.IsOpenVR)
			{
				profile.Tab.ContextMenu = FindResource("OpenVRContextMenu") as ContextMenu;
			}
			else if (profile.IsOpenXR)
			{
				profile.Tab.ContextMenu = FindResource("OpenXRContextMenu") as ContextMenu;
			}
		}
		Grid.SetRow(profile.Lab, gridValues.Children.Count);
		Grid.SetColumn(profile.Lab, 0);
		gridValues.Children.Add(profile.Lab);
		int num;
		if ((ShowBackupProfile || !profile.IsBackup) && (ShowMonitorProfile || !profile.IsMonitor) && (ShowOculusProfile || !profile.Name.Equals("Oculus")) && (ShowOpenVRProfile || !profile.Name.Equals("OpenVR")))
		{
			if (!ShowOpenXRProfile)
			{
				num = (profile.Name.Equals("OpenXR") ? 1 : 0);
				if (num != 0)
				{
					goto IL_017f;
				}
			}
			else
			{
				num = 0;
			}
			goto IL_0197;
		}
		num = 1;
		goto IL_017f;
		IL_0197:
		if (num == 0 && !profile.IsBackup)
		{
			cboTargetProfile.Items.Add(profile.Cbo);
		}
		return;
		IL_017f:
		profile.Tab.Visibility = Visibility.Hidden;
		profile.Lab.Visibility = Visibility.Hidden;
		goto IL_0197;
	}

	private void tabProfiles_Change(object sender, SelectionChangedEventArgs args)
	{
		DataSaveNotes();
		if ((sender as TabControl).SelectedItem is MetroTabItem metroTabItem)
		{
			ProfileView = ProfileList[metroTabItem.Tag as string];
			if (((Selector)(object)tabSections).SelectedIndex == 0 && ProfileView.DisplayMode == iniDisplayMode.Monitor && ((Selector)(object)tabMonitorDisplay).SelectedIndex == 2)
			{
				((Selector)(object)tabMonitorDisplay).SelectedIndex = 0;
			}
			loadControlValues();
		}
	}

	private void tabSections_Change(object sender, SelectionChangedEventArgs args)
	{
		if (!tabGraphics.IsSelected && panelGFXWizard.IsSelected)
		{
			HideGFXWizard();
		}
		TabItem tabItem = (sender as TabControl).SelectedItem as TabItem;
		lblCopySettings.Content = "Copy " + tabItem.Header.ToString().Replace("| ", "") + " settings to";
	}

	private void tabProfiles_DragOver(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return;
		}
		string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].EndsWith(".zip"))
			{
				e.Effects = DragDropEffects.Link;
				e.Handled = true;
				break;
			}
		}
	}

	private void tabProfiles_Drop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return;
		}
		string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
		foreach (string text in array)
		{
			if (text.EndsWith(".zip", StringComparison.CurrentCultureIgnoreCase))
			{
				fileImport(text);
				break;
			}
		}
	}

	public bool ConfirmAction(string Msg, string Title)
	{
		if (!ConfirmActions)
		{
			return true;
		}
		return MessageBox.Show(Msg + "\n\nDo you want to proceed?", Title, MessageBoxButton.OKCancel) == MessageBoxResult.OK;
	}

	public void appHistoryLoaded()
	{
	}

	private void Oculus_Apply(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("Oculus app settings applied to app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile appProfile2 = ProfileList["Oculus"];
		string value = monitor.irApp.tagGetValue("SplitsDeltas", "comparisonLapFileName");
		monitor.irApp.CloneFrom(appProfile2.irApp);
		monitor.irApp.tagSetValue("SplitsDeltas", "comparisonLapFileName", value);
		monitor.IsModified = true;
		CopyTagValue(appProfile2, monitor, iniDX11.Graphic_VRMode);
		try
		{
			iniWatchEnable(enable: false);
			monitor.Save();
		}
		finally
		{
			iniWatchEnable(enable: true);
		}
		appProfile2.CarCameras.Apply();
		if (ProfileView.IsMonitor)
		{
			loadControlValues();
		}
	}

	private void Oculus_Update(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("Oculus app settings updated from app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile obj = ProfileList["Oculus"];
		obj.irApp.CloneFrom(monitor.irApp);
		obj.IsModified = true;
		obj.CarCameras.CloneFrom(monitor.CarCameras);
		if (ProfileView.IsOculus)
		{
			loadControlValues();
		}
	}

	private void OpenVR_Apply(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("OpenVR app settings applied to app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile appProfile2 = ProfileList["OpenVR"];
		string value = monitor.irApp.tagGetValue("SplitsDeltas", "comparisonLapFileName");
		monitor.irApp.CloneFrom(appProfile2.irApp);
		monitor.irApp.tagSetValue("SplitsDeltas", "comparisonLapFileName", value);
		monitor.IsModified = true;
		CopyTagValue(appProfile2, monitor, iniDX11.Graphic_VRMode);
		try
		{
			iniWatchEnable(enable: false);
			monitor.Save();
		}
		finally
		{
			iniWatchEnable(enable: false);
		}
		appProfile2.CarCameras.Apply();
		if (ProfileView.IsMonitor)
		{
			loadControlValues();
		}
	}

	private void OpenVR_Update(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("OpenVR app settings updated from app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile obj = ProfileList["OpenVR"];
		obj.irApp.CloneFrom(monitor.irApp);
		obj.IsModified = true;
		obj.CarCameras.CloneFrom(monitor.CarCameras);
		if (ProfileView.IsOpenVR)
		{
			loadControlValues();
		}
	}

	private void OpenXR_Apply(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("OpenXR app settings applied to app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile appProfile2 = ProfileList["OpenXR"];
		string value = monitor.irApp.tagGetValue("SplitsDeltas", "comparisonLapFileName");
		monitor.irApp.CloneFrom(appProfile2.irApp);
		monitor.irApp.tagSetValue("SplitsDeltas", "comparisonLapFileName", value);
		monitor.IsModified = true;
		CopyTagValue(appProfile2, monitor, iniDX11.Graphic_VRMode);
		try
		{
			iniWatchEnable(enable: false);
			monitor.Save();
		}
		finally
		{
			iniWatchEnable(enable: true);
		}
		appProfile2.CarCameras.Apply();
		if (ProfileView.IsMonitor)
		{
			loadControlValues();
		}
	}

	private void OpenXR_Update(object sender, RoutedEventArgs e)
	{
		TraceLog.Info("OpenXR app settings updated from app.ini");
		appProfile monitor = ProfileList.Monitor;
		appProfile obj = ProfileList["OpenXR"];
		obj.irApp.CloneFrom(monitor.irApp);
		obj.IsModified = true;
		obj.CarCameras.CloneFrom(monitor.CarCameras);
		if (ProfileView.IsOpenXR)
		{
			loadControlValues();
		}
	}

	private void Configure_Monitor(object sender, RoutedEventArgs e)
	{
		if (ProfileView != null && !ProfileView.IsBackup && !ProfileView.IsMonitor && !ProfileView.IsIRacingVR)
		{
			ProfileView.DisplayMode = iniDisplayMode.Monitor;
			txtDisplayMode.Text = ProfileView.DisplayMode.ToString();
		}
	}

	private void Configure_Oculus(object sender, RoutedEventArgs e)
	{
		if (ProfileView != null && !ProfileView.IsBackup && !ProfileView.IsMonitor && !ProfileView.IsIRacingVR)
		{
			ProfileView.DisplayMode = iniDisplayMode.Oculus;
			txtDisplayMode.Text = ProfileView.DisplayMode.ToString();
		}
	}

	private void Configure_OpenVR(object sender, RoutedEventArgs e)
	{
		if (ProfileView != null && !ProfileView.IsBackup && !ProfileView.IsMonitor && !ProfileView.IsIRacingVR)
		{
			ProfileView.DisplayMode = iniDisplayMode.OpenVR;
			txtDisplayMode.Text = ProfileView.DisplayMode.ToString();
		}
	}

	private void Configure_OpenXR(object sender, RoutedEventArgs e)
	{
		if (ProfileView != null && !ProfileView.IsBackup && !ProfileView.IsMonitor && !ProfileView.IsIRacingVR)
		{
			ProfileView.DisplayMode = iniDisplayMode.OpenXR;
			txtDisplayMode.Text = ProfileView.DisplayMode.ToString();
		}
	}

	private void DisplayMode_ContextMenuOpening(object sender, ContextMenuEventArgs e)
	{
		ContextMenu contextMenu = (sender as TextBlock).ContextMenu;
		bool isEnabled = ProfileView != null && !ProfileView.IsBackup && !ProfileView.IsMonitor && !ProfileView.IsIRacingVR;
		(contextMenu.Items[0] as MenuItem).IsEnabled = isEnabled;
		(contextMenu.Items[1] as MenuItem).IsEnabled = isEnabled;
		(contextMenu.Items[2] as MenuItem).IsEnabled = isEnabled;
		(contextMenu.Items[3] as MenuItem).IsEnabled = isEnabled;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/winmain.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((winMain)target).Loaded += winLoaded;
			((winMain)target).Closed += winClosed;
			((winMain)target).ContentRendered += winContentRendered;
			break;
		case 2:
			((MenuItem)target).Click += Oculus_Apply;
			break;
		case 3:
			((MenuItem)target).Click += Oculus_Update;
			break;
		case 4:
			((MenuItem)target).Click += OpenVR_Apply;
			break;
		case 5:
			((MenuItem)target).Click += OpenVR_Update;
			break;
		case 6:
			((MenuItem)target).Click += OpenXR_Apply;
			break;
		case 7:
			((MenuItem)target).Click += OpenXR_Update;
			break;
		case 8:
			((CommandBinding)target).CanExecute += canBackup;
			((CommandBinding)target).Executed += cmdBackup;
			break;
		case 9:
			((CommandBinding)target).CanExecute += canRestore;
			((CommandBinding)target).Executed += cmdRestore;
			break;
		case 10:
			((CommandBinding)target).CanExecute += canHistory;
			((CommandBinding)target).Executed += cmdHistory;
			break;
		case 11:
			((CommandBinding)target).CanExecute += canSave;
			((CommandBinding)target).Executed += cmdSave;
			break;
		case 12:
			((CommandBinding)target).CanExecute += canRun;
			((CommandBinding)target).Executed += cmdRun;
			break;
		case 13:
			((CommandBinding)target).CanExecute += canUpdateFromIRacing;
			((CommandBinding)target).Executed += cmdUpdateFromIRacing;
			break;
		case 14:
			((CommandBinding)target).CanExecute += canApply;
			((CommandBinding)target).Executed += cmdApply;
			break;
		case 15:
			((CommandBinding)target).CanExecute += canCopy;
			((CommandBinding)target).Executed += cmdCopy;
			break;
		case 16:
			((CommandBinding)target).CanExecute += canRename;
			((CommandBinding)target).Executed += cmdRename;
			break;
		case 17:
			((CommandBinding)target).CanExecute += canDelete;
			((CommandBinding)target).Executed += cmdDelete;
			break;
		case 18:
			((CommandBinding)target).CanExecute += canImport;
			((CommandBinding)target).Executed += cmdImport;
			break;
		case 19:
			((CommandBinding)target).CanExecute += canExport;
			((CommandBinding)target).Executed += cmdExport;
			break;
		case 20:
			((CommandBinding)target).CanExecute += canSettings;
			((CommandBinding)target).Executed += cmdSettings;
			break;
		case 21:
			((CommandBinding)target).CanExecute += canUpdate;
			((CommandBinding)target).Executed += cmdUpdate;
			break;
		case 22:
			((CommandBinding)target).CanExecute += canDiscord;
			((CommandBinding)target).Executed += cmdDiscord;
			break;
		case 23:
			((CommandBinding)target).CanExecute += canForum;
			((CommandBinding)target).Executed += cmdForum;
			break;
		case 24:
			((CommandBinding)target).CanExecute += canHelp;
			((CommandBinding)target).Executed += cmdHelp;
			break;
		case 25:
			((CommandBinding)target).CanExecute += canClose;
			((CommandBinding)target).Executed += cmdClose;
			break;
		case 26:
			((CommandBinding)target).CanExecute += canGFXPreset;
			((CommandBinding)target).Executed += cmdGFXLow;
			break;
		case 27:
			((CommandBinding)target).CanExecute += canGFXPreset;
			((CommandBinding)target).Executed += cmdGFXFaster;
			break;
		case 28:
			((CommandBinding)target).CanExecute += canGFXPreset;
			((CommandBinding)target).Executed += cmdGFXStandard;
			break;
		case 29:
			((CommandBinding)target).CanExecute += canGFXPreset;
			((CommandBinding)target).Executed += cmdGFXPretty;
			break;
		case 30:
			((CommandBinding)target).CanExecute += canGFXPreset;
			((CommandBinding)target).Executed += cmdGFXHigh;
			break;
		case 31:
			((CommandBinding)target).CanExecute += canGFXSync;
			((CommandBinding)target).Executed += cmdGFXSync;
			break;
		case 32:
			((CommandBinding)target).CanExecute += canGFXWizard;
			((CommandBinding)target).Executed += cmdGFXWizard;
			break;
		case 33:
			((CommandBinding)target).CanExecute += canGFXVRR;
			((CommandBinding)target).Executed += cmdGFXVRR;
			break;
		case 34:
			((CommandBinding)target).CanExecute += canSettingSearch;
			((CommandBinding)target).Executed += cmdSettingSearch;
			break;
		case 35:
			((CommandBinding)target).CanExecute += canCopySettings;
			((CommandBinding)target).Executed += cmdCopySettings;
			break;
		case 36:
			lblCopySettings = (Label)target;
			break;
		case 37:
			cboTargetProfile = (ComboBox)target;
			break;
		case 38:
			txtDisplayMode = (TextBlock)target;
			txtDisplayMode.ContextMenuOpening += DisplayMode_ContextMenuOpening;
			break;
		case 39:
			((MenuItem)target).Click += Configure_Monitor;
			break;
		case 40:
			((MenuItem)target).Click += Configure_Oculus;
			break;
		case 41:
			((MenuItem)target).Click += Configure_OpenVR;
			break;
		case 42:
			((MenuItem)target).Click += Configure_OpenXR;
			break;
		case 43:
			tabSections = (MetroTabControl)target;
			break;
		case 44:
			tabView = (MetroTabItem)target;
			break;
		case 45:
			tabMonitorDisplay = (MetroTabControl)target;
			break;
		case 46:
			tabMonitor = (MetroTabItem)target;
			break;
		case 47:
			lblNumMonitors = (Label)target;
			break;
		case 48:
			cboNumMonitors = (ComboBox)target;
			cboNumMonitors.SelectionChanged += cboNumScreens_Changed;
			break;
		case 49:
			bowNumScreens = (clsRainbow)target;
			break;
		case 50:
			lblMonitorWidth = (Label)target;
			break;
		case 51:
			numMonitorWidth = (NumericUpDown)target;
			numMonitorWidth.GotFocus += Graphic_GotFocus;
			numMonitorWidth.LostFocus += Graphic_LostFocus;
			numMonitorWidth.ValueChanged += Monitor_Changed;
			break;
		case 52:
			bowMonitorWidth = (clsRainbow)target;
			break;
		case 53:
			lblMonitorVisible = (Label)target;
			break;
		case 54:
			numMonitorVisible = (NumericUpDown)target;
			numMonitorVisible.GotFocus += Graphic_GotFocus;
			numMonitorVisible.LostFocus += Graphic_LostFocus;
			numMonitorVisible.ValueChanged += Monitor_Changed;
			break;
		case 55:
			bowMonitorVisible = (clsRainbow)target;
			break;
		case 56:
			lblDiagonalWidth = (Label)target;
			break;
		case 57:
			numDiagonalWidth = (NumericUpDown)target;
			numDiagonalWidth.GotFocus += Graphic_GotFocus;
			numDiagonalWidth.LostFocus += Graphic_LostFocus;
			numDiagonalWidth.ValueChanged += Monitor_Changed;
			break;
		case 58:
			bowDiagonalWidth = (clsRainbow)target;
			break;
		case 59:
			lblScreenAngle = (Label)target;
			break;
		case 60:
			numScreenAngle = (NumericUpDown)target;
			numScreenAngle.GotFocus += Graphic_GotFocus;
			numScreenAngle.LostFocus += Graphic_LostFocus;
			numScreenAngle.ValueChanged += Monitor_Changed;
			break;
		case 61:
			bowScreenAngle = (clsRainbow)target;
			break;
		case 62:
			lblViewDistance = (Label)target;
			break;
		case 63:
			numViewDistance = (NumericUpDown)target;
			numViewDistance.GotFocus += Graphic_GotFocus;
			numViewDistance.LostFocus += Graphic_LostFocus;
			numViewDistance.ValueChanged += Monitor_Changed;
			break;
		case 64:
			bowViewDistance = (clsRainbow)target;
			break;
		case 65:
			lblFOV = (Label)target;
			break;
		case 66:
			numFOV = (NumericUpDown)target;
			numFOV.GotFocus += Graphic_GotFocus;
			numFOV.LostFocus += Graphic_LostFocus;
			break;
		case 67:
			bowFOV = (clsRainbow)target;
			break;
		case 68:
			grpFOV = (GroupBox)target;
			break;
		case 69:
			btnScreenAngle = (Button)target;
			btnScreenAngle.Click += btnScreenAngle_Click;
			break;
		case 70:
			btnViewDistance = (Button)target;
			btnViewDistance.Click += btnViewDistance_Click;
			break;
		case 71:
			btnFOV = (Button)target;
			btnFOV.Click += btnFOV_Click;
			break;
		case 72:
			lblRadius = (Label)target;
			break;
		case 73:
			cboRadius = (ComboBox)target;
			break;
		case 74:
			bowRadius = (clsRainbow)target;
			break;
		case 75:
			lblReflex = (Label)target;
			break;
		case 76:
			cboReflex = (ComboBox)target;
			break;
		case 77:
			bowReflex = (clsRainbow)target;
			break;
		case 78:
			lblResolution = (Label)target;
			break;
		case 79:
			cboResolution = (ComboBox)target;
			cboResolution.SelectionChanged += cboResolution_Changed;
			break;
		case 80:
			bowResolution = (clsRainbow)target;
			break;
		case 81:
			lblRefreshRate = (Label)target;
			break;
		case 82:
			numRefreshRate = (NumericUpDown)target;
			break;
		case 83:
			bowRefreshRate = (clsRainbow)target;
			break;
		case 84:
			chkMultiProjection = (CheckBox)target;
			break;
		case 85:
			chkSMP = (CheckBox)target;
			break;
		case 86:
			bowMultiProjection = (clsRainbow)target;
			break;
		case 87:
			chkAutoConfigDone = (CheckBox)target;
			break;
		case 88:
			bowAutoConfigDone = (clsRainbow)target;
			break;
		case 89:
			imgMonitorWidth = (System.Windows.Controls.Image)target;
			break;
		case 90:
			imgVisibleWidth = (System.Windows.Controls.Image)target;
			break;
		case 91:
			imgDiagonalWidth = (System.Windows.Controls.Image)target;
			break;
		case 92:
			imgDistance = (System.Windows.Controls.Image)target;
			break;
		case 93:
			imgAngle = (System.Windows.Controls.Image)target;
			break;
		case 94:
			imgFOV = (System.Windows.Controls.Image)target;
			break;
		case 95:
			gridMonitors = (Grid)target;
			break;
		case 96:
			((MenuItem)target).Click += PhysicalMonitors_Click;
			break;
		case 97:
			((MenuItem)target).Click += Triple1920_Click;
			break;
		case 98:
			((MenuItem)target).Click += Triple2560_Click;
			break;
		case 99:
			((MenuItem)target).Click += Triple3440_Click;
			break;
		case 100:
			((MenuItem)target).Click += Triple4k_Click;
			break;
		case 101:
			((MenuItem)target).Click += Single1920_Click;
			break;
		case 102:
			((MenuItem)target).Click += Single2560_Click;
			break;
		case 103:
			((MenuItem)target).Click += Single3440_Click;
			break;
		case 104:
			((MenuItem)target).Click += SamsungG9_Click;
			break;
		case 105:
			((MenuItem)target).Click += Surround1920_Click;
			break;
		case 106:
			((MenuItem)target).Click += Surround2560_Click;
			break;
		case 107:
			((MenuItem)target).Click += Surround3440_Click;
			break;
		case 108:
			((MenuItem)target).Click += Quad1920Above_Click;
			break;
		case 109:
			((MenuItem)target).Click += Quad2560Above_Click;
			break;
		case 110:
			((MenuItem)target).Click += Quad3440Above_Click;
			break;
		case 111:
			((MenuItem)target).Click += TripleMismatched_Click;
			break;
		case 112:
			((MenuItem)target).Click += TripleVertical_Click;
			break;
		case 113:
			tabDisplay = (MetroTabItem)target;
			break;
		case 114:
			lblResScale = (Label)target;
			break;
		case 115:
			cboResScale = (ComboBox)target;
			cboResScale.SelectionChanged += cboResScale_SelectionChanged;
			break;
		case 116:
			bowResScale = (clsRainbow)target;
			break;
		case 117:
			lblFSRSharpness = (Label)target;
			break;
		case 118:
			numFSRSharpness = (NumericUpDown)target;
			break;
		case 119:
			bowFSRSharpness = (clsRainbow)target;
			break;
		case 120:
			lblDeviceIdx = (Label)target;
			break;
		case 121:
			numDeviceIdx = (NumericUpDown)target;
			break;
		case 122:
			bowDeviceIdx = (clsRainbow)target;
			break;
		case 123:
			chkFullScreen = (CheckBox)target;
			break;
		case 124:
			bowFullScreen = (clsRainbow)target;
			break;
		case 125:
			chkBorder = (CheckBox)target;
			break;
		case 126:
			bowBorder = (clsRainbow)target;
			break;
		case 127:
			chkHDRFormat = (CheckBox)target;
			break;
		case 128:
			bowHDRFormat = (clsRainbow)target;
			break;
		case 129:
			lblAlign = (Label)target;
			break;
		case 130:
			cboAlign = (ComboBox)target;
			break;
		case 131:
			bowAlign = (clsRainbow)target;
			break;
		case 132:
			lblGamma = (Label)target;
			break;
		case 133:
			numGamma = (NumericUpDown)target;
			break;
		case 134:
			bowGamma = (clsRainbow)target;
			break;
		case 135:
			lblBrightness = (Label)target;
			break;
		case 136:
			numBrightness = (NumericUpDown)target;
			break;
		case 137:
			bowBrightness = (clsRainbow)target;
			break;
		case 138:
			lblContrast = (Label)target;
			break;
		case 139:
			numContrast = (NumericUpDown)target;
			break;
		case 140:
			bowContrast = (clsRainbow)target;
			break;
		case 141:
			sliderUIScale = (XSlider)target;
			break;
		case 142:
			bowUIScale = (clsRainbow)target;
			break;
		case 143:
			sliderBezelProtect = (XSlider)target;
			break;
		case 144:
			bowBezelProtect = (clsRainbow)target;
			break;
		case 145:
			sliderBottomOffset = (XSlider)target;
			break;
		case 146:
			bowBottomOffset = (clsRainbow)target;
			break;
		case 147:
			chkDriveUIFullScreen = (CheckBox)target;
			break;
		case 148:
			bowDriveUIFullScreen = (clsRainbow)target;
			break;
		case 149:
			lblDriveUITransparency = (Label)target;
			break;
		case 150:
			numDriveUITransparency = (NumericUpDown)target;
			break;
		case 151:
			bowDriveUITransparency = (clsRainbow)target;
			break;
		case 152:
			chkSessionUIFullScreen = (CheckBox)target;
			break;
		case 153:
			bowSessionUIFullScreen = (clsRainbow)target;
			break;
		case 154:
			lblSessionUITransparency = (Label)target;
			break;
		case 155:
			numSessionUITransparency = (NumericUpDown)target;
			break;
		case 156:
			bowSessionUITransparency = (clsRainbow)target;
			break;
		case 157:
			tabVR = (MetroTabItem)target;
			break;
		case 158:
			chkOculusWait4Sync = (CheckBox)target;
			break;
		case 159:
			chkOpenVRWait4Sync = (CheckBox)target;
			break;
		case 160:
			bowWait4Sync = (clsRainbow)target;
			break;
		case 161:
			lblVRMode = (Label)target;
			break;
		case 162:
			cboVRMode = (ComboBox)target;
			break;
		case 163:
			bowVRMode = (clsRainbow)target;
			break;
		case 164:
			chkOculusEnabled = (CheckBox)target;
			break;
		case 165:
			bowOculusEnabled = (clsRainbow)target;
			break;
		case 166:
			chkOpenVREnabled = (CheckBox)target;
			break;
		case 167:
			bowOpenVREnabled = (clsRainbow)target;
			break;
		case 168:
			chkOpenXREnabled = (CheckBox)target;
			break;
		case 169:
			bowOpenXREnabled = (clsRainbow)target;
			break;
		case 170:
			lblUIScreenWidthCM = (Label)target;
			break;
		case 171:
			numUIScreenWidthCM = (NumericUpDown)target;
			break;
		case 172:
			bowUIScreenWidthCM = (clsRainbow)target;
			break;
		case 173:
			lblUIScreenDistCM = (Label)target;
			break;
		case 174:
			numUIScreenDistCM = (NumericUpDown)target;
			break;
		case 175:
			bowUIScreenDistCM = (clsRainbow)target;
			break;
		case 176:
			lblFoveatedOuterPctRes = (Label)target;
			break;
		case 177:
			numFoveatedOuterPctRes = (NumericUpDown)target;
			break;
		case 178:
			bowFoveatedOuterPctRes = (clsRainbow)target;
			break;
		case 179:
			lblFoveatedInsetWidthPct = (Label)target;
			break;
		case 180:
			numFoveatedInsetWidthPct = (NumericUpDown)target;
			break;
		case 181:
			bowFoveatedInsetWidthPct = (clsRainbow)target;
			break;
		case 182:
			lblResolutionScalePct = (Label)target;
			break;
		case 183:
			numResolutionScalePct = (NumericUpDown)target;
			break;
		case 184:
			bowResolutionScalePct = (clsRainbow)target;
			break;
		case 185:
			tabGraphics = (MetroTabItem)target;
			break;
		case 186:
			lblSky = (Label)target;
			break;
		case 187:
			cboSky = (ComboBox)target;
			break;
		case 188:
			bowSky = (clsRainbow)target;
			break;
		case 189:
			lblCars = (Label)target;
			break;
		case 190:
			cboCars = (ComboBox)target;
			break;
		case 191:
			bowCars = (clsRainbow)target;
			break;
		case 192:
			lblPits = (Label)target;
			break;
		case 193:
			cboPits = (ComboBox)target;
			break;
		case 194:
			bowPits = (clsRainbow)target;
			break;
		case 195:
			lblEvent = (Label)target;
			break;
		case 196:
			cboEvent = (ComboBox)target;
			break;
		case 197:
			bowEvent = (clsRainbow)target;
			break;
		case 198:
			lblGrandstands = (Label)target;
			break;
		case 199:
			cboGrandstands = (ComboBox)target;
			break;
		case 200:
			bowGrandstands = (clsRainbow)target;
			break;
		case 201:
			lblCrowds = (Label)target;
			break;
		case 202:
			cboCrowds = (ComboBox)target;
			break;
		case 203:
			bowCrowds = (clsRainbow)target;
			break;
		case 204:
			lblObjects = (Label)target;
			break;
		case 205:
			cboObjects = (ComboBox)target;
			break;
		case 206:
			bowObjects = (clsRainbow)target;
			break;
		case 207:
			lblFoliage = (Label)target;
			break;
		case 208:
			cboFoliage = (ComboBox)target;
			break;
		case 209:
			bowFoliage = (clsRainbow)target;
			break;
		case 210:
			lblParticles = (Label)target;
			break;
		case 211:
			cboParticles = (ComboBox)target;
			break;
		case 212:
			bowParticles = (clsRainbow)target;
			break;
		case 213:
			chkFullRes = (CheckBox)target;
			break;
		case 214:
			bowFullRes = (clsRainbow)target;
			break;
		case 215:
			lblMaxCars = (Label)target;
			break;
		case 216:
			numMaxCars = (NumericUpDown)target;
			break;
		case 217:
			cboDrawCars = (ComboBox)target;
			cboDrawCars.SelectionChanged += cboDrawCars_Changed;
			break;
		case 218:
			bowDrawCars = (clsRainbow)target;
			break;
		case 219:
			cboDrawPits = (ComboBox)target;
			cboDrawPits.SelectionChanged += cboDrawPits_Changed;
			break;
		case 220:
			bowDrawPits = (clsRainbow)target;
			break;
		case 221:
			numLODFPS = (NumericUpDown)target;
			break;
		case 222:
			lblLODFPS = (Label)target;
			break;
		case 223:
			numLODBias = (NumericUpDown)target;
			break;
		case 224:
			lblLODBias = (Label)target;
			break;
		case 225:
			cboLODWorld = (ComboBox)target;
			cboLODWorld.SelectionChanged += cboLODWorld_Changed;
			break;
		case 226:
			bowLODWorld = (clsRainbow)target;
			break;
		case 227:
			cboLODCar = (ComboBox)target;
			cboLODCar.SelectionChanged += cboLODCar_Changed;
			break;
		case 228:
			bowLODCar = (clsRainbow)target;
			break;
		case 229:
			boxFrameRate = (GroupBox)target;
			break;
		case 230:
			rbFPSNoLimit = (RadioButton)target;
			rbFPSNoLimit.Click += rbFPSNoLimit_Change;
			break;
		case 231:
			rbFPSVSync = (RadioButton)target;
			rbFPSVSync.Click += rbFPSVSync_Change;
			break;
		case 232:
			rbFPSLimit = (RadioButton)target;
			rbFPSLimit.Click += rbFPSLimit_Change;
			break;
		case 233:
			numFPSLimit = (NumericUpDown)target;
			break;
		case 234:
			bowFPSFrameRate = (clsRainbow)target;
			break;
		case 235:
			lblMaxPrerenderedFrames = (Label)target;
			break;
		case 236:
			numMaxPrerenderedFrames = (NumericUpDown)target;
			break;
		case 237:
			bowMaxPrerenderedFrames = (clsRainbow)target;
			break;
		case 238:
			lblVisibilityFrameDelay = (Label)target;
			break;
		case 239:
			numVisibilityFrameDelay = (NumericUpDown)target;
			break;
		case 240:
			bowVisibilityFrameDelay = (clsRainbow)target;
			break;
		case 241:
			lblGpuMem = (Label)target;
			break;
		case 242:
			numGpuMem = (NumericUpDown)target;
			break;
		case 243:
			bowGpuMem = (clsRainbow)target;
			break;
		case 244:
			lblSysMem = (Label)target;
			break;
		case 245:
			numSysMem = (NumericUpDown)target;
			break;
		case 246:
			bowSysMem = (clsRainbow)target;
			break;
		case 247:
			lblAntiAliasMethod = (Label)target;
			break;
		case 248:
			cboAntiAliasMethod = (ComboBox)target;
			break;
		case 249:
			bowAntiAliasMethod = (clsRainbow)target;
			break;
		case 250:
			lblMSAASamples = (Label)target;
			break;
		case 251:
			cboMSAASamples = (ComboBox)target;
			cboMSAASamples.SelectionChanged += cboMSAASamples_Changed;
			break;
		case 252:
			bowMSAASamples = (clsRainbow)target;
			break;
		case 253:
			lblMSAAUseFilter = (Label)target;
			break;
		case 254:
			cboMSAAUseFilter = (ComboBox)target;
			break;
		case 255:
			bowMSAAUseFilter = (clsRainbow)target;
			break;
		case 256:
			chkShadowMapsDay = (CheckBox)target;
			chkShadowMapsDay.Click += chkShadowMapsDay_Click;
			break;
		case 257:
			bowShadowMapsDay = (clsRainbow)target;
			break;
		case 258:
			chkObjSelfShadow = (CheckBox)target;
			break;
		case 259:
			bowObjSelfShadow = (clsRainbow)target;
			break;
		case 260:
			lblObjDynamic = (Label)target;
			break;
		case 261:
			cboObjDynamic = (ComboBox)target;
			break;
		case 262:
			bowObjDynamic = (clsRainbow)target;
			break;
		case 263:
			chkShadowMapsNight = (CheckBox)target;
			break;
		case 264:
			chkShadowWalls = (CheckBox)target;
			break;
		case 265:
			bowShadowMapsNight = (clsRainbow)target;
			break;
		case 266:
			lblLights = (Label)target;
			break;
		case 267:
			numLights = (NumericUpDown)target;
			break;
		case 268:
			bowNumLights = (clsRainbow)target;
			break;
		case 269:
			lblShadowNightFilter = (Label)target;
			break;
		case 270:
			cboShadowNightFilter = (ComboBox)target;
			break;
		case 271:
			bowShadowNightFilter = (clsRainbow)target;
			break;
		case 272:
			lblDynCubeMaps = (Label)target;
			break;
		case 273:
			numDynCubeMaps = (NumericUpDown)target;
			break;
		case 274:
			bowDynCubeMaps = (clsRainbow)target;
			break;
		case 275:
			lblFixCubeMaps = (Label)target;
			break;
		case 276:
			numFixCubeMaps = (NumericUpDown)target;
			break;
		case 277:
			bowFixCubeMaps = (clsRainbow)target;
			break;
		case 278:
			lblShaderQuality = (Label)target;
			break;
		case 279:
			cboShaderQuality = (ComboBox)target;
			break;
		case 280:
			bowShaderQuality = (clsRainbow)target;
			break;
		case 281:
			lblDynShadowRes = (Label)target;
			break;
		case 282:
			cboDynShadowRes = (ComboBox)target;
			break;
		case 283:
			bowDynShadowRes = (clsRainbow)target;
			break;
		case 284:
			lblStaticShadowRes = (Label)target;
			break;
		case 285:
			cboStaticShadowRes = (ComboBox)target;
			break;
		case 286:
			bowStaticShadowRes = (clsRainbow)target;
			break;
		case 287:
			lblStaticShadowCount = (Label)target;
			break;
		case 288:
			numStaticShadowCount = (NumericUpDown)target;
			break;
		case 289:
			bowStaticShadowCount = (clsRainbow)target;
			break;
		case 290:
			chkTrackDisplacement = (CheckBox)target;
			break;
		case 291:
			chkParallelSorting = (CheckBox)target;
			break;
		case 292:
			bowTrackDisplacement = (clsRainbow)target;
			break;
		case 293:
			lblObstructions = (Label)target;
			break;
		case 294:
			cboObstructions = (ComboBox)target;
			break;
		case 295:
			bowObstructions = (clsRainbow)target;
			break;
		case 296:
			lblSteerWheel = (Label)target;
			break;
		case 297:
			cboSteerWheel = (ComboBox)target;
			cboSteerWheel.SelectionChanged += cboSteerWheel_Changed;
			break;
		case 298:
			bowSteerWheel = (clsRainbow)target;
			break;
		case 299:
			chkTrees2Pass = (CheckBox)target;
			break;
		case 300:
			bowTrees2Pass = (clsRainbow)target;
			break;
		case 301:
			chkTreesHighQ = (CheckBox)target;
			break;
		case 302:
			bowTreesHighQ = (clsRainbow)target;
			break;
		case 303:
			lblMirrors = (Label)target;
			break;
		case 304:
			cboMirrors = (ComboBox)target;
			break;
		case 305:
			bowMirrors = (clsRainbow)target;
			break;
		case 306:
			chkMirrorHighQ = (CheckBox)target;
			break;
		case 307:
			bowMirrorHighQ = (clsRainbow)target;
			break;
		case 308:
			lblHeadlights = (Label)target;
			break;
		case 309:
			cboHeadlights = (ComboBox)target;
			break;
		case 310:
			bowHeadlights = (clsRainbow)target;
			break;
		case 311:
			chkHeadlightsTrackMirror = (CheckBox)target;
			break;
		case 312:
			bowHeadlightsTrackMirror = (clsRainbow)target;
			break;
		case 313:
			chkVMirror = (CheckBox)target;
			break;
		case 314:
			cboVMirrorSize = (ComboBox)target;
			break;
		case 315:
			numVMirrorFOV = (NumericUpDown)target;
			break;
		case 316:
			bowVMirrorSize = (clsRainbow)target;
			break;
		case 317:
			lblMotionBlur = (Label)target;
			break;
		case 318:
			cboMotionBlur = (ComboBox)target;
			break;
		case 319:
			bowMotionBlur = (clsRainbow)target;
			break;
		case 320:
			lblSSR = (Label)target;
			break;
		case 321:
			cboSSR = (ComboBox)target;
			cboSSR.SelectionChanged += cboSSR_Changed;
			break;
		case 322:
			bowSSR = (clsRainbow)target;
			break;
		case 323:
			chkSharpening = (CheckBox)target;
			break;
		case 324:
			chkDistortion = (CheckBox)target;
			break;
		case 325:
			bowDistortion = (clsRainbow)target;
			break;
		case 326:
			chkHDR = (CheckBox)target;
			break;
		case 327:
			chkAutoExposure = (CheckBox)target;
			break;
		case 328:
			bowHDR = (clsRainbow)target;
			break;
		case 329:
			chkSSAO = (CheckBox)target;
			break;
		case 330:
			chkMonoHeadlights = (CheckBox)target;
			break;
		case 331:
			bowMonoHeadlights = (clsRainbow)target;
			break;
		case 332:
			chkGPUMemSwap = (CheckBox)target;
			break;
		case 333:
			bowGPUMemSwap = (clsRainbow)target;
			break;
		case 334:
			chkCar2048 = (CheckBox)target;
			break;
		case 335:
			chkHeatHaze = (CheckBox)target;
			break;
		case 336:
			bowCar2048 = (clsRainbow)target;
			break;
		case 337:
			chkNumCustom = (CheckBox)target;
			break;
		case 338:
			chkZBuffer = (CheckBox)target;
			break;
		case 339:
			bowZBuffer = (clsRainbow)target;
			break;
		case 340:
			lblFXAAEdge = (Label)target;
			break;
		case 341:
			cboFXAAEdge = (ComboBox)target;
			cboFXAAEdge.SelectionChanged += cboFXAAEdge_Changed;
			break;
		case 342:
			bowFXAAEdge = (clsRainbow)target;
			break;
		case 343:
			lblFXAASubPix = (Label)target;
			break;
		case 344:
			cboFXAASubPix = (ComboBox)target;
			cboFXAASubPix.SelectionChanged += cboFXAASubPix_Changed;
			break;
		case 345:
			bowFXAASubPix = (clsRainbow)target;
			break;
		case 346:
			tabReplay = (MetroTabItem)target;
			break;
		case 347:
			lblSkyReplay = (Label)target;
			break;
		case 348:
			cboSkyReplay = (ComboBox)target;
			break;
		case 349:
			bowSkyReplay = (clsRainbow)target;
			break;
		case 350:
			lblCarsReplay = (Label)target;
			break;
		case 351:
			cboCarsReplay = (ComboBox)target;
			break;
		case 352:
			bowCarsReplay = (clsRainbow)target;
			break;
		case 353:
			lblPitsReplay = (Label)target;
			break;
		case 354:
			cboPitsReplay = (ComboBox)target;
			break;
		case 355:
			bowPitsReplay = (clsRainbow)target;
			break;
		case 356:
			lblEventReplay = (Label)target;
			break;
		case 357:
			cboEventReplay = (ComboBox)target;
			break;
		case 358:
			bowEventReplay = (clsRainbow)target;
			break;
		case 359:
			lblGrandstandsReplay = (Label)target;
			break;
		case 360:
			cboGrandstandsReplay = (ComboBox)target;
			break;
		case 361:
			bowGrandstandsReplay = (clsRainbow)target;
			break;
		case 362:
			lblCrowdsReplay = (Label)target;
			break;
		case 363:
			cboCrowdsReplay = (ComboBox)target;
			break;
		case 364:
			bowCrowdsReplay = (clsRainbow)target;
			break;
		case 365:
			lblObjectsReplay = (Label)target;
			break;
		case 366:
			cboObjectsReplay = (ComboBox)target;
			break;
		case 367:
			bowObjectsReplay = (clsRainbow)target;
			break;
		case 368:
			lblFoliageReplay = (Label)target;
			break;
		case 369:
			cboFoliageReplay = (ComboBox)target;
			break;
		case 370:
			bowFoliageReplay = (clsRainbow)target;
			break;
		case 371:
			lblParticlesReplay = (Label)target;
			break;
		case 372:
			cboParticlesReplay = (ComboBox)target;
			break;
		case 373:
			bowParticlesReplay = (clsRainbow)target;
			break;
		case 374:
			chkFullResReplay = (CheckBox)target;
			break;
		case 375:
			bowFullResReplay = (clsRainbow)target;
			break;
		case 376:
			cboDrawCarsReplay = (ComboBox)target;
			cboDrawCarsReplay.SelectionChanged += cboDrawCarsReplay_Changed;
			break;
		case 377:
			bowDrawCarsReplay = (clsRainbow)target;
			break;
		case 378:
			cboDrawPitsReplay = (ComboBox)target;
			cboDrawPitsReplay.SelectionChanged += cboDrawPitsReplay_Changed;
			break;
		case 379:
			bowDrawPitsReplay = (clsRainbow)target;
			break;
		case 380:
			numLODFPSReplay = (NumericUpDown)target;
			break;
		case 381:
			lblLODFPSReplay = (Label)target;
			break;
		case 382:
			cboLODWorldReplay = (ComboBox)target;
			cboLODWorldReplay.SelectionChanged += cboLODWorldReplay_Changed;
			break;
		case 383:
			bowLODWorldReplay = (clsRainbow)target;
			break;
		case 384:
			cboLODCarReplay = (ComboBox)target;
			cboLODCarReplay.SelectionChanged += cboLODCarReplay_Changed;
			break;
		case 385:
			bowLODCarReplay = (clsRainbow)target;
			break;
		case 386:
			chkShadowMapsDayReplay = (CheckBox)target;
			chkShadowMapsDayReplay.Click += chkShadowMapsDayReplay_Click;
			break;
		case 387:
			bowShadowMapsDayReplay = (clsRainbow)target;
			break;
		case 388:
			chkObjSelfShadowReplay = (CheckBox)target;
			break;
		case 389:
			bowObjSelfShadowReplay = (clsRainbow)target;
			break;
		case 390:
			lblObjDynamicReplay = (Label)target;
			break;
		case 391:
			cboObjDynamicReplay = (ComboBox)target;
			break;
		case 392:
			bowObjDynamicReplay = (clsRainbow)target;
			break;
		case 393:
			chkShadowMapsNightReplay = (CheckBox)target;
			break;
		case 394:
			chkShadowWallsReplay = (CheckBox)target;
			break;
		case 395:
			bowShadowMapsNightReplay = (clsRainbow)target;
			break;
		case 396:
			bowShadowHeadlightsReplay = (clsRainbow)target;
			break;
		case 397:
			lblLightsReplay = (Label)target;
			break;
		case 398:
			numLightsReplay = (NumericUpDown)target;
			break;
		case 399:
			bowNumLightsReplay = (clsRainbow)target;
			break;
		case 400:
			lblShadowNightFilterReplay = (Label)target;
			break;
		case 401:
			cboShadowNightFilterReplay = (ComboBox)target;
			break;
		case 402:
			bowShadowNightFilterReplay = (clsRainbow)target;
			break;
		case 403:
			lblDynCubeMapsReplay = (Label)target;
			break;
		case 404:
			numDynCubeMapsReplay = (NumericUpDown)target;
			break;
		case 405:
			bowDynCubeMapsReplay = (clsRainbow)target;
			break;
		case 406:
			lblFixCubeMapsReplay = (Label)target;
			break;
		case 407:
			numFixCubeMapsReplay = (NumericUpDown)target;
			break;
		case 408:
			bowFixCubeMapsReplay = (clsRainbow)target;
			break;
		case 409:
			lblObstructionsReplay = (Label)target;
			break;
		case 410:
			cboObstructionsReplay = (ComboBox)target;
			break;
		case 411:
			bowObstructionsReplay = (clsRainbow)target;
			break;
		case 412:
			lblSteerWheelReplay = (Label)target;
			break;
		case 413:
			cboSteerWheelReplay = (ComboBox)target;
			cboSteerWheelReplay.SelectionChanged += cboSteerWheelReplay_Changed;
			break;
		case 414:
			bowSteerWheelReplay = (clsRainbow)target;
			break;
		case 415:
			chkTrees2PassReplay = (CheckBox)target;
			break;
		case 416:
			bowTrees2PassReplay = (clsRainbow)target;
			break;
		case 417:
			lblMirrorsReplay = (Label)target;
			break;
		case 418:
			cboMirrorsReplay = (ComboBox)target;
			break;
		case 419:
			bowMirrorsReplay = (clsRainbow)target;
			break;
		case 420:
			chkMirrorHighQReplay = (CheckBox)target;
			break;
		case 421:
			bowMirrorHighQReplay = (clsRainbow)target;
			break;
		case 422:
			lblMotionBlurReplay = (Label)target;
			break;
		case 423:
			cboMotionBlurReplay = (ComboBox)target;
			break;
		case 424:
			bowMotionBlurReplay = (clsRainbow)target;
			break;
		case 425:
			chkMotionBlurCarCams = (CheckBox)target;
			break;
		case 426:
			chkMotionBlurBroadcast = (CheckBox)target;
			break;
		case 427:
			bowMotionBlurBroadcast = (clsRainbow)target;
			break;
		case 428:
			lblSSRReplay = (Label)target;
			break;
		case 429:
			cboSSRReplay = (ComboBox)target;
			cboSSRReplay.SelectionChanged += cboSSRReplay_Changed;
			break;
		case 430:
			bowSSRReplay = (clsRainbow)target;
			break;
		case 431:
			chkSharpeningReplay = (CheckBox)target;
			break;
		case 432:
			chkDistortionReplay = (CheckBox)target;
			break;
		case 433:
			bowDistortionReplay = (clsRainbow)target;
			break;
		case 434:
			chkSSAOReplay = (CheckBox)target;
			break;
		case 435:
			bowSSAOReplay = (clsRainbow)target;
			break;
		case 436:
			chkDOFReplay = (CheckBox)target;
			break;
		case 437:
			chkHeatHazeReplay = (CheckBox)target;
			break;
		case 438:
			bowHeatHazeReplay = (clsRainbow)target;
			break;
		case 439:
			chkRenderReplay = (CheckBox)target;
			break;
		case 440:
			bowRenderReplay = (clsRainbow)target;
			break;
		case 441:
			lblFXAAEdgeReplay = (Label)target;
			break;
		case 442:
			cboFXAAEdgeReplay = (ComboBox)target;
			cboFXAAEdgeReplay.SelectionChanged += cboFXAAEdgeReplay_Changed;
			break;
		case 443:
			bowFXAAEdgeReplay = (clsRainbow)target;
			break;
		case 444:
			lblFXAASubPixReplay = (Label)target;
			break;
		case 445:
			cboFXAASubPixReplay = (ComboBox)target;
			cboFXAASubPixReplay.SelectionChanged += cboFXAASubPixReplay_Changed;
			break;
		case 446:
			bowFXAASubPixReplay = (clsRainbow)target;
			break;
		case 447:
			tabExtras = (MetroTabItem)target;
			break;
		case 448:
			chkTicker = (CheckBox)target;
			break;
		case 449:
			bowTicker = (clsRainbow)target;
			break;
		case 450:
			chkConnectSockets = (CheckBox)target;
			break;
		case 451:
			bowConnectSockets = (clsRainbow)target;
			break;
		case 452:
			chkSDKEnableMem = (CheckBox)target;
			break;
		case 453:
			bowSDKEnableMem = (clsRainbow)target;
			break;
		case 454:
			chkAutoTelemetry = (CheckBox)target;
			break;
		case 455:
			bowAutoTelemetry = (clsRainbow)target;
			break;
		case 456:
			chkAskSaveReplay = (CheckBox)target;
			break;
		case 457:
			bowAskSaveReplay = (clsRainbow)target;
			break;
		case 458:
			chkAutoResetFastRepair = (CheckBox)target;
			break;
		case 459:
			bowAutoResetFastRepair = (clsRainbow)target;
			break;
		case 460:
			chkAutoResetPitBox = (CheckBox)target;
			break;
		case 461:
			bowAutoResetPitBox = (clsRainbow)target;
			break;
		case 462:
			chkAutoFuel = (CheckBox)target;
			break;
		case 463:
			bowAutoFuel = (clsRainbow)target;
			break;
		case 464:
			lblAutoFuelLapMargin = (Label)target;
			break;
		case 465:
			numAutoFuelLapMargin = (NumericUpDown)target;
			break;
		case 466:
			bowAutoFuelLapMargin = (clsRainbow)target;
			break;
		case 467:
			chkForceCrowdVisible = (CheckBox)target;
			break;
		case 468:
			bowForceCrowdVisible = (clsRainbow)target;
			break;
		case 469:
			chkForceControlsVisible = (CheckBox)target;
			break;
		case 470:
			bowForceControlsVisible = (clsRainbow)target;
			break;
		case 471:
			chkShowMsgUsr = (CheckBox)target;
			break;
		case 472:
			bowShowMsgUsr = (clsRainbow)target;
			break;
		case 473:
			chkShowMsgSys = (CheckBox)target;
			break;
		case 474:
			bowShowMsgSys = (clsRainbow)target;
			break;
		case 475:
			chkShowMsgInc = (CheckBox)target;
			break;
		case 476:
			bowShowMsgInc = (clsRainbow)target;
			break;
		case 477:
			chkShowMsgJoin = (CheckBox)target;
			break;
		case 478:
			bowShowMsgJoin = (clsRainbow)target;
			break;
		case 479:
			chkLoadPaintDriving = (CheckBox)target;
			break;
		case 480:
			bowLoadPaintDriving = (clsRainbow)target;
			break;
		case 481:
			lblDriveBlackBox = (Label)target;
			break;
		case 482:
			cboDriveBlackBox = (ComboBox)target;
			break;
		case 483:
			bowDriveBlackBox = (clsRainbow)target;
			break;
		case 484:
			lblPitBlackBox = (Label)target;
			break;
		case 485:
			cboPitBlackBox = (ComboBox)target;
			break;
		case 486:
			bowPitBlackBox = (clsRainbow)target;
			break;
		case 487:
			chkDisableSplitsRaceStart = (CheckBox)target;
			break;
		case 488:
			bowDisableSplitsRaceStart = (clsRainbow)target;
			break;
		case 489:
			chkFadeGhost = (CheckBox)target;
			break;
		case 490:
			bowFadeGhost = (clsRainbow)target;
			break;
		case 491:
			lblGhostOffset = (Label)target;
			break;
		case 492:
			numGhostOffset = (NumericUpDown)target;
			break;
		case 493:
			bowGhostOffset = (clsRainbow)target;
			break;
		case 494:
			lblGhostOpacity = (Label)target;
			break;
		case 495:
			numGhostOpacity = (NumericUpDown)target;
			break;
		case 496:
			bowGhostOpacity = (clsRainbow)target;
			break;
		case 497:
			lblWorkerThreads = (Label)target;
			break;
		case 498:
			numWorkerThreads = (NumericUpDown)target;
			break;
		case 499:
			bowWorkerThreads = (clsRainbow)target;
			break;
		case 500:
			lblDamageThreads = (Label)target;
			break;
		case 501:
			numDamageThreads = (NumericUpDown)target;
			break;
		case 502:
			bowDamageThreads = (clsRainbow)target;
			break;
		case 503:
			lblTestPitLocation = (Label)target;
			break;
		case 504:
			numTestPitLocation = (NumericUpDown)target;
			break;
		case 505:
			bowTestPitLocation = (clsRainbow)target;
			break;
		case 506:
			lblDriveHeightAdj = (Label)target;
			break;
		case 507:
			numDriveHeightAdj = (NumericUpDown)target;
			break;
		case 508:
			bowDriveHeightAdj = (clsRainbow)target;
			break;
		case 509:
			lblDriveVanishY = (Label)target;
			break;
		case 510:
			numDriveVanishY = (NumericUpDown)target;
			break;
		case 511:
			bowDriveVanishY = (clsRainbow)target;
			break;
		case 512:
			lblLookInstant = (Label)target;
			break;
		case 513:
			cboLookInstant = (ComboBox)target;
			break;
		case 514:
			bowLookInstant = (clsRainbow)target;
			break;
		case 515:
			lblLookSideAngle = (Label)target;
			break;
		case 516:
			numLookSideAngle = (NumericUpDown)target;
			break;
		case 517:
			bowLookSideAngle = (clsRainbow)target;
			break;
		case 518:
			lblLookUpAngle = (Label)target;
			break;
		case 519:
			numLookUpAngle = (NumericUpDown)target;
			break;
		case 520:
			bowLookUpAngle = (clsRainbow)target;
			break;
		case 521:
			lblLookDownAngle = (Label)target;
			break;
		case 522:
			numLookDownAngle = (NumericUpDown)target;
			break;
		case 523:
			bowLookDownAngle = (clsRainbow)target;
			break;
		case 524:
			chkCompressVertices = (CheckBox)target;
			break;
		case 525:
			bowCompressVertices = (clsRainbow)target;
			break;
		case 526:
			chkCompressCars = (CheckBox)target;
			break;
		case 527:
			bowCompressCars = (clsRainbow)target;
			break;
		case 528:
			chkCompressSuits = (CheckBox)target;
			break;
		case 529:
			bowCompressSuits = (clsRainbow)target;
			break;
		case 530:
			chkCompressHelmets = (CheckBox)target;
			break;
		case 531:
			bowCompressHelmets = (clsRainbow)target;
			break;
		case 532:
			chkPauseReplayOnExit = (CheckBox)target;
			break;
		case 533:
			bowPauseReplayOnExit = (clsRainbow)target;
			break;
		case 534:
			lblReplaySecondsNormal = (Label)target;
			break;
		case 535:
			numReplaySecondsNormal = (NumericUpDown)target;
			break;
		case 536:
			bowReplaySecondsNormal = (clsRainbow)target;
			break;
		case 537:
			lblReplaySecondsTeam = (Label)target;
			break;
		case 538:
			numReplaySecondsTeam = (NumericUpDown)target;
			break;
		case 539:
			bowReplaySecondsTeam = (clsRainbow)target;
			break;
		case 540:
			chkCaptureEnabled = (CheckBox)target;
			break;
		case 541:
			bowCaptureEnabled = (clsRainbow)target;
			break;
		case 542:
			lblScreenshotFmt = (Label)target;
			break;
		case 543:
			cboScreenshotFmt = (ComboBox)target;
			break;
		case 544:
			bowScreenshotFmt = (clsRainbow)target;
			break;
		case 545:
			lblScreenshotWidth = (Label)target;
			break;
		case 546:
			((MenuItem)target).Click += SS2X_Click;
			break;
		case 547:
			((MenuItem)target).Click += SS3X_Click;
			break;
		case 548:
			((MenuItem)target).Click += SS4X_Click;
			break;
		case 549:
			numScreenshotWidth = (NumericUpDown)target;
			break;
		case 550:
			bowScreenshotWidth = (clsRainbow)target;
			break;
		case 551:
			lblScreenshotHeight = (Label)target;
			break;
		case 552:
			numScreenshotHeight = (NumericUpDown)target;
			break;
		case 553:
			bowScreenshotHeight = (clsRainbow)target;
			break;
		case 554:
			lblVideoFmt = (Label)target;
			break;
		case 555:
			cboVideoFmt = (ComboBox)target;
			break;
		case 556:
			bowVideoFmt = (clsRainbow)target;
			break;
		case 557:
			lblVideoFrameRate = (Label)target;
			break;
		case 558:
			cboVideoFrameRate = (ComboBox)target;
			break;
		case 559:
			bowVideoFrameRate = (clsRainbow)target;
			break;
		case 560:
			lblVideoImgSize = (Label)target;
			break;
		case 561:
			cboVideoImgSize = (ComboBox)target;
			break;
		case 562:
			bowVideoImgSize = (clsRainbow)target;
			break;
		case 563:
			chkHiContrastCursor = (CheckBox)target;
			break;
		case 564:
			bowHiContrastCursor = (clsRainbow)target;
			break;
		case 565:
			chkPitLineVisible = (CheckBox)target;
			break;
		case 566:
			bowPitLineVisible = (clsRainbow)target;
			break;
		case 567:
			lblRaceLineWidth = (Label)target;
			break;
		case 568:
			numRaceLineWidth = (NumericUpDown)target;
			break;
		case 569:
			bowRaceLineWidth = (clsRainbow)target;
			break;
		case 570:
			lblPitLine = (Label)target;
			break;
		case 571:
			colPitLine = (ColorPicker)target;
			break;
		case 572:
			bowPitLine = (clsRainbow)target;
			break;
		case 573:
			lblFastLine = (Label)target;
			break;
		case 574:
			colFastLine = (ColorPicker)target;
			break;
		case 575:
			bowFastLine = (clsRainbow)target;
			break;
		case 576:
			lblSameLine = (Label)target;
			break;
		case 577:
			colSameLine = (ColorPicker)target;
			break;
		case 578:
			bowSameLine = (clsRainbow)target;
			break;
		case 579:
			lblSlowLine = (Label)target;
			break;
		case 580:
			colSlowLine = (ColorPicker)target;
			break;
		case 581:
			bowSlowLine = (clsRainbow)target;
			break;
		case 582:
			tabFFB = (MetroTabItem)target;
			break;
		case 583:
			chkWheelDisplay = (CheckBox)target;
			break;
		case 584:
			bowWheelDisplay = (clsRainbow)target;
			break;
		case 585:
			chkWheelDisplayBlink = (CheckBox)target;
			break;
		case 586:
			bowWheelDisplayBlink = (clsRainbow)target;
			break;
		case 587:
			chkVibratePedalWheel = (CheckBox)target;
			break;
		case 588:
			bowVibratePedalWheel = (clsRainbow)target;
			break;
		case 589:
			chkFFB360Hz = (CheckBox)target;
			break;
		case 590:
			bowFFB360Hz = (clsRainbow)target;
			break;
		case 591:
			chkAsetekAPI = (CheckBox)target;
			chkAsetekAPI.Click += chkFFBAPI_Click;
			break;
		case 592:
			bowAsetekAPI = (clsRainbow)target;
			break;
		case 593:
			chkConspitAPI = (CheckBox)target;
			chkConspitAPI.Click += chkFFBAPI_Click;
			break;
		case 594:
			bowConspitAPI = (clsRainbow)target;
			break;
		case 595:
			chkFanatecAPI = (CheckBox)target;
			chkFanatecAPI.Click += chkFFBAPI_Click;
			break;
		case 596:
			bowFanatecAPI = (clsRainbow)target;
			break;
		case 597:
			chkMozaAPI = (CheckBox)target;
			chkMozaAPI.Click += chkFFBAPI_Click;
			break;
		case 598:
			bowMozaAPI = (clsRainbow)target;
			break;
		case 599:
			chkSimagicAPI = (CheckBox)target;
			chkSimagicAPI.Click += chkFFBAPI_Click;
			break;
		case 600:
			bowSimagicAPI = (clsRainbow)target;
			break;
		case 601:
			chkSimuCubeAPI = (CheckBox)target;
			chkSimuCubeAPI.Click += chkFFBAPI_Click;
			break;
		case 602:
			bowSimuCubeAPI = (clsRainbow)target;
			break;
		case 603:
			chkVRSAPI = (CheckBox)target;
			chkVRSAPI.Click += chkFFBAPI_Click;
			break;
		case 604:
			bowVRSAPI = (clsRainbow)target;
			break;
		case 605:
			lblFFBScaling = (Label)target;
			break;
		case 606:
			numFFBScaling = (NumericUpDown)target;
			break;
		case 607:
			bowFFBScaling = (clsRainbow)target;
			break;
		case 608:
			lblFFBSmoothing = (Label)target;
			break;
		case 609:
			cboFFBSmoothing = (ComboBox)target;
			break;
		case 610:
			bowFFBSmoothing = (clsRainbow)target;
			break;
		case 611:
			lblFFBClutchLaunchMode = (Label)target;
			break;
		case 612:
			cboFFBClutchLaunchMode = (ComboBox)target;
			break;
		case 613:
			bowFFBClutchLaunchMode = (clsRainbow)target;
			break;
		case 614:
			chkTrueForceAPI = (CheckBox)target;
			chkTrueForceAPI.Click += chkFFBAPI_Click;
			break;
		case 615:
			bowTrueForceAPI = (clsRainbow)target;
			break;
		case 616:
			chkTrueForceVibe = (CheckBox)target;
			break;
		case 617:
			bowTrueForceVibe = (clsRainbow)target;
			break;
		case 618:
			chkForceVibePhysics = (CheckBox)target;
			break;
		case 619:
			bowForceVibePhysics = (clsRainbow)target;
			break;
		case 620:
			sliderTrueForceDamper = (XSlider)target;
			break;
		case 621:
			bowTrueForceDamper = (clsRainbow)target;
			break;
		case 622:
			sliderTrueForceMaster = (XSlider)target;
			break;
		case 623:
			bowTrueForceMaster = (clsRainbow)target;
			break;
		case 624:
			sliderTrueForceCarBody = (XSlider)target;
			break;
		case 625:
			bowTrueForceCarBody = (clsRainbow)target;
			break;
		case 626:
			sliderTrueForceDriveShaft = (XSlider)target;
			break;
		case 627:
			bowTrueForceDriveShaft = (clsRainbow)target;
			break;
		case 628:
			sliderTrueForceEngineRPM = (XSlider)target;
			break;
		case 629:
			bowTrueForceEngineRPM = (clsRainbow)target;
			break;
		case 630:
			sliderTrueForceGearChange = (XSlider)target;
			break;
		case 631:
			bowTrueForceGearChange = (clsRainbow)target;
			break;
		case 632:
			sliderTrueForceRevLimit = (XSlider)target;
			break;
		case 633:
			bowTrueForceRevLimit = (clsRainbow)target;
			break;
		case 634:
			sliderTrueForceRoadTexture = (XSlider)target;
			break;
		case 635:
			bowTrueForceRoadTexture = (clsRainbow)target;
			break;
		case 636:
			sliderTrueForceRumbleStrip = (XSlider)target;
			break;
		case 637:
			bowTrueForceRumbleStrip = (clsRainbow)target;
			break;
		case 638:
			sliderTrueForceWheelSlip = (XSlider)target;
			break;
		case 639:
			bowTrueForceWheelSlip = (clsRainbow)target;
			break;
		case 640:
			tabMacros = (MetroTabItem)target;
			break;
		case 641:
			bowMacroEnable = (clsRainbow)target;
			break;
		case 642:
			bowMacro01 = (clsRainbow)target;
			break;
		case 643:
			bowMacro02 = (clsRainbow)target;
			break;
		case 644:
			bowMacro03 = (clsRainbow)target;
			break;
		case 645:
			bowMacro04 = (clsRainbow)target;
			break;
		case 646:
			bowMacro05 = (clsRainbow)target;
			break;
		case 647:
			bowMacro06 = (clsRainbow)target;
			break;
		case 648:
			bowMacro07 = (clsRainbow)target;
			break;
		case 649:
			bowMacro08 = (clsRainbow)target;
			break;
		case 650:
			bowMacro09 = (clsRainbow)target;
			break;
		case 651:
			bowMacro10 = (clsRainbow)target;
			break;
		case 652:
			bowMacro11 = (clsRainbow)target;
			break;
		case 653:
			bowMacro12 = (clsRainbow)target;
			break;
		case 654:
			bowMacro13 = (clsRainbow)target;
			break;
		case 655:
			bowMacro14 = (clsRainbow)target;
			break;
		case 656:
			bowMacro15 = (clsRainbow)target;
			break;
		case 657:
			chkMacroEnable = (CheckBox)target;
			break;
		case 658:
			txtMacro01 = (TextBox)target;
			break;
		case 659:
			txtMacro02 = (TextBox)target;
			break;
		case 660:
			txtMacro03 = (TextBox)target;
			break;
		case 661:
			txtMacro04 = (TextBox)target;
			break;
		case 662:
			txtMacro05 = (TextBox)target;
			break;
		case 663:
			txtMacro06 = (TextBox)target;
			break;
		case 664:
			txtMacro07 = (TextBox)target;
			break;
		case 665:
			txtMacro08 = (TextBox)target;
			break;
		case 666:
			txtMacro09 = (TextBox)target;
			break;
		case 667:
			txtMacro10 = (TextBox)target;
			break;
		case 668:
			txtMacro11 = (TextBox)target;
			break;
		case 669:
			txtMacro12 = (TextBox)target;
			break;
		case 670:
			txtMacro13 = (TextBox)target;
			break;
		case 671:
			txtMacro14 = (TextBox)target;
			break;
		case 672:
			txtMacro15 = (TextBox)target;
			break;
		case 673:
			tabSound = (MetroTabItem)target;
			break;
		case 674:
			lblSoundDevice = (Label)target;
			break;
		case 675:
			bowSoundDevice = (clsRainbow)target;
			break;
		case 676:
			chkAmbientMusic = (CheckBox)target;
			break;
		case 677:
			bowAmbientMusic = (clsRainbow)target;
			break;
		case 678:
			lblDimensions = (Label)target;
			break;
		case 679:
			cboDimensions = (ComboBox)target;
			break;
		case 680:
			bowDimensions = (clsRainbow)target;
			break;
		case 681:
			lblNotification = (Label)target;
			break;
		case 682:
			cboNotification = (ComboBox)target;
			break;
		case 683:
			bowNotification = (clsRainbow)target;
			break;
		case 684:
			chkDownshiftAlert = (CheckBox)target;
			break;
		case 685:
			bowDownshiftAlert = (clsRainbow)target;
			break;
		case 686:
			chkRotateVR = (CheckBox)target;
			break;
		case 687:
			bowRotateVR = (clsRainbow)target;
			break;
		case 688:
			sliderLoudMaster = (XSlider)target;
			break;
		case 689:
			bowLoudMaster = (clsRainbow)target;
			break;
		case 690:
			sliderLoudEngines = (XSlider)target;
			break;
		case 691:
			bowLoudEngines = (clsRainbow)target;
			break;
		case 692:
			sliderLoudTyres = (XSlider)target;
			break;
		case 693:
			bowLoudTyres = (clsRainbow)target;
			break;
		case 694:
			sliderLoudCrashes = (XSlider)target;
			break;
		case 695:
			bowLoudCrashes = (clsRainbow)target;
			break;
		case 696:
			sliderLoudWind = (XSlider)target;
			break;
		case 697:
			bowLoudWind = (clsRainbow)target;
			break;
		case 698:
			sliderLoudRain = (XSlider)target;
			break;
		case 699:
			bowLoudRain = (clsRainbow)target;
			break;
		case 700:
			sliderLoudInCar = (XSlider)target;
			break;
		case 701:
			bowLoudInCar = (clsRainbow)target;
			break;
		case 702:
			sliderLoudAmbient = (XSlider)target;
			break;
		case 703:
			bowLoudAmbient = (clsRainbow)target;
			break;
		case 704:
			sliderLoudSpotter = (XSlider)target;
			break;
		case 705:
			bowLoudSpotter = (clsRainbow)target;
			break;
		case 706:
			sliderLoudChat = (XSlider)target;
			break;
		case 707:
			bowLoudChat = (clsRainbow)target;
			break;
		case 708:
			sliderLoudReplay = (XSlider)target;
			break;
		case 709:
			bowLoudReplay = (clsRainbow)target;
			break;
		case 710:
			lblEarProtection = (Label)target;
			break;
		case 711:
			cboEarProtection = (ComboBox)target;
			break;
		case 712:
			bowEarProtection = (clsRainbow)target;
			break;
		case 713:
			lblCompressorReplay = (Label)target;
			break;
		case 714:
			cboCompressorReplay = (ComboBox)target;
			break;
		case 715:
			bowCompressorReplay = (clsRainbow)target;
			break;
		case 716:
			lblChatSpeaker = (Label)target;
			break;
		case 717:
			bowChatSpeaker = (clsRainbow)target;
			break;
		case 718:
			lblChatMike = (Label)target;
			break;
		case 719:
			bowChatMike = (clsRainbow)target;
			break;
		case 720:
			chkEnableChat = (CheckBox)target;
			break;
		case 721:
			chkChatMuted = (CheckBox)target;
			break;
		case 722:
			bowEnableChat = (clsRainbow)target;
			break;
		case 723:
			chkChatWhileDriving = (CheckBox)target;
			break;
		case 724:
			bowChatWhileDriving = (clsRainbow)target;
			break;
		case 725:
			lblLFEDevice = (Label)target;
			break;
		case 726:
			bowLFEDevice = (clsRainbow)target;
			break;
		case 727:
			chkEnableLFE = (CheckBox)target;
			break;
		case 728:
			bowEnableLFE = (clsRainbow)target;
			break;
		case 729:
			chk10dbCut = (CheckBox)target;
			break;
		case 730:
			bow10dbCut = (clsRainbow)target;
			break;
		case 731:
			sliderLFEMaster = (XSlider)target;
			break;
		case 732:
			bowLFEMaster = (clsRainbow)target;
			break;
		case 733:
			sliderLFEGame = (XSlider)target;
			break;
		case 734:
			bowLFEGame = (clsRainbow)target;
			break;
		case 735:
			sliderLFEImpact = (XSlider)target;
			break;
		case 736:
			bowLFEImpact = (clsRainbow)target;
			break;
		case 737:
			sliderLFEEngine = (XSlider)target;
			break;
		case 738:
			bowLFEEngine = (clsRainbow)target;
			break;
		case 739:
			sliderLFEGear = (XSlider)target;
			break;
		case 740:
			bowLFEGear = (clsRainbow)target;
			break;
		case 741:
			sliderLFERevLimiter = (XSlider)target;
			break;
		case 742:
			bowLFERevLimiter = (clsRainbow)target;
			break;
		case 743:
			sliderLFERumble = (XSlider)target;
			break;
		case 744:
			bowLFERumble = (clsRainbow)target;
			break;
		case 745:
			sliderLFEWheels = (XSlider)target;
			break;
		case 746:
			bowLFEWheels = (clsRainbow)target;
			break;
		case 747:
			sliderLFERoadTexture = (XSlider)target;
			break;
		case 748:
			bowLFERoadTexture = (clsRainbow)target;
			break;
		case 749:
			sliderLFELowPassFreq = (XSlider)target;
			break;
		case 750:
			bowLFELowPassFreq = (clsRainbow)target;
			break;
		case 751:
			chkUiControlsInDb = (CheckBox)target;
			break;
		case 752:
			bowUiControlsInDb = (clsRainbow)target;
			break;
		case 753:
			tabSpotter = (MetroTabItem)target;
			break;
		case 754:
			lblSpotDevice = (Label)target;
			break;
		case 755:
			bowSpotDevice = (clsRainbow)target;
			break;
		case 756:
			chkSpotEnable = (CheckBox)target;
			break;
		case 757:
			bowSpotEnable = (clsRainbow)target;
			break;
		case 758:
			chkSpotMuteIfLive = (CheckBox)target;
			break;
		case 759:
			bowSpotMuteIfLive = (clsRainbow)target;
			break;
		case 760:
			chkSpotReduceIfLive = (CheckBox)target;
			break;
		case 761:
			bowSpotReduceIfLive = (clsRainbow)target;
			break;
		case 762:
			chkShowSpotterUIForSpectators = (CheckBox)target;
			break;
		case 763:
			bowShowSpotterUIForSpectators = (clsRainbow)target;
			break;
		case 764:
			lblSpotDisplay = (Label)target;
			break;
		case 765:
			cboSpotDisplay = (ComboBox)target;
			break;
		case 766:
			bowSpotDisplay = (clsRainbow)target;
			break;
		case 767:
			lblSpotVoice = (Label)target;
			break;
		case 768:
			cboSpotVoice = (ComboBox)target;
			cboSpotVoice.SelectionChanged += cboSpotVoice_Changed;
			break;
		case 769:
			bowSpotVoice = (clsRainbow)target;
			break;
		case 770:
			lblSpotChatty = (Label)target;
			break;
		case 771:
			cboSpotChatty = (ComboBox)target;
			break;
		case 772:
			bowSpotChatty = (clsRainbow)target;
			break;
		case 773:
			lblHushDuration = (Label)target;
			break;
		case 774:
			numHushDuration = (NumericUpDown)target;
			break;
		case 775:
			bowHushDuration = (clsRainbow)target;
			break;
		case 776:
			chkSpotHiLoStart = (CheckBox)target;
			break;
		case 777:
			bowSpotHiLoStart = (clsRainbow)target;
			break;
		case 778:
			lblSpotHiLowPadding = (Label)target;
			break;
		case 779:
			numSpotHiLowPadding = (NumericUpDown)target;
			break;
		case 780:
			bowSpotHiLowPadding = (clsRainbow)target;
			break;
		case 781:
			chkLeaderChanged = (CheckBox)target;
			break;
		case 782:
			bowLeaderChanged = (clsRainbow)target;
			break;
		case 783:
			chkLeaderTimes = (CheckBox)target;
			break;
		case 784:
			bowLeaderTimes = (clsRainbow)target;
			break;
		case 785:
			chkReportGap = (CheckBox)target;
			break;
		case 786:
			bowReportGap = (clsRainbow)target;
			break;
		case 787:
			chkLappingTraffic = (CheckBox)target;
			break;
		case 788:
			bowLappingTraffic = (clsRainbow)target;
			break;
		case 789:
			chkPitboxCountdown = (CheckBox)target;
			break;
		case 790:
			bowPitboxCountdown = (clsRainbow)target;
			break;
		case 791:
			chkPitNotify = (CheckBox)target;
			break;
		case 792:
			bowPitNotify = (clsRainbow)target;
			break;
		case 793:
			chkReportLaps = (CheckBox)target;
			break;
		case 794:
			chkReportMinute = (CheckBox)target;
			break;
		case 795:
			bowReportLaps = (clsRainbow)target;
			break;
		case 796:
			lblSpotPrecision = (Label)target;
			break;
		case 797:
			numSpotPrecision = (NumericUpDown)target;
			break;
		case 798:
			bowSpotPrecision = (clsRainbow)target;
			break;
		case 799:
			lblSpotReporting = (Label)target;
			break;
		case 800:
			cboSpotReporting = (ComboBox)target;
			break;
		case 801:
			bowSpotReporting = (clsRainbow)target;
			break;
		case 802:
			sliderSPCCTextFactor = (XSlider)target;
			break;
		case 803:
			bowSPCCTextFactor = (clsRainbow)target;
			break;
		case 804:
			chkReportFuel = (CheckBox)target;
			break;
		case 805:
			bowReportFuel = (clsRainbow)target;
			break;
		case 806:
			chkNewClassBest = (CheckBox)target;
			break;
		case 807:
			bowNewClassBest = (clsRainbow)target;
			break;
		case 808:
			chkNewClassBestTime = (CheckBox)target;
			break;
		case 809:
			bowNewClassBestTime = (clsRainbow)target;
			break;
		case 810:
			chkNewPersonalBestRace = (CheckBox)target;
			break;
		case 811:
			bowNewPersonalBestRace = (clsRainbow)target;
			break;
		case 812:
			chkNewPersonalBest = (CheckBox)target;
			break;
		case 813:
			bowNewPersonalBest = (clsRainbow)target;
			break;
		case 814:
			tabNotes = (MetroTabItem)target;
			break;
		case 815:
			txtNotes = (TextBox)target;
			txtNotes.TextChanged += txtNotes_TextChanged;
			break;
		case 816:
			tabPanel = (MetroTabControl)target;
			break;
		case 817:
			panelProfiles = (MetroTabItem)target;
			break;
		case 818:
			barProfiles = (ToolBar)target;
			break;
		case 819:
			gridValues = (Grid)target;
			break;
		case 820:
			tabProfiles = (MetroTabControl)target;
			((UIElement)(object)tabProfiles).DragOver += tabProfiles_DragOver;
			((UIElement)(object)tabProfiles).Drop += tabProfiles_Drop;
			break;
		case 821:
			panelGFXWizard = (MetroTabItem)target;
			break;
		case 822:
			stackGFXWizard = (StackPanel)target;
			break;
		case 823:
			stackGFXCard = (StackPanel)target;
			break;
		case 824:
			barGFXWizard = (ToolBar)target;
			break;
		case 825:
			cboGPUCard = (ComboBox)target;
			cboGPUCard.SelectionChanged += cboGPUCard_SelectionChanged;
			break;
		case 826:
			lblGFXPreset = (Label)target;
			break;
		case 827:
			btnGFXPreset = (Button)target;
			btnGFXPreset.Click += btnGFXPreset_Click;
			break;
		case 828:
			imgGFXPreset = (ImageBrush)target;
			break;
		case 829:
			btnCloseWizard = (Button)target;
			btnCloseWizard.Click += btnCloseWizard_Click;
			break;
		case 830:
			imgCloseWizard = (ImageBrush)target;
			break;
		case 831:
			txtGFXCard = (TextBlock)target;
			break;
		case 832:
			itemsGFXWizard = (ItemsControl)target;
			break;
		case 835:
			panelSearch = (MetroTabItem)target;
			break;
		case 836:
			btnCloseSearch = (Button)target;
			btnCloseSearch.Click += btnCloseSearch_Click;
			break;
		case 837:
			imgCloseSearch = (ImageBrush)target;
			break;
		case 838:
			txtSearch = (TextBox)target;
			txtSearch.KeyDown += txtSearch_KeyDown;
			break;
		case 839:
			btnSearch = (Button)target;
			btnSearch.Click += btnSearch_Click;
			break;
		case 840:
			imgSearch = (ImageBrush)target;
			break;
		case 841:
			lstControls = (ListView)target;
			lstControls.SelectionChanged += lstControls_SelectionChanged;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 833:
			((Button)target).Click += btnDismiss_Click;
			break;
		case 834:
			((Button)target).Click += btnApply_Click;
			break;
		}
	}
}
