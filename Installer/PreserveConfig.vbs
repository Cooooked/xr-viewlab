Sub BackupConfig()
  On Error Resume Next

  Dim fso, shell, source, targetDir, target
  Set fso = CreateObject("Scripting.FileSystemObject")
  Set shell = CreateObject("WScript.Shell")

  source = shell.ExpandEnvironmentStrings("%ProgramFiles%") & "\xr-viewlab\xr-viewlab.ini"
  targetDir = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\XR ViewLab"
  target = targetDir & "\xr-viewlab.ini"

  If fso.FileExists(source) Then
    If Not fso.FolderExists(targetDir) Then
      fso.CreateFolder(targetDir)
    End If

    If Not fso.FileExists(target) Then
      fso.CopyFile source, target, False
    End If
  End If
End Sub
