---
description: Begin a FastCart dev session — load the roadmap + last session's state and plan today's work
argument-hint: "[optional focus, e.g. 'phase 2' or 'finish checkout']"
---

You are resuming work on the **FastCart backend** (ASP.NET Core, per the TZ). Do this in order:

1. Read `ROADMAP.md` — the master plan and progress checkboxes. Identify the current phase and the next unchecked items.
2. Read `.claude/state/session-log.md` — pay special attention to the **▶ RESUME HERE** block at the very top; it holds the exact next action and any blockers from last session.
3. Open `FastCart-Backend-Technical-Specification-v1.2.md` (the TZ) only for the specific sections relevant to today's work — don't re-read it whole.
4. If the working tree already has code, run a quick `git status` / build check so you know the real state, not just what the log claims.

Then report back, concisely:
- **Where we are:** current phase + % of its items done.
- **Last session:** 1–2 lines on what was completed.
- **Blockers / open decisions:** anything from the RESUME block or ROADMAP "Open items".
- **Plan for this session:** the next 1–3 concrete tasks.

If the user passed a focus in `$ARGUMENTS`, prioritize that. Otherwise propose the next roadmap items.

Then **wait for my go-ahead** before making large changes — unless I already told you to proceed, in which case start on the top task.
