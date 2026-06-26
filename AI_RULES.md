# AI_RULES.md

## Core Rules
1. **Prove root cause before patching.** Verify the issue definitively before making any changes.
2. **No speculative edits.** Do not modify code or configuration unless the need is certain and justified.
3. **No broad refactors.** Changes must be targeted; avoid restructuring unrelated parts of the system.
4. **No renaming unless explicitly requested.** Preserve existing identifiers and file names.
5. **No unrelated cleanup during bug fixes.** Isolate changes strictly to the bug’s scope.
6. **One bug, one fix, one commit.** Each commit addresses a single, clearly identified issue.
7. **State files to be changed before changing them.** Document the affected files in the commit message or documentation before modification.
8. **If root cause is unknown, stay in plan mode.** Do not implement a fix until the cause is identified.
9. **Build mode only after root cause is proven.** Ensure the build succeeds and passes tests before deploying.
10. **Do not touch DLL code while fixing WPF UI unless explicitly required.** Keep concerns separated.
11. **Do not touch WPF UI while fixing DLL runtime logic unless explicitly required.** Maintain separation of concerns.
12. **Do not change shared-memory structs unless absolutely necessary and documented.** Shared-memory contracts are stable.
13. **If another bug is discovered, document it but do not fix it.** Add to `KNOWN_ISSUES.md`.

## Required Bug Workflow
1. **Observed behaviour.** Clearly describe what the system does.
2. **Expected behaviour.** Describe the intended behavior.
3. **Relevant files.** List files involved in the issue.
4. **Call graph.** Outline the flow of execution.
5. **Exact root cause.** Identify the precise cause of the problem.
6. **Minimal fix.** Implement the smallest change that resolves the issue.
7. **Build.** Verify the build succeeds.
8. **Test.** Confirm the fix works.
9. **Report changed files.** Document which files were modified.

## Forbidden Behaviour
- **“While I’m here” changes.** Do not make unrelated edits while working on a task.
- **Formatting-only rewrites.** Do not reformat code without a functional reason.
- **Architecture rewrites.** Do not redesign the system architecture without explicit direction.
- **Guessing.** Do not assume intent or behavior; verify before acting.
- **Chasing unrelated files.** Do not modify files not directly related to the issue.
- **Editing generated files.** Avoid changing files that are auto-generated unless necessary.
- **Committing binaries/build output.** Do not add build artifacts to version control.