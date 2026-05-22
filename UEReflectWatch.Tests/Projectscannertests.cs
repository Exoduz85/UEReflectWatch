using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace UEReflectWatch.Tests
{
    // Builds a temporary directory structure for each test and cleans it up
    // afterwards via IDisposable. Tests never touch real project files.
    public sealed class TempProjectDir : IDisposable
    {
        public string Root { get; }

        public TempProjectDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "UEReflectWatchTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        // Creates a file at a path relative to Root, creating any intermediate
        // directories as needed. Returns the full path.
        public string CreateFile(string relativePath, string content = "")
        {
            var fullPath = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        // Creates an empty directory at a path relative to Root.
        public string CreateDir(string relativePath)
        {
            var fullPath = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch { /* best effort */ }
        }
    }

    public sealed class ProjectScannerTests
    {
        private static StateStore MakeFreshStore()
        {
            // Each test gets its own state store backed by a unique temp file
            // so tests do not share state.
            var storageDir = Path.Combine(
                Path.GetTempPath(),
                "UEReflectWatchStoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storageDir);
            return new StateStore(storageDir);
        }

        // -----------------------------------------------------------------------
        // Source folder preference
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_PreferSourceSubfolder_WhenItExists()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            // File in Source/ should be scanned.
            tmp.CreateFile(@"Source\MyActor.h", "UPROPERTY(EditAnywhere)\nfloat Health;");

            // File in root should not be scanned when Source/ exists.
            tmp.CreateFile("RootLevel.h", "UPROPERTY(EditAnywhere)\nfloat Ignored;");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(1, result.FilesScanned);
            Assert.Equal(1, result.FilesWithMacros);
        }

        [Fact]
        public void Scan_FallsBackToRoot_WhenNoSourceSubfolder()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            tmp.CreateFile("MyActor.h", "UPROPERTY(EditAnywhere)\nfloat Health;");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(1, result.FilesScanned);
        }

        // -----------------------------------------------------------------------
        // Folder exclusion
        // -----------------------------------------------------------------------

        [Theory]
        [InlineData("Binaries")]
        [InlineData("Intermediate")]
        [InlineData("DerivedDataCache")]
        [InlineData("Saved")]
        [InlineData("Plugins")]
        [InlineData(".vs")]
        [InlineData(".git")]
        public void Scan_ExcludedFolder_IsNotScanned(string excludedFolder)
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            // Header inside an excluded folder.
            tmp.CreateFile($@"Source\{excludedFolder}\SomeHeader.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");

            // Real source file alongside it.
            tmp.CreateFile(@"Source\MyActor.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            // Only the real source file should be counted.
            Assert.Equal(1, result.FilesScanned);
        }

        [Fact]
        public void Scan_ExcludedFolderAtRoot_IsNotScanned()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            // No Source/ subfolder so root is scanned, but Binaries/ inside
            // root should still be excluded.
            tmp.CreateFile(@"Binaries\SomeHeader.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");

            tmp.CreateFile("MyActor.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(1, result.FilesScanned);
        }

        [Fact]
        public void Scan_NonExcludedSubfolder_IsScanned()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            // Headers in regular subdirectories should be found.
            tmp.CreateFile(@"Source\Public\MyActor.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");
            tmp.CreateFile(@"Source\Private\MyActorImpl.h",
                "UFUNCTION(BlueprintCallable)\nvoid DoThing();");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(2, result.FilesScanned);
        }

        // -----------------------------------------------------------------------
        // State store seeding
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_FileWithMacros_SeedsStateStore()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            var filePath = tmp.CreateFile(@"Source\MyActor.h",
                "UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};");

            ProjectScanner.ScanAndSeed(tmp.Root, store);

            var stored = store.GetMacros(filePath);

            // UCLASS and UPROPERTY should be stored. GENERATED_BODY should not.
            Assert.Equal(2, stored.Count);
            Assert.Contains(stored, m => m.Kind == MacroKind.UCLASS);
            Assert.Contains(stored, m => m.Kind == MacroKind.UPROPERTY);
            Assert.DoesNotContain(stored, m => m.Raw.StartsWith("GENERATED_BODY"));
        }

        [Fact]
        public void Scan_FileWithNoMacros_IsStillSeededWithEmptyList()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            var filePath = tmp.CreateFile(@"Source\ForwardDeclarations.h",
                "#pragma once\nclass AFoo;\nclass ABar;");

            ProjectScanner.ScanAndSeed(tmp.Root, store);

            // The file should be recorded even though it has no macros, so that
            // the first save of this file does not show everything as "added".
            var stored = store.GetMacros(filePath);
            Assert.NotNull(stored);
            Assert.Empty(stored);
        }

        [Fact]
        public void Scan_AfterInitialScan_FirstSaveProducesNoDiff()
        {
            // This is the core scenario the initial scan exists to solve.
            // Without a scan, the first save of a file shows all macros as added.
            // After a scan, the first save produces no diff if nothing changed.
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            const string content = "UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};";
            var filePath = tmp.CreateFile(@"Source\MyActor.h", content);

            // Simulate the initial scan.
            ProjectScanner.ScanAndSeed(tmp.Root, store);

            // Simulate the first save of the file after the scan (content unchanged).
            var currentMacros = MacroScanner.Scan(content);
            var previousMacros = store.GetMacros(filePath);
            var diff = MacroScanner.Diff(previousMacros, currentMacros);

            Assert.False(diff.HasChanges);
        }

        [Fact]
        public void Scan_WithoutInitialScan_FirstSaveProducesFalsePositive()
        {
            // Demonstrates why the initial scan is needed. Without it, the first
            // save of any file looks like "all macros were added" because the
            // previous state is empty.
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            const string content = "UCLASS()\nclass AMyActor : public AActor\n{\n    GENERATED_BODY()\n\n    UPROPERTY(EditAnywhere)\n    float Health;\n};";
            var filePath = tmp.CreateFile(@"Source\MyActor.h", content);

            // No initial scan. Simulate the first save.
            var currentMacros = MacroScanner.Scan(content);
            var previousMacros = store.GetMacros(filePath); // returns empty list
            var diff = MacroScanner.Diff(previousMacros, currentMacros);

            // This would trigger a false positive rebuild prompt.
            Assert.True(diff.HasChanges);
            Assert.NotEmpty(diff.Added);
        }

        // -----------------------------------------------------------------------
        // Scan results summary
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_ReturnsCorrectFileCounts()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            tmp.CreateFile(@"Source\ActorA.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");
            tmp.CreateFile(@"Source\ActorB.h",
                "UFUNCTION(BlueprintCallable)\nvoid DoThing();");
            tmp.CreateFile(@"Source\ForwardDecls.h",
                "#pragma once\nclass AFoo;"); // no macros

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(3, result.FilesScanned);
            Assert.Equal(2, result.FilesWithMacros);
            Assert.Equal(2, result.TotalMacros);
        }

        [Fact]
        public void Scan_CountsMacrosAcrossMultipleFiles()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            tmp.CreateFile(@"Source\ActorA.h",
                "UCLASS()\nclass AActor\n{\n    UPROPERTY(EditAnywhere)\n    float Health;\n};");
            tmp.CreateFile(@"Source\ActorB.h",
                "UCLASS()\nclass BActor\n{\n    UPROPERTY(EditAnywhere)\n    float Speed;\n    UFUNCTION(BlueprintCallable)\n    void Run();\n};");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(2, result.FilesScanned);
            Assert.Equal(2, result.FilesWithMacros);
            Assert.Equal(5, result.TotalMacros); // 2 UCLASS + 2 UPROPERTY + 1 UFUNCTION
        }

        [Fact]
        public void Scan_EmptySourceFolder_ReturnsZeroCounts()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            tmp.CreateDir("Source");

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.Equal(0, result.FilesScanned);
            Assert.Equal(0, result.FilesWithMacros);
            Assert.Equal(0, result.TotalMacros);
            Assert.Empty(result.Errors);
        }

        // -----------------------------------------------------------------------
        // Error handling
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_UnreadableFile_IsRecordedInErrors()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            var filePath = tmp.CreateFile(@"Source\Locked.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");

            // Lock the file to make it unreadable during the scan.
            using var lockStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            Assert.NotEmpty(result.Errors);
            Assert.Equal(0, result.FilesScanned);
        }

        [Fact]
        public void Scan_MixOfReadableAndUnreadableFiles_ScansReadableOnes()
        {
            using var tmp = new TempProjectDir();
            var store = MakeFreshStore();

            var lockedPath = tmp.CreateFile(@"Source\Locked.h",
                "UPROPERTY(EditAnywhere)\nfloat Health;");
            tmp.CreateFile(@"Source\Readable.h",
                "UFUNCTION(BlueprintCallable)\nvoid DoThing();");

            using var lockStream = new FileStream(
                lockedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);

            var result = ProjectScanner.ScanAndSeed(tmp.Root, store);

            // The readable file should still be scanned.
            Assert.Equal(1, result.FilesScanned);
            Assert.Single(result.Errors);
        }
    }
}