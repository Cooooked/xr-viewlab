using System.Collections.Generic;

namespace irSidekickProfiles;

public static class iniApp
{
	private static Dictionary<string, iniSetting> Settings = new Dictionary<string, iniSetting>();

	public static iniSetting browserWindowedXPos = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Graphics DX11",
		Name = "browserWindowedXPos"
	};

	public static iniSetting browserWindowedYPos = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Graphics DX11",
		Name = "browserWindowedYPos"
	};

	public static iniSetting browserWindowedWidth = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Graphics DX11",
		Name = "browserWindowedWidth"
	};

	public static iniSetting browserWindowedHeight = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Graphics DX11",
		Name = "browserWindowedHeight"
	};

	public static iniSetting EnableTicker = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Overlay",
		Name = "EnableTicker",
		Comment = "set to 1 to turn on ticker when session UI is disabled"
	};

	public static iniSetting earProtection = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "earProtection",
		Comment = "Ear protection or helmet audio effect (0-4)"
	};

	public static iniSetting compressorReplay = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "compressorReplay",
		Comment = "Dynamic range compressor, makes the replay louder if enabled and XAudio2 is used (0-2)"
	};

	public static iniSetting UiControlsInDb = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "soundUiControlsInDb",
		Comment = "Volume controls in Sound Settings are shown in dB units (1) or 0-100 (0)"
	};

	public static iniSetting askToSaveOnQuit = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Replay",
		Name = "askToSaveOnQuit",
		Comment = "To prevent exit withou saving the replay"
	};

	public static iniSetting PauseReplayOnExit = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Replay",
		Name = "pauseReplayOnExit",
		Comment = "Pause replay when exiting your car"
	};

	public static iniSetting autoResetFastRepair = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Pit Service",
		Name = "autoResetFastRepair",
		Comment = "Automatically request fast repair service once your vehicle exits pit road"
	};

	public static iniSetting autoResetPitBox = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Pit Service",
		Name = "autoResetPitBox",
		Comment = "Automatically request full pit service once your vehicle exits pit road"
	};

	public static iniSetting autoFuelDefaultEnable = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Pit Service",
		Name = "autoFuelDefaultEnable",
		Comment = "1 - Autofuel will be enabled by default, 0 - Autofuel will remain disabled"
	};

	public static iniSetting autoFuelDefaultMarginLaps = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Pit Service",
		Name = "autoFuelDefaultMarginLaps",
		ValueIsDouble = true,
		Comment = "# of laps of safety margin to use by default for Autofuel"
	};

	public static iniSetting showSysMessagesWhileDriving = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "showSysMessagesWhileDriving",
		Comment = "Show system messages while driving"
	};

	public static iniSetting showUserMessagesWhileDriving = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "showUserMessagesWhileDriving",
		Comment = "Show user chat messages while driving"
	};

	public static iniSetting showIncidentMessagesWhileDriving = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "showIncidentMessagesWhileDriving",
		Comment = "Show Incident messages while driving"
	};

	public static iniSetting showJoinLeave = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "showJoinLeave",
		Comment = "Show player join/leave messages"
	};

	public static iniSetting irsdkEnableMem = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "irsdkEnableMem",
		Comment = "enable memory based telemetry"
	};

	public static iniSetting irsdkAutoLogDisk = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Misc",
		Name = "irsdkAutoLogDisk",
		Comment = "Automatically log disk telemetry when you enter your car, this can fill up your disk!"
	};

	public static iniSetting ShowSpotterUIForSpectators = new iniSetting
	{
		Config = iniConfig.App,
		Section = "spectator",
		Name = "ShowSpotterUIForSpectators",
		Comment = "Present the Start/Stop spotting functionality if you are a Spectator in the session."
	};

	public static iniSetting blackBox = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Drive Screen",
		Name = "blackBox",
		Comment = "Which drive black box to display, -1 = hide"
	};

	public static iniSetting blackBoxPitStop = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Drive Screen",
		Name = "blackBoxPitStop",
		Comment = "Which pit stop black box to display"
	};

	public static iniSetting comparisonLapFileName = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SplitsDeltas",
		Name = "comparisonLapFileName",
		Comment = "User specified split delta file used for comparison"
	};

	public static iniSetting disableAtRaceStart = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SplitsDeltas",
		Name = "disableAtRaceStart",
		Comment = "If 1 disable the split time at start of race, you can manually enable it again later."
	};

	public static iniSetting fadeGhostCarWhenClose = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SplitsDeltas",
		Name = "fadeGhostCarWhenClose",
		Comment = "If 1 then increase the ghost car transparency as you drive near it."
	};

	public static iniSetting ghostCarOffsetSec = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SplitsDeltas",
		Name = "ghostCarOffsetSec",
		ValueIsDouble = true,
		Comment = "How many seconds to offset the ghost car by."
	};

	public static iniSetting ghostCarTransp = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SplitsDeltas",
		Name = "ghostCarTransp",
		ValueIsDouble = true,
		Comment = "Set level of transparency for ghost car (reference lap car)."
	};

	public static iniSetting cockpitLookInstant = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "cockpitLookInstant",
		Comment = "does digital look left/right/up/down switch instantly, or transition smoothly?"
	};

	public static iniSetting drivingCamFOV = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "drivingCamFOV",
		ValueIsDouble = true,
		Comment = "driving camera field of view"
	};

	public static iniSetting driverHeightAdj = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "driverHeightAdj",
		ValueIsDouble = true,
		Comment = "Range -0.050m to 0.050m  (approx. +/- 2 in.)"
	};

	public static iniSetting DrivingVanishY = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "DrivingVanishY",
		ValueIsDouble = true,
		Comment = "Shift the driving view up/down to make it easier to see the dash."
	};

	public static iniSetting cockpitLookAngle = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "cockpitLookAngle",
		ValueIsDouble = true,
		Comment = "Angle in degrees to rotate head when looking left/right"
	};

	public static iniSetting cockpitLookUpAngle = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "cockpitLookUpAngle",
		ValueIsDouble = true,
		Comment = "Angle in degrees to tilt head when looking up"
	};

	public static iniSetting cockpitLookDownAngle = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "cockpitLookDownAngle",
		ValueIsDouble = true,
		Comment = "Angle in degrees to tilt head when looking down"
	};

	public static iniSetting vidCaptureEnable = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Video",
		Name = "vidCaptureEnable",
		Comment = "Set to 0 to disable loading of video capture module"
	};

	public static iniSetting screenshotFileFormat = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Video",
		Name = "screenshotFileFormat",
		Comment = "Screenshot file format, 0 = png, 1 = jpg, 2 = bmp"
	};

	public static iniSetting videoFileFrmt = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Video",
		Name = "videoFileFrmt",
		Comment = "Video encoder container, 0 = mp4, 1 = wmv, 2 = avi2, 3 = avi"
	};

	public static iniSetting videoFramerate = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Video",
		Name = "videoFramerate",
		Comment = "Video framerate, 0 = 60 fps, 1 = 30 fps"
	};

	public static iniSetting videoImgSize = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Video",
		Name = "videoImgSize",
		Comment = "Video max dimensions, 0 = auto, 1=1920x1080, 2=1280x720, 3=854x480"
	};

	public static iniSetting highContrastCursor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "highContrastCursor",
		Comment = "Set to 1 to use a high contrast mouse cursor"
	};

	public static iniSetting pitLineAlwaysVisible = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "pitLineAlwaysVisible",
		Comment = "Force the pitline to always be visible, not just when there is a pit exit line rule in place"
	};

	public static iniSetting raceLineWidth = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "raceLineWidth",
		Comment = "Width of the race line in meters"
	};

	public static iniSetting pitlineColor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "pitlineColor"
	};

	public static iniSetting racelineFastColor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "racelineFastColor"
	};

	public static iniSetting racelineSameColor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "racelineSameColor"
	};

	public static iniSetting racelineSlowColor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Adaptive",
		Name = "racelineSlowColor"
	};

	public static iniSetting autoForceFactor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "autoForceFactor",
		ValueIsDouble = true,
		Comment = "Controls how weak or strong the auto force system sets the wheel strength."
	};

	public static iniSetting smoothingFilterType = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "smoothingFilterType",
		Comment = "0-slew rate limited filter or 1-boxcar filter."
	};

	public static iniSetting loadTrueForceAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "loadTrueForceAPI",
		Comment = "Enable TrueForce API in Logitech TrueForce wheels"
	};

	public static iniSetting enableTrueForceVibe = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "enableTrueForceVibe",
		Comment = "Enable TrueForce style audio vibrations in some wheels"
	};

	public static iniSetting trueForceVibePhysics = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "trueForceVibePhysics",
		Comment = "1 - use physics to generate vibrations, 0 - use game audio with TrueForce style wheels"
	};

	public static iniSetting trueForceDamper = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "trueForceDamperPct",
		Comment = "Add a bit of internal damping to the wheel"
	};

	public static iniSetting volTrueForcePhysMaster = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForceMaster_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the TrueForce style vibration effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysCarBody = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysCarBodyAccel_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the car body acceleration TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysDriveShaft = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysDriveShaft_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the drive shaft TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysEngineRPM = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysEngineRPM_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the engine rpm TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysGearChange = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysGearChange_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the gear change TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysRevLimit = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysRevLimit_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the rev limit TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysRoadTexture = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysRoadTexture_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the road texture TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysRumbleStrip = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysRumbleStrip_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the rumble strip TrueForce style effects, in Decibels"
	};

	public static iniSetting volTrueForcePhysWheelSlip = new iniSetting
	{
		Config = iniConfig.App,
		Section = "TrueForce",
		Name = "volTrueForcePhysWheelSlip_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the wheel slip TrueForce style effects, in Decibels"
	};

	public static iniSetting loadAsetekAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadAsetekAPI",
		Comment = "Enable Asetek pedal API"
	};

	public static iniSetting loadConspitAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadConspitAPI",
		Comment = "Enable Conspit pedal API"
	};

	public static iniSetting loadFanatecAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadFanatecAPI",
		Comment = "Enable Fanatec pedal API"
	};

	public static iniSetting loadMozaAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadMozaAPI",
		Comment = "Enable Moza pedal API"
	};

	public static iniSetting loadSimagicAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadSimagicAPI",
		Comment = "Enable Simagic wheel API"
	};

	public static iniSetting loadSimucubeAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadSimucubeAPI",
		Comment = "Enable Simucube wheel API"
	};

	public static iniSetting loadVRSAPI = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "loadVRSAPI",
		Comment = "Enable VRS API with 360hz"
	};

	public static iniSetting enableFFB360HzInterpolated = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "enableFFB360HzInterpolated",
		Comment = "Enable FFB 360hz via interpolation"
	};

	public static iniSetting EnableWheelDisplay = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "enableWheelDisplay",
		Comment = "Enable the use of steering wheel displays"
	};

	public static iniSetting enableWheelDisplayBlink = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "enableWheelDisplayBlink",
		Comment = "Enable the display lights to blink when at the rev limit"
	};

	public static iniSetting clutchLaunchMode = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "clutchLaunchMode",
		Comment = "0 - release launch control to activate, 1 - press and hold launch control to activate"
	};

	public static iniSetting VibratePedal = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Force Feedback",
		Name = "joyEnableVibratePedal",
		Comment = "Enables wheel vibration when pedals vibrate"
	};

	public static iniSetting AutoChatStr1 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr1",
		Comment = "Auto chat message, use $ at the end to auto transmit without hitting enter"
	};

	public static iniSetting AutoChatStr2 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr2",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr3 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr3",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr4 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr4",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr5 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr5",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr6 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr6",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr7 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr7",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr8 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr8",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr9 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr9",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr10 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr10",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr11 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr11",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr12 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr12",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr13 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr13",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr14 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr14",
		Comment = "Auto chat message"
	};

	public static iniSetting AutoChatStr15 = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Autochat Messages",
		Name = "AutoChatStr15",
		Comment = "Auto chat message"
	};

	public static iniSetting radioScriptsEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "radioScriptsEnabled",
		Comment = "Run startup, start/stop driving, and start/stop spotting radio scripts in documents/iracing/scripts/radio"
	};

	public static iniSetting devSPCCId = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "devSPCCId",
		Comment = "Identifier for the selected sound device"
	};

	public static iniSetting devSPCCName = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "devSPCCName",
		Comment = "Name of the sound device"
	};

	public static iniSetting dimensions = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "dimensions",
		Comment = "1 = mono, 2 = stereo, 3 = surround"
	};

	public static iniSetting voiceChatNotificationStyle = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "voiceChatNotificationStyle",
		Comment = "Voice chat notification style"
	};

	public static iniSetting ambientMusicDisabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "ambientMusicDisabled",
		Comment = "Disable music playing in the environment (PA speakers, etc)"
	};

	public static iniSetting rotateWithHeadset = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "rotateWithHeadset",
		Comment = "0 = no, 1 = rotate microphone with VR headset movement"
	};

	public static iniSetting masterVolumedB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "masterVolumedB",
		ValueIsDouble = true,
		Comment = "Master volume adjustment in dB, range is -40 dB to 0 dB"
	};

	public static iniSetting loudnessEngine = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessEngine",
		ValueIsDouble = true,
		Comment = "Volume adjustment for engines in dB"
	};

	public static iniSetting loudnessTires = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessTires",
		ValueIsDouble = true,
		Comment = "Volume adjustment for tires in dB"
	};

	public static iniSetting loudnessCrash = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessCrash",
		ValueIsDouble = true,
		Comment = "Volume adjustment for scrapes and crashes in dB"
	};

	public static iniSetting loudnessWind = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessWind",
		ValueIsDouble = true,
		Comment = "Volume adjustment for wind in dB"
	};

	public static iniSetting loudnessRain = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessRain",
		ValueIsDouble = true,
		Comment = "Volume adjustment for rain water in dB"
	};

	public static iniSetting loudnessIncar = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessIncar",
		ValueIsDouble = true,
		Comment = "Volume adjustment for incar sounds in dB"
	};

	public static iniSetting loudnessAmbient = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessAmbient",
		ValueIsDouble = true,
		Comment = "Volume adjustment for ambient noise in dB"
	};

	public static iniSetting loudnessSPCC = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessSPCC",
		ValueIsDouble = true,
		Comment = "Volume adjustment for spotter noise in dB"
	};

	public static iniSetting loudnessVoiceChat = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessVoiceChat",
		ValueIsDouble = true,
		Comment = "Volume adjustment for voice chat noise in dB"
	};

	public static iniSetting loudnessReplay = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessReplay",
		ValueIsDouble = true,
		Comment = "Volume adjustment for overall replay volume versus driving volume in dB"
	};

	public static iniSetting voiceChatEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "voiceChatEnabled",
		Comment = "Enable or Disable Voice Chat."
	};

	public static iniSetting voiceChatMuted = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "voiceChatMuted",
		Comment = "Mute voice chat - overrides voiceChatVolume."
	};

	public static iniSetting loudnessLFE = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "loudnessLFE",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of all LFE bass shaker effects, in Decibels"
	};

	public static iniSetting voiceChatEnabledWhileDriving = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "voiceChatEnabledWhileDriving",
		Comment = "Enable voice chat while driving."
	};

	public static iniSetting downshiftProtectionAlert = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Audio",
		Name = "downshiftProtectionNoiseEnabled",
		Comment = "Play a warning noise when a downshift is not allowed."
	};

	public static iniSetting LFEEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "LFEEnabled",
		Comment = "Enable or Disable LFE bass shaker effect"
	};

	public static iniSetting enableLFEBKAmpCut = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "enableLFEBKAmpCut",
		Comment = "cut output by -10 dB to avoide compressor in ButtKicker BKA-PLUS amp"
	};

	public static iniSetting volGameMaster_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volGameMaster_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the game audio bass shaker effect, in Decibels"
	};

	public static iniSetting volPhysCarBodyAccel_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysCarBodyAccel_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the car body acceleration bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysEngineRPM_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysEngineRPM_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the engine rpm bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysGearChange_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysGearChange_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the gear change bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysRevLimit_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysRevLimit_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the rev limit bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysRumbleStrip_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysRumbleStrip_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the rumble strip bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysWheelSlip_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysWheelSlip_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the wheel slip bass shaker effects, in Decibels"
	};

	public static iniSetting volPhysRoadTextures_dB = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "volPhysRoadTextures_dB",
		ValueIsDouble = true,
		Comment = "How much to duck or raise volume of the road texture bass shaker effects, in Decibels"
	};

	public static iniSetting freqGameLowpass_Hz = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "freqGameLowpass_Hz",
		ValueIsDouble = true,
		Comment = "Set the cuttoff frequency in Hz of the game audio lowpass filter"
	};

	public static iniSetting freqGameHighpass_Hz = new iniSetting
	{
		Config = iniConfig.App,
		Section = "BassShaker",
		Name = "freqGameHighpass_Hz",
		ValueIsDouble = true,
		Comment = "Set the cuttoff frequency in Hz of the physics audio highpass filter or -1 to disable"
	};

	public static iniSetting serverTransmitMaxCars = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Graphics",
		Name = "serverTransmitMaxCars",
		Comment = "Limit number of cars transmitted to client (values from 10 to 64)"
	};

	public static iniSetting virtualMirrorFOV = new iniSetting
	{
		Config = iniConfig.App,
		Section = "View",
		Name = "virtualMirrorFOV",
		Comment = "virtual mirror field of view"
	};

	public static iniSetting enabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "enabled",
		Comment = "Is the spotter enabled at all?"
	};

	public static iniSetting muteSpotterIfLive = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "muteSpotterIfLive",
		Comment = "Mute your spotter if you have a live spotter already spotting for you"
	};

	public static iniSetting carLowHiAtStart = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "carLowHiAtStart",
		Comment = "If true enable car low_high calls as soon as green flag is out"
	};

	public static iniSetting carLowHiPadding = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "carLowHiPadding",
		Comment = "How much clearance, front and back in meters, to give a car before reporting it as clear"
	};

	public static iniSetting display = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "display"
	};

	public static iniSetting voicePack = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "voicePack",
		Comment = "Voice pack for spotter, leave blank for default spotter"
	};

	public static iniSetting verbosity = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "verbosity",
		Comment = "How chatty is the spotter?"
	};

	public static iniSetting reduceVerbosityIfLive = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reduceVerbosityIfLive",
		Comment = "Reduce the spotters chattiness if you have a live spotter already spotting for you"
	};

	public static iniSetting reportFuelData = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportFuelData",
		Comment = "Give reminder to put in laps for fuel data, and a notice when data has been collected"
	};

	public static iniSetting reportLapsEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsEnabled",
		Comment = "Call out player lap times every lap"
	};

	public static iniSetting reportLapsMode_n = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsMode_n",
		Comment = "Spotter calls out lap times, 0 - time, 1 - avg speed"
	};

	public static iniSetting reportLapsPrecision = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsPrecision",
		Comment = "How much precision to call out lap times with"
	};

	public static iniSetting reportLapsMinute = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsMinute",
		Comment = "Call out the minute when calling the time"
	};

	public static iniSetting textDurationFactor = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "textDurationFactor",
		ValueIsDouble = true,
		Comment = "Multiplyer to extend duration that spotter text is shown"
	};

	public static iniSetting hushDuration = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "hushDuration",
		Comment = "How many seconds to shut up for when the spotter is told to stop talking"
	};

	public static iniSetting reportNewLeaderEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportNewLeaderEnabled",
		Comment = "Report when the race has a new leader"
	};

	public static iniSetting reportLapsLeaderLapTimeEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsLeaderLapTimeEnabled",
		Comment = "Occasionally report lap time of in-class leader"
	};

	public static iniSetting reportGainingLosingEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportGainingLosingEnabled",
		Comment = "Report when the car in front or behind for position is getting closer/further"
	};

	public static iniSetting reportFasterCarBehindEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportFasterCarBehindEnabled",
		Comment = "Report when a faster class car is about to pass"
	};

	public static iniSetting reportPitboxCount = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportPitboxCount",
		Comment = "Enable Pit Box Countdown"
	};

	public static iniSetting reportCompetitorPitEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportCompetitorPitEnabled",
		Comment = "Report when the car in front/behind for position is pitting"
	};

	public static iniSetting reportLapsNewBestEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsNewBestEnabled",
		Comment = "Report when new in-class best lap is set"
	};

	public static iniSetting reportLapsNewBestLapTimeEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsNewBestLapTimeEnabled",
		Comment = "Report lap time of in-class best lap of session"
	};

	public static iniSetting reportLapsNewPersonalBestEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsNewPersonalBestEnabled",
		Comment = "Report when new personal best lap is set in practice/testing/time attack/warmup sessions"
	};

	public static iniSetting reportLapsNewPersonalBestInRaceEnabled = new iniSetting
	{
		Config = iniConfig.App,
		Section = "SPCC",
		Name = "reportLapsNewPersonalBestInRaceEnabled",
		Comment = "Report when new personal best lap is set in a race session"
	};

	public static iniSetting playSecondsFromReplayEndOnCarExitNonTeam = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Replay",
		Name = "playSecondsFromReplayEndOnCarExitNonTeam",
		Comment = "Number of seconds to back up from the end when exiting the car in a non-team session (0=min,60=max)"
	};

	public static iniSetting playSecondsFromReplayEndOnCarExitTeamEvt = new iniSetting
	{
		Config = iniConfig.App,
		Section = "Replay",
		Name = "playSecondsFromReplayEndOnCarExitTeamEvt",
		Comment = "Number of seconds to back up from the end when exiting the car in a team session (0=min,60=max)"
	};

	public static iniSetting xWidth = new iniSetting
	{
		Config = iniConfig.App,
		Section = "XXX Dev Use Only",
		Name = "xWidth",
		Comment = "Screenshot width, must be multiple of 4*xTilesAcross!"
	};

	public static iniSetting xHeight = new iniSetting
	{
		Config = iniConfig.App,
		Section = "XXX Dev Use Only",
		Name = "xHeight",
		Comment = "Screenshot height"
	};

	public static iniSetting xTilesAcross = new iniSetting
	{
		Config = iniConfig.App,
		Section = "XXX Dev Use Only",
		Name = "xTilesAcross",
		Comment = "mosaic"
	};

	private static void BuildDictionary()
	{
		browserWindowedXPos.DictionaryAdd(Settings);
		browserWindowedYPos.DictionaryAdd(Settings);
		browserWindowedWidth.DictionaryAdd(Settings);
		browserWindowedHeight.DictionaryAdd(Settings);
		earProtection.DictionaryAdd(Settings);
		compressorReplay.DictionaryAdd(Settings);
		UiControlsInDb.DictionaryAdd(Settings);
		ShowSpotterUIForSpectators.DictionaryAdd(Settings);
		EnableTicker.DictionaryAdd(Settings);
		irsdkEnableMem.DictionaryAdd(Settings);
		irsdkAutoLogDisk.DictionaryAdd(Settings);
		askToSaveOnQuit.DictionaryAdd(Settings);
		PauseReplayOnExit.DictionaryAdd(Settings);
		autoResetFastRepair.DictionaryAdd(Settings);
		autoResetPitBox.DictionaryAdd(Settings);
		autoFuelDefaultEnable.DictionaryAdd(Settings);
		autoFuelDefaultMarginLaps.DictionaryAdd(Settings);
		showUserMessagesWhileDriving.DictionaryAdd(Settings);
		showSysMessagesWhileDriving.DictionaryAdd(Settings);
		showIncidentMessagesWhileDriving.DictionaryAdd(Settings);
		showJoinLeave.DictionaryAdd(Settings);
		blackBox.DictionaryAdd(Settings);
		blackBoxPitStop.DictionaryAdd(Settings);
		comparisonLapFileName.DictionaryAdd(Settings);
		disableAtRaceStart.DictionaryAdd(Settings);
		fadeGhostCarWhenClose.DictionaryAdd(Settings);
		ghostCarOffsetSec.DictionaryAdd(Settings);
		ghostCarTransp.DictionaryAdd(Settings);
		drivingCamFOV.DictionaryAdd(Settings);
		driverHeightAdj.DictionaryAdd(Settings);
		DrivingVanishY.DictionaryAdd(Settings);
		cockpitLookInstant.DictionaryAdd(Settings);
		cockpitLookAngle.DictionaryAdd(Settings);
		cockpitLookUpAngle.DictionaryAdd(Settings);
		cockpitLookDownAngle.DictionaryAdd(Settings);
		vidCaptureEnable.DictionaryAdd(Settings);
		screenshotFileFormat.DictionaryAdd(Settings);
		xWidth.DictionaryAdd(Settings);
		xHeight.DictionaryAdd(Settings);
		videoFileFrmt.DictionaryAdd(Settings);
		videoFramerate.DictionaryAdd(Settings);
		videoImgSize.DictionaryAdd(Settings);
		highContrastCursor.DictionaryAdd(Settings);
		pitLineAlwaysVisible.DictionaryAdd(Settings);
		raceLineWidth.DictionaryAdd(Settings);
		pitlineColor.DictionaryAdd(Settings);
		racelineFastColor.DictionaryAdd(Settings);
		racelineSameColor.DictionaryAdd(Settings);
		racelineSlowColor.DictionaryAdd(Settings);
		loadTrueForceAPI.DictionaryAdd(Settings);
		enableTrueForceVibe.DictionaryAdd(Settings);
		trueForceVibePhysics.DictionaryAdd(Settings);
		trueForceDamper.DictionaryAdd(Settings);
		volTrueForcePhysMaster.DictionaryAdd(Settings);
		volTrueForcePhysCarBody.DictionaryAdd(Settings);
		volTrueForcePhysDriveShaft.DictionaryAdd(Settings);
		volTrueForcePhysEngineRPM.DictionaryAdd(Settings);
		volTrueForcePhysGearChange.DictionaryAdd(Settings);
		volTrueForcePhysRevLimit.DictionaryAdd(Settings);
		volTrueForcePhysRoadTexture.DictionaryAdd(Settings);
		volTrueForcePhysRumbleStrip.DictionaryAdd(Settings);
		volTrueForcePhysWheelSlip.DictionaryAdd(Settings);
		loadConspitAPI.DictionaryAdd(Settings);
		loadFanatecAPI.DictionaryAdd(Settings);
		loadMozaAPI.DictionaryAdd(Settings);
		loadSimagicAPI.DictionaryAdd(Settings);
		loadSimucubeAPI.DictionaryAdd(Settings);
		loadVRSAPI.DictionaryAdd(Settings);
		autoForceFactor.DictionaryAdd(Settings);
		smoothingFilterType.DictionaryAdd(Settings);
		enableFFB360HzInterpolated.DictionaryAdd(Settings);
		EnableWheelDisplay.DictionaryAdd(Settings);
		enableWheelDisplayBlink.DictionaryAdd(Settings);
		clutchLaunchMode.DictionaryAdd(Settings);
		VibratePedal.DictionaryAdd(Settings);
		AutoChatStr1.DictionaryAdd(Settings);
		AutoChatStr2.DictionaryAdd(Settings);
		AutoChatStr3.DictionaryAdd(Settings);
		AutoChatStr4.DictionaryAdd(Settings);
		AutoChatStr5.DictionaryAdd(Settings);
		AutoChatStr6.DictionaryAdd(Settings);
		AutoChatStr7.DictionaryAdd(Settings);
		AutoChatStr8.DictionaryAdd(Settings);
		AutoChatStr9.DictionaryAdd(Settings);
		AutoChatStr10.DictionaryAdd(Settings);
		AutoChatStr11.DictionaryAdd(Settings);
		AutoChatStr12.DictionaryAdd(Settings);
		AutoChatStr13.DictionaryAdd(Settings);
		AutoChatStr14.DictionaryAdd(Settings);
		AutoChatStr15.DictionaryAdd(Settings);
		radioScriptsEnabled.DictionaryAdd(Settings);
		devSPCCId.DictionaryAdd(Settings);
		devSPCCName.DictionaryAdd(Settings);
		dimensions.DictionaryAdd(Settings);
		voiceChatNotificationStyle.DictionaryAdd(Settings);
		ambientMusicDisabled.DictionaryAdd(Settings);
		rotateWithHeadset.DictionaryAdd(Settings);
		masterVolumedB.DictionaryAdd(Settings);
		loudnessEngine.DictionaryAdd(Settings);
		loudnessTires.DictionaryAdd(Settings);
		loudnessCrash.DictionaryAdd(Settings);
		loudnessWind.DictionaryAdd(Settings);
		loudnessRain.DictionaryAdd(Settings);
		loudnessIncar.DictionaryAdd(Settings);
		loudnessAmbient.DictionaryAdd(Settings);
		loudnessSPCC.DictionaryAdd(Settings);
		loudnessVoiceChat.DictionaryAdd(Settings);
		loudnessReplay.DictionaryAdd(Settings);
		voiceChatEnabled.DictionaryAdd(Settings);
		voiceChatMuted.DictionaryAdd(Settings);
		voiceChatEnabledWhileDriving.DictionaryAdd(Settings);
		downshiftProtectionAlert.DictionaryAdd(Settings);
		LFEEnabled.DictionaryAdd(Settings);
		enableLFEBKAmpCut.DictionaryAdd(Settings);
		loudnessLFE.DictionaryAdd(Settings);
		volGameMaster_dB.DictionaryAdd(Settings);
		volPhysCarBodyAccel_dB.DictionaryAdd(Settings);
		volPhysEngineRPM_dB.DictionaryAdd(Settings);
		volPhysGearChange_dB.DictionaryAdd(Settings);
		volPhysRevLimit_dB.DictionaryAdd(Settings);
		volPhysRumbleStrip_dB.DictionaryAdd(Settings);
		volPhysWheelSlip_dB.DictionaryAdd(Settings);
		volPhysRoadTextures_dB.DictionaryAdd(Settings);
		freqGameLowpass_Hz.DictionaryAdd(Settings);
		serverTransmitMaxCars.DictionaryAdd(Settings);
		virtualMirrorFOV.DictionaryAdd(Settings);
		enabled.DictionaryAdd(Settings);
		muteSpotterIfLive.DictionaryAdd(Settings);
		carLowHiAtStart.DictionaryAdd(Settings);
		carLowHiPadding.DictionaryAdd(Settings);
		display.DictionaryAdd(Settings);
		voicePack.DictionaryAdd(Settings);
		verbosity.DictionaryAdd(Settings);
		reduceVerbosityIfLive.DictionaryAdd(Settings);
		reportLapsEnabled.DictionaryAdd(Settings);
		reportLapsMode_n.DictionaryAdd(Settings);
		reportLapsPrecision.DictionaryAdd(Settings);
		reportLapsMinute.DictionaryAdd(Settings);
		textDurationFactor.DictionaryAdd(Settings);
		hushDuration.DictionaryAdd(Settings);
		reportNewLeaderEnabled.DictionaryAdd(Settings);
		reportLapsLeaderLapTimeEnabled.DictionaryAdd(Settings);
		reportGainingLosingEnabled.DictionaryAdd(Settings);
		reportFasterCarBehindEnabled.DictionaryAdd(Settings);
		reportPitboxCount.DictionaryAdd(Settings);
		reportCompetitorPitEnabled.DictionaryAdd(Settings);
		reportLapsNewBestEnabled.DictionaryAdd(Settings);
		reportLapsNewBestLapTimeEnabled.DictionaryAdd(Settings);
		reportLapsNewPersonalBestEnabled.DictionaryAdd(Settings);
		reportLapsNewPersonalBestInRaceEnabled.DictionaryAdd(Settings);
		playSecondsFromReplayEndOnCarExitNonTeam.DictionaryAdd(Settings);
		playSecondsFromReplayEndOnCarExitTeamEvt.DictionaryAdd(Settings);
		xWidth.DictionaryAdd(Settings);
		xHeight.DictionaryAdd(Settings);
		xTilesAcross.DictionaryAdd(Settings);
	}

	public static iniSetting Find(string tag, bool NullIfNotFound = true)
	{
		if (Settings.Values.Count == 0)
		{
			BuildDictionary();
		}
		if (Settings.ContainsKey(tag))
		{
			return Settings[tag];
		}
		if (NullIfNotFound)
		{
			return null;
		}
		return new iniSetting(tag);
	}

	public static iniSetting Find(string tag, string comment)
	{
		if (Settings.Values.Count == 0)
		{
			BuildDictionary();
		}
		if (Settings.ContainsKey(tag))
		{
			return Settings[tag];
		}
		iniSetting iniSetting2 = new iniSetting(tag, comment);
		if (Settings.Values.Count > 0)
		{
			iniSetting2.DictionaryAdd(Settings);
		}
		return iniSetting2;
	}
}
