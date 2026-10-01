# FS Golf PL 0.6

Rozszerzona nakładka języka polskiego dla FS Golf.

Wersja 0.6 obejmuje szeroki słownik całego interfejsu: ekran główny, nawigację, Session Setup, tryby Full Swing/Training/Chipping/Putting, dane uderzenia, ustawienia radaru, widoki trajektorii, profil i Chmurę FS.

Zastosowane są dwa mechanizmy: AccessibilityService dla zwykłych elementów tekstowych oraz OCR dla tekstu rysowanego wewnątrz kafelków i niestandardowych widoków.

Uwaga: nie można zagwarantować tłumaczenia tekstu dynamicznego, nazw użytkownika, wartości liczbowych ani elementów, których OCR nie rozpozna. Stałe angielskie etykiety interfejsu są objęte słownikiem możliwie szeroko.

## Aktualna wersja 0.6 — MediaProjection i OCR

Dodano osobny przepływ OCR oparty na zgodzie systemowej MediaProjection, usłudze pierwszoplanowej Androida i ML Kit Text Recognition. W aplikacji włącz nakładkę i usługę dostępności, a następnie wybierz „Uruchom tłumaczenie OCR ekranu” i zaakceptuj systemowy komunikat przechwytywania. Zgoda dotyczy sesji przechwytywania; aby zatrzymać OCR, wróć do FS Golf PL i wybierz zatrzymanie.

Usługa dostępności dalej obsługuje tekst z drzewa UI. OCR uzupełnia ją dla etykiet rysowanych w kafelkach. Przechwytywanie jest wyłącznie do odczytu i nie wysyła dotknięć do FS Golf.

## Budowanie APK na GitHub

Workflow `.github/workflows/build-apk.yml` uruchamia się po wypchnięciu zmian na `main` albo ręcznie z karty Actions. Na Ubuntu przygotowuje Java 17, Android SDK 35 i Gradle 8.11.1, a następnie buduje APK debug. Gotowy plik `FS-Golf-PL-0.6-debug.apk` można pobrać z artefaktów danego uruchomienia workflow.


## Historia wersji 0.5 — stabilizacja usługi Android

Wersja 0.4 uruchamiała OCR przez `AccessibilityService.takeScreenshot()` przy zmianach ekranu. Na części urządzeń Android 11 powodowało to zatrzymanie usługi. Wersja 0.5 usuwa ten mechanizm z usługi i pozostawia stabilne tłumaczenie tekstu dostępnego w drzewie dostępności. Dzięki temu nakładka nie powinna się wyłączać podczas pracy FS Golf.

