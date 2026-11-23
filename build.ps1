# Run from the MagicPanel folder in PowerShell. Needs: .NET 8 SDK + Inno Setup 6.
$ErrorActionPreference = "Stop"
dotnet test tests/MagicPanel.Tests/MagicPanel.Tests.csproj -c Release
dotnet publish src/MagicPanel/MagicPanel.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\MagicPanel.iss
Write-Host "`nInstaller: installer-output\MagicPanel-Setup.exe"
