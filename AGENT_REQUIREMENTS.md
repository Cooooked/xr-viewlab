# AGENT WORKSPACE REQUIREMENTS

## MANDATORY SETUP FOR ALL AI AGENTS

**Before starting any work, every AI agent MUST:**

1. **Read AGENTS.md completely** - especially the "CRITICAL: AGENT SAFETY RULES" section
2. **Create a Git workspace/branch** - never work directly on master
3. **Run this check before any file modification:**
   ```powershell
   git status
   ```
   If there are uncommitted changes, STOP and ask the user for direction.

## FORBIDDEN OPERATIONS

These operations are BANNED unless the user explicitly approves them by name:

- `git restore` - NEVER run this command
- `git checkout` - NEVER run this command
- `git reset` - NEVER run this command
- `git revert` - NEVER run this command
- `git stash` - NEVER run this command
- Any command that begins with `git restore` or `git checkout`
- Any command that begins with `git reset`

## IF ASKED TO "RESTORE" OR "ROLLBACK"

1. STOP - do not execute any git command
2. Ask for clarification:
   - "Which specific build number should I restore to?"
   - "Which specific commit hash?"
   - "Should I restore from a backup zip file?"
   - "Should I restore from a recent MSI?"
3. Check these locations for the real working state:
   - `dist/` folder - recent MSIs may represent the real working state
   - `backups/` folder - patches or zip snapshots
   - Git reflog - recent operations
   - Uncommitted local changes - these may be the real working state

## IF THE USER REPORTS A REGRESSION

1. STOP ALL IMMEDIATE CHANGES
2. Ask:
   - "What was the last working build number?"
   - "What time/date was it working?"
   - "What specific functionality broke?"
3. Do NOT assume Git HEAD is the source of truth
4. Do NOT run any git restore/checkout/reset commands
5. The real working state is likely in:
   - Uncommitted local changes (not in git at all)
   - A recent MSI in `dist/`
   - A patch in `backups/`
   - A zip snapshot in `backups/`

## WORKSPACE POLICY

This project MUST be edited in a Git workspace/branch, not directly on master.

**Required workflow:**
1. Create a feature branch: `git checkout -b feature-name`
2. Make changes on the branch
3. Build and test
4. Commit to the branch
5. Only merge to master after explicit user approval

**If working directly on master:**
- Commit frequently after each successful build
- Tag the commit with the build number: `git tag -a v4.1.XX -m "Working build"`
- This provides a recovery point if changes need to be reverted

## SOURCE OF TRUTH

**The real working state is NOT necessarily Git HEAD.**

Check in this order:
1. Current uncommitted changes (`git status`)
2. Most recent MSI in `dist/`
3. Most recent patch in `backups/`
4. Most recent zip snapshot in `backups/`
5. Git HEAD (only if all above are unavailable)

## EMERGENCY RECOVERY

If data loss occurs:
1. Check `dist/` for the last working MSI
2. Check `backups/` for patches or snapshots
3. Check git reflog for recent operations
4. Check git stash if it was used
5. Install the last working MSI as a fallback

## SIGNOFF

By editing this project, every AI agent acknowledges:
- They have read AGENTS.md completely
- They understand the forbidden operations
- They will never run git restore/checkout/reset/revert without explicit approval
- They will create a workspace/branch before making changes
- They will verify the real working state before any rollback operation
