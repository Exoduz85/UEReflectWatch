package com.ueReflectWatch.resolver

import com.ueReflectWatch.runner.PlatformAdapter
import com.ueReflectWatch.settings.UEReflectWatchSettings
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import java.io.File

data class UnrealProject(
    val uprojectPath: String,
    val projectName: String,
    val enginePath: String,
    val engineVersion: String,
    val adapter: com.ueReflectWatch.runner.PlatformAdapter
) {
    val buildScriptPath: String get() = adapter.buildScriptPath(enginePath)
    val editorExecutablePath: String get() = adapter.editorExecutablePath(enginePath)
    val buildPlatformArg: String get() = adapter.buildPlatformArg()
}

@Serializable
private data class UprojectJson(
    val EngineAssociation: String = ""
)

object ProjectResolver {

    private val json = Json { ignoreUnknownKeys = true }

    fun resolve(solutionDir: String, adapter: com.ueReflectWatch.runner.PlatformAdapter): UnrealProject? {
        val uprojectFile = findUproject(solutionDir) ?: return null
        return resolveFromUproject(uprojectFile, adapter)
    }

    private fun findUproject(dir: String): File? {
        return File(dir).listFiles()
            ?.firstOrNull { it.extension == "uproject" }
    }

    private fun resolveFromUproject(
        uprojectFile: File,
        adapter: com.ueReflectWatch.runner.PlatformAdapter
    ): UnrealProject? {
        return try {
            val raw = uprojectFile.readText()
            val parsed = json.decodeFromString<UprojectJson>(raw)
            val projectName = uprojectFile.nameWithoutExtension
            val engineVersion = parsed.EngineAssociation

            // User override takes priority.
            val settings = UEReflectWatchSettings.instance
            val overridePath = settings.enginePathOverride
            if (overridePath.isNotBlank() && File(overridePath).exists()) {
                return UnrealProject(
                    uprojectPath = uprojectFile.absolutePath,
                    projectName = projectName,
                    enginePath = overridePath,
                    engineVersion = engineVersion,
                    adapter = adapter
                )
            }

            if (engineVersion.isBlank()) return null

            val defaultPath = adapter.defaultEnginePath(engineVersion)
            if (!File(defaultPath).exists()) return null

            UnrealProject(
                uprojectPath = uprojectFile.absolutePath,
                projectName = projectName,
                enginePath = defaultPath,
                engineVersion = engineVersion,
                adapter = adapter
            )
        } catch (_: Exception) {
            null
        }
    }
}
