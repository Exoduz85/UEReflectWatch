plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.3.0"
    id("org.jetbrains.kotlin.plugin.serialization") version "2.3.0"
    id("org.jetbrains.intellij.platform")
}

group = "com.ueReflectWatch"
version = "0.1.2"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        rider("2026.1.2")
        instrumentationTools()
    }

    // JSON serialization is provided by the IntelliJ Platform bundled libraries.
    // No need to declare it explicitly.
}

intellijPlatform {
    pluginConfiguration {
        name = "UE Reflect Watch"
        version = "0.1.2"

        ideaVersion {
            sinceBuild = "261"
            untilBuild = provider { null }
        }
    }

    publishing {
        token = providers.environmentVariable("JETBRAINS_TOKEN")
    }

    signing {
        certificateChain = providers.environmentVariable("CERTIFICATE_CHAIN")
        privateKey = providers.environmentVariable("PRIVATE_KEY")
        password = providers.environmentVariable("PRIVATE_KEY_PASSWORD")
    }
}

kotlin {
    jvmToolchain(21)
}

tasks {
    withType<org.jetbrains.kotlin.gradle.tasks.KotlinCompile> {
        compilerOptions.jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_21
    }
    withType<JavaCompile> {
        sourceCompatibility = "21"
        targetCompatibility = "21"
    }
    test {
        useJUnitPlatform()
    }
    // buildSearchableOptions launches the IDE to index settings and does not
    // work reliably for Rider plugins. Disable it; settings are still fully
    // functional, they just won't appear in Rider's global search.
    named("buildSearchableOptions") {
        enabled = false
    }
    // All tests live in :logic-tests. Disable the root test task entirely
    // so the IntelliJ Platform plugin does not try to launch a Rider sandbox.
    named("test") {
        enabled = false
    }
    named("compileTestKotlin") {
        enabled = false
    }
}
