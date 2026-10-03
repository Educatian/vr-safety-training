// Result card sharing (Runtime/ShareResult.cs). Called from a button click, so the browser still counts it as a user
// gesture. Phones get the native share sheet; desktops get the clipboard, with an execCommand fallback.
mergeInto(LibraryManager.library, {
  CP_Share: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      var touch = ("ontouchstart" in window) || (navigator.maxTouchPoints > 0);
      if (touch && navigator.share) { navigator.share({ text: text }).catch(function () { }); return 1; }
      if (navigator.clipboard && window.isSecureContext) { navigator.clipboard.writeText(text).catch(function () { }); return 2; }
      var ta = document.createElement("textarea");
      ta.value = text; ta.style.position = "fixed"; ta.style.opacity = "0";
      document.body.appendChild(ta); ta.select();
      var ok = document.execCommand("copy");
      document.body.removeChild(ta);
      return ok ? 2 : 0;
    } catch (e) { return 0; }
  }
});
