# FS Golf PL 0.4

Rozszerzona nakładka języka polskiego dla FS Golf.

Wersja 0.4 obejmuje szeroki słownik całego interfejsu: ekran główny, nawigację, Session Setup, tryby Full Swing/Training/Chipping/Putting, dane uderzenia, ustawienia radaru, widoki trajektorii, profil i Chmurę FS.

Zastosowane są dwa mechanizmy: AccessibilityService dla zwykłych elementów tekstowych oraz OCR dla tekstu rysowanego wewnątrz kafelków i niestandardowych widoków.

Uwaga: nie można zagwarantować tłumaczenia tekstu dynamicznego, nazw użytkownika, wartości liczbowych ani elementów, których OCR nie rozpozna. Stałe angielskie etykiety interfejsu są objęte słownikiem możliwie szeroko.

To jest projekt źródłowy, nie gotowy APK.


## Wersja 0.5 — stabilizacja usługi Android

Wersja 0.4 uruchamiała OCR przez `AccessibilityService.takeScreenshot()` przy zmianach ekranu. Na części urządzeń Android 11 powodowało to zatrzymanie usługi. Wersja 0.5 usuwa ten mechanizm z usługi i pozostawia stabilne tłumaczenie tekstu dostępnego w drzewie dostępności. Dzięki temu nakładka nie powinna się wyłączać podczas pracy FS Golf.
