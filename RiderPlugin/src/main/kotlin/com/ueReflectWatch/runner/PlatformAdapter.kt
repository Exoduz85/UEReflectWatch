package com.ueReflectWatch.runner

import java.io.File

// Platform abstraction so the same plugin binary handles Windows and Mac.
interface PlatformAdapter {
    fun defaultEnginePath(version: String): String
    fun buildScriptPath(enginePath: String): String
    fun editorExecutablePath(enginePath: String): String
    fun buildPlatformArg(): String
    fun killEditor()
    fun launchEditor(enginePath: String, uprojectPath: String)
}

class WindowsAdapter : PlatformAdapter {

    override fun defaultEnginePath(version: String): String =
        "C:\\Program Files\\Epic Games\\UE_$version"

    override fun buildScriptPath(enginePath: String): String =
        "$enginePath\\Engine\\Build\\BatchFiles\\Build.bat"

    override fun editorExecutablePath(enginePath: String): String =
        "$enginePath\\Engine\\Binaries\\Win64\\UnrealEditor.exe"

    override fun buildPlatformArg(): String = "Win64"

    override fun killEditor() {
        ProcessBuilder("taskkill", "/IM", "UnrealEditor.exe", "/F")
            .redirectErrorStream(true)
            .start()
            .waitFor()
    }

    override fun launchEditor(enginePath: String, uprojectPath: String) {
        val exe = editorExecutablePath(enginePath)
        ProcessBuilder(exe, uprojectPath)
            .start()
    }
}

class MacAdapter : PlatformAdapter {

    override fun defaultEnginePath(version: String): String =
        "/Users/Shared/Epic Games/UE_$version"

    override fun buildScriptPath(enginePath: String): String =
        "$enginePath/Engine/Build/BatchFiles/Build.sh"

    override fun editorExecutablePath(enginePath: String): String =
        "$enginePath/Engine/Binaries/Mac/UnrealEditor"

    override fun buildPlatformArg(): String = "Mac"

    override fun killEditor() {
        ProcessBuilder("pkill", "-x", "UnrealEditor")
            .redirectErrorStream(true)
            .start()
            .waitFor()
    }

    override fun launchEditor(enginePath: String, uprojectPath: String) {
        val exe = editorExecutablePath(enginePath)
        ProcessBuilder(exe, uprojectPath)
            .start()
    }
}

// Linux adapter included for completeness.
class LinuxAdapter : PlatformAdapter {

    override fun defaultEnginePath(version: String): String =
        "${System.getProperty("user.home")}/UnrealEngine"

    override fun buildScriptPath(enginePath: String): String =
        "$enginePath/Engine/Build/BatchFiles/Build.sh"

    override fun editorExecutablePath(enginePath: String): String =
        "$enginePath/Engine/Binaries/Linux/UnrealEditor"

    override fun buildPlatformArg(): String = "Linux"

    override fun killEditor() {
        ProcessBuilder("pkill", "-x", "UnrealEditor")
            .redirectErrorStream(true)
            .start()
            .waitFor()
    }

    override fun launchEditor(enginePath: String, uprojectPath: String) {
        val exe = editorExecutablePath(enginePath)
        ProcessBuilder(exe, uprojectPath)
            .start()
    }
}

object PlatformAdapterFactory {
    fun create(): PlatformAdapter {
        val os = System.getProperty("os.name").lowercase()
        return when {
            os.contains("win") -> WindowsAdapter()
            os.contains("mac") -> MacAdapter()
            else -> LinuxAdapter()
        }
    }
}
