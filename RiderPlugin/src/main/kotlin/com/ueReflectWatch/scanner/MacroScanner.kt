package com.ueReflectWatch.scanner

enum class MacroKind {
    UCLASS, UPROPERTY, UFUNCTION, USTRUCT, UENUM
}

data class MacroEntry(
    val kind: MacroKind,
    val line: Int,
    val raw: String,
    // The declaration line immediately following the macro (variable type+name
    // or function signature). Included in the key so that renaming a variable
    // or changing its type is detected even if the macro specifiers are unchanged.
    val declarationLine: String = ""
) {
    val key: String get() = "${kind}|${raw}|${declarationLine.trim()}"
}

data class MacroDiff(
    val added: List<MacroEntry>,
    val removed: List<MacroEntry>
) {
    val hasChanges: Boolean get() = added.isNotEmpty() || removed.isNotEmpty()
}

object MacroScanner {

    private val PATTERN = Regex("""^\s*(UCLASS|UPROPERTY|UFUNCTION|USTRUCT|UENUM)\s*(\(|$)""")

    fun scan(content: String): List<MacroEntry> {
        val results = mutableListOf<MacroEntry>()
        val lines = content.lines()

        lines.forEachIndexed { index, line ->
            val match = PATTERN.find(line) ?: return@forEachIndexed
            val kindStr = match.groupValues[1]
            val kind = MacroKind.valueOf(kindStr)
            val raw = line.trim()

            // Capture the next non-blank line as the declaration.
            // This includes the variable type+name or function signature,
            // so that renaming a variable or changing its type is detected.
            val declarationLine = lines
                .drop(index + 1)
                .firstOrNull { it.isNotBlank() }
                ?.trim() ?: ""

            results.add(MacroEntry(kind = kind, line = index + 1, raw = raw, declarationLine = declarationLine))
        }

        return results
    }

    fun diff(previous: List<MacroEntry>, current: List<MacroEntry>): MacroDiff {
        val prevByKey = previous.groupBy { it.key }.mapValues { it.value.size }
        val currByKey = current.groupBy { it.key }.mapValues { it.value.size }

        val added = mutableListOf<MacroEntry>()
        val removed = mutableListOf<MacroEntry>()

        for (entry in current) {
            val prevCount = prevByKey.getOrDefault(entry.key, 0)
            val currCount = currByKey.getValue(entry.key)
            if (currCount > prevCount && added.none { it.key == entry.key }) {
                added.add(entry)
            }
        }

        for (entry in previous) {
            val prevCount = prevByKey.getValue(entry.key)
            val currCount = currByKey.getOrDefault(entry.key, 0)
            if (prevCount > currCount && removed.none { it.key == entry.key }) {
                removed.add(entry)
            }
        }

        return MacroDiff(added = added, removed = removed)
    }

    fun summariseDiff(diff: MacroDiff, fileName: String): String {
        val parts = mutableListOf<String>()

        if (diff.added.isNotEmpty()) {
            val byKind = diff.added.groupBy { it.kind }
                .map { (kind, entries) -> "+${entries.size} $kind" }
            parts.add("Added: ${byKind.joinToString(", ")}")
        }

        if (diff.removed.isNotEmpty()) {
            val byKind = diff.removed.groupBy { it.kind }
                .map { (kind, entries) -> "-${entries.size} $kind" }
            parts.add("Removed: ${byKind.joinToString(", ")}")
        }

        return "$fileName: ${parts.joinToString(" | ")}"
    }
}