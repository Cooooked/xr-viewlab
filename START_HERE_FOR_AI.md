# Start Here

You are in the correct XR ViewLab source tree if your current folder is:

`F:\AI-Projects\ViewLab`

The goal file is:

`F:\AI-Projects\ViewLab\PROJECT_GOAL.md`

Read that first, update it as you work, then build from `F:\AI-Projects\ViewLab`.

Do not reference or depend on files outside `F:\AI-Projects\ViewLab`.

After any implementation change, run the full installer build unless explicitly told not to:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

Final replies must include the exact runnable MSI path, including file name, in a plain text block suitable
for Windows Run. Do not give only the folder path.

Current latest built installer:

```text
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi
```
