package com.ueReflectWatch.toolwindow

import com.intellij.execution.filters.TextConsoleBuilderFactory
import com.intellij.execution.ui.ConsoleView
import com.intellij.execution.ui.ConsoleViewContentType
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import com.intellij.ui.content.ContentFactory
import java.time.LocalTime
import java.time.format.DateTimeFormatter

class UEReflectWatchToolWindowFactory : ToolWindowFactory {
    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val console = UEReflectWatchConsole.getOrCreate(project)
        val content = ContentFactory.getInstance()
            .createContent(console.component, "", false)
        toolWindow.contentManager.addContent(content)
    }
}

object UEReflectWatchConsole {

    private val consoles = mutableMapOf<Project, ConsoleView>()
    private val timeFormatter = DateTimeFormatter.ofPattern("HH:mm:ss")

    fun getOrCreate(project: Project): ConsoleView {
        return consoles.getOrPut(project) {
            TextConsoleBuilderFactory.getInstance()
                .createBuilder(project)
                .console
        }
    }

    fun log(project: Project, message: String) {
        val console = getOrCreate(project)
        val time = LocalTime.now().format(timeFormatter)
        console.print("[$time] $message\n", ConsoleViewContentType.NORMAL_OUTPUT)
    }

    fun logError(project: Project, message: String) {
        val console = getOrCreate(project)
        val time = LocalTime.now().format(timeFormatter)
        console.print("[$time] ERROR: $message\n", ConsoleViewContentType.ERROR_OUTPUT)
    }

    fun logSuccess(project: Project, message: String) {
        val console = getOrCreate(project)
        val time = LocalTime.now().format(timeFormatter)
        console.print("[$time] $message\n", ConsoleViewContentType.LOG_INFO_OUTPUT)
    }

    fun clear(project: Project) {
        consoles[project]?.clear()
    }

    fun remove(project: Project) {
        consoles.remove(project)
    }
}