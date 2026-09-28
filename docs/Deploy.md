# Deploy & operate (web)

| Piece | Where |
|---|---|
| Game (Unity WebGL) | https://competent-person.pages.dev/ |
| Instructor page | https://competent-person.pages.dev/instructor.html |
| Cloudflare Pages project | `competent-person` (Functions in `Web/functions/api/`) |
| Database | Cloudflare D1 `competent-person` (`Web/migrations/`) |
| Secrets (Pages) | `OPENROUTER_API_KEY` (from the gitignored project `.env`), `INSTRUCTOR_KEY` (copy in gitignored `Web/.instructor_key`), `CODE_SECRET` (server only) |

## Deploy
```bash
# 1) Unity WebGL build  -> Builds/WebGL
"C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod Jobsite.Editor.WebBuild.Build -logFile Logs/webbuild.log
# 2) Package + migrate D1 + deploy Pages
bash Web/deploy.sh
```

## API (same-origin only; rate-limited in D1)
- `POST /api/events` — batched play events (pseudonymous class code + student ID, no names, no chat text).
- `POST /api/complete` — episode summary → server-signed completion code `CPn-XXXX-XXXX`.
- `GET /api/verify?code=` — public check of a code.
- `GET /api/report?class=` (+ header `X-Instructor-Key`) — completions + activity; `&format=csv` for raw events.
- `POST /api/chat` — AI crew proxy: model pinned (claude-haiku-4.5), 20 msgs / 10 min per IP, 3,000/day site-wide,
  server-owned guard prompt, client system text demoted to a clipped "character sheet".

## Instructor workflow
1. Pick a class code (e.g. `CE3100-F26`) and give each student an ID (not their name).
2. Students type both on the title screen, then play an episode.
3. At the end they get a completion code; paste it on the instructor page to verify, or load the class report / CSV.

## Privacy notes
- Stored: class code, student ID, episode, event kinds, hazard ids, timings, scores. Not stored: names, emails, chat text, IPs (rate limiting keys are SHA-256 hashes).
- AI chat asks for consent first and can be turned off in Esc → Settings (built-in answers).
- Training records only — not an OSHA 10/30 card or a competent-person designation.
