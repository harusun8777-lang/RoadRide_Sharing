package com.roadride.sharing

import android.content.Intent
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import com.roadride.sharing.databinding.ActivitySplashBinding

/**
 * 起動画面：利用者 or 配車担当者を選択してWebViewへ遷移する
 */
class SplashActivity : AppCompatActivity() {

    private lateinit var binding: ActivitySplashBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySplashBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // 利用者として開く
        binding.btnRider.setOnClickListener {
            openWebView(BuildConfig.RIDER_URL, Mode.RIDER)
        }

        // 配車担当者として開く
        binding.btnDispatcher.setOnClickListener {
            openWebView(BuildConfig.DISPATCHER_URL, Mode.DISPATCHER)
        }
    }

    private fun openWebView(url: String, mode: Mode) {
        val intent = Intent(this, MainActivity::class.java).apply {
            putExtra(MainActivity.EXTRA_URL, url)
            putExtra(MainActivity.EXTRA_MODE, mode.name)
        }
        startActivity(intent)
    }
}
