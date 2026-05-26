# UE Reflect Watch

**Stop manually closing and reopening Unreal Editor every time you add a UPROPERTY.**

UE Reflect Watch monitors your Unreal Engine C++ header files for reflection macro changes and automatically handles the rebuild cycle: closing the editor, rebuilding the project, and relaunching.

Available for both **Visual Studio** and **JetBrains Rider**.

---

## The problem

Every Unreal C++ developer knows this:

1. Add a `UPROPERTY` or `UFUNCTION` to a header file
2. Remember to close the Unreal Editor
3. Build the project
4. Wait
5. Reopen the editor
6. Continue working

Live Coding cannot handle reflection table changes. This is a hard limitation of how Unreal's macro system works. The restart is unavoidable and doing it manually every single time is not.

---

## What it detects

Changes to any of the following macros trigger the rebuild cycle:

- `UCLASS`
- `UPROPERTY`
- `UFUNCTION`
- `USTRUCT`
- `UENUM`

This includes adding, removing, changing specifiers (e.g. `EditAnywhere` to `EditDefaultsOnly`), and renaming or retyping the variable or function the macro is attached to. Changes to function bodies, regular member variables without macros, includes, and comments do not trigger a rebuild.

---

## Downloads

| IDE | Marketplace | Status |
| --- | --- | --- |
| Visual Studio 2022 / 2026 | [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=FredrikWallander.Beta1) | Preview |
| JetBrains Rider | [JetBrains Marketplace](https://plugins.jetbrains.com) | Preview |

---

## Requirements

### Visual Studio extension
- Windows
- Visual Studio 2022 (17.x) or Visual Studio 2026 (18.x)
- Unreal Engine 5.x installed via the Epic Games Launcher
- A solution opened from the root of a `.uproject` folder

### Rider plugin
- Windows or macOS
- JetBrains Rider 2026.1 or later
- Unreal Engine 5.x installed via the Epic Games Launcher
- A solution opened from the root of a `.uproject` folder

---

## Getting started

### Important: run the Initial Project Scan first

If you are adding this tool to an existing project, **run the Initial Project Scan before you start working**. Without it, the tool has no baseline to compare against and the first save of every header file will look like all macros were just added, triggering false positive rebuild prompts.

### Visual Studio

1. Install the extension from the Visual Studio Marketplace or from the `.vsix` file.
2. Open your Unreal project solution in Visual Studio.
3. Go to **Extensions > UE Reflect Watch > Initial Project Scan** and run it once.
4. Right-click the toolbar strip and enable the **UE Reflect Watch** toolbar.
5. Add a `UPROPERTY` to any header file and save. The extension takes it from there.

### Rider

1. Install the plugin via **File > Settings > Plugins > Install Plugin from Disk**, or search for it on the JetBrains Marketplace.
2. Restart Rider.
3. Open your Unreal project solution.
4. Go to **Tools > UE Reflect Watch > Initial Project Scan** and run it once.
5. The **UE Reflect Watch** tool window appears at the bottom of the IDE (alongside Terminal and Run).
6. Add a `UPROPERTY` to any header file and save with `Ctrl+S`. The plugin takes it from there.

---

## Engine path setup

The tool reads the `EngineAssociation` field from your `.uproject` file and constructs the default Epic Games Launcher install path automatically:

- **Windows:** `C:\Program Files\Epic Games\UE_5.x`
- **macOS:** `/Users/Shared/Epic Games/UE_5.x`

If your engine is installed elsewhere, set the path override:

- **Visual Studio:** Tools > Options > UE Reflect Watch > Engine Path Override
- **Rider:** File > Settings > Tools > UE Reflect Watch > Engine Path Override

---

## Building from source

### Visual Studio extension

1. Open `VSExtension/UEReflectWatch.sln` in Visual Studio 2022 or 2026.
2. Build the solution in Release mode.
3. The `.vsix` file is produced in the output folder.
4. Close Visual Studio, double-click the `.vsix` to install, reopen Visual Studio.

### Rider plugin

1. Open `RiderPlugin/` as a project in IntelliJ IDEA.
2. Let Gradle sync (downloads the IntelliJ Platform SDK on first run).
3. Run `.\gradlew buildPlugin`.
4. The `.zip` file is produced in `RiderPlugin/build/distributions/`.
5. In Rider: **File > Settings > Plugins > gear icon > Install Plugin from Disk**.

---

## License

MIT, See [LICENSE.txt](LICENSE.txt).

---

*Currently in beta. Use at your own risk.*