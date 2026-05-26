using System.Collections.Generic;
using Xunit;

namespace UEReflectWatch.Tests
{
    public class MacroScannerTests
    {
        // -----------------------------------------------------------------------
        // Scan: basic detection
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_EmptyFile_ReturnsNoMacros()
        {
            var result = MacroScanner.Scan(string.Empty);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_FileWithNoMacros_ReturnsEmpty()
        {
            var content = @"
#pragma once
#include ""CoreMinimal.h""

class AFoo : public AActor
{
    float Health = 100.0f;
    void TakeDamage(float Amount);
};";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("UCLASS()", MacroKind.UCLASS)]
        [InlineData("UPROPERTY(EditAnywhere)", MacroKind.UPROPERTY)]
        [InlineData("UFUNCTION(BlueprintCallable)", MacroKind.UFUNCTION)]
        [InlineData("USTRUCT(BlueprintType)", MacroKind.USTRUCT)]
        [InlineData("UENUM(BlueprintType)", MacroKind.UENUM)]
        public void Scan_SingleMacro_DetectsCorrectKind(string line, MacroKind expectedKind)
        {
            var result = MacroScanner.Scan(line);
            Assert.Single(result);
            Assert.Equal(expectedKind, result[0].Kind);
        }

        [Fact]
        public void Scan_MacroWithNoParentheses_IsDetected()
        {
            // UCLASS and USTRUCT can appear without parentheses
            var content = "UCLASS()\nclass AFoo {};";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(MacroKind.UCLASS, result[0].Kind);
        }

        [Fact]
        public void Scan_MultipleProperties_DetectsAll()
        {
            var content = @"
UCLASS()
class MYPROJECT_API AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = ""Foo"")
    float Health = 100.0f;

    UPROPERTY(EditDefaultsOnly, Category = ""Foo"")
    float MaxHealth = 100.0f;

    UFUNCTION(BlueprintCallable, Category = ""Foo"")
    void TakeDamage(float Amount);
};";
            var result = MacroScanner.Scan(content);
            Assert.Equal(4, result.Count);
        }

        [Fact]
        public void Scan_ReportsCorrectLineNumbers()
        {
            var content = "line one\nUPROPERTY(EditAnywhere)\nline three";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(2, result[0].Line);
        }

        [Fact]
        public void Scan_LeadingWhitespace_IsDetected()
        {
            var content = "    UPROPERTY(EditAnywhere)";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(MacroKind.UPROPERTY, result[0].Kind);
        }

        // -----------------------------------------------------------------------
        // Scan: comments must not be treated as macros
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_SingleLineComment_IsIgnored()
        {
            var content = "// UPROPERTY(EditAnywhere)";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_InlineComment_MacroBeforeCommentIsDetected()
        {
            // The macro is real; the comment is after it on the same line
            var content = "UPROPERTY(EditAnywhere) // this is a comment";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
        }

        [Fact]
        public void Scan_BlockComment_IsIgnored()
        {
            var content = @"
/* 
 * UPROPERTY(EditAnywhere)
 * UFUNCTION(BlueprintCallable)
 */
float Health;";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_MacroInString_IsIgnored()
        {
            // A string literal containing a macro keyword should not be detected
            var content = @"FString Desc = TEXT(""UPROPERTY(EditAnywhere) is a macro"");";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_CommentedOutBlock_IsIgnored()
        {
            var content = @"
// UCLASS()
// class AFoo
// {
//     UPROPERTY(EditAnywhere)
//     float Health;
// };";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_PartialMacroName_IsNotDetected()
        {
            // Words that contain macro names but are not macros
            var content = @"
// MYPROPERTY is not a UPROPERTY
// SUBCLASS is not a UCLASS
SomeFunction(UPROPERTY_COUNT);";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_LowercaseMacroName_IsNotDetected()
        {
            // Macros are always uppercase in Unreal
            var content = "uproperty(EditAnywhere)";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        // -----------------------------------------------------------------------
        // Scan: specifier content
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_MacroWithComplexSpecifiers_IsDetected()
        {
            var content = @"UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = ""Stats|Combat"", meta = (ClampMin = 0.0, ClampMax = 1000.0))";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(MacroKind.UPROPERTY, result[0].Kind);
        }

        [Fact]
        public void Scan_RawTextIsPreserved()
        {
            var line = "    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = \"Foo\")";
            var result = MacroScanner.Scan(line);
            Assert.Single(result);
            // Raw should be the trimmed version of the line
            Assert.Equal(line.Trim(), result[0].Raw);
        }

        // -----------------------------------------------------------------------
        // Diff: no changes
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_IdenticalLists_HasNoChanges()
        {
            var macros = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };

            var diff = MacroScanner.Diff(macros, macros);
            Assert.False(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.Empty(diff.Removed);
        }

        [Fact]
        public void Diff_EmptyBothSides_HasNoChanges()
        {
            var diff = MacroScanner.Diff(new List<MacroEntry>(), new List<MacroEntry>());
            Assert.False(diff.HasChanges);
        }

        // -----------------------------------------------------------------------
        // Diff: additions
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_NewPropertyAdded_IsDetectedAsAdded()
        {
            var previous = new List<MacroEntry>();
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Empty(diff.Removed);
            Assert.Equal(MacroKind.UPROPERTY, diff.Added[0].Kind);
        }

        [Fact]
        public void Diff_NewFunctionAdded_IsDetectedAsAdded()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Empty(diff.Removed);
            Assert.Equal(MacroKind.UFUNCTION, diff.Added[0].Kind);
        }

        [Fact]
        public void Diff_MultipleNewMacrosAdded_AllDetected()
        {
            var previous = new List<MacroEntry>();
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UCLASS, 1, "UCLASS()"),
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Equal(3, diff.Added.Count);
            Assert.Empty(diff.Removed);
        }

        // -----------------------------------------------------------------------
        // Diff: removals
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_PropertyRemoved_IsDetectedAsRemoved()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.Single(diff.Removed);
            Assert.Equal(MacroKind.UPROPERTY, diff.Removed[0].Kind);
        }

        [Fact]
        public void Diff_AllMacrosRemoved_AllDetected()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };
            var current = new List<MacroEntry>();

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.Equal(2, diff.Removed.Count);
        }

        // -----------------------------------------------------------------------
        // Diff: specifier changes
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_SpecifierChanged_IsDetectedAsAddedAndRemoved()
        {
            // Changing EditAnywhere to EditDefaultsOnly is a change that requires
            // a rebuild because the reflection tables encode the specifiers.
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditDefaultsOnly)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Single(diff.Removed);
        }

        [Fact]
        public void Diff_BlueprintReadWriteAdded_IsDetected()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere, BlueprintReadWrite)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
        }

        [Fact]
        public void Diff_OnlyLineNumberChanged_IsNotDetected()
        {
            // Moving a property to a different line is not a meaningful change.
            // The diff uses raw text as the identity key, not line number.
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 12, "UPROPERTY(EditAnywhere)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.False(diff.HasChanges);
        }

        // -----------------------------------------------------------------------
        // Diff: mixed add and remove
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_OneAddedOneRemoved_BothDetected()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Single(diff.Removed);
            Assert.Equal(MacroKind.UFUNCTION, diff.Added[0].Kind);
            Assert.Equal(MacroKind.UPROPERTY, diff.Removed[0].Kind);
        }

        // -----------------------------------------------------------------------
        // SummariseDiff
        // -----------------------------------------------------------------------

        [Fact]
        public void SummariseDiff_AddedOnly_ContainsPlusSign()
        {
            var diff = new MacroDiff();
            diff.Added.Add(new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"));

            var summary = MacroScanner.SummariseDiff(diff, "Door.h");
            Assert.Contains("+", summary);
            Assert.Contains("UPROPERTY", summary);
            Assert.Contains("Door.h", summary);
        }

        [Fact]
        public void SummariseDiff_RemovedOnly_ContainsMinusSign()
        {
            var diff = new MacroDiff();
            diff.Removed.Add(new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)"));

            var summary = MacroScanner.SummariseDiff(diff, "Door.h");
            Assert.Contains("-", summary);
            Assert.Contains("UFUNCTION", summary);
        }

        [Fact]
        public void SummariseDiff_BothAddedAndRemoved_ContainsBoth()
        {
            var diff = new MacroDiff();
            diff.Added.Add(new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"));
            diff.Removed.Add(new MacroEntry(MacroKind.UFUNCTION, 9, "UFUNCTION(BlueprintCallable)"));

            var summary = MacroScanner.SummariseDiff(diff, "Door.h");
            Assert.Contains("+", summary);
            Assert.Contains("-", summary);
        }

        // -----------------------------------------------------------------------
        // Round-trip: scan then diff
        // -----------------------------------------------------------------------

        [Fact]
        public void RoundTrip_ScanThenDiff_NoChanges_WhenSaved()
        {
            var content = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;

    UFUNCTION(BlueprintCallable)
    void TakeDamage(float Amount);
};";
            var first = MacroScanner.Scan(content);
            var second = MacroScanner.Scan(content);
            var diff = MacroScanner.Diff(first, second);
            Assert.False(diff.HasChanges);
        }

        [Fact]
        public void RoundTrip_AddPropertyToExistingFile_DetectedCorrectly()
        {
            var before = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;
};";

            var after = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;

    UPROPERTY(EditAnywhere)
    float Armor;
};";

            var previous = MacroScanner.Scan(before);
            var current = MacroScanner.Scan(after);
            var diff = MacroScanner.Diff(previous, current);

            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Empty(diff.Removed);
        }

        [Fact]
        public void RoundTrip_RemovePropertyFromExistingFile_DetectedCorrectly()
        {
            var before = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;

    UPROPERTY(EditAnywhere)
    float Armor;
};";

            var after = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;
};";

            var previous = MacroScanner.Scan(before);
            var current = MacroScanner.Scan(after);
            var diff = MacroScanner.Diff(previous, current);

            Assert.True(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.Single(diff.Removed);
        }

        [Fact]
        public void RoundTrip_CommentOutProperty_DetectedAsRemoved()
        {
            var before = @"
    UPROPERTY(EditAnywhere)
    float Health;";

            var after = @"
    // UPROPERTY(EditAnywhere)
    float Health;";

            var previous = MacroScanner.Scan(before);
            var current = MacroScanner.Scan(after);
            var diff = MacroScanner.Diff(previous, current);

            Assert.True(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.Single(diff.Removed);
        }

        [Fact]
        public void RoundTrip_UncommentProperty_DetectedAsAdded()
        {
            var before = @"
    // UPROPERTY(EditAnywhere)
    float Health;";

            var after = @"
    UPROPERTY(EditAnywhere)
    float Health;";

            var previous = MacroScanner.Scan(before);
            var current = MacroScanner.Scan(after);
            var diff = MacroScanner.Diff(previous, current);

            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Empty(diff.Removed);
        }

        [Fact]
        public void RoundTrip_ChangeVariableType_IsDetected()
        {
            var before = "    UPROPERTY(EditAnywhere)\n    bool bIsActive;";
            var after  = "    UPROPERTY(EditAnywhere)\n    float Speed;";
            var diff = MacroScanner.Diff(MacroScanner.Scan(before), MacroScanner.Scan(after));
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Single(diff.Removed);
        }

        [Fact]
        public void RoundTrip_RenameVariable_IsDetected()
        {
            var before = "    UPROPERTY(EditAnywhere)\n    float Health;";
            var after  = "    UPROPERTY(EditAnywhere)\n    float MaxHealth;";
            var diff = MacroScanner.Diff(MacroScanner.Scan(before), MacroScanner.Scan(after));
            Assert.True(diff.HasChanges);
            Assert.Single(diff.Added);
            Assert.Single(diff.Removed);
        }

        [Fact]
        public void RoundTrip_SameTypeAndName_NoChange()
        {
            var content = "    UPROPERTY(EditAnywhere)\n    float Health;";
            var diff = MacroScanner.Diff(MacroScanner.Scan(content), MacroScanner.Scan(content));
            Assert.False(diff.HasChanges);
        }

        [Fact]
        public void RoundTrip_OnlyBodyChanged_NoMacroChange()
        {
            var before = @"
    UFUNCTION(BlueprintCallable)
    void TakeDamage(float Amount)
    {
        Health -= Amount;
    }";

            var after = @"
    UFUNCTION(BlueprintCallable)
    void TakeDamage(float Amount)
    {
        Health = FMath::Max(0.0f, Health - Amount);
        OnHealthChanged.Broadcast(Health);
    }";

            var previous = MacroScanner.Scan(before);
            var current = MacroScanner.Scan(after);
            var diff = MacroScanner.Diff(previous, current);

            Assert.False(diff.HasChanges);
        }

        // -----------------------------------------------------------------------
        // 1. GENERATED_BODY() must never be detected as a macro
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_GeneratedBody_IsNotDetected()
        {
            var content = @"
UCLASS()
class MYPROJECT_API AFoo : public AActor
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere)
    float Health;
};";
            var result = MacroScanner.Scan(content);

            // Should find UCLASS and UPROPERTY but not GENERATED_BODY
            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, m => m.Raw.StartsWith("GENERATED_BODY"));
        }

        [Fact]
        public void Scan_GeneratedBodyAlone_IsNotDetected()
        {
            var content = "    GENERATED_BODY()";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        [Fact]
        public void Scan_GeneratedUBodyMacros_AreNotDetected()
        {
            // Other generated macros that should not trigger a rebuild
            var content = @"
    GENERATED_BODY()
    GENERATED_USTRUCT_BODY()
    GENERATED_UCLASS_BODY()";
            var result = MacroScanner.Scan(content);
            Assert.Empty(result);
        }

        // -----------------------------------------------------------------------
        // 2. UCLASS with class keyword on the same or next line
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_UClassOnOwnLine_DetectsOneUClass()
        {
            var content = @"
UCLASS()
class AFoo : public AActor
{
};";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(MacroKind.UCLASS, result[0].Kind);
        }

        [Fact]
        public void Scan_UClassAndClassOnSameLine_DetectsOneUClass()
        {
            // Some codebases condense this onto one line
            var content = "UCLASS() class AFoo : public AActor {};";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(MacroKind.UCLASS, result[0].Kind);
        }

        [Fact]
        public void Scan_MultipleClasses_DetectsOneUClassEach()
        {
            var content = @"
UCLASS()
class AFoo : public AActor
{
    GENERATED_BODY()
};

UCLASS()
class ABar : public AActor
{
    GENERATED_BODY()
};";
            var result = MacroScanner.Scan(content);
            Assert.Equal(2, result.Count);
            Assert.All(result, m => Assert.Equal(MacroKind.UCLASS, m.Kind));
        }

        // -----------------------------------------------------------------------
        // 3. Duplicate identical macros - count changes are detected
        // -----------------------------------------------------------------------

        [Fact]
        public void Diff_DuplicateIdenticalMacro_AddingThirdIsDetected()
        {
            // Two identical UPROPERTY lines (different properties, same specifiers)
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
            };

            // A third identical one is added
            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UPROPERTY, 11, "UPROPERTY(EditAnywhere)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.NotEmpty(diff.Added);
            Assert.Empty(diff.Removed);
        }

        [Fact]
        public void Diff_DuplicateIdenticalMacro_RemovingOneIsDetected()
        {
            var previous = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
            };

            var current = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)")
            };

            var diff = MacroScanner.Diff(previous, current);
            Assert.True(diff.HasChanges);
            Assert.Empty(diff.Added);
            Assert.NotEmpty(diff.Removed);
        }

        [Fact]
        public void Diff_TwoIdenticalMacrosSavedTwice_NoChange()
        {
            var macros = new List<MacroEntry>
            {
                new MacroEntry(MacroKind.UPROPERTY, 5, "UPROPERTY(EditAnywhere)"),
                new MacroEntry(MacroKind.UPROPERTY, 8, "UPROPERTY(EditAnywhere)")
            };

            var diff = MacroScanner.Diff(macros, macros);
            Assert.False(diff.HasChanges);
        }

        // -----------------------------------------------------------------------
        // 4. Windows line endings (CRLF)
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_CrlfLineEndings_DetectsMacros()
        {
            var content = "UCLASS()\r\nclass AFoo : public AActor\r\n{\r\n    GENERATED_BODY()\r\n\r\n    UPROPERTY(EditAnywhere)\r\n    float Health;\r\n};";
            var result = MacroScanner.Scan(content);

            // Should find UCLASS and UPROPERTY only
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Scan_CrlfLineEndings_RawTextHasNoCarriageReturn()
        {
            // The Raw text stored in the entry should not contain \r
            // since that would cause the diff key to differ between
            // CRLF and LF versions of the same file
            var content = "UPROPERTY(EditAnywhere)\r\n";
            var result = MacroScanner.Scan(content);

            Assert.Single(result);
            Assert.DoesNotContain("\r", result[0].Raw);
        }

        [Fact]
        public void Diff_CrlfVsLf_SameContent_NoChange()
        {
            // A file saved with CRLF and then saved again with LF
            // (or vice versa) should not trigger a rebuild if the macros
            // are otherwise identical
            var crlfContent = "UPROPERTY(EditAnywhere)\r\nfloat Health;\r\n";
            var lfContent = "UPROPERTY(EditAnywhere)\nfloat Health;\n";

            var previous = MacroScanner.Scan(crlfContent);
            var current = MacroScanner.Scan(lfContent);
            var diff = MacroScanner.Diff(previous, current);

            Assert.False(diff.HasChanges);
        }

        // -----------------------------------------------------------------------
        // 5. Empty lines and whitespace between macros - line number accuracy
        // -----------------------------------------------------------------------

        [Fact]
        public void Scan_EmptyLinesBetweenMacros_LineNumbersAreCorrect()
        {
            var content = "line1\n\nUPROPERTY(EditAnywhere)\n\nUFUNCTION(BlueprintCallable)";
            var result = MacroScanner.Scan(content);

            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Line);  // UPROPERTY is on line 3
            Assert.Equal(5, result[1].Line);  // UFUNCTION is on line 5
        }

        [Fact]
        public void Scan_WhitespaceOnlyLines_DoNotAffectDetection()
        {
            var content = "    \n\t\n UPROPERTY(EditAnywhere)\n    \n UFUNCTION(BlueprintCallable)";
            var result = MacroScanner.Scan(content);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Scan_MacroOnFirstLine_LineNumberIsOne()
        {
            var content = "UCLASS()";
            var result = MacroScanner.Scan(content);
            Assert.Single(result);
            Assert.Equal(1, result[0].Line);
        }

        [Fact]
        public void Scan_LargeFileWithManyBlankLines_AllMacrosDetected()
        {
            // Simulates a realistic header file with spacing between sections
            var content = @"#pragma once

#include ""CoreMinimal.h""
#include ""GameFramework/Actor.h""
#include ""Door.generated.h""

UCLASS()
class ESCAPEROOMLAB_API ADoor : public AActor
{
    GENERATED_BODY()

public:
    ADoor();

protected:
    virtual void BeginPlay() override;

    UPROPERTY(VisibleAnywhere, Category = ""Door"")
    TObjectPtr<UStaticMeshComponent> Mesh;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = ""Door|State"")
    bool bIsLocked = false;

    UPROPERTY(EditDefaultsOnly, Category = ""Door|Animation"")
    float OpenAngle = 90.0f;

public:
    UFUNCTION(BlueprintCallable, Category = ""Door"")
    bool TryOpen();

    UFUNCTION(BlueprintImplementableEvent, Category = ""Door"")
    void OnDoorOpened();
};";
            var result = MacroScanner.Scan(content);

            // UCLASS + 3 UPROPERTY + 2 UFUNCTION = 6
            // GENERATED_BODY should not be counted
            Assert.Equal(6, result.Count);
            Assert.DoesNotContain(result, m => m.Raw.StartsWith("GENERATED_BODY"));
        }
    }
}
