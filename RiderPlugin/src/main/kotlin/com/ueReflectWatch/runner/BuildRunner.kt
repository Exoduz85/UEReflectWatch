package com.ueReflectWatch.runner

import com.intellij.openapi.diagnostic.Logger
import com.ueReflectWatch.resolver.UnrealProject
import com.ueReflectWatch.settings.UEReflectWatchSettings
import java.io.File

private val LOG = Logger.getInstance("UEReflectWatch.BuildRunner")

enum class BuildResult { Succeeded, Failed }

data class BuildCycleResult(
        val result: BuildResult,
        val output: List<String>
)

object BuildRunner {

    fun runCycle(project: UnrealProject, onOutput: (String) -> Unit): BuildCycleResult {
        val settings = UEReflectWatchSettings.instance
        val output = mutableListOf<String>()

        fun log(line: String) {
            output.add(line)
            onOutput(line)
            LOG.info(line)
        }

        log("")
        log("=== UE Reflect Watch: Rebuild Cycle Started ===")
        log("Project : ${project.projectName}")
        log("Engine  : ${project.enginePath}")
        log("")

        // Step 1: Kill the editor.
        log("[1/3] Closing Unreal Editor...")
        try {
            project.adapter.killEditor()
            log("      Editor closed. Waiting ${settings.killEditorGracePeriodMs}ms...")
            Thread.sleep(settings.killEditorGracePeriodMs.toLong())
        } catch (e: Exception) {
            log("      Editor was not running or could not be closed: ${e.message}")
        }

        // Step 2: Build.
        log("[2/3] Building...")
        val buildResult = build(project, ::log)

        if (buildResult == BuildResult.Failed) {
            log("")
            log("=== Build FAILED. Editor will not be relaunched. ===")
            return BuildCycleResult(BuildResult.Failed, output)
        }

        log("")
        log("[3/3] Build succeeded.")

        // Step 3: Relaunch editor.
        if (settings.autoRelaunchEditor) {
            log("      Launching Unreal Editor...")
            try {
                project.adapter.launchEditor(project.enginePath, project.uprojectPath)
                log("      Editor launched.")
            } catch (e: Exception) {
                log("      Failed to launch editor: ${e.message}")
            }
        } else {
            log("      Auto-relaunch is disabled. Open the editor manually.")
        }

        log("")
        log("=== UE Reflect Watch: Rebuild Cycle Complete ===")

        return BuildCycleResult(BuildResult.Succeeded, output)
    }

    private fun build(
            project: UnrealProject,
            log: (String) -> Unit
    ): BuildResult {
        val args = listOf(
                project.buildScriptPath,
                "${project.projectName}Editor",
                project.buildPlatformArg,
                "Development",
                "-Project=${project.uprojectPath}",
                "-WaitMutex"
        )

        log("      ${args.joinToString(" ")}")
        log("")

        return try {
            val process = ProcessBuilder(args)
                    .redirectErrorStream(true)
                    .directory(File(project.enginePath))
                    .start()

            process.inputStream.bufferedReader().forEachLine { line ->
                log("      $line")
            }

            process.waitFor()

            if (process.exitValue() == 0) BuildResult.Succeeded else BuildResult.Failed
        } catch (e: Exception) {
            log("      Process error: ${e.message}")
            BuildResult.Failed
        }
    }
}