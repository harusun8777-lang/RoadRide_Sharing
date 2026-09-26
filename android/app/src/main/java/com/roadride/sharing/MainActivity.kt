package com.roadride.sharing

import android.annotation.SuppressLint
import android.content.Context
import android.graphics.Bitmap
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.Bundle
import android.view.View
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.appcompat.app.AppCompatActivity
import com.roadride.sharing.databinding.ActivityMainBinding

/**
 * WebView を全画面で表示するActivity
 * - JavaScriptを有効化
 * - Androidの戻るボタンでWebViewの履歴を戻る
 * - ネットワークエラー時にエラー画面を表示し再試行できる
 * - SwipeRefreshLayoutでプルリフレッシュに対応
 */
class MainActivity : AppCompatActivity() {

    companion object {
        const val EXTRA_URL  = "extra_url"
        const val EXTRA_MODE = "extra_mode"
    }

    private lateinit var binding: ActivityMainBinding
    private var currentUrl: String = ""

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityMainBinding.inflate(layoutInflater)
        setContentView(binding.root)

        currentUrl = intent.getStringExtra(EXTRA_URL) ?: BuildConfig.RIDER_URL

        setupWebView()
        setupSwipeRefresh()
        setupRetryButton()

        loadUrl(currentUrl)
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun setupWebView() {
        binding.webView.settings.apply {
            javaScriptEnabled = true                          // JavaScriptを有効化
            domStorageEnabled = true                          // localStorageを有効化
            loadWithOverviewMode = true
            useWideViewPort = true
            setSupportZoom(false)                             // ピンチズームを無効化（アプリらしく）
            cacheMode = WebSettings.LOAD_DEFAULT
            mixedContentMode = WebSettings.MIXED_CONTENT_NEVER_ALLOW
        }

        binding.webView.webViewClient = object : WebViewClient() {

            override fun onPageStarted(view: WebView?, url: String?, favicon: Bitmap?) {
                super.onPageStarted(view, url, favicon)
                binding.progressBar.visibility = View.VISIBLE
                binding.errorView.visibility   = View.GONE
            }

            override fun onPageFinished(view: WebView?, url: String?) {
                super.onPageFinished(view, url)
                binding.progressBar.visibility  = View.GONE
                binding.swipeRefresh.isRefreshing = false
            }

            override fun onReceivedError(
                view: WebView?,
                request: WebResourceRequest?,
                error: WebResourceError?
            ) {
                super.onReceivedError(view, request, error)
                // メインフレームのエラーだけエラー画面を表示する
                if (request?.isForMainFrame == true) {
                    binding.progressBar.visibility  = View.GONE
                    binding.webView.visibility       = View.GONE
                    binding.errorView.visibility     = View.VISIBLE
                    binding.swipeRefresh.isRefreshing = false
                }
            }
        }
    }

    private fun setupSwipeRefresh() {
        binding.swipeRefresh.setOnRefreshListener {
            if (isNetworkAvailable()) {
                binding.webView.visibility  = View.VISIBLE
                binding.errorView.visibility = View.GONE
                binding.webView.reload()
            } else {
                binding.swipeRefresh.isRefreshing = false
                binding.errorView.visibility       = View.VISIBLE
            }
        }
    }

    private fun setupRetryButton() {
        binding.btnRetry.setOnClickListener {
            if (isNetworkAvailable()) {
                binding.webView.visibility  = View.VISIBLE
                binding.errorView.visibility = View.GONE
                loadUrl(currentUrl)
            }
        }
    }

    private fun loadUrl(url: String) {
        if (isNetworkAvailable()) {
            binding.webView.loadUrl(url)
        } else {
            binding.progressBar.visibility = View.GONE
            binding.errorView.visibility   = View.VISIBLE
        }
    }

    /** ネットワーク接続確認 */
    private fun isNetworkAvailable(): Boolean {
        val cm = getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val network = cm.activeNetwork ?: return false
        val caps    = cm.getNetworkCapabilities(network) ?: return false
        return caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
    }

    /** 戻るボタンでWebViewの履歴を戻る */
    @Suppress("OVERRIDE_DEPRECATION")
    override fun onBackPressed() {
        if (binding.webView.canGoBack()) {
            binding.webView.goBack()
        } else {
            super.onBackPressed()
        }
    }
}
