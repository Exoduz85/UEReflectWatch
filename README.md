# UE Reflect Watch

A Visual Studio 2022 and 2026 extension that monitors Unreal Engine C++ header files for reflection macro changes (`UCLASS`, `UPROPERTY`, `UFUNCTION`, `USTRUCT`, `UENUM`) and automates the rebuild cycle: closes the Unreal Editor, rebuilds the project, and relaunches the editor.

***Currently in Beta version, use at your own risk, might contain bugs!***

## The problem it solves

When you add or remove any Unreal reflection macro in a header file, you must close the Unreal Editor, do a full build, and reopen the editor before the change takes effect. Live Coding cannot handle reflection table changes. This extension automates that cycle so you never have to do it manually.

## What it does

1. On every `.h` file save, scans for `UCLASS`, `UPROPERTY`, `UFUNCTION`, `USTRUCT`, and `UENUM` macros.
2. Compares against the last saved state (stored in a JSON file in `%LocalAppData%\UEReflectWatch\`).
3. If macros were added, removed, or had their specifiers changed:
   - Shows an optional confirmation dialog: "Have you saved your work in the Unreal Editor?"
   - If auto-rebuild is off, asks: "Rebuild now?"
   - Closes `UnrealEditor.exe`, runs `Build.bat`, and relaunches the editor on success.
4. Streams all build output to a dedicated **UE Reflect Watch** output pane in Visual Studio.

## Requirements

- Windows.
- Visual Studio 2022 (17.x) or Visual Studio 2026 (18.x).
- Unreal Engine 5.x installed via the Epic Games Launcher.
- A solution opened from the root of a `.uproject` folder.

## Installation

### From the Marketplace

Install directly from the Visual Studio Marketplace.

### From source

1. Open `UEReflectWatch.sln` in Visual Studio 2022 or 2026.
2. Build the solution. This produces `UEReflectWatch.vsix` in the output folder.
3. Close Visual Studio.
4. Double-click the `.vsix` file to install it.
5. Reopen Visual Studio.

## First time setup

> **Important:** If you are adding this extension to an existing project, run the Initial Project Scan before you start working. Without it, the extension has no baseline to compare against, and the first save of every `.h` file will look like all macros were just added, triggering false positive rebuild prompts.

### Initial Project Scan

The Initial Project Scan walks all header files in your project's `Source/` folder, records their current macro state, and writes it to the state store. After the scan, the extension has a correct baseline and will only prompt when something actually changes.

**How to run it:**

1. Open your Unreal project solution in Visual Studio.
2. Go to **Extensions > UE Reflect Watch > Initial Project Scan**.
3. If a baseline already exists, the extension will ask whether you want to overwrite it.
4. When the scan completes, a summary dialog shows how many files were scanned and how many macros were found.

You only need to run this once per project. After that, the extension tracks changes automatically on every save.

### Engine path

The extension reads the `EngineAssociation` field from your `.uproject` file (for example `"5.7"`) and constructs the default Epic Games Launcher install path:

```
C:\Program Files\Epic Games\UE_5.7
```

If your engine is installed elsewhere, set the override in **Tools > Options > UE Reflect Watch > Engine Path Override**.

## Toolbar

The extension adds a **UE Reflect Watch** toolbar to Visual Studio. Right-click any empty area on the toolbar strip and tick **UE Reflect Watch** to show it.

| Button | Description |
| --- | --- |
| UE: Rebuild Now | Triggers the full rebuild cycle immediately. Closes the editor, builds, and relaunches. |
| UE: Silent OFF / Silent ON | Toggles silent mode. When ON, macro changes are logged but no prompt appears on save. Use the Rebuild Now button when you are ready to rebuild. |

## Menu

All extension commands are also available under **Extensions > UE Reflect Watch**:

| Command | Description |
| --- | --- |
| Rebuild Now | Triggers the full rebuild cycle immediately. |
| Silent Mode: OFF / Silent Mode: ON | Toggles silent mode. |
| Initial Project Scan | Scans all header files and builds a macro baseline. |

## Options

Access via **Tools > Options > UE Reflect Watch**.

| Option | Default | Description |
| --- | --- | --- |
| Silent Mode | false | When ON, suppresses the automatic prompt on save. Changes are still logged to the output pane. |
| Auto Rebuild | false | Rebuild automatically without prompting when macro changes are detected. |
| Confirm Before Rebuild | true | Show a confirmation dialog asking whether you have saved your work before the rebuild starts. |
| Auto Relaunch Editor | true | Relaunch the Unreal Editor after a successful build. |
| Kill Editor Grace Period (ms) | 2000 | Milliseconds to wait after closing the editor process before starting the build. |
| Engine Path Override | (empty) | Override the engine path. Leave empty to use the default Epic Games Launcher location. |

## Output pane

All build output is streamed to the **UE Reflect Watch** pane in the Visual Studio Output window (**View > Output**, then select "UE Reflect Watch" in the dropdown). This shows the full UnrealBuildTool output including any compiler errors, which is easier to read than the Error List.

## What counts as a change

A rebuild is flagged when any of the following happens in a `.h` file:

- A new `UCLASS()`, `UPROPERTY(...)`, `UFUNCTION(...)`, `USTRUCT()`, or `UENUM()` macro is added.
- An existing macro of those types is removed.
- The specifiers on an existing macro change (e.g. `EditAnywhere` changed to `EditDefaultsOnly`).

Changes to function bodies, regular member variables without `UPROPERTY`, includes, and comments do not trigger a rebuild.

## State storage

Macro state is persisted to:

```
%LocalAppData%\UEReflectWatch\macro-state.json
```

Delete this file to reset the state, or run **Initial Project Scan** again to rebuild it from the current project. All header files will be treated as new on the next save if the file does not exist.

## Limitations

- Windows only. Process management uses the Windows API directly.
- Only monitors files saved through Visual Studio. Files edited externally are not scanned until saved through the IDE.
- The extension kills `UnrealEditor.exe` without asking you to save first. Use the confirmation dialog (enabled by default) to give yourself time to save before the process is terminated.
- If you have multiple Unreal projects in the same solution, the extension uses the first `.uproject` it finds in the solution directory.
- The Settings page under **All Settings > UE Reflect Watch** shows a "not migrated" notice in Visual Studio 2026. This is a Visual Studio 2026 issue affecting all extensions that use the legacy options API. The settings are still fully accessible and functional via **Tools > Options > UE Reflect Watch**.