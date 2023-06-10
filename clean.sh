#! /bin/bash

find . -name bin -type d -exec rm -rf {} \; &> /dev/null
find . -name obj -type d -exec rm -rf {} \; &> /dev/null
find . -name .vs -type d -exec rm -rf {} \; &> /dev/null
find . -name .vscode -type d -exec rm -rf {} \; &> /dev/null
