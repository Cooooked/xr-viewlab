Sub BackupConfig()
  On Error Resume Next

  Dim fso, shell, userIniDir, userIni, backupIni, source, targetDir, target
  Set fso   = CreateObject("Scripting.FileSystemObject")
  Set shell = CreateObject("WScript.Shell")

  ' Back up the user's live settings from LOCALAPPDATA before any cleanup runs.
  ' This is overwritten on every install/upgrade so the backup always reflects
  ' what was on disk just before this installer touched anything.
  userIniDir = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\XR ViewLab"
  userIni    = userIniDir & "\xr-viewlab.ini"
  backupIni  = userIniDir & "\xr-viewlab.bak.ini"

  If fso.FileExists(userIni) Then
    fso.CopyFile userIni, backupIni, True
  End If

  ' Legacy migration: if the old build stored its INI in Program Files, move it
  ' to LOCALAPPDATA (only runs once; does nothing once the target exists).
  source    = shell.ExpandEnvironmentStrings("%ProgramFiles%") & "\xr-viewlab\xr-viewlab.ini"
  targetDir = userIniDir
  target    = userIni

  If fso.FileExists(source) Then
    If Not fso.FolderExists(targetDir) Then
      fso.CreateFolder(targetDir)
    End If
    If Not fso.FileExists(target) Then
      fso.CopyFile source, target, False
    End If
  End If

  CleanupApiLayerRegistry
End Sub

Sub CleanupApiLayerRegistry()
  On Error Resume Next

  Dim fso, reg, names, types, i, valueName
  Set fso = CreateObject("Scripting.FileSystemObject")
  Set reg = GetObject("winmgmts:{impersonationLevel=impersonate}!\\.\root\default:StdRegProv")
  reg.EnumValues &H80000002, "Software\Khronos\OpenXR\1\ApiLayers\Implicit", names, types
  If IsArray(names) Then
    For i = LBound(names) To UBound(names)
      valueName = names(i)
      If InStr(1, valueName, "XR_APILAYER_cooooked_xrviewlab.json", vbTextCompare) > 0 Then
        If (InStr(1, valueName, "\AppData\Local\Temp\", vbTextCompare) > 0) Or (Not fso.FileExists(valueName)) Then
          reg.DeleteValue &H80000002, "Software\Khronos\OpenXR\1\ApiLayers\Implicit", valueName
        End If
      End If
    Next
  End If
End Sub

' Called on every install and upgrade.
' Strips all visor-overlay keys from the INI and resets mask_enabled=0 in every
' per-app registry profile. Crop values (render height, horizontal, tangents,
' render_scale) are never touched. This means any version of ViewLab — old or new —
' always starts from a known-safe visor state after install.
Sub ResetVisorSettings()
  On Error Resume Next

  Dim shell, fso, iniPath
  Set shell = CreateObject("WScript.Shell")
  Set fso   = CreateObject("Scripting.FileSystemObject")

  iniPath = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\XR ViewLab\xr-viewlab.ini"

  ' --- Strip visor keys from INI ----------------------------------------
  ' These keys control the D3D11 composition-layer overlay. Leaving them at
  ' values written by a newer build can silently break an older build.
  Dim visorKeys
  visorKeys = Array( _
    "mask_enabled", _
    "mask_size", _
    "mask_width_scale", _
    "mask_height_scale", _
    "mask_corner", _
    "visor_technique", _
    "visor_hd", _
    "visor_antialiasing", _
    "visibility_mask_visor", _
    "mask_outer_apex_y", _
    "mask_inner_lower_y", _
    "mask_inner_bridge_width", _
    "mask_inner_bridge_rise", _
    "mask_inner_bridge_peak_x", _
    "mask_inner_bridge_steepness", _
    "mask_offset_x", _
    "mask_offset_y", _
    "mask_top_bias", _
    "mask_bottom_bias", _
    "mask_left_bias", _
    "mask_right_bias", _
    "mask_top_curve", _
    "mask_bottom_curve", _
    "mask_vertical", _
    "mask_horizontal", _
    "mask_rounded" _
  )

  If fso.FileExists(iniPath) Then
    Dim inFile, content, lines, kept(), keptCount, i, j, ln, lnLower, keyLower, skip
    Set inFile = fso.OpenTextFile(iniPath, 1)
    content = inFile.ReadAll()
    inFile.Close

    lines = Split(content, vbNewLine)
    ReDim kept(UBound(lines))
    keptCount = 0

    For i = 0 To UBound(lines)
      ln      = lines(i)
      lnLower = LCase(Trim(ln))
      skip    = False
      For j = 0 To UBound(visorKeys)
        keyLower = LCase(visorKeys(j)) & "="
        If Left(lnLower, Len(keyLower)) = keyLower Then
          skip = True
          Exit For
        End If
      Next
      If Not skip Then
        kept(keptCount) = ln
        keptCount = keptCount + 1
      End If
    Next

    Dim outFile
    Set outFile = fso.OpenTextFile(iniPath, 2, True)
    For i = 0 To keptCount - 1
      If i < keptCount - 1 Then
        outFile.WriteLine kept(i)
      Else
        outFile.Write kept(i)
      End If
    Next
    outFile.Close
  End If

  ' --- Reset visor in every per-app registry profile ---------------------
  ' Only mask_enabled and the new visor_size/width/height keys are touched.
  ' Per-app crop keys (top_tangent, bottom_tangent, horizontal_render_width,
  ' render_scale, profile_enabled, etc.) are left completely alone.
  Dim reg, appNames, n, appKey
  Set reg = GetObject("winmgmts:{impersonationLevel=impersonate}!\\.\root\default:StdRegProv")

  reg.EnumKey &H80000001, "Software\cooooked\xr-viewlab\Apps", appNames
  If IsArray(appNames) Then
    For n = 0 To UBound(appNames)
      appKey = "Software\cooooked\xr-viewlab\Apps\" & appNames(n)
      reg.SetDWORDValue  &H80000001, appKey, "mask_enabled",  0
      reg.DeleteValue    &H80000001, appKey, "visor_size"
      reg.DeleteValue    &H80000001, appKey, "visor_width"
      reg.DeleteValue    &H80000001, appKey, "visor_height"
      reg.DeleteValue    &H80000001, appKey, "mask_outer_apex_y"
      reg.DeleteValue    &H80000001, appKey, "mask_inner_lower_y"
      reg.DeleteValue    &H80000001, appKey, "mask_inner_bridge_width"
      reg.DeleteValue    &H80000001, appKey, "mask_inner_bridge_rise"
      reg.DeleteValue    &H80000001, appKey, "mask_inner_bridge_peak_x"
      reg.DeleteValue    &H80000001, appKey, "mask_inner_bridge_steepness"
    Next
  End If
End Sub
