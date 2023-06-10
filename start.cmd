@echo off
rd /S /Q .vs > nul 2>&1
rd /S /Q bin > nul 2>&1
rd /S /Q obj > nul 2>&1

dotnet restore --no-cache
dotnet run -c Release
echo Press ENTER to exit.
pause
