name: Build FSGolfPL APK

on:
  workflow_dispatch:
  push:
    branches: ["main"]

jobs:
  build-apk:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: Android

    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Set up Java 17
        uses: actions/setup-java@v5
        with:
          distribution: temurin
          java-version: "17"

      - name: Set up Gradle
        uses: gradle/actions/setup-gradle@v6
        with:
          gradle-version: "8.10"
          cache-provider: basic

      - name: Build debug APK
        run: gradle --no-daemon assembleDebug

      - name: Upload APK
        uses: actions/upload-artifact@v4
        with:
          name: FSGolfPL-debug-apk
          path: Android/app/build/outputs/apk/debug/*.apk
          if-no-files-found: error
}
