package pl.fsgolfpl

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.provider.Settings
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView

class MainActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 48, 32, 32)
        }
        layout.addView(TextView(this).apply {
            text = "FS Golf PL\n\nNakładka tłumaczy widoczne napisy FS Golf na język polski.\n\n1. Włącz wyświetlanie nad innymi aplikacjami.\n2. Włącz usługę FS Golf PL w Ułatwieniach dostępu.\n3. Uruchom FS Golf."
            textSize = 18f
        })
        layout.addView(Button(this).apply {
            text = "Włącz wyświetlanie nad innymi aplikacjami"
            setOnClickListener {
                startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:$packageName")))
            }
        })
        layout.addView(Button(this).apply {
            text = "Włącz usługę FS Golf PL"
            setOnClickListener { startActivity(Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS)) }
        })
        setContentView(layout)
    }
}
