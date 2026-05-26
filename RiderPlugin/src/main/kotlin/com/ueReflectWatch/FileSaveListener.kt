package com.ueReflectWatch

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.diagnostic.Logger
import com.intellij.openapi.editor.Document
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileDocumentManagerListener
import com.intellij.openapi.project.Project
import com.intellij.openapi.ui.Messages
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.openapi.vfs.newvfs.BulkFileListener
import com.intellij.openapi.vfs.newvfs.events.VFileContentChangeEvent
import com.intellij.openapi.vfs.newvfs.events.VFileEvent
import com.intellij.openapi.wm.ToolWindowManager
import com.ueReflectWatch.runner.BuildResult
import com.ueReflectWatch.runner.BuildRunner
import com.ueReflectWatch.runner.PlatformAdapterFactory
import com.ueReflectWatch.resolver.ProjectResolver
import com.ueReflectWatch.resolver.UnrealProject
import com.ueReflectWatch.scanner.MacroScanner
import com.ueReflectWatch.settings.UEReflectWatchSettings
import com.ueReflectWatch.store.StateStore
import com.ueReflectWatch.toolwindow.UEReflectWatchConsole
import java.util.concurrent.Executors

private val LOG = Logger.getInstance("UEReflectWatch.FileSaveListener")

class FileSaveListener(private val project: Project) : BulkFileListener, FileDocumentManagerListener {

    private val stateStore = StateStore()
    private val adapter = PlatformAdapterFactory.create()
    private val executor = Executors.newSingleThreadExecutor()
    private var cachedProject: UnrealProject? = null

    init {
        val bus = project.messageBus.connect()
        // VFS listener: catches external file changes.
        bus.subscribe(com.intellij.openapi.vfs.VirtualFileManager.VFS_CHANGES, this)
        // Document listener: catches Ctrl+S reliably every time.
        bus.subscribe(FileDocumentManagerListener.TOPIC, this)
        resolveProject()
    }

    private fun resolveProject() {
        val basePath = project.basePath ?: return
        cachedProject = ProjectResolver.resolve(basePath, adapter)
        if (cachedProject != null) {
            log("Activated for project: ${cachedProject!!.projectName}")
            log("Engine  : ${cachedProject!!.enginePath}")
        } else {
            logError("No .uproject found or engine path could not be resolved.")
            logError("Set the Engine Path Override in Settings > Tools > UE Reflect Watch.")
        }
    }

    // --- FileDocumentManagerListener: fires on every Ctrl+S ---

    override fun beforeDocumentSaving(document: Document) {
        val file = FileDocumentManager.getInstance().getFile(document) ?: return
        if (file.extension != "h") return
        processFile(file, document.text)
    }

    // --- BulkFileListener: catches external/VFS-level saves ---

    override fun after(events: MutableList<out VFileEvent>) {
        for (event in events) {
            if (event !is VFileContentChangeEvent) continue
            val file = event.file
            if (file.extension != "h") continue
            // Skip files already handled by beforeDocumentSaving (open in editor).
            if (FileDocumentManager.getInstance().getCachedDocument(file) != null) continue
            val content = try {
                String(file.contentsToByteArray())
            } catch (_: Exception) {
                continue
            }
            processFile(file, content)
        }
    }

    private fun processFile(file: VirtualFile, content: String) {
        val filePath = file.path
        val currentMacros = MacroScanner.scan(content)
        val previousMacros = stateStore.getMacros(filePath)
        val diff = MacroScanner.diff(previousMacros, currentMacros)

        stateStore.setMacros(filePath, currentMacros)

        if (!diff.hasChanges) return

        val fileName = file.name
        val summary = MacroScanner.summariseDiff(diff, fileName)

        log("Reflection macro change detected:")
        log("  $summary")
        diff.added.forEach { log("  + Line ${it.line}: ${it.raw}") }
        diff.removed.forEach { log("  - Line ${it.line}: ${it.raw}") }

        val settings = UEReflectWatchSettings.instance
        if (settings.silentMode) {
            log("Silent mode is ON. Skipping prompt. Use Rebuild Now when ready.")
            return
        }

        ApplicationManager.getApplication().invokeLater {
            triggerRebuild(summary)
        }
    }

    fun triggerRebuild(reason: String) {
        val basePath = project.basePath
        if (basePath != null) {
            cachedProject = ProjectResolver.resolve(basePath, adapter)
        }

        val unrealProject = cachedProject ?: run {
            logError("No .uproject found or engine path not resolved.")
            logError("Set the Engine Path Override in Settings > Tools > UE Reflect Watch.")
            Messages.showWarningDialog(
                project,
                "No .uproject found or engine path not resolved.\n\nSet the Engine Path Override in Settings > Tools > UE Reflect Watch.",
                "UE Reflect Watch: Cannot Rebuild"
            )
            return
        }

        val settings = UEReflectWatchSettings.instance

        if (settings.confirmBeforeRebuild) {
            val confirmed = Messages.showYesNoDialog(
                project,
                "The Unreal Editor will be closed and the project will be rebuilt.\n\n$reason\n\nHave you saved your work in the Unreal Editor?",
                "UE Reflect Watch: Confirm Rebuild",
                Messages.getWarningIcon()
            )
            if (confirmed != Messages.YES) {
                log("Rebuild cancelled by user.")
                return
            }
        }

        if (!settings.autoRebuild) {
            val rebuild = Messages.showYesNoDialog(
                project,
                "$reason\n\nRebuild now?",
                "UE Reflect Watch",
                Messages.getQuestionIcon()
            )
            if (rebuild != Messages.YES) {
                log("Rebuild deferred by user.")
                return
            }
        }

        ApplicationManager.getApplication().invokeLater {
            ToolWindowManager.getInstance(project)
                .getToolWindow("UE Reflect Watch")
                ?.show()
        }

        executor.submit {
            val result = BuildRunner.runCycle(unrealProject) { line ->
                logRaw(line)
            }

            ApplicationManager.getApplication().invokeLater {
                if (result.result == BuildResult.Succeeded) {
                    UEReflectWatchConsole.logSuccess(project, "=== Build succeeded. ===")
                    if (settings.autoRelaunchEditor) {
                        UEReflectWatchConsole.logSuccess(project, "Unreal Editor is relaunching.")
                    }
                } else {
                    UEReflectWatchConsole.logError(project, "=== Build FAILED. See output above. ===")
                    Messages.showErrorDialog(
                        project,
                        "Build failed for ${unrealProject.projectName}.\n\nSee the UE Reflect Watch tool window for details.",
                        "UE Reflect Watch: Build Failed"
                    )
                }
            }
        }
    }

    fun getStateStore(): StateStore = stateStore
    fun getCachedProject(): UnrealProject? = cachedProject

    private fun log(message: String) {
        LOG.info("[UEReflectWatch] $message")
        UEReflectWatchConsole.log(project, message)
    }

    private fun logError(message: String) {
        LOG.warn("[UEReflectWatch] $message")
        UEReflectWatchConsole.logError(project, message)
    }

    private fun logRaw(message: String) {
        LOG.info("[UEReflectWatch] $message")
        UEReflectWatchConsole.log(project, message)
    }
}