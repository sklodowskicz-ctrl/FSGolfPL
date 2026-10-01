package pl.fsgolfpl

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Bundle
import android.provider.Settings
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView

class MainActivity : Activity() {
    private val captureRequest = 4106

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(32, 48, 32, 32)
        }
        layout.addView(TextView(this).apply {
            text = "FS Golf PL 0.7\n\nTłumaczy napisy FS Golf na język polski. OCR działa przez przechwytywanie ekranu Androida (MediaProjection).\n\n1. Zezwól na wyświetlanie nad innymi aplikacjami.\n2. Włącz usługę FS Golf PL w Ułatwieniach dostępu.\n3. Naciśnij przycisk przechwytywania i zaakceptuj komunikat Androida.\n4. Otwórz FS Golf."
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
        layout.addView(Button(this).apply {
            text = "Uruchom tłumaczenie OCR ekranu"
            setOnClickListener {
                if (!Settings.canDrawOverlays(this@MainActivity)) {
                    startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:$packageName")))
                } else {
                    val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
                    startActivityForResult(manager.createScreenCaptureIntent(), captureRequest)
                }
            }
        })
        layout.addView(Button(this).apply {
            text = "Zatrzymaj tłumaczenie OCR"
            setOnClickListener { stopService(Intent(this@MainActivity, ProjectionOcrService::class.java)) }
        })
        setContentView(layout)
    }

    @Deprecated("Required for the MediaProjection consent result on supported Android versions")
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode != captureRequest || resultCode != RESULT_OK || data == null) return
        val intent = Intent(this, ProjectionOcrService::class.java).apply {
            putExtra(ProjectionOcrService.EXTRA_RESULT_CODE, resultCode)
            putExtra(ProjectionOcrService.EXTRA_RESULT_DATA, data)
        }
        startForegroundService(intent)
    }
}

