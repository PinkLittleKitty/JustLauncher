# JustLauncher

> A fast, lightweight, and modern cross-platform Minecraft launcher built with .NET 8 and Avalonia UI.

![JustLauncher](Assets/logo.png)

## Features

- **Cross-Platform**: Runs natively on Windows, Linux, and macOS.
- **Modern UI**: Clean, responsive interface powered by Avalonia UI.
- **Instance Management**: Create and manage multiple isolated Minecraft instances with custom settings.
- **Smart Java Management**:
  - Automatically detects installed Java versions.
  - **Auto-downloads** missing Java runtimes (e.g., Java 8, 17, 21) required for specific Minecraft versions.
  - Per-instance Java version and memory configuration.
- **Account Support**: 
  - Offline Mode (Crack).
  - Microsoft Account Login (OAuth).
- **Update System**: Automatic update checking and in-app changelog viewer.
- **Customization**:
  - Light/Dark Mode.
  - "Saki Mode" assistant.
  - Custom themes and backgrounds.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) is required to build the project.

## Getting Started

### Cloning the Repository

```bash
git clone https://github.com/JustNeki/JustLauncher.git
cd JustLauncher
```

### Running in Development

To run the application directly from source:

```bash
dotnet restore
dotnet run
```

## Building for Production

### Linux (Single-File Executable)

To create a standalone executable that doesn't require .NET runtime to be installed on the target machine:

```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

The output binary will be located at:
`bin/Release/net8.0/linux-x64/publish/JustLauncher`

### Windows

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The output binary will be located at:
`bin/Release/net8.0/win-x64/publish/JustLauncher.exe`

## Usage

1.  **Accounts**: Go to the Accounts page to log in with Microsoft or create an offline profile.
2.  **Instances**: Click "+" to create a new Minecraft installation. Select version, loader (Vanilla/Fabric/Forge support planned), and memory settings.
3.  **Settings**: Configure global Java preferences, theme, and update behavior in the Settings page.
4.  **Play**: Select an instance and click Play. JustLauncher will handle asset downloading, Java verification, and game launching.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

[MIT License](LICENSE)
