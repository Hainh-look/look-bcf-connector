@echo off
title Look BCF Installer for Revit
echo Dang chuan bi cai dat Look BCF...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-LookBcf.ps1"
pause
