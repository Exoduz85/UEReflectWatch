package com.ueReflectWatch.resolver

import com.ueReflectWatch.scanner.MacroScanner
import com.ueReflectWatch.store.StateStore
import java.io.File

data class ProjectScanResult(
    val filesScanned: Int,
    val filesWithMacros: Int,
    val totalMacros: Int,
    val errors: List<String>
)

object ProjectScanner {

    private val EXCLUDED_FOLDERS = setOf(
        "Binaries", "Intermediate", "DerivedDataCache",
        "Saved", "Plugins", ".vs", ".git"
    )

    fun scanAndSeed(projectRootDir: String, stateStore: StateStore): ProjectScanResult {
        var filesScanned = 0
        var filesWithMacros = 0
        var totalMacros = 0
        val errors = mutableListOf<String>()

        val sourceDir = File(projectRootDir, "Source")
            .takeIf { it.exists() } ?: File(projectRootDir)

        val headerFiles = findHeaderFiles(sourceDir, errors)

        for (file in headerFiles) {
            try {
                val content = file.readText()
                val macros = MacroScanner.scan(content)
                stateStore.setMacros(file.absolutePath, macros)
                filesScanned++
                if (macros.isNotEmpty()) {
                    filesWithMacros++
                    totalMacros += macros.size
                }
            } catch (e: Exception) {
                errors.add("${file.name}: ${e.message}")
            }
        }

        return ProjectScanResult(filesScanned, filesWithMacros, totalMacros, errors)
    }

    private fun findHeaderFiles(dir: File, errors: MutableList<String>): List<File> {
        val results = mutableListOf<File>()

        try {
            dir.listFiles()?.forEach { entry ->
                when {
                    entry.isFile && entry.extension == "h" -> results.add(entry)
                    entry.isDirectory && entry.name !in EXCLUDED_FOLDERS ->
                        results.addAll(findHeaderFiles(entry, errors))
                }
            }
        } catch (e: Exception) {
            errors.add("Could not read directory ${dir.path}: ${e.message}")
        }

        return results
    }
}
