package com.ueReflectWatch.settings

import com.intellij.openapi.options.Configurable
import javax.swing.*
import java.awt.GridBagConstraints
import java.awt.GridBagLayout
import java.awt.Insets

class UEReflectWatchConfigurable : Configurable {

    private var panel: JPanel? = null
    private var silentModeCheckbox: JCheckBox? = null
    private var autoRebuildCheckbox: JCheckBox? = null
    private var confirmBeforeRebuildCheckbox: JCheckBox? = null
    private var autoRelaunchEditorCheckbox: JCheckBox? = null
    private var killEditorGracePeriodField: JTextField? = null
    private var enginePathOverrideField: JTextField? = null

    override fun getDisplayName(): String = "UE Reflect Watch"

    override fun createComponent(): JComponent {
        val p = JPanel(GridBagLayout())
        panel = p

        val gbc = GridBagConstraints().apply {
            anchor = GridBagConstraints.WEST
            insets = Insets(4, 8, 4, 8)
        }

        fun addLabel(text: String, row: Int) {
            gbc.gridx = 0; gbc.gridy = row; gbc.gridwidth = 2
            gbc.fill = GridBagConstraints.HORIZONTAL
            p.add(JLabel("<html><b>$text</b></html>"), gbc)
            gbc.gridwidth = 1
        }

        fun addCheckbox(text: String, tooltip: String, row: Int): JCheckBox {
            val cb = JCheckBox(text).apply { toolTipText = tooltip }
            gbc.gridx = 0; gbc.gridy = row; gbc.gridwidth = 2
            gbc.fill = GridBagConstraints.HORIZONTAL
            p.add(cb, gbc)
            gbc.gridwidth = 1
            return cb
        }

        fun addField(label: String, tooltip: String, row: Int): JTextField {
            gbc.gridx = 0; gbc.gridy = row; gbc.fill = GridBagConstraints.NONE
            p.add(JLabel(label), gbc)
            val field = JTextField(30).apply { toolTipText = tooltip }
            gbc.gridx = 1; gbc.fill = GridBagConstraints.HORIZONTAL; gbc.weightx = 1.0
            p.add(field, gbc)
            gbc.weightx = 0.0
            return field
        }

        var row = 0

        addLabel("Behaviour", row++)

        silentModeCheckbox = addCheckbox(
                "Silent mode",
                "When ON, macro changes are logged but no prompt appears on save. Use Rebuild Now when ready.",
                row++
        )

        autoRebuildCheckbox = addCheckbox(
                "Auto rebuild without prompting",
                "Rebuild automatically when macro changes are detected, without showing the rebuild prompt.",
                row++
        )

        confirmBeforeRebuildCheckbox = addCheckbox(
                "Confirm before rebuild",
                "Show a confirmation dialog asking whether you have saved your work before the rebuild starts.",
                row++
        )

        autoRelaunchEditorCheckbox = addCheckbox(
                "Auto relaunch editor after build",
                "Automatically relaunch the Unreal Editor after a successful build.",
                row++
        )

        addLabel("Timing", row++)

        killEditorGracePeriodField = addField(
                "Kill editor grace period (ms)",
                "Milliseconds to wait after closing the Unreal Editor before starting the build.",
                row++
        )

        addLabel("Engine", row++)

        enginePathOverrideField = addField(
                "Engine path override",
                "Override the Unreal Engine install path. Leave empty to use the default Epic Games Launcher location.",
                row++
        )

        // Filler to push everything to the top.
        gbc.gridx = 0; gbc.gridy = row; gbc.gridwidth = 2
        gbc.fill = GridBagConstraints.BOTH; gbc.weighty = 1.0
        p.add(JPanel(), gbc)

        reset()
        return p
    }

    override fun isModified(): Boolean {
        val s = UEReflectWatchSettings.instance
        return silentModeCheckbox?.isSelected != s.silentMode ||
                autoRebuildCheckbox?.isSelected != s.autoRebuild ||
                confirmBeforeRebuildCheckbox?.isSelected != s.confirmBeforeRebuild ||
                autoRelaunchEditorCheckbox?.isSelected != s.autoRelaunchEditor ||
                killEditorGracePeriodField?.text != s.killEditorGracePeriodMs.toString() ||
                enginePathOverrideField?.text != s.enginePathOverride
    }

    override fun apply() {
        val s = UEReflectWatchSettings.instance
        s.silentMode = silentModeCheckbox?.isSelected ?: false
        s.autoRebuild = autoRebuildCheckbox?.isSelected ?: false
        s.confirmBeforeRebuild = confirmBeforeRebuildCheckbox?.isSelected ?: true
        s.autoRelaunchEditor = autoRelaunchEditorCheckbox?.isSelected ?: true
        s.killEditorGracePeriodMs = killEditorGracePeriodField?.text?.toIntOrNull() ?: 2000
        s.enginePathOverride = enginePathOverrideField?.text ?: ""
    }

    override fun reset() {
        val s = UEReflectWatchSettings.instance
        silentModeCheckbox?.isSelected = s.silentMode
        autoRebuildCheckbox?.isSelected = s.autoRebuild
        confirmBeforeRebuildCheckbox?.isSelected = s.confirmBeforeRebuild
        autoRelaunchEditorCheckbox?.isSelected = s.autoRelaunchEditor
        killEditorGracePeriodField?.text = s.killEditorGracePeriodMs.toString()
        enginePathOverrideField?.text = s.enginePathOverride
    }
}