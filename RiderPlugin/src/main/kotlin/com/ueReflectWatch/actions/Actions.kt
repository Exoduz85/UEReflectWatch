package com.ueReflectWatch.actions

import com.intellij.openapi.actionSystem.ActionUpdateThread
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.ui.Messages
import com.ueReflectWatch.FileSaveListener
import com.ueReflectWatch.resolver.ProjectScanner
import com.ueReflectWatch.settings.UEReflectWatchSettings
import com.ueReflectWatch.toolwindow.UEReflectWatchConsole

class RebuildNowAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val listener = project.getService(FileSaveListener::class.java) ?: return
        listener.triggerRebuild("Manual rebuild requested.")
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}

class ToggleSilentModeAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val settings = UEReflectWatchSettings.instance
        settings.silentMode = !settings.silentMode
        updatePresentation(e)
    }

    override fun update(e: AnActionEvent) {
        updatePresentation(e)
    }

    private fun updatePresentation(e: AnActionEvent) {
        val settings = UEReflectWatchSettings.instance
        val isOn = settings.silentMode
        val actionId = e.actionManager.getId(this) ?: ""
        e.presentation.text = when {
            actionId.contains("Toolbar") -> if (isOn) "UE: Silent ON" else "UE: Silent OFF"
            else -> if (isOn) "Silent Mode: ON" else "Silent Mode: OFF"
        }
        // Swap icon to reflect current state.
        val iconPath = if (isOn) "/icons/silentModeOn.svg" else "/icons/silentModeOff.svg"
        e.presentation.icon = com.intellij.openapi.util.IconLoader.getIcon(iconPath, javaClass)
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}

class InitialScanAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val listener = project.getService(FileSaveListener::class.java) ?: return
        val stateStore = listener.getStateStore()
        val basePath = project.basePath ?: return

        val existingFiles = stateStore.getAllFiles()
        if (existingFiles.isNotEmpty()) {
            val overwrite = Messages.showYesNoDialog(
                project,
                "UE Reflect Watch already has a baseline for ${existingFiles.size} file(s).\n\n" +
                        "Running an initial scan will replace the existing baseline with the current state of all header files.\n\n" +
                        "This will not trigger a rebuild. Any macro changes made after the scan will be detected normally on the next save.\n\n" +
                        "Continue?",
                "UE Reflect Watch: Initial Project Scan",
                Messages.getQuestionIcon()
            )
            if (overwrite != Messages.YES) return
        }

        val result = ProjectScanner.scanAndSeed(basePath, stateStore)

        val errorNote = if (result.errors.isNotEmpty())
            "\n\n${result.errors.size} file(s) could not be read:\n${result.errors.joinToString("\n")}"
        else ""

        UEReflectWatchConsole.log(project,
            "Initial scan complete: ${result.filesScanned} files, " +
                    "${result.filesWithMacros} with macros, ${result.totalMacros} total macros.")

        Messages.showInfoMessage(
            project,
            "Initial scan complete.\n\n" +
                    "Files scanned: ${result.filesScanned}\n" +
                    "Files with macros: ${result.filesWithMacros}\n" +
                    "Total macros found: ${result.totalMacros}\n\n" +
                    "The extension now has a baseline for your project. " +
                    "Macro changes will be detected correctly from the next save onwards." +
                    errorNote,
            "UE Reflect Watch: Initial Scan Complete"
        )
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}

class ClearLogAction : AnAction() {

    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        UEReflectWatchConsole.clear(project)
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }

    override fun getActionUpdateThread(): ActionUpdateThread = ActionUpdateThread.BGT
}