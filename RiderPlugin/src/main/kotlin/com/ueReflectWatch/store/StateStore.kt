package com.ueReflectWatch.store

import com.ueReflectWatch.scanner.MacroEntry
import com.ueReflectWatch.scanner.MacroKind
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.io.File
import java.nio.file.Files
import java.nio.file.Paths

@Serializable
data class SerializableMacro(
    val kind: String,
    val line: Int,
    val raw: String,
    val declarationLine: String = ""
)

@Serializable
data class FileMacroState(
    val filePath: String,
    val lastScanned: String,
    val macros: List<SerializableMacro>
)

class StateStore(storageDir: String? = null) {

    private val storeFile: File
    private val state: MutableMap<String, FileMacroState> = mutableMapOf()

    private val json = Json { prettyPrint = true; ignoreUnknownKeys = true }

    init {
        val dir = storageDir ?: defaultStorageDir()
        Files.createDirectories(Paths.get(dir))
        storeFile = File(dir, "macro-state.json")
        load()
    }

    private fun defaultStorageDir(): String {
        val os = System.getProperty("os.name").lowercase()
        return when {
            os.contains("win") ->
                "${System.getenv("LOCALAPPDATA")}\\UEReflectWatch"
            os.contains("mac") ->
                "${System.getProperty("user.home")}/Library/Application Support/UEReflectWatch"
            else ->
                "${System.getProperty("user.home")}/.config/UEReflectWatch"
        }
    }

    private fun load() {
        try {
            if (!storeFile.exists()) return
            val raw = storeFile.readText()
            val loaded = json.decodeFromString<Map<String, FileMacroState>>(raw)
            state.putAll(loaded)
        } catch (_: Exception) {
            state.clear()
        }
    }

    private fun save() {
        try {
            storeFile.writeText(json.encodeToString(state))
        } catch (e: Exception) {
            System.err.println("[UEReflectWatch] Failed to save state: ${e.message}")
        }
    }

    fun getMacros(filePath: String): List<MacroEntry> {
        return state[filePath]?.macros?.mapNotNull { m ->
            runCatching { MacroEntry(MacroKind.valueOf(m.kind), m.line, m.raw, m.declarationLine) }.getOrNull()
        } ?: emptyList()
    }

    fun setMacros(filePath: String, macros: List<MacroEntry>) {
        state[filePath] = FileMacroState(
            filePath = filePath,
            lastScanned = java.time.Instant.now().toString(),
            macros = macros.map { SerializableMacro(it.kind.name, it.line, it.raw, it.declarationLine) }
        )
        save()
    }

    fun removeFile(filePath: String) {
        state.remove(filePath)
        save()
    }

    fun clearAll() {
        state.clear()
        save()
    }

    fun getAllFiles(): List<String> = state.keys.toList()

    fun getFullState(): Map<String, FileMacroState> = state.toMap()
}
