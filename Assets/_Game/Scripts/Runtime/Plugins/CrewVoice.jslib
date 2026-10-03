// Browser speech synthesis for crew lines (Runtime/CrewVoice.cs). Picks an English voice of the requested family
// when the browser exposes one, cancels the previous line so crew never talk over each other, and fails silently
// where speechSynthesis is missing (captions carry every line anyway).
mergeInto(LibraryManager.library, {
  CP_Speak: function (textPtr, pitch, rate, volume, female) {
    try {
      var synth = window.speechSynthesis;
      if (!synth || typeof SpeechSynthesisUtterance === "undefined") return;
      var text = UTF8ToString(textPtr);
      if (!text) return;
      var u = new SpeechSynthesisUtterance(text);
      u.lang = "en-US"; u.pitch = pitch; u.rate = rate; u.volume = volume;
      var voices = synth.getVoices() || [];
      var en = voices.filter(function (v) { return /^en[-_]/i.test(v.lang); });
      var femaleRe = /female|woman|zira|aria|jenny|samantha|victoria|karen|moira|tessa|fiona|susan|hazel|libby|sonia|michelle|google us english/i;
      var maleRe = /male|man|david|guy|mark|daniel|alex|fred|george|ryan|christopher|eric|james|tom|google uk english male/i;
      var pick = en.filter(function (v) { return female ? femaleRe.test(v.name) && !/\bmale\b/i.test(v.name) : maleRe.test(v.name) && !/female/i.test(v.name); })[0] || en[0];
      if (pick) u.voice = pick;
      synth.cancel();
      synth.speak(u);
    } catch (e) { }
  },
  CP_StopSpeech: function () {
    try { if (window.speechSynthesis) window.speechSynthesis.cancel(); } catch (e) { }
  }
});
