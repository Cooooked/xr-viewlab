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
