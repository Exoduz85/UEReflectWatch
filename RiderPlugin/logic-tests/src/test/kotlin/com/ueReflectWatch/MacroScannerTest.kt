package com.ueReflectWatch

import com.ueReflectWatch.scanner.MacroEntry
import com.ueReflectWatch.scanner.MacroKind
import com.ueReflectWatch.scanner.MacroScanner
import org.junit.jupiter.api.Assertions.*
import org.junit.jupiter.api.Test

class MacroScannerTest {

    // -----------------------------------------------------------------------
    // Scan: basic detection
    // -----------------------------------------------------------------------

    @Test
    fun `scan empty file returns no macros`() {
        val result = MacroScanner.scan("")
        assertTrue(result.isEmpty())
    }

    @Test
    fun `scan file with no macros returns empty`() {
        val content = """
            #pragma once
            #include "CoreMinimal.h"
            
            class AFoo : public AActor
            {
                float Health = 100.0f;
                void TakeDamage(float Amount);
            };
        """.trimIndent()
        assertTrue(MacroScanner.scan(content).isEmpty())
    }

    @Test
    fun `scan detects UCLASS`() {
        val result = MacroScanner.scan("UCLASS()")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UCLASS, result[0].kind)
    }

    @Test
    fun `scan detects UPROPERTY`() {
        val result = MacroScanner.scan("UPROPERTY(EditAnywhere)")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UPROPERTY, result[0].kind)
    }

    @Test
    fun `scan detects UFUNCTION`() {
        val result = MacroScanner.scan("UFUNCTION(BlueprintCallable)")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UFUNCTION, result[0].kind)
    }

    @Test
    fun `scan detects USTRUCT`() {
        val result = MacroScanner.scan("USTRUCT(BlueprintType)")
        assertEquals(1, result.size)
        assertEquals(MacroKind.USTRUCT, result[0].kind)
    }

    @Test
    fun `scan detects UENUM`() {
        val result = MacroScanner.scan("UENUM(BlueprintType)")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UENUM, result[0].kind)
    }

    @Test
    fun `scan detects multiple macros`() {
        val content = """
            UCLASS()
            class MYPROJECT_API AFoo : public AActor
            {
                GENERATED_BODY()
                
                UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Foo")
                float Health = 100.0f;
                
                UPROPERTY(EditDefaultsOnly, Category = "Foo")
                float MaxHealth = 100.0f;
                
                UFUNCTION(BlueprintCallable, Category = "Foo")
                void TakeDamage(float Amount);
            };
        """.trimIndent()
        val result = MacroScanner.scan(content)
        assertEquals(4, result.size)
    }

    @Test
    fun `scan reports correct line numbers`() {
        val content = "line one\nUPROPERTY(EditAnywhere)\nline three"
        val result = MacroScanner.scan(content)
        assertEquals(1, result.size)
        assertEquals(2, result[0].line)
    }

    @Test
    fun `scan detects macro with leading whitespace`() {
        val result = MacroScanner.scan("    UPROPERTY(EditAnywhere)")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UPROPERTY, result[0].kind)
    }

    // -----------------------------------------------------------------------
    // Scan: comments must not be treated as macros
    // -----------------------------------------------------------------------

    @Test
    fun `scan ignores single line comment`() {
        val result = MacroScanner.scan("// UPROPERTY(EditAnywhere)")
        assertTrue(result.isEmpty())
    }

    @Test
    fun `scan detects macro before inline comment`() {
        val result = MacroScanner.scan("UPROPERTY(EditAnywhere) // this is a comment")
        assertEquals(1, result.size)
    }

    @Test
    fun `scan ignores block comment`() {
        val content = """
            /* 
             * UPROPERTY(EditAnywhere)
             * UFUNCTION(BlueprintCallable)
             */
            float Health;
        """.trimIndent()
        assertTrue(MacroScanner.scan(content).isEmpty())
    }

    @Test
    fun `scan ignores commented out block`() {
        val content = """
            // UCLASS()
            // class AFoo
            // {
            //     UPROPERTY(EditAnywhere)
            //     float Health;
            // };
        """.trimIndent()
        assertTrue(MacroScanner.scan(content).isEmpty())
    }

    @Test
    fun `scan ignores partial macro name`() {
        val content = """
            // MYPROPERTY is not a UPROPERTY
            // SUBCLASS is not a UCLASS
        """.trimIndent()
        assertTrue(MacroScanner.scan(content).isEmpty())
    }

    @Test
    fun `scan ignores lowercase macro name`() {
        assertTrue(MacroScanner.scan("uproperty(EditAnywhere)").isEmpty())
    }

    // -----------------------------------------------------------------------
    // Scan: GENERATED_BODY must not be detected
    // -----------------------------------------------------------------------

    @Test
    fun `scan does not detect GENERATED_BODY`() {
        val content = """
            UCLASS()
            class MYPROJECT_API AFoo : public AActor
            {
                GENERATED_BODY()
                
                UPROPERTY(EditAnywhere)
                float Health;
            };
        """.trimIndent()
        val result = MacroScanner.scan(content)
        assertEquals(2, result.size)
        assertTrue(result.none { it.raw.startsWith("GENERATED_BODY") })
    }

    @Test
    fun `scan does not detect GENERATED_BODY alone`() {
        assertTrue(MacroScanner.scan("    GENERATED_BODY()").isEmpty())
    }

    @Test
    fun `scan does not detect GENERATED_USTRUCT_BODY`() {
        assertTrue(MacroScanner.scan("    GENERATED_USTRUCT_BODY()").isEmpty())
    }

    // -----------------------------------------------------------------------
    // Scan: UCLASS with class keyword variations
    // -----------------------------------------------------------------------

    @Test
    fun `scan UCLASS on own line detects one UCLASS`() {
        val content = "UCLASS()\nclass AFoo : public AActor\n{};"
        val result = MacroScanner.scan(content)
        assertEquals(1, result.size)
        assertEquals(MacroKind.UCLASS, result[0].kind)
    }

    @Test
    fun `scan UCLASS and class on same line detects one UCLASS`() {
        val result = MacroScanner.scan("UCLASS() class AFoo : public AActor {};")
        assertEquals(1, result.size)
        assertEquals(MacroKind.UCLASS, result[0].kind)
    }

    @Test
    fun `scan multiple classes detects one UCLASS each`() {
        val content = """
            UCLASS()
            class AFoo : public AActor { GENERATED_BODY() };
            
            UCLASS()
            class ABar : public AActor { GENERATED_BODY() };
        """.trimIndent()
        val result = MacroScanner.scan(content)
        assertEquals(2, result.size)
        assertTrue(result.all { it.kind == MacroKind.UCLASS })
    }

    // -----------------------------------------------------------------------
    // Scan: CRLF line endings
    // -----------------------------------------------------------------------

    @Test
    fun `scan handles CRLF line endings`() {
        val content = "UCLASS()\r\nclass AFoo\r\n{\r\n    GENERATED_BODY()\r\n\r\n    UPROPERTY(EditAnywhere)\r\n    float Health;\r\n};"
        val result = MacroScanner.scan(content)
        assertEquals(2, result.size)
    }

    @Test
    fun `scan raw text has no carriage return for CRLF files`() {
        val result = MacroScanner.scan("UPROPERTY(EditAnywhere)\r\n")
        assertEquals(1, result.size)
        assertFalse(result[0].raw.contains("\r"))
    }

    @Test
    fun `diff CRLF vs LF same content no change`() {
        val crlfContent = "UPROPERTY(EditAnywhere)\r\nfloat Health;\r\n"
        val lfContent = "UPROPERTY(EditAnywhere)\nfloat Health;\n"
        val previous = MacroScanner.scan(crlfContent)
        val current = MacroScanner.scan(lfContent)
        assertFalse(MacroScanner.diff(previous, current).hasChanges)
    }

    // -----------------------------------------------------------------------
    // Scan: blank lines and line number accuracy
    // -----------------------------------------------------------------------

    @Test
    fun `scan reports correct line numbers with empty lines between macros`() {
        val content = "line1\n\nUPROPERTY(EditAnywhere)\n\nUFUNCTION(BlueprintCallable)"
        val result = MacroScanner.scan(content)
        assertEquals(2, result.size)
        assertEquals(3, result[0].line)
        assertEquals(5, result[1].line)
    }

    @Test
    fun `scan whitespace only lines do not affect detection`() {
        val content = "    \n\t\n UPROPERTY(EditAnywhere)\n    \n UFUNCTION(BlueprintCallable)"
        assertEquals(2, MacroScanner.scan(content).size)
    }

    @Test
    fun `scan macro on first line reports line number one`() {
        val result = MacroScanner.scan("UCLASS()")
        assertEquals(1, result[0].line)
    }

    @Test
    fun `scan realistic header file detects correct count`() {
        val content = """
            #pragma once
            
            #include "CoreMinimal.h"
            #include "GameFramework/Actor.h"
            #include "MyActor.generated.h"
            
            UCLASS()
            class MYPROJECT_API AMyActor : public AActor
            {
                GENERATED_BODY()
            
            public:
                AMyActor();
            
            protected:
                virtual void BeginPlay() override;
            
                UPROPERTY(VisibleAnywhere, Category = "MyActor")
                TObjectPtr<UStaticMeshComponent> Mesh;
            
                UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "MyActor|State")
                bool bIsActive = false;
            
                UPROPERTY(EditDefaultsOnly, Category = "MyActor|Animation")
                float AnimSpeed = 1.0f;
            
            public:
                UFUNCTION(BlueprintCallable, Category = "MyActor")
                void Activate();
            
                UFUNCTION(BlueprintImplementableEvent, Category = "MyActor")
                void OnActivated();
            };
        """.trimIndent()
        val result = MacroScanner.scan(content)
        assertEquals(6, result.size)
        assertTrue(result.none { it.raw.startsWith("GENERATED_BODY") })
    }

    // -----------------------------------------------------------------------
    // Diff: no changes
    // -----------------------------------------------------------------------

    @Test
    fun `diff identical lists has no changes`() {
        val macros = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
        )
        assertFalse(MacroScanner.diff(macros, macros).hasChanges)
    }

    @Test
    fun `diff empty both sides has no changes`() {
        assertFalse(MacroScanner.diff(emptyList(), emptyList()).hasChanges)
    }

    @Test
    fun `diff only line number changed has no changes`() {
        val previous = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        val current = listOf(MacroEntry(MacroKind.UPROPERTY, 12, "UPROPERTY(EditAnywhere)"))
        assertFalse(MacroScanner.diff(previous, current).hasChanges)
    }

    // -----------------------------------------------------------------------
    // Diff: additions
    // -----------------------------------------------------------------------

    @Test
    fun `diff new property added is detected`() {
        val previous = emptyList<MacroEntry>()
        val current = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        val diff = MacroScanner.diff(previous, current)
        assertTrue(diff.hasChanges)
        assertEquals(1, diff.added.size)
        assertTrue(diff.removed.isEmpty())
    }

    @Test
    fun `diff multiple macros added all detected`() {
        val current = listOf(
            MacroEntry(MacroKind.UCLASS, 1, "UCLASS()"),
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
        )
        val diff = MacroScanner.diff(emptyList(), current)
        assertTrue(diff.hasChanges)
        assertEquals(3, diff.added.size)
    }

    // -----------------------------------------------------------------------
    // Diff: removals
    // -----------------------------------------------------------------------

    @Test
    fun `diff property removed is detected`() {
        val previous = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
        )
        val current = listOf(MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)"))
        val diff = MacroScanner.diff(previous, current)
        assertTrue(diff.hasChanges)
        assertTrue(diff.added.isEmpty())
        assertEquals(1, diff.removed.size)
        assertEquals(MacroKind.UPROPERTY, diff.removed[0].kind)
    }

    @Test
    fun `diff all macros removed all detected`() {
        val previous = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
        )
        val diff = MacroScanner.diff(previous, emptyList())
        assertTrue(diff.hasChanges)
        assertEquals(2, diff.removed.size)
    }

    // -----------------------------------------------------------------------
    // Diff: specifier changes
    // -----------------------------------------------------------------------

    @Test
    fun `diff specifier changed is detected`() {
        val previous = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        val current = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditDefaultsOnly)"))
        assertTrue(MacroScanner.diff(previous, current).hasChanges)
    }

    @Test
    fun `diff BlueprintReadWrite added is detected`() {
        val previous = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        val current = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere, BlueprintReadWrite)"))
        assertTrue(MacroScanner.diff(previous, current).hasChanges)
    }

    // -----------------------------------------------------------------------
    // Diff: duplicate identical macros
    // -----------------------------------------------------------------------

    @Test
    fun `diff adding third identical macro is detected`() {
        val previous = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
        )
        val current = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UPROPERTY, 11, "UPROPERTY(EditAnywhere)")
        )
        val diff = MacroScanner.diff(previous, current)
        assertTrue(diff.hasChanges)
        assertTrue(diff.added.isNotEmpty())
    }

    @Test
    fun `diff removing one of two identical macros is detected`() {
        val previous = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
        )
        val current = listOf(MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"))
        val diff = MacroScanner.diff(previous, current)
        assertTrue(diff.hasChanges)
        assertTrue(diff.removed.isNotEmpty())
    }

    @Test
    fun `diff two identical macros saved twice no change`() {
        val macros = listOf(
            MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
            MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
        )
        assertFalse(MacroScanner.diff(macros, macros).hasChanges)
    }

    // -----------------------------------------------------------------------
    // Round-trip: scan then diff
    // -----------------------------------------------------------------------

    @Test
    fun `round trip scan then diff no changes when saved twice`() {
        val content = """
            UCLASS()
            class AFoo : public AActor
            {
                GENERATED_BODY()
                UPROPERTY(EditAnywhere)
                float Health;
                UFUNCTION(BlueprintCallable)
                void TakeDamage(float Amount);
            };
        """.trimIndent()
        val first = MacroScanner.scan(content)
        val second = MacroScanner.scan(content)
        assertFalse(MacroScanner.diff(first, second).hasChanges)
    }

    @Test
    fun `round trip add property detected correctly`() {
        val before = "UCLASS()\nclass AFoo\n{\n    GENERATED_BODY()\n    UPROPERTY(EditAnywhere)\n    float Health;\n};"
        val after = "UCLASS()\nclass AFoo\n{\n    GENERATED_BODY()\n    UPROPERTY(EditAnywhere)\n    float Health;\n    UPROPERTY(EditAnywhere)\n    float Armor;\n};"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertEquals(1, diff.added.size)
        assertTrue(diff.removed.isEmpty())
    }

    @Test
    fun `round trip remove property detected correctly`() {
        val before = "UCLASS()\nclass AFoo\n{\n    GENERATED_BODY()\n    UPROPERTY(EditAnywhere)\n    float Health;\n    UPROPERTY(EditAnywhere)\n    float Armor;\n};"
        val after = "UCLASS()\nclass AFoo\n{\n    GENERATED_BODY()\n    UPROPERTY(EditAnywhere)\n    float Health;\n};"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertTrue(diff.added.isEmpty())
        assertEquals(1, diff.removed.size)
    }

    @Test
    fun `round trip comment out property detected as removed`() {
        val before = "    UPROPERTY(EditAnywhere)\n    float Health;"
        val after = "    // UPROPERTY(EditAnywhere)\n    float Health;"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertTrue(diff.added.isEmpty())
        assertEquals(1, diff.removed.size)
    }

    @Test
    fun `round trip uncomment property detected as added`() {
        val before = "    // UPROPERTY(EditAnywhere)\n    float Health;"
        val after = "    UPROPERTY(EditAnywhere)\n    float Health;"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertEquals(1, diff.added.size)
        assertTrue(diff.removed.isEmpty())
    }

    @Test
    fun `round trip change variable type is detected`() {
        val before = "    UPROPERTY(EditAnywhere)\n    bool bIsActive;"
        val after  = "    UPROPERTY(EditAnywhere)\n    float Speed;"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertEquals(1, diff.added.size)
        assertEquals(1, diff.removed.size)
    }

    @Test
    fun `round trip rename variable is detected`() {
        val before = "    UPROPERTY(EditAnywhere)\n    float Health;"
        val after  = "    UPROPERTY(EditAnywhere)\n    float MaxHealth;"
        val diff = MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after))
        assertTrue(diff.hasChanges)
        assertEquals(1, diff.added.size)
        assertEquals(1, diff.removed.size)
    }

    @Test
    fun `round trip same type and name no change`() {
        val content = "    UPROPERTY(EditAnywhere)\n    float Health;"
        val diff = MacroScanner.diff(MacroScanner.scan(content), MacroScanner.scan(content))
        assertFalse(diff.hasChanges)
    }

    @Test
    fun `round trip only body changed no macro change`() {
        val before = "    UFUNCTION(BlueprintCallable)\n    void TakeDamage(float Amount)\n    {\n        Health -= Amount;\n    }"
        val after = "    UFUNCTION(BlueprintCallable)\n    void TakeDamage(float Amount)\n    {\n        Health = FMath::Max(0.0f, Health - Amount);\n    }"
        assertFalse(MacroScanner.diff(MacroScanner.scan(before), MacroScanner.scan(after)).hasChanges)
    }
}