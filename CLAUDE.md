The active project handoff doc lives on branch `Multiplayer`, not here. Read it before doing anything multiplayer-related:

```
git show origin/Multiplayer:AGENT_CONTEXT.md
```

To work on Multiplayer: `git checkout Multiplayer` — but note Multiplayer may already be checked out in another worktree (run `git worktree list` first; the same branch can't be checked out twice).

User preferences (context-warning convention, etc.) live in auto-memory at `~/.claude/projects/-Users-nadav-Documents-GitHub-super-inverters-game/memory/` and are loaded automatically.
