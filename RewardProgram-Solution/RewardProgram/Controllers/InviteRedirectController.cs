using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RewardProgram.Application.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace RewardProgram.API.Controllers;

[ApiController]
[Route("invite")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class InviteRedirectController : ControllerBase
{
    private static readonly Regex CodePattern = new("^[A-Z0-9]{8}$", RegexOptions.Compiled);

    private readonly InvitationOptions _options;

    public InviteRedirectController(IOptions<InvitationOptions> options)
    {
        _options = options.Value;
    }

    [HttpGet("{code}")]
    public IActionResult Get(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code))
            return NotFound();

        Response.Headers.CacheControl = "public, max-age=300";
        return Content(BuildHtml(code), "text/html; charset=utf-8");
    }

    private string BuildHtml(string code)
    {
        var safeCode = WebUtility.HtmlEncode(code);
        var iosStore = WebUtility.HtmlEncode(_options.IosAppStoreUrl);
        var androidStore = WebUtility.HtmlEncode(_options.AndroidPlayStoreUrl);

        return $$"""
<!DOCTYPE html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no">
  <title>دعوة رائد - {{safeCode}}</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", "Tahoma", sans-serif;
      background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }
    .card {
      background: #fff;
      color: #1f2937;
      border-radius: 20px;
      padding: 32px 24px;
      max-width: 420px;
      width: 100%;
      box-shadow: 0 20px 60px rgba(0,0,0,0.25);
      text-align: center;
    }
    h1 { margin: 0 0 8px; font-size: 22px; color: #1e3a8a; }
    .subtitle { margin: 0 0 24px; color: #6b7280; font-size: 15px; }
    .code-label { font-size: 13px; color: #6b7280; margin-bottom: 8px; }
    .code-box {
      background: #f3f4f6;
      border: 2px dashed #3b82f6;
      border-radius: 12px;
      padding: 16px;
      margin-bottom: 12px;
      font-family: ui-monospace, "SF Mono", Menlo, monospace;
      font-size: 28px;
      letter-spacing: 4px;
      font-weight: 700;
      color: #1e3a8a;
      direction: ltr;
    }
    .copy-btn {
      background: transparent;
      border: 1px solid #d1d5db;
      color: #4b5563;
      padding: 8px 16px;
      border-radius: 8px;
      font-size: 14px;
      cursor: pointer;
      margin-bottom: 24px;
      font-family: inherit;
    }
    .copy-btn:active { background: #f3f4f6; }
    .copy-btn.copied { background: #10b981; color: #fff; border-color: #10b981; }
    .stores { display: grid; gap: 10px; }
    .store-btn {
      display: block;
      padding: 14px;
      border-radius: 12px;
      text-decoration: none;
      font-weight: 600;
      font-size: 15px;
      text-align: center;
    }
    .store-btn.primary { background: #1e3a8a; color: #fff; }
    .store-btn.secondary { background: #f3f4f6; color: #1f2937; }
    .hint { margin-top: 20px; font-size: 13px; color: #9ca3af; line-height: 1.5; }
  </style>
</head>
<body>
  <div class="card">
    <h1>أنت مدعو إلى تطبيق رائد</h1>
    <p class="subtitle">استخدم رمز الدعوة عند التسجيل واحصل على نقاط ترحيبية</p>

    <div class="code-label">رمز الدعوة</div>
    <div class="code-box" id="code">{{safeCode}}</div>
    <button class="copy-btn" id="copyBtn" type="button">نسخ الرمز</button>

    <div class="stores">
      <a class="store-btn primary" id="androidBtn" href="{{androidStore}}">تنزيل من Google Play</a>
      <a class="store-btn secondary" id="iosBtn" href="{{iosStore}}">تنزيل من App Store</a>
    </div>

    <p class="hint">إذا كان التطبيق مثبتًا، سيُفتح تلقائيًا. وإلا حمّله من المتجر وأدخل الرمز عند التسجيل.</p>
  </div>

  <script>
    (function () {
      var code = {{System.Text.Json.JsonSerializer.Serialize(code)}};
      var deepLink = {{System.Text.Json.JsonSerializer.Serialize(_options.DeepLinkScheme + code)}};
      var iosStore = {{System.Text.Json.JsonSerializer.Serialize(_options.IosAppStoreUrl)}};
      var androidPackage = {{System.Text.Json.JsonSerializer.Serialize(_options.AndroidPackageName)}};
      var androidStoreEncoded = {{System.Text.Json.JsonSerializer.Serialize(_options.AndroidPlayStoreUrl)}};

      // Copy to clipboard
      var btn = document.getElementById('copyBtn');
      btn.addEventListener('click', function () {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(code).then(function () {
            btn.textContent = 'تم النسخ ✓';
            btn.classList.add('copied');
            setTimeout(function () {
              btn.textContent = 'نسخ الرمز';
              btn.classList.remove('copied');
            }, 1800);
          }).catch(function () { fallbackCopy(); });
        } else {
          fallbackCopy();
        }
      });
      function fallbackCopy() {
        var range = document.createRange();
        range.selectNode(document.getElementById('code'));
        var sel = window.getSelection();
        sel.removeAllRanges();
        sel.addRange(range);
        try { document.execCommand('copy'); btn.textContent = 'تم النسخ ✓'; btn.classList.add('copied'); }
        catch (e) {}
        sel.removeAllRanges();
      }

      // Best-effort deep link
      var ua = navigator.userAgent || '';
      var isIos = /iPhone|iPad|iPod/i.test(ua);
      var isAndroid = /Android/i.test(ua);

      if (isAndroid && androidPackage) {
        // Intent URL falls back to Play Store automatically if app missing
        var intentUrl = 'intent://invite/' + encodeURIComponent(code) +
          '#Intent;scheme=raedreward;package=' + androidPackage +
          ';S.browser_fallback_url=' + encodeURIComponent(androidStoreEncoded) + ';end';
        window.location.href = intentUrl;
      } else if (isIos && deepLink) {
        // Try custom scheme; if app not installed, Safari shows a prompt and we
        // fall back to App Store after a short delay. Universal Links (once
        // AASA is set up) will skip the prompt entirely.
        var start = Date.now();
        var timer = setTimeout(function () {
          if (Date.now() - start < 2000 && iosStore) {
            window.location.href = iosStore;
          }
        }, 1500);
        window.addEventListener('pagehide', function () { clearTimeout(timer); });
        window.location.href = deepLink;
      }
      // Desktop: just show the page; user copies code or uses store buttons.
    })();
  </script>
</body>
</html>
""";
    }
}
