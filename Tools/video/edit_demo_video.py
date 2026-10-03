#!/usr/bin/env python3
"""Cut the Competent Person demo video from the scripted gameplay capture.

Inputs (project root):
  Captures/demo/<segment>/f00000.jpg ...   30 fps frames from Jobsite.PlayTests.DemoVideoTests
  Captures/demo/vo/01.wav ... 12.wav       English narration (Higgsfield, seed_audio, voice "Holden")
  Assets/_Game/Resources/Audio/ambience.wav  site bed under the voice
Outputs: Captures/demo/out/competent_person_demo.mp4 (+ .srt)

Each narration line is one block; the block's clips are retimed (0.7x-1.7x) to the line, then trimmed or held.
Subtitles are burned in; a section label sits top-left for the first seconds of each block.
"""
import json, os, re, subprocess, sys, wave

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
CAP = os.path.join(ROOT, "Captures", "demo")
OUT = os.path.join(CAP, os.environ.get("DEMO_OUT", "out"))
FONT_DIR = os.path.join(ROOT, "Assets", "_Game", "Resources", "Fonts")
FONT = os.path.join(FONT_DIR, "BarlowCondensed-SemiBold.ttf")
AMB = os.path.join(ROOT, "Assets", "_Game", "Resources", "Audio", "ambience.wav")
FPS = 30
W, H = 1920, 1080   # output
PW, PH = 1280, 720  # subtitle layout space (libass scales it to the output)

BLOCKS = [
    ("01", ["01_hook"], "",
     "On a construction site, the most dangerous hazard is the one nobody noticed. Competent Person puts you in the boots of the person whose job is to notice."),
    ("02", ["02_menu"], "A FREE BROWSER SERIOUS GAME",
     "It's a free browser serious game built on OSHA's construction standards (29 CFR 1926). Work a five-day course as the site's competent person, or jump straight into Hazard Hunt."),
    ("03", ["03_hunt"], "HAZARD HUNT · DAILY SITE",
     "Hazard Hunt drops everyone on the same daily site. You get three real minutes, while the shift clock races ahead."),
    ("04", ["04_report"], "SPOT · PHOTOGRAPH · ASSESS · CONTROL",
     "Spot something, photograph it, and your field tablet asks the competent person's questions. What energy could hurt someone? How likely, how bad? Which control fixes it? Report a harmless look-alike, and you lose points and the crew's trust."),
    ("05", ["05a_laser", "05b_cord"], "HANDS-ON · MEASURE & INSPECT",
     "Some calls need evidence. Shoot a laser measurement across the spoil pile, or pick up a damaged cord and turn it over until you find the defect."),
    ("06", ["06a_midrail", "06b_cover", "06c_barricade"], "HANDS-ON · INSTALL THE FIX",
     "Then fix it with your own hands. Set a midrail at the right height, cover a floor hole and mark it, and ring the excavator's swing path with cones."),
    ("07", ["07_interview"], "TALK TO THE CREW",
     "The crew is alive. Ask them anything, in your own words. Dolores remembers last night's rain, and that clue points you to a failing trench wall."),
    ("08", ["08a_coaching", "08b_speakup"], "COACH · STOP WORK · SPEAK UP",
     "When a worker cuts a corner, coach them: ask, explain why, and agree on a fix, so the change actually sticks. Stop the work, and the foreman pushes back. Hold your ground, respectfully."),
    ("09", ["09_incident"], "CONSEQUENCES",
     "Leave a hazard alone, and the clock keeps running. Exposures turn into near misses, and the crew remembers."),
    ("10", ["10_results"], "EVIDENCE-CENTERED SCORING",
     "Every action is evidence. The game scores fourteen competencies, anchored to OSHA rules, and ends with a spoiler-free results grid you can share, plus a daily leaderboard."),
    ("11", ["11_course"], "COURSE MODE · FIVE DAYS",
     "In course mode, each day starts with the gate check-in and a briefing. You keep the excavation inspection log, and you close with a toolbox talk for tomorrow's crew, in your own words."),
    ("12", ["12_outro"], "",
     "Competent Person. Spot the hazard before someone gets hurt. Play free in your browser at competent-person.pages.dev"),
]
GAP = 0.55        # silence after each line
LEAD = 0.9        # picture before the first line
TAIL = 2.6        # picture after the last line
EXTRA = {"11": 3.0}               # extra picture after a line (busy segments)
SPEED_CAP = {"11": 2.4, "08": 2.0}      # max speed-up per block (typing reads fine faster)


def run(cmd, cwd=None):
    print("+", " ".join(cmd)[:300], flush=True)
    subprocess.run(cmd, check=True, cwd=cwd)


def probe(path):
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                         capture_output=True, text=True, check=True).stdout
    return float(out.strip())


def silences(path, db=-40, d=0.3):
    err = subprocess.run(["ffmpeg", "-hide_banner", "-i", path, "-af", f"silencedetect=n={db}dB:d={d}", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    s = [float(x) for x in re.findall(r"silence_start: ([0-9.]+)", err)]
    e = [float(x) for x in re.findall(r"silence_end: ([0-9.]+)", err)]
    return list(zip(s, e + [None] * (len(s) - len(e))))


def trim_vo(n):
    """Cut leading/trailing silence; returns (path, duration, inner pauses [(start, end)] relative to the cut)."""
    src = os.path.join(CAP, "vo", f"{n}.wav")
    full = probe(src)
    sil = silences(src)
    start = 0.0; end = full
    if sil and sil[0][0] < 0.05 and sil[0][1]:
        start = max(0.0, sil[0][1] - 0.08)
    if sil and (sil[-1][1] is None or sil[-1][1] >= full - 0.05):
        end = min(full, sil[-1][0] + 0.15)
    inner = [(a - start, b - start) for a, b in sil if b is not None and a > start + 0.1 and b < end - 0.1]
    dst = os.path.join(OUT, f"vo_{n}.wav")
    run(["ffmpeg", "-y", "-v", "error", "-i", src, "-ss", f"{start:.3f}", "-to", f"{end:.3f}", "-ar", "48000", "-ac", "1", dst])
    return dst, end - start, inner


def phrases(text):
    """Split a line into subtitle cues (sentences; long ones at commas / colons), max ~70 characters."""
    parts = re.split(r"(?<=[.?!])\s+", text)   # sentence ends only (keeps "pages.dev" whole)
    out = []
    for p in (x.strip() for x in parts if x.strip()):
        if len(p) <= 70:
            out.append(p); continue
        bits = re.split(r"(?<=[,:;])\s+", p)
        cur = ""
        for b in bits:
            if cur and len(cur) + 1 + len(b) > 70:
                out.append(cur); cur = b
            else:
                cur = (cur + " " + b).strip()
        if cur:
            out.append(cur)
    return out


def cue_times(text, dur, pauses):
    """Character-proportional cue boundaries over the speech, snapped to the narrator's pauses."""
    cues = phrases(text)
    total = sum(len(c) for c in cues)
    speech = dur - sum(b - a for a, b in pauses)
    mids = [(a + b) / 2 for a, b in pauses]

    def at_speech(s):   # speech-time -> wall time (pauses inserted)
        t = s
        for a, b in pauses:
            if a < t:
                t += b - a
        return t

    bounds = [0.0]; acc = 0
    for c in cues[:-1]:
        acc += len(c)
        guess = at_speech(speech * acc / total)
        near = min(mids, key=lambda m: abs(m - guess)) if mids else None
        bounds.append(near if near is not None and abs(near - guess) < 0.9 and near > bounds[-1] + 0.4 else guess)
    bounds.append(dur)
    return [(cues[i], bounds[i], bounds[i + 1]) for i in range(len(cues))]


def frames(seg):
    d = os.path.join(CAP, seg)
    return len([f for f in os.listdir(d) if f.endswith(".jpg")]) if os.path.isdir(d) else 0


def block_video(i, segs, target, cap=1.7):
    """Retime the block's clips to `target` seconds; returns the clip path."""
    segs = [s for s in segs if frames(s) > 0]
    raw = [frames(s) / FPS for s in segs]
    total = sum(raw)
    k = total / target
    speed = min(cap, max(0.7, k))
    parts = []
    for s, r in zip(segs, raw):
        share = target * r / total            # this clip's share of the block
        src_len = min(r, share * speed)        # source seconds used at that speed
        dst = os.path.join(OUT, f"clip{H}_{i:02d}_{s}_{share:.2f}_{speed:.3f}.mp4")
        parts.append(dst)
        if os.path.exists(dst):          # resumable: the edit runs in short steps
            continue
        vf = f"setpts=(PTS-STARTPTS)/{speed:.4f},fps={FPS},scale={W}:{H}"
        hold = share - src_len / speed
        if hold > 0.02:
            vf += f",tpad=stop_mode=clone:stop_duration={hold:.3f}"
        run(["ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-t", f"{src_len:.3f}", "-i", os.path.join(CAP, s, "f%05d.jpg"),
             "-vf", vf, "-t", f"{share:.3f}", "-c:v", "libx264", "-preset", "veryfast", "-crf", "16",
             "-pix_fmt", "yuv420p", "-an", dst + ".part.mp4"])
        os.replace(dst + ".part.mp4", dst)
    lst = os.path.join(OUT, f"block_{i:02d}.txt")
    with open(lst, "w") as f:
        f.writelines(f"file '{os.path.basename(p)}'\n" for p in parts)
    dst = os.path.join(OUT, f"block_{i:02d}.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", dst])
    return dst, speed, k


def ts(t, ass=False):
    h = int(t // 3600); m = int(t % 3600 // 60); s = t % 60
    return f"{h}:{m:02d}:{s:05.2f}" if ass else f"{h:02d}:{m:02d}:{int(s):02d},{int(round((s - int(s)) * 1000)):03d}"


def ass_escape(s):
    return s.replace("{", "(").replace("}", ")")


def install_font():
    """libass finds the game's font through fontconfig (fontsdir alone falls back to a default sans)."""
    d = os.path.expanduser("~/.fonts")
    if not os.path.exists(os.path.join(d, os.path.basename(FONT))):
        os.makedirs(d, exist_ok=True)
        subprocess.run(["cp", FONT, d], check=True)
        subprocess.run(["fc-cache", "-f", d], check=False)


def main():
    os.makedirs(OUT, exist_ok=True)
    install_font()
    timeline = []      # (n, segs, label, text, vo_path, vo_dur, start)
    t = LEAD
    for n, segs, label, text in BLOCKS:
        vo, dur, pauses = trim_vo(n)
        timeline.append((n, segs, label, text, vo, dur, pauses, t))
        t += dur + GAP + EXTRA.get(n, 0.0)
    total = t - GAP + TAIL
    # block picture lengths: from the block's voice start to the next one (first block also covers the lead-in)
    starts = [0.0] + [b[7] for b in timeline[1:]] + [total]
    report = []
    blocks = []
    for i, b in enumerate(timeline):
        length = starts[i + 1] - starts[i]
        clip, speed, k = block_video(i, b[1], length, SPEED_CAP.get(b[0], 1.7))
        blocks.append(clip)
        report.append({"block": b[0], "segments": b[1], "seconds": round(length, 2), "speed": round(speed, 2), "wanted": round(k, 2)})
    lst = os.path.join(OUT, "blocks.txt")
    with open(lst, "w") as f:
        f.writelines(f"file '{os.path.basename(p)}'\n" for p in blocks)
    picture = os.path.join(OUT, "picture.mp4")
    run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", picture])

    # subtitles (SRT for players, ASS burned in)
    srt, events = [], []
    for n, segs, label, text, vo, dur, pauses, start in timeline:
        for cue, a, b2 in cue_times(text, dur, pauses):
            srt.append((start + a, start + b2, cue))
    with open(os.path.join(OUT, "competent_person_demo.srt"), "w", encoding="utf-8") as f:
        for k, (a, b2, c) in enumerate(srt, 1):
            f.write(f"{k}\n{ts(a)} --> {ts(b2)}\n{c}\n\n")
    lab = []
    for i, (n, segs, label, text, vo, dur, pauses, start) in enumerate(timeline):
        if label:
            lab.append((starts[i] + 0.2, min(starts[i] + 3.6, starts[i + 1] - 0.2), label))
    ass = os.path.join(OUT, "subs.ass")
    with open(ass, "w", encoding="utf-8") as f:
        f.write(f"""[Script Info]
ScriptType: v4.00+
PlayResX: {PW}
PlayResY: {PH}
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Sub,Barlow Condensed SemiBold,40,&H00FFFFFF,&H00FFFFFF,&H00101010,&H90000000,0,0,0,0,100,100,0.3,0,4,2,0,2,120,120,34,1
Style: Label,Barlow Condensed SemiBold,30,&H0040C8F0,&H0040C8F0,&H00101010,&HA0000000,0,0,0,0,100,100,1.2,0,3,2,0,7,36,36,28,1
Style: Title,Barlow Condensed SemiBold,96,&H0040C8F0,&H0040C8F0,&H00101010,&H00000000,0,0,0,0,100,100,2,0,1,3,2,5,40,40,40,1
Style: Tag,Barlow Condensed SemiBold,40,&H00FFFFFF,&H00FFFFFF,&H00101010,&H00000000,0,0,0,0,100,100,1,0,1,2,1,5,40,40,40,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
""")
        for a, b2, c in srt:
            f.write(f"Dialogue: 0,{ts(a, True)},{ts(b2, True)},Sub,,0,0,0,,{ass_escape(c)}\n")
        for a, b2, c in lab:
            f.write(f"Dialogue: 0,{ts(a, True)},{ts(b2, True)},Label,,0,0,0,,{{\\fad(250,250)}}{ass_escape(c)}\n")
        # Opening title over the hook, closing card over the outro.
        f.write(f"Dialogue: 1,{ts(0.4, True)},{ts(4.6, True)},Title,,0,0,0,,{{\\fad(500,600)\\pos({PW // 2},{PH // 2 - 40})}}COMPETENT PERSON\n")
        f.write(f"Dialogue: 1,{ts(0.9, True)},{ts(4.6, True)},Tag,,0,0,0,,{{\\fad(500,600)\\pos({PW // 2},{PH // 2 + 40})}}A serious game about seeing hazards before they hurt someone\n")
        o = timeline[-1][7]
        f.write(f"Dialogue: 1,{ts(o + 0.2, True)},{ts(total, True)},Title,,0,0,0,,{{\\fad(600,0)\\pos({PW // 2},{PH // 2 - 70})}}COMPETENT PERSON\n")
        f.write(f"Dialogue: 1,{ts(o + 1.0, True)},{ts(total, True)},Tag,,0,0,0,,{{\\fad(600,0)\\pos({PW // 2},{PH // 2 + 10})}}Play free in your browser · competent-person.pages.dev\n")

    # audio: narration lines on the timeline + the site bed, ducked under the voice
    inputs, filt, mix = [], [], []
    for k, (n, segs, label, text, vo, dur, pauses, start) in enumerate(timeline):
        inputs += ["-i", vo]
        d = int(start * 1000)
        filt.append(f"[{k}:a]adelay={d}|{d},apad,atrim=0:{total:.3f}[v{k}]")
        mix.append(f"[v{k}]")
    nv = len(timeline)
    filt.append(f"{''.join(mix)}amix=inputs={nv}:normalize=0,aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=mono[voice]")
    if os.path.exists(AMB):
        inputs += ["-stream_loop", "-1", "-i", AMB]
        filt.append(f"[{nv}:a]aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=mono,atrim=0:{total:.3f},volume=0.16,afade=t=in:d=1.2,afade=t=out:st={total - 2:.3f}:d=2[bed]")
        filt.append("[voice]asplit[voice1][voice2]")
        filt.append("[bed][voice2]sidechaincompress=threshold=0.03:ratio=6:attack=40:release=500[ducked]")
        filt.append("[voice1][ducked]amix=inputs=2:normalize=0,loudnorm=I=-16:TP=-1.5:LRA=11[aout]")
    else:
        filt.append("[voice]loudnorm=I=-16:TP=-1.5:LRA=11[aout]")
    audio = os.path.join(OUT, "audio.wav")
    run(["ffmpeg", "-y", "-v", "error"] + inputs + ["-filter_complex", ";".join(filt), "-map", "[aout]", "-ar", "48000", "-ac", "2", audio])

    final = os.path.join(OUT, "competent_person_demo.mp4")
    vf = f"subtitles=subs.ass:fontsdir='{FONT_DIR}',fade=t=in:st=0:d=0.6,fade=t=out:st={total - 0.8:.3f}:d=0.8"
    run(["ffmpeg", "-y", "-v", "error", "-i", "picture.mp4", "-i", "audio.wav", "-vf", vf, "-c:v", "libx264", "-preset", "veryfast",
         "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", "-shortest",
         "competent_person_demo.mp4"], cwd=OUT)
    with open(os.path.join(OUT, "edit_report.json"), "w") as f:
        json.dump({"seconds": round(total, 2), "blocks": report}, f, indent=1)
    print(json.dumps({"seconds": round(total, 2), "blocks": report}, indent=1))


if __name__ == "__main__":
    main()
