package pl.fsgolfpl

import android.accessibilityservice.AccessibilityService
import android.graphics.Color
import android.graphics.PixelFormat
import android.graphics.Rect
import android.provider.Settings
import android.view.Gravity
import android.view.WindowManager
import android.widget.TextView
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo

/**
 * FS Golf PL - accessibility based translation overlay.
 * It reads visible UI text exposed by FS Golf and places a Polish label
 * directly over the corresponding English label.
 */
class FsAccessibilityService : AccessibilityService() {
    private var wm: WindowManager? = null
    private val overlays = mutableMapOf<String, TextView>()

    private val dict = mapOf(
        "Carry" to "Lot", "Roll" to "Toczenie", "Total" to "Dystans całkowity",
        "Lateral" to "Odchylenie boczne", "Club Speed" to "Prędkość kija",
        "Ball Speed" to "Prędkość piłki", "Spin" to "Obroty", "Spin Axis" to "Oś obrotu",
        "Spin Loft" to "Loft dynamiczny", "Smash" to "Współczynnik uderzenia",
        "Launch V" to "Kąt startu pionowy", "Launch H" to "Kąt startu poziomy",
        "AOA" to "Kąt natarcia", "Height" to "Wysokość", "Flight Time" to "Czas lotu",
        "Shot Type" to "Typ uderzenia", "Club" to "Kij", "Ball" to "Piłka",
        "Ready" to "Gotowy", "Connected" to "Połączony", "Finish Session" to "Zakończ sesję",
        "Radar Data" to "Dane radaru", "Trajectory View" to "Widok trajektorii",
        "Settings" to "Ustawienia", "Full Swing" to "Pełny zamach",
        "Full Swing Session" to "Sesja pełnego zamachu", "Putting Session" to "Sesja putting",
        "Swing Training" to "Trening zamachu", "Chipping Session" to "Sesja chipping",
        "Review Session" to "Przegląd sesji", "Play Mode" to "Tryb gry",
        "Listener Mode" to "Tryb nasłuchiwania", "FlightScope Cloud" to "Chmura FlightScope",
        "Planning Tool" to "Narzędzie planowania", "Club Analysis - 2D" to "Analiza kija — 2D",
        "Club Analysis - 3D" to "Analiza kija — 3D", "D-Plane" to "Płaszczyzna D",
        "Face Impact" to "Miejsce kontaktu z piłką", "Speed/Acceleration Profiles" to "Profile prędkości/przyspieszenia",
        "Top Trajectory" to "Trajektoria z góry", "Side Trajectory" to "Trajektoria z boku",
        "Radar Camera" to "Kamera radaru", "Outdoor" to "Na zewnątrz",
        "Limited Flight" to "Ograniczony lot", "Surface Type" to "Rodzaj nawierzchni",
        "Soft" to "Miękka", "Medium" to "Średnia", "Hard" to "Twarda",
        "Tilt" to "Pochylenie", "Roll" to "Przechylenie",
        "Battery" to "Bateria", "Device Type" to "Typ urządzenia",
        "Players" to "Gracze", "Equipment" to "Sprzęt", "Environment" to "Środowisko",
        "Rangefinder" to "Dalmierz", "Video Recording" to "Nagrywanie wideo",
        "Measurements Units" to "Jednostki pomiarowe", "Trajectories Display" to "Wyświetlanie trajektorii",
        "Data Margins" to "Marginesy danych", "Text to Speech" to "Synteza mowy",
        "Help & Support" to "Pomoc i wsparcie", "Launch Angle" to "Kąt startu",
        "Spin Rate" to "Prędkość obrotowa", "Distance" to "Odległość", "Total Distance" to "Dystans całkowity"
    )

    override fun onServiceConnected() {
        super.onServiceConnected()
        wm = getSystemService(WINDOW_SERVICE) as WindowManager
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        if (event == null || !Settings.canDrawOverlays(this)) return
        val root = rootInActiveWindow ?: return
        val found = mutableSetOf<String>()
        walk(root, found)
        overlays.keys.filter { it !in found }.toList().forEach { key ->
            overlays.remove(key)?.let { runCatching { wm?.removeView(it) } }
        }
    }

    private fun walk(node: AccessibilityNodeInfo, found: MutableSet<String>) {
        val raw = node.text?.toString()?.trim().orEmpty()
        val pl = dict[raw]
        if (pl != null && node.isVisibleToUser) {
            val bounds = Rect()
            node.getBoundsInScreen(bounds)
            if (bounds.width() > 0 && bounds.height() > 0) {
                val key = "$raw@${bounds.left},${bounds.top}"
                found.add(key)
                if (!overlays.containsKey(key)) addOverlay(key, pl, bounds)
            }
        }
        for (i in 0 until node.childCount) node.getChild(i)?.let { child ->
            walk(child, found)
            child.recycle()
        }
    }

    private fun addOverlay(key: String, pl: String, bounds: Rect) {
        val tv = TextView(this).apply {
            text = pl
            textSize = 11f
            setTextColor(Color.WHITE)
            setBackgroundColor(0xD9000000.toInt())
            setPadding(6, 3, 6, 3)
            maxLines = 2
        }
        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,
            PixelFormat.TRANSLUCENT
        )
        params.gravity = Gravity.TOP or Gravity.START
        params.x = bounds.left
        params.y = bounds.top
        runCatching { wm?.addView(tv, params); overlays[key] = tv }
    }

    override fun onInterrupt() = Unit

    override fun onDestroy() {
        overlays.values.forEach { runCatching { wm?.removeView(it) } }
        overlays.clear()
        super.onDestroy()
    }
}
