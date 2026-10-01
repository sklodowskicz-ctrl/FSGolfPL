# FS Golf PL — nakładka Android

Projekt aplikacji Android dla FS Golf, która wykorzystuje usługę Ułatwień dostępu do odczytywania widocznych napisów i wyświetlania ich polskich odpowiedników jako nakładki.

## Automatyczne budowanie APK

Projekt zawiera workflow GitHub Actions:

`.github/workflows/build-apk.yml`

Po umieszczeniu projektu w repozytorium GitHub workflow automatycznie buduje **debug APK** i zapisuje go jako artefakt `FSGolfPL-debug-apk`.

Workflow używa GitHub-hosted runnera, JDK 17 oraz Gradle 8.10. Nie wymaga instalowania Android Studio na komputerze użytkownika.

## Aktualny zakres wersji 0.2

- nakładka `TYPE_APPLICATION_OVERLAY`;
- odczyt widocznych tekstów przez Accessibility Service;
- pozycjonowanie polskiego tłumaczenia przy oryginalnym napisie;
- słownik parametrów Full Swing i elementów menu FS Golf;
- obsługa zgody na wyświetlanie nad innymi aplikacjami;
- ekran konfiguracji w języku polskim.

## Ważne

Samo udane zbudowanie APK **nie oznacza jeszcze, że FS Golf udostępnia wszystkie swoje napisy przez Accessibility API**. To trzeba sprawdzić na rzeczywistym urządzeniu. Jeżeli FS Golf nie udostępnia części tekstów, kolejnym etapem będzie OCR/screen capture.
