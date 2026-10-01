package pl.fsgolfpl

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.Color
import android.graphics.PixelFormat
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.Image
import android.media.ImageReader
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Handler
import android.os.HandlerThread
import android.os.IBinder
import android.util.DisplayMetrics
import android.view.Gravity
import android.view.WindowManager
import android.widget.TextView
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.latin.TextRecognizerOptions
import java.util.concurrent.atomic.AtomicBoolean

class ProjectionOcrService : Service() {
    companion object {
        const val EXTRA_RESULT_CODE = "projection_result_code"
        const val EXTRA_RESULT_DATA = "projection_result_data"
        private const val CHANNEL_ID = "fsgolfpl_projection"
        private const val NOTIFICATION_ID = 6006
    }

    private var projection: MediaProjection? = null
    private var display: VirtualDisplay? = null
    private var reader: ImageReader? = null
    private var worker: HandlerThread? = null
    private var wm: WindowManager? = null
    private var lastFrameAt = 0L
    private val processing = AtomicBoolean(false)
    private val labels = mutableMapOf<String, TextView>()
    private val missingFrames = mutableMapOf<String, Int>()
    private val recognizer by lazy { TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS) }

    private val projectionCallback = object : MediaProjection.Callback() {
        override fun onStop() {
            stopSelf()
        }
    }

    override fun onCreate() {
        super.onCreate()
        wm = getSystemService(WINDOW_SERVICE) as WindowManager
        val channel = NotificationChannel(CHANNEL_ID, "Tłumaczenie ekranu", NotificationManager.IMPORTANCE_LOW)
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        startForeground(NOTIFICATION_ID, Notification.Builder(this, CHANNEL_ID)
            .setContentTitle("FS Golf PL")
            .setContentText("Tłumaczenie OCR ekranu jest aktywne")
            .setSmallIcon(android.R.drawable.ic_menu_search)
            .setOngoing(true)
            .build())
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val resultCode = intent?.getIntExtra(EXTRA_RESULT_CODE, 0) ?: 0
        val resultData = if (Build.VERSION.SDK_INT >= 33) {
            intent?.getParcelableExtra(EXTRA_RESULT_DATA, Intent::class.java)
        } else {
            @Suppress("DEPRECATION")
            intent?.getParcelableExtra(EXTRA_RESULT_DATA)
        }
        if (resultCode == 0 || resultData == null) {
            stopSelf(startId)
            return START_NOT_STICKY
        }
        runCatching { startCapture(resultCode, resultData) }.onFailure { stopSelf(startId) }
        return START_NOT_STICKY
    }

    private fun startCapture(resultCode: Int, data: Intent) {
        val metrics = DisplayMetrics()
        @Suppress("DEPRECATION")
        (getSystemService(WINDOW_SERVICE) as WindowManager).defaultDisplay.getRealMetrics(metrics)
        val width = metrics.widthPixels
        val height = metrics.heightPixels
        val density = metrics.densityDpi
        val thread = HandlerThread("FSGolfPL-OCR").apply { start() }
        worker = thread
        val handler = Handler(thread.looper)
        val imageReader = ImageReader.newInstance(width, height, PixelFormat.RGBA_8888, 2)
        reader = imageReader
        imageReader.setOnImageAvailableListener({ source -> processLatestFrame(source, handler) }, handler)
        val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        val mediaProjection = manager.getMediaProjection(resultCode, data)
        projection = mediaProjection
        mediaProjection.registerCallback(projectionCallback, handler)
        display = mediaProjection.createVirtualDisplay(
            "FS Golf PL OCR", width, height, density,
            DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
            imageReader.surface, null, handler
        )
    }

    private fun processLatestFrame(source: ImageReader, handler: Handler) {
        val image = runCatching { source.acquireLatestImage() }.getOrNull() ?: return
        val now = System.currentTimeMillis()
        if (now - lastFrameAt < 850 || !processing.compareAndSet(false, true)) {
            image.close()
            return
        }
        lastFrameAt = now
        val bitmap = runCatching { imageToBitmap(image) }.getOrNull()
        image.close()
        if (bitmap == null) {
            processing.set(false)
            return
        }
        recognizer.process(InputImage.fromBitmap(bitmap, 0))
            .addOnSuccessListener { result -> updateLabels(result.textBlocks, bitmap.width, bitmap.height) }
            .addOnCompleteListener { bitmap.recycle(); processing.set(false) }
    }

    private fun imageToBitmap(image: Image): Bitmap {
        val plane = image.planes[0]
        val pixelStride = plane.pixelStride
        val rowStride = plane.rowStride
        val paddedWidth = rowStride / pixelStride
        val padded = Bitmap.createBitmap(paddedWidth, image.height, Bitmap.Config.ARGB_8888)
        plane.buffer.rewind()
        padded.copyPixelsFromBuffer(plane.buffer)
        if (paddedWidth == image.width) return padded
        val cropped = Bitmap.createBitmap(padded, 0, 0, image.width, image.height)
        padded.recycle()
        return cropped
    }

    private fun updateLabels(blocks: List<com.google.mlkit.vision.text.Text.TextBlock>, frameWidth: Int, frameHeight: Int) {
        val metrics = DisplayMetrics()
        @Suppress("DEPRECATION")
        (getSystemService(WINDOW_SERVICE) as WindowManager).defaultDisplay.getRealMetrics(metrics)
        val scaleX = metrics.widthPixels.toFloat() / frameWidth
        val scaleY = metrics.heightPixels.toFloat() / frameHeight
        val activeKeys = mutableSetOf<String>()
        for (block in blocks) for (line in block.lines) {
            val source = line.text.trim().trimEnd('.', ':')
            val translated = FsAccessibilityService.translate(source) ?: continue
            val box = line.boundingBox ?: continue
            if (box.width() < 18 || box.height() < 8) continue
            val key = "ocr:${box.left},${box.top},${box.right},${box.bottom}"
            activeKeys.add(key)
            missingFrames.remove(key)
            val view = labels[key]
            if (view != null) {
                view.text = translated
                continue
            }
            val label = TextView(this).apply {
                text = translated
                textSize = 15f
                setTextColor(Color.WHITE)
                setBackgroundColor(0xE6000000.toInt())
                setPadding(6, 2, 6, 2)
                gravity = Gravity.CENTER
                maxLines = 2
                includeFontPadding = true
            }
            val width = (box.width() * scaleX).toInt().coerceAtLeast((translated.length * 9 + 12).coerceAtMost(360))
            val height = (box.height() * scaleY).toInt().coerceAtLeast(34)
            val params = WindowManager.LayoutParams(
                width.coerceAtMost(metrics.widthPixels), height,
                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE or
                    WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN or WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS,
                PixelFormat.TRANSLUCENT
            ).apply {
                gravity = Gravity.TOP or Gravity.START
                x = (box.left * scaleX).toInt().coerceAtLeast(0)
                y = (box.top * scaleY).toInt().coerceAtLeast(0)
            }
            runCatching { wm?.addView(label, params); labels[key] = label }
        }
        labels.keys.filter { it !in activeKeys }.toList().forEach { key ->
            val absent = (missingFrames[key] ?: 0) + 1
            if (absent >= 4) {
                labels.remove(key)?.let { runCatching { wm?.removeView(it) } }
                missingFrames.remove(key)
            } else {
                missingFrames[key] = absent
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        display?.release()
        reader?.setOnImageAvailableListener(null, null)
        reader?.close()
        projection?.unregisterCallback(projectionCallback)
        projection?.stop()
        worker?.quitSafely()
        labels.values.forEach { runCatching { wm?.removeView(it) } }
        labels.clear()
        missingFrames.clear()
        recognizer.close()
        stopForeground(STOP_FOREGROUND_REMOVE)
        super.onDestroy()
    }
}

