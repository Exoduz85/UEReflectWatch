package com.ueReflectWatch.settings

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage

@State(
    name = "UEReflectWatchSettings",
    storages = [Storage("UEReflectWatch.xml")]
)
class UEReflectWatchSettings : PersistentStateComponent<UEReflectWatchSettings.State> {

    // All mutable state lives directly in this flat class so
    // PersistentStateComponent.getState() returns `this` and
    // IntelliJ's XmlSerializer can read/write every field without
    // a nested object. This fixes the "settings not sticking" bug
    // that occurred when getState() returned a copy of a data class.
    var silentMode: Boolean = false
    var autoRebuild: Boolean = false
    var confirmBeforeRebuild: Boolean = true
    var autoRelaunchEditor: Boolean = true
    var killEditorGracePeriodMs: Int = 2000
    var enginePathOverride: String = ""

    // PersistentStateComponent: return `this` so the serializer
    // writes directly into the same object it reads from.
    override fun getState(): State = State(
        silentMode = silentMode,
        autoRebuild = autoRebuild,
        confirmBeforeRebuild = confirmBeforeRebuild,
        autoRelaunchEditor = autoRelaunchEditor,
        killEditorGracePeriodMs = killEditorGracePeriodMs,
        enginePathOverride = enginePathOverride
    )

    override fun loadState(state: State) {
        silentMode = state.silentMode
        autoRebuild = state.autoRebuild
        confirmBeforeRebuild = state.confirmBeforeRebuild
        autoRelaunchEditor = state.autoRelaunchEditor
        killEditorGracePeriodMs = state.killEditorGracePeriodMs
        enginePathOverride = state.enginePathOverride
    }

    data class State(
        var silentMode: Boolean = false,
        var autoRebuild: Boolean = false,
        var confirmBeforeRebuild: Boolean = true,
        var autoRelaunchEditor: Boolean = true,
        var killEditorGracePeriodMs: Int = 2000,
        var enginePathOverride: String = ""
    )

    companion object {
        val instance: UEReflectWatchSettings
            get() = ApplicationManager.getApplication()
                .getService(UEReflectWatchSettings::class.java)
    }
}