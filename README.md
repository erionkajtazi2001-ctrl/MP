# Magic Panel (Phase 1 build)

Standalone Windows trading-panel calculator: four instrument tabs (NQ, MNQ, GC, MGC),
saved risk settings, candle-high/low sizing, separate 3-tick stop-loss.
This build has NO NinjaTrader connection yet and sends NO orders (see roadmap Phase 5).

## Get the installer (no tools needed on your PC)
1. Create a free GitHub account and a new private repository.
2. Upload the contents of this folder (including the hidden `.github` folder) and commit to `main`.
3. Open the repo's **Actions** tab, wait for "Build Windows installer" to finish (about 3-5 minutes).
4. Open the finished run, download the **MagicPanel-Setup** artifact, unzip it, double-click `MagicPanel-Setup.exe`.

## Or build it on a Windows PC
Install the .NET 8 SDK and Inno Setup 6, then run `./build.ps1` in PowerShell.
Output: `installer-output\MagicPanel-Setup.exe`.

## Notes
- Settings and the calculation log live in `%AppData%\MagicPanel\`.
- Enter prices with `.` as the decimal separator and no thousands separators (25000.50, not 25,000.50).
- The installer is unsigned, so Windows SmartScreen may say "unknown publisher". Choose "More info" > "Run anyway".
