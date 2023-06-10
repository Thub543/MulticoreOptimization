#! /bin/bash

find . -name bin -type d -exec rm -rf {} \; &> /dev/null
find . -name obj -type d -exec rm -rf {} \; &> /dev/null
find . -name .vs -type d -exec rm -rf {} \; &> /dev/null
find . -name .vscode -type d -exec rm -rf {} \; &> /dev/null

dotnet restore --no-cache
dotnet run -c Release
read -p "Press ENTER to exit."
