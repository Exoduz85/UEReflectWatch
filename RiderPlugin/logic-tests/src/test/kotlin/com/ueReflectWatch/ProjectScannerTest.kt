package com.ueReflectWatch

import com.ueReflectWatch.resolver.ProjectScanner
import com.ueReflectWatch.scanner.MacroKind
import com.ueReflectWatch.scanner.MacroScanner
import com.ueReflectWatch.store.StateStore
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.io.TempDir
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import java.io.File
import java.nio.file.Path

class ProjectScannerTest {

    // JUnit 5 @TempDir creates and cleans up a temp directory per test automatically.

    private fun makeStore(): StateStore {
        val dir = createTempDir("UEReflectWatchStore")
        return StateStore(dir.absolutePath)
    }

    private fun createTempDir(prefix: String): File {
        val dir = kotlin.io.path.createTempDirectory(prefix).toFile()
        dir.deleteOnExit()
        return dir
    }

    // -----------------------------------------------------------------------
    // Source folder preference
    // -----------------------------------------------------------------------

    @Test
    fun `scan prefers Source subfolder when it exists`(@TempDir root: Path) {
        val store = makeStore()
        File(root.toFile(), "Source").also { it.mkdirs() }
            .resolve("MyActor.h")
            .writeText("UPROPERTY(EditAnywhere)\nfloat Health;")
        File(root.toFile(), "RootLevel.h")
            .writeText("UPROPERTY(EditAnywhere)\nfloat Ignored;")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(1, result.filesScanned)
        assertEquals(1, result.filesWithMacros)
    }

    @Test
    fun `scan falls back to root when no Source subfolder`(@TempDir root: Path) {
        val store = makeStore()
        root.resolve("MyActor.h").toFile()
            .writeText("UPROPERTY(EditAnywhere)\nfloat Health;")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(1, result.filesScanned)
    }

    // -----------------------------------------------------------------------
    // Folder exclusion
    // -----------------------------------------------------------------------

    @ParameterizedTest
    @ValueSource(strings = ["Binaries", "Intermediate", "DerivedDataCache", "Saved", "Plugins", ".vs", ".git"])
    fun `scan excluded folder is not scanned`(excludedFolder: String, @TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        File(source, excludedFolder).also { it.mkdirs() }
            .resolve("SomeHeader.h")
            .writeText("UPROPERTY(EditAnywhere)\nfloat Health;")
        source.resolve("MyActor.h")
            .writeText("UPROPERTY(EditAnywhere)\nfloat Health;")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(1, result.filesScanned)
    }

    @Test
    fun `scan non excluded subfolder is scanned`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        File(source, "Public").also { it.mkdirs() }
            .resolve("MyActor.h")
            .writeText("UPROPERTY(EditAnywhere)\nfloat Health;")
        File(source, "Private").also { it.mkdirs() }
            .resolve("MyActorImpl.h")
            .writeText("UFUNCTION(BlueprintCallable)\nvoid DoThing();")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(2, result.filesScanned)
    }

    // -----------------------------------------------------------------------
    // State store seeding
    // -----------------------------------------------------------------------

    @Test
    fun `scan file with macros seeds state store correctly`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        val file = source.resolve("MyActor.h").also {
            it.writeText("UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};")
        }

        ProjectScanner.scanAndSeed(root.toString(), store)

        val stored = store.getMacros(file.absolutePath)
        assertEquals(2, stored.size)
        assertTrue(stored.any { it.kind == MacroKind.UCLASS })
        assertTrue(stored.any { it.kind == MacroKind.UPROPERTY })
        assertTrue(stored.none { it.raw.startsWith("GENERATED_BODY") })
    }

    @Test
    fun `scan file with no macros is still seeded with empty list`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        val file = source.resolve("ForwardDeclarations.h").also {
            it.writeText("#pragma once\nclass AFoo;\nclass ABar;")
        }

        ProjectScanner.scanAndSeed(root.toString(), store)

        val stored = store.getMacros(file.absolutePath)
        assertNotNull(stored)
        assertTrue(stored.isEmpty())
    }

    @Test
    fun `after initial scan first save produces no diff`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        val content = "UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};"
        val file = source.resolve("MyActor.h").also { it.writeText(content) }

        ProjectScanner.scanAndSeed(root.toString(), store)

        val currentMacros = MacroScanner.scan(content)
        val previousMacros = store.getMacros(file.absolutePath)
        val diff = MacroScanner.diff(previousMacros, currentMacros)

        assertFalse(diff.hasChanges)
    }

    @Test
    fun `without initial scan first save produces false positive`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        val content = "UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};"
        val file = source.resolve("MyActor.h").also { it.writeText(content) }

        // No initial scan.
        val currentMacros = MacroScanner.scan(content)
        val previousMacros = store.getMacros(file.absolutePath) // returns empty list
        val diff = MacroScanner.diff(previousMacros, currentMacros)

        assertTrue(diff.hasChanges)
        assertTrue(diff.added.isNotEmpty())
    }

    // -----------------------------------------------------------------------
    // Scan results summary
    // -----------------------------------------------------------------------

    @Test
    fun `scan returns correct file counts`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        source.resolve("ActorA.h").writeText("UPROPERTY(EditAnywhere)\nfloat Health;")
        source.resolve("ActorB.h").writeText("UFUNCTION(BlueprintCallable)\nvoid DoThing();")
        source.resolve("ForwardDecls.h").writeText("#pragma once\nclass AFoo;")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(3, result.filesScanned)
        assertEquals(2, result.filesWithMacros)
        assertEquals(2, result.totalMacros)
    }

    @Test
    fun `scan counts macros across multiple files`(@TempDir root: Path) {
        val store = makeStore()
        val source = File(root.toFile(), "Source").also { it.mkdirs() }
        source.resolve("ActorA.h").writeText("UCLASS()\nclass AActor\n{\n    UPROPERTY(EditAnywhere)\n    float Health;\n};")
        source.resolve("ActorB.h").writeText("UCLASS()\nclass BActor\n{\n    UPROPERTY(EditAnywhere)\n    float Speed;\n    UFUNCTION(BlueprintCallable)\n    void Run();\n};")

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(2, result.filesScanned)
        assertEquals(2, result.filesWithMacros)
        assertEquals(5, result.totalMacros)
    }

    @Test
    fun `scan empty source folder returns zero counts`(@TempDir root: Path) {
        val store = makeStore()
        File(root.toFile(), "Source").mkdirs()

        val result = ProjectScanner.scanAndSeed(root.toString(), store)

        assertEquals(0, result.filesScanned)
        assertEquals(0, result.filesWithMacros)
        assertEquals(0, result.totalMacros)
        assertTrue(result.errors.isEmpty())
    }
}