"""Cut the recorded trailer run into the final MP4: trim to the demo, step captions from the [Demo] marks, fades.
python Web/qa/edit_demo.py  ->  Captures/video/competent_person_gameplay.mp4
"""
import json, os, subprocess

D = r"C:\Users\jewoo\GameDev\vr-safety-training\Captures\video"
t = json.load(open(os.path.join(D, "timing.json"), encoding="utf-8"))
start = min([t["startAt"]] + [m["t"] for m in t["marks"]]) - 0.1
end = t["doneAt"] + 0.4
speed = max(1.0, (end - start) / 31.0)          # target ~30 s
dur = (end - start) / speed
marks = [((m["t"] - start) / speed, m["label"]) for m in t["marks"]]
font = "C\\:/Windows/Fonts/arialbd.ttf"

filters = [f"setpts=PTS/{speed:.4f}"]
for i, (at, label) in enumerate(marks):
    until = marks[i + 1][0] if i + 1 < len(marks) else dur
    tf = os.path.join(D, f"cap_{i}.txt")
    open(tf, "w", encoding="utf-8").write(label)
    tfe = tf.replace("\\", "/").replace(":", "\\:")
    big = i == 0
    filters.append(
        f"drawtext=fontfile='{font}':textfile='{tfe}':fontcolor=white:fontsize={34 if big else 26}:"
        f"box=1:boxcolor=0x111111@0.62:boxborderw=14:x={'(w-text_w)/2' if big else '36'}:y={'h*0.42' if big else '36'}:"
        f"enable='between(t,{max(0, at):.2f},{until - 0.05:.2f})':alpha='min(1,(t-{max(0, at):.2f})*4)'")
filters.append(f"fade=t=in:st=0:d=0.5,fade=t=out:st={dur - 0.7:.2f}:d=0.7")
out = os.path.join(D, "competent_person_gameplay.mp4")
cmd = ["ffmpeg", "-y", "-loglevel", "error", "-ss", f"{start:.2f}", "-i", os.path.join(D, "raw.webm"), "-t", f"{end - start:.2f}",
       "-vf", ",".join(filters), "-r", "30", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20", "-preset", "medium", "-an", out]
subprocess.run(cmd, check=True)
print(out, f"{dur:.1f}s speed x{speed:.2f}", len(marks), "captions")
