pluginManagement {
    repositories {
        maven("https://oss.sonatype.org/content/repositories/snapshots/")
        gradlePluginPortal()
        mavenCentral()
    }
}

plugins {
    id("org.jetbrains.intellij.platform.settings") version "2.1.0"
}

rootProject.name = "UEReflectWatch"

// logic-tests is a plain Kotlin/JUnit subproject with no IntelliJ Platform
// dependency. Run tests there with: .\gradlew :logic-tests:test
include("logic-tests")
