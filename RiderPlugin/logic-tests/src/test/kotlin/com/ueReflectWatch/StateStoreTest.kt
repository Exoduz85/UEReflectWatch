package com.ueReflectWatch

import com.ueReflectWatch.scanner.MacroEntry
import com.ueReflectWatch.scanner.MacroKind
import com.ueReflectWatch.store.StateStore
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.io.TempDir
import java.nio.file.Path

class StateStoreTest {

    private fun makeStore(dir: Path): StateStore = StateStore(dir.toString())

    // -----------------------------------------------------------------------
    // Basic get and set
    // -----------------------------------------------------------------------

    @Test
    fun `getMacros returns empty list for unknown file`(@TempDir dir: Path) {
        val store = makeStore(dir)
        assertTrue(store.getMacros("/some/unknown/file.h").isEmpty())
    }

    @Test
    fun `setMacros then getMacros returns same macros`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val macros = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
        )
        store.setMacros("/my/file.h", macros)
        val retrieved = store.getMacros("/my/file.h")

        assertEquals(2, retrieved.size)
        assertEquals(MacroKind.UPROPERTY, retrieved[0].kind)
        assertEquals(MacroKind.UFUNCTION, retrieved[1].kind)
    }

    @Test
    fun `setMacros empty list is stored and retrieved as empty`(@TempDir dir: Path) {
        val store = makeStore(dir)
        store.setMacros("/my/file.h", emptyList())
        assertTrue(store.getMacros("/my/file.h").isEmpty())
    }

    // -----------------------------------------------------------------------
    // Persistence across instances
    // -----------------------------------------------------------------------

    @Test
    fun `macros survive creating a new StateStore instance from same directory`(@TempDir dir: Path) {
        val macros = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))

        val store1 = makeStore(dir)
        store1.setMacros("/my/file.h", macros)

        // Create a second instance pointing to the same directory.
        val store2 = makeStore(dir)
        val retrieved = store2.getMacros("/my/file.h")

        assertEquals(1, retrieved.size)
        assertEquals(MacroKind.UPROPERTY, retrieved[0].kind)
        assertEquals("UPROPERTY(EditAnywhere)", retrieved[0].raw)
    }

    // -----------------------------------------------------------------------
    // removeFile and clearAll
    // -----------------------------------------------------------------------

    @Test
    fun `removeFile removes only that file`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val macros = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        store.setMacros("/file/a.h", macros)
        store.setMacros("/file/b.h", macros)

        store.removeFile("/file/a.h")

        assertTrue(store.getMacros("/file/a.h").isEmpty())
        assertEquals(1, store.getMacros("/file/b.h").size)
    }

    @Test
    fun `clearAll removes all files`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val macros = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        store.setMacros("/file/a.h", macros)
        store.setMacros("/file/b.h", macros)
        store.setMacros("/file/c.h", macros)

        store.clearAll()

        assertTrue(store.getAllFiles().isEmpty())
    }

    // -----------------------------------------------------------------------
    // getAllFiles
    // -----------------------------------------------------------------------

    @Test
    fun `getAllFiles returns all tracked file paths`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val macros = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        store.setMacros("/file/a.h", macros)
        store.setMacros("/file/b.h", macros)

        val files = store.getAllFiles()

        assertEquals(2, files.size)
        assertTrue(files.contains("/file/a.h"))
        assertTrue(files.contains("/file/b.h"))
    }

    @Test
    fun `getAllFiles returns empty list when store is empty`(@TempDir dir: Path) {
        val store = makeStore(dir)
        assertTrue(store.getAllFiles().isEmpty())
    }

    // -----------------------------------------------------------------------
    // MacroEntry round-trip fidelity
    // -----------------------------------------------------------------------

    @Test
    fun `all macro kinds survive serialization round trip`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val macros = MacroKind.values().mapIndexed { index, kind ->
            MacroEntry(kind, index + 1, "$kind()")
        }
        store.setMacros("/my/file.h", macros)

        val store2 = makeStore(dir)
        val retrieved = store2.getMacros("/my/file.h")

        assertEquals(MacroKind.values().size, retrieved.size)
        MacroKind.values().forEach { kind ->
            assertTrue(retrieved.any { it.kind == kind })
        }
    }

    @Test
    fun `line number survives serialization round trip`(@TempDir dir: Path) {
        val store = makeStore(dir)
        store.setMacros("/my/file.h", listOf(MacroEntry(MacroKind.UPROPERTY, 42, "UPROPERTY(EditAnywhere)")))

        val store2 = makeStore(dir)
        assertEquals(42, store2.getMacros("/my/file.h")[0].line)
    }

    @Test
    fun `raw text survives serialization round trip`(@TempDir dir: Path) {
        val store = makeStore(dir)
        val raw = "UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = \"Stats|Combat\")"
        store.setMacros("/my/file.h", listOf(MacroEntry(MacroKind.UPROPERTY, 5, raw)))

        val store2 = makeStore(dir)
        assertEquals(raw, store2.getMacros("/my/file.h")[0].raw)
    }
}