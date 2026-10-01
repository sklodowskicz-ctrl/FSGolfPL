@echo off
where dotnet >nul 2>nul || (echo Zainstaluj .NET 8 SDK. & pause & exit /b 1)
dotnet publish FSGolfPL.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
pause
