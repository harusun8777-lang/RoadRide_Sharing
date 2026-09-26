# WebView で使用するクラスは難読化しない
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}
