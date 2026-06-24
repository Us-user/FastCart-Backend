---
description: End a FastCart dev session — summarize progress and save state so the next session resumes instantly
argument-hint: "[optional note to carry into next session]"
---

You are ending a **FastCart backend** work session. Capture state so a fresh session (with no memory of this one) can pick up exactly where we stopped.

1. **Review the session.** Look at what actually changed: run `git status` / `git diff --stat` if it's a git repo, and recall key decisions, what works, and what's half-done. Don't trust memory alone — check the tree.

2. **Update `ROADMAP.md`:**
   - Check off `[x]` items that are genuinely complete.
   - Mark `[~]` for anything in progress.
   - Mark `[!]` for blocked items and note why.
   - Add any newly discovered tasks under the right phase.

3. **Prepend a new entry to `.claude/state/session-log.md`** (newest first), using this shape:
   ```
   ## Session NN — YYYY-MM-DD
   **Phase:** <current phase>
   **Done this session:** <bullets — concrete, file-level where useful>
   **Decisions / deviations from TZ:** <bullets, or "none">
   **Known issues / blockers:** <bullets, or "none">
   **Next steps (exact):** <ordered, concrete — the first one becomes RESUME HERE>
   ```

4. **Rewrite the ▶ RESUME HERE block** at the very top of `session-log.md` so it points to the single clearest next action, the current phase, and any blocker. This is the first thing `/start` reads.

5. If `$ARGUMENTS` contains a note, fold it into the entry and the RESUME block.

6. Confirm to me, in 2–3 lines, what you saved and what the next session will start with. **Do not commit or push unless I ask.**
