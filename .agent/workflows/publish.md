---
description: how to build official releases for Linux and Windows
---

This workflow guide explains how to generate self-contained, single-file executables for JustLauncher. These builds do not require the user to have .NET installed.

### 🐧 Build for Linux (x64)
Run this command from the project root:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o ./publish/linux
```

### 🪟 Build for Windows (x64)
Run this command from the project root:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o ./publish/windows
```

### 📋 Flag Explanations
- `-c Release`: Optimizes the code for performance.
- `-r [runtime]`: Specifies the target platform (linux-x64 or win-x64).
- `--self-contained true`: Includes the .NET runtime in the executable.
- `-p:PublishSingleFile=true`: Bundles everything into one executable file.
- `-p:PublishReadyToRun=true`: Compiles to native code for faster startup.
- `-o [folder]`: Where to save the output.
