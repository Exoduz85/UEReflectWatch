plugins {
    id("org.jetbrains.kotlin.jvm") version "2.3.0"
    id("org.jetbrains.kotlin.plugin.serialization") version "2.3.0"
}

repositories {
    mavenCentral()
}

dependencies {
    // Source files under test - compiled directly into this module.
    implementation(kotlin("stdlib"))
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.7.3")

    testImplementation("org.junit.jupiter:junit-jupiter:5.10.2")
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
    testRuntimeOnly("org.junit.jupiter:junit-jupiter-engine:5.10.2")
}

kotlin {
    jvmToolchain(21)
}

tasks {
    test {
        useJUnitPlatform()
    }
}

// Pull only the self-contained source packages that have no IntelliJ
// or platform dependencies. ProjectResolver is excluded because it
// depends on PlatformAdapter and UEReflectWatchSettings which need the platform.
sourceSets {
    main {
        kotlin {
            srcDir("../src/main/kotlin/com/ueReflectWatch/scanner")
            srcDir("../src/main/kotlin/com/ueReflectWatch/store")
            // Include resolver dir but exclude ProjectResolver which depends
            // on PlatformAdapter and UEReflectWatchSettings (IntelliJ platform).
            srcDir("../src/main/kotlin/com/ueReflectWatch/resolver")
            exclude("**/ProjectResolver.kt")
        }
    }
    test {
        kotlin {
            srcDir("src/test/kotlin")
        }
    }
}
