Function PromptDesktopShortcut()
  On Error Resume Next

  If Session.Property("Installed") <> "" Then
    PromptDesktopShortcut = 0
    Exit Function
  End If

  answer = MsgBox("Create a desktop shortcut for XR ViewLab?", vbQuestion + vbYesNo, "XR ViewLab Setup")
  If answer <> vbYes Then
    PromptDesktopShortcut = 0
    Exit Function
  End If

  installDir = Session.TargetPath("INSTALLFOLDER")
  exePath = installDir & "xr-viewlab.exe"
  iconPath = installDir & "xr-viewlab.ico"

  Set shell = CreateObject("WScript.Shell")
  desktop = shell.SpecialFolders("Desktop")
  Set shortcut = shell.CreateShortcut(desktop & "\XR ViewLab.lnk")
  shortcut.TargetPath = exePath
  shortcut.WorkingDirectory = installDir
  shortcut.Description = "Configure OpenXR VR render height and width"
  shortcut.IconLocation = iconPath
  shortcut.Save

  PromptDesktopShortcut = 0
End Function
