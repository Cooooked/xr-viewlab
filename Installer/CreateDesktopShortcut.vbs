Function PromptDesktopShortcut()
  On Error Resume Next

  If Session.Property("Installed") <> "" Then
    PromptDesktopShortcut = 0
    Exit Function
  End If

  answer = MsgBox("Create a desktop shortcut for ViewLab?", 292, "ViewLab Setup")
  If answer <> vbYes Then
    PromptDesktopShortcut = 0
    Exit Function
  End If

  installDir = Session.TargetPath("INSTALLFOLDER")
  exePath = installDir & "xr-viewlab.exe"
  iconPath = installDir & "xr-viewlab.ico"

  Set shell = CreateObject("WScript.Shell")
  desktop = shell.SpecialFolders("Desktop")
  Set shortcut = shell.CreateShortcut(desktop & "\ViewLab.lnk")
  shortcut.TargetPath = exePath
  shortcut.WorkingDirectory = installDir
  shortcut.Description = "Configure OpenXR VR render height and width"
  shortcut.IconLocation = iconPath
  shortcut.Save

  PromptDesktopShortcut = 0
End Function

Function CloseRunningXRViewLab()
  On Error Resume Next

  Set shell = CreateObject("WScript.Shell")
  shell.Run "taskkill /IM xr-viewlab.exe /T /F", 0, True

  CloseRunningXRViewLab = 0
End Function

Function PromptLaunchXRViewLab()
  On Error Resume Next

  If Session.Property("Installed") <> "" Then
    PromptLaunchXRViewLab = 0
    Exit Function
  End If

  answer = MsgBox("Open ViewLab now?", vbQuestion + vbYesNo, "ViewLab Setup")
  If answer <> vbYes Then
    PromptLaunchXRViewLab = 0
    Exit Function
  End If

  installDir = Session.TargetPath("INSTALLFOLDER")
  exePath = installDir & "xr-viewlab.exe"

  Set shell = CreateObject("WScript.Shell")
  shell.Run Chr(34) & exePath & Chr(34), 1, False

  PromptLaunchXRViewLab = 0
End Function
