package pl.fsgolfpl

import android.accessibilityservice.AccessibilityService
import android.graphics.Color
import android.graphics.PixelFormat
import android.graphics.Rect
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.provider.Settings
import android.view.Gravity
import android.view.WindowManager
import android.widget.TextView
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo

/**
 * FS Golf PL 0.4
 *
 * Two translation paths are used:
 * 1) Accessibility tree - fast and precise for normal Android text widgets.
 * 2) Screenshot OCR - catches text rendered inside cards/images/custom views,
 *    which is why labels such as Full Swing, Putting and Challenges were
 *    previously missed.
 */
class FsAccessibilityService : AccessibilityService() {
    private var wm: WindowManager? = null
    private val handler = Handler(Looper.getMainLooper())
    private val overlays = mutableMapOf<String, TextView>()
    companion object {
    private val dict = linkedMapOf(
        // ===== Ekran główny / nawigacja =====
        "Home" to "Główna",
        "Profile" to "Profil",
        "Learn Basics" to "Podstawy",
        "Learn Basic" to "Podstawy",
        "FS Cloud" to "Chmura FS",
        "FlightScope Cloud" to "Chmura FlightScope",
        "Challenges" to "Wyzwania",
        "Challenge" to "Wyzwanie",
        "Full Swing" to "Pełny zamach",
        "Full Swing Session" to "Sesja pełnego zamachu",
        "Swing Training" to "Trening zamachu",
        "Chipping" to "Chipping",
        "Chipping Session" to "Sesja chipping",
        "Putting" to "Putting",
        "Putting Session" to "Sesja putting",
        "Gapping" to "Dobór odległości",
        "Gapping Session" to "Sesja doboru odległości",
        "Listener Session" to "Sesja nasłuchiwania",
        "Listener Mode" to "Tryb nasłuchiwania",
        "Swing Training Session" to "Sesja treningu zamachu",
        "Practice" to "Trening",
        "Training" to "Trening",
        "Start" to "Start",
        "Start New Session" to "Rozpocznij nową sesję",
        "New Session" to "Nowa sesja",
        "Resume Session" to "Wznów sesję",
        "Review Session" to "Przegląd sesji",
        "Finish Session" to "Zakończ sesję",
        "End Session" to "Zakończ sesję",
        "Save Session" to "Zapisz sesję",
        "Cancel" to "Anuluj",
        "Close" to "Zamknij",
        "Back" to "Wstecz",
        "Next" to "Dalej",
        "Done" to "Gotowe",
        "Continue" to "Kontynuuj",
        "Confirm" to "Potwierdź",
        "Apply" to "Zastosuj",
        "Reset" to "Resetuj",
        "Retry" to "Spróbuj ponownie",
        "Skip" to "Pomiń",
        "More" to "Więcej",
        "Details" to "Szczegóły",
        "Help" to "Pomoc",
        "Help & Support" to "Pomoc i wsparcie",
        "Support" to "Wsparcie",
        "Settings" to "Ustawienia",
        "About" to "Informacje",

        // ===== Session Setup =====
        "Session Setup" to "Ustawienia sesji",
        "Session Selection" to "Wybór sesji",
        "Select your mode" to "Wybierz tryb",
        "Select Mode" to "Wybierz tryb",
        "Set up your radar" to "Ustaw radar",
        "Setup your radar" to "Ustaw radar",
        "Align Radar To Target" to "Wyrównaj radar do celu",
        "Align Radar to Target" to "Wyrównaj radar do celu",
        "Target Alignment" to "Wyrównanie do celu",
        "Setup Verification" to "Weryfikacja ustawienia",
        "Radar Adjustment" to "Regulacja radaru",
        "Distance to Ball" to "Odległość od piłki",
        "Tee Surface Height" to "Wysokość powierzchni tee",
        "Radar Tilt" to "Pochylenie radaru",
        "Radar Roll" to "Przechylenie radaru",
        "Limited Flight" to "Ograniczony lot",
        "Outdoor" to "Na zewnątrz",
        "Indoor" to "W pomieszczeniu",
        "Tracks ball for min" to "Śledzi piłkę od min.",
        "to max" to "do maks.",
        "Disconnect" to "Rozłącz",
        "Disconnected" to "Rozłączono",
        "Connected" to "Połączono",
        "Connecting" to "Łączenie",
        "Ready" to "Gotowy",
        "Not Ready" to "Niegotowy",

        // ===== Radar / urządzenie =====
        "Radar Data" to "Dane radaru",
        "Radar Settings" to "Ustawienia radaru",
        "Radar" to "Radar",
        "Device Type" to "Typ urządzenia",
        "Battery" to "Bateria",
        "Environment" to "Środowisko",
        "Players" to "Gracze",
        "Player" to "Gracz",
        "Equipment" to "Sprzęt",
        "Club" to "Kij",
        "Club Type" to "Rodzaj kija",
        "Ball" to "Piłka",
        "Ball Type" to "Rodzaj piłki",
        "RCT" to "RCT",
        "Rangefinder" to "Dalmierz",
        "Video Recording" to "Nagrywanie wideo",
        "MultiCam" to "Wiele kamer",
        "Primary Camera" to "Kamera główna",
        "Secondary Camera" to "Kamera dodatkowa",
        "Radar Camera" to "Kamera radaru",
        "Camera" to "Kamera",

        // ===== Tryby i widoki =====
        "Play Mode" to "Tryb gry",
        "Training Mode" to "Tryb treningowy",
        "Planning Tool" to "Narzędzie planowania",
        "Trajectory View" to "Widok trajektorii",
        "Trajectory" to "Trajektoria",
        "Top Trajectory" to "Trajektoria z góry",
        "Side Trajectory" to "Trajektoria z boku",
        "Club Analysis - 2D" to "Analiza kija — 2D",
        "Club Analysis - 3D" to "Analiza kija — 3D",
        "D-Plane" to "Płaszczyzna D",
        "Face Impact" to "Miejsce kontaktu z piłką",
        "Face Impact Location" to "Miejsce kontaktu na główce",
        "Speed/Acceleration Profiles" to "Profile prędkości/przyspieszenia",
        "Speed Profiles" to "Profile prędkości",
        "Acceleration Profiles" to "Profile przyspieszenia",
        "Setup" to "Ustawienie",
        "Session" to "Sesja",
        "Sessions" to "Sesje",

        // ===== Dane uderzenia =====
        "Carry" to "Lot",
        "Roll" to "Toczenie",
        "Total Distance" to "Dystans całkowity",
        "Total" to "Dystans całkowity",
        "Lateral" to "Odchylenie boczne",
        "Club Speed" to "Prędkość kija",
        "Ball Speed" to "Prędkość piłki",
        "Spin Axis" to "Oś obrotu",
        "Spin Loft" to "Loft dynamiczny",
        "Spin Rate" to "Prędkość obrotowa",
        "Spin" to "Obroty",
        "Smash" to "Współczynnik uderzenia",
        "Launch V" to "Kąt startu pionowy",
        "Launch H" to "Kąt startu poziomy",
        "Launch Angle" to "Kąt startu",
        "AOA" to "Kąt natarcia",
        "Height" to "Wysokość",
        "Flight Time" to "Czas lotu",
        "Shot Type" to "Typ uderzenia",
        "Shot" to "Uderzenie",
        "Distance" to "Odległość",
        "Speed" to "Prędkość",
        "Angle" to "Kąt",
        "Direction" to "Kierunek",
        "Target" to "Cel",
        "Launch" to "Start",
        "Apex" to "Punkt szczytowy",

        // ===== Ustawienia =====
        "Surface Type" to "Rodzaj nawierzchni",
        "Soft" to "Miękka",
        "Medium" to "Średnia",
        "Hard" to "Twarda",
        "Tilt" to "Pochylenie",
        "Roll" to "Przechylenie",
        "Measurements Units" to "Jednostki pomiarowe",
        "Measurement Units" to "Jednostki pomiarowe",
        "Trajectories Display" to "Wyświetlanie trajektorii",
        "Data Margins" to "Marginesy danych",
        "Text to Speech" to "Synteza mowy",
        "Badger AI" to "Badger AI",
        "General" to "Ogólne",
        "Display" to "Wyświetlanie",
        "Audio" to "Dźwięk",
        "Notifications" to "Powiadomienia",
        "Language" to "Język",
        "Units" to "Jednostki",
        "Metric" to "Metryczne",
        "Imperial" to "Anglosaskie",
        "Yards" to "Jardy",
        "Feet" to "Stopy",
        "Meters" to "Metry",
        "Miles per hour" to "Mile na godzinę",
        "Kilometers per hour" to "Kilometry na godzinę",

        // ===== Komunikaty / status =====
        "No Data" to "Brak danych",
        "No data available" to "Brak dostępnych danych",
        "Waiting" to "Oczekiwanie",
        "Waiting for shot" to "Oczekiwanie na uderzenie",
        "Searching" to "Wyszukiwanie",
        "Loading" to "Ładowanie",
        "Processing" to "Przetwarzanie",
        "Error" to "Błąd",
        "Warning" to "Ostrzeżenie",
        "Success" to "Gotowe",
        "Unavailable" to "Niedostępne",
        "Enabled" to "Włączone",
        "Disabled" to "Wyłączone",
        "On" to "Włączone",
        "Off" to "Wyłączone",
        "Yes" to "Tak",
        "No" to "Nie",

        // ===== Pozostałe =====
        "Save" to "Zapisz",
        "Edit" to "Edytuj",
        "Delete" to "Usuń",
        "Add" to "Dodaj",
        "Remove" to "Usuń",
        "Search" to "Szukaj",
        "Filter" to "Filtruj",
        "Sort" to "Sortuj",
        "More Options" to "Więcej opcji",
        "Information" to "Informacje",
        "Learn More" to "Dowiedz się więcej",
        "Refresh" to "Odśwież",
        "Update" to "Aktualizuj",
        "Download" to "Pobierz",
        "Upload" to "Prześlij",
        "Share" to "Udostępnij",
        "Cloud" to "Chmura"
    )

    fun translate(raw: String): String? {
        dict[raw]?.let { return it }
        return dict.entries.firstOrNull { it.key.equals(raw, ignoreCase = true) }?.value
    }
    }

    override fun onServiceConnected() {
        super.onServiceConnected()
        wm = getSystemService(WINDOW_SERVICE) as WindowManager
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        if (event == null || !Settings.canDrawOverlays(this)) return

        // Keep the accessibility service deliberately conservative.
        // Screen OCR runs in the dedicated MediaProjection foreground service,
        // rather than from this event callback, which keeps accessibility stable.
        // This stable build uses only the accessibility tree.
        runCatching {
            val root = rootInActiveWindow ?: return
            val packageName = root.packageName?.toString().orEmpty()
            if (packageName == applicationContext.packageName) return

            val found = mutableSetOf<String>()
            walk(root, found)
            removeStale(found)
        }
    }

    private fun walk(node: AccessibilityNodeInfo, found: MutableSet<String>) {
        val candidates = listOfNotNull(
            node.text?.toString()?.trim(),
            if (Build.VERSION.SDK_INT >= 26) node.hintText?.toString()?.trim() else null,
            node.contentDescription?.toString()?.trim()
        )

        for (raw in candidates) {
            val pl = exactTranslation(raw) ?: continue
            if (node.isVisibleToUser) {
                val bounds = Rect()
                node.getBoundsInScreen(bounds)
                if (bounds.width() > 0 && bounds.height() > 0) {
                    val key = "a:$raw@${bounds.left},${bounds.top},${bounds.right},${bounds.bottom}"
                    found.add(key)
                    addOrUpdateOverlay(key, pl, bounds)
                    break
                }
            }
        }

        for (i in 0 until node.childCount) {
            node.getChild(i)?.let { child ->
                walk(child, found)
                child.recycle()
            }
        }
    }

    private fun exactTranslation(raw: String): String? {
        return translate(raw)
    }

    private fun removeStale(found: MutableSet<String>) {
        overlays.keys.filter { it.startsWith("a:") && it !in found }.toList().forEach { key ->
            overlays.remove(key)?.let { runCatching { wm?.removeView(it) } }
        }
    }

    private fun addOrUpdateOverlay(key: String, pl: String, bounds: Rect) {
        val existing = overlays[key]
        if (existing != null) {
            existing.text = pl
            return
        }

        val tv = TextView(this).apply {
            text = pl
            textSize = 17f
            setTextColor(Color.WHITE)
            setBackgroundColor(0xE6000000.toInt())
            setPadding(10, 6, 10, 6)
            gravity = Gravity.CENTER
            maxLines = 2
            includeFontPadding = true
        }

        val width = maxOf(bounds.width(), estimateWidth(pl, 17))
        val height = maxOf(bounds.height(), 42)
        val params = WindowManager.LayoutParams(
            width,
            height,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,
            PixelFormat.TRANSLUCENT
        )
        params.gravity = Gravity.TOP or Gravity.START
        params.x = bounds.left
        params.y = bounds.top
        runCatching {
            wm?.addView(tv, params)
            overlays[key] = tv
        }
    }

    private fun estimateWidth(text: String, sp: Int): Int {
        return (text.length * sp * 0.65f + 24).toInt().coerceAtLeast(80)
    }

    override fun onInterrupt() = Unit

    override fun onDestroy() {
        handler.removeCallbacksAndMessages(null)
        overlays.values.forEach { runCatching { wm?.removeView(it) } }
        overlays.clear()
        super.onDestroy()
    }
}

