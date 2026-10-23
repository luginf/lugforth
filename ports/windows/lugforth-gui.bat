@echo off
rem Opens the Lugforth console window (SDL2). The Windows console that comes with it is minimized.
cd /d "%~dp0"
start "" /min lugforth.exe --gui
