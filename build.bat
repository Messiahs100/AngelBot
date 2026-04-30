@echo off
echo Baue AngelBot...
cd /d "%~dp0AngelBot"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -o "..\Ausgabe"
echo.
echo Kopiere Infodaten...
if exist "..\..\..\Infodaten" (
    xcopy /E /I /Y "..\..\..\Infodaten" "..\Ausgabe\Infodaten"
) else (
    mkdir "..\Ausgabe\Infodaten"
    echo Bitte Infodaten-Ordner manuell kopieren!
)
echo.
echo Fertig! Ausgabe in: %~dp0Ausgabe\
pause
