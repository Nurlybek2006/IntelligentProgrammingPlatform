# Enhancement 3 — manual browser checklist

**Status: Pending. Actual browser viewports checked: 0.** Browser inventory returned no apps or browsers; opening the in-app browser returned `Browser is not available: iab`. HTTP/SQL/Docker and offline client checks passed separately. No screenshots, visual rendering, clean browser console or live model behavior are claimed.

## Setup and viewport record

Start SQL Server and Docker Desktop, apply migrations with `dotnet ef database update`, and run `dotnet run --launch-profile https`. Open `https://localhost:7115` and sign in as a Student. Use another Student in a separate browser profile for ownership checks. Prefer existing saved feedback so this visual review needs no paid analysis. If choosing to inspect newly generated advice, one analysis supplies all three hints; revealing requires no further analysis.

Use responsive emulation at 100% zoom. Record browser/version, date, screenshots and any failures. Never include credentials, cookies or tokens in shared screenshots.

| Check | 1440 × 900 | 1024 × 768 | 390 × 844 |
|---|---|---|---|
| Hint ladder, progress dots and summary/explanation | Pending | Pending | Pending |
| Reveal buttons, loading state and persistence | Pending | Pending | Pending |
| Learning Map and topic cards | Pending | Pending | Pending |
| Official error and AI-analyzed pattern rows | Pending | Pending | Pending |
| Practice Next card and navigation | Pending | Pending | Pending |
| Attempt Journey hint counts | Pending | Pending | Pending |
| Keyboard focus, overflow and CSP/JS console | Pending | Pending | Pending |

## Progressive hints

1. Open your Submission Details with a saved three-hint analysis at count 1. Summary, explanation and error category should be readable immediately. Check “Hints revealed: 1 / 3”, one filled dot and only Hint 1. The action should say **Reveal next hint**.
2. Inspect the entire Network document response / View Source. Hint 2 and Hint 3 must be absent, including script blocks, hidden elements, attributes and comments. Inspecting only visible text is insufficient. Automated fixtures already verified this boundary; use a known test row if repeating it manually.
3. Read the encouragement to try changing the solution first. Activate Reveal with the keyboard. Verify the button disables while the form submits, the POST includes an antiforgery token, and the result returns to the same submission.
4. Now check 2 / 3, two filled dots and Hint 1 + Hint 2. Hint 3 must still be absent from the response. The action should say **Reveal final hint**.
5. Reveal again: check 3 / 3, all three hints and “All available hints revealed.” No reveal button should remain. Refresh, navigate away/back, and sign out/in; count and visible hints must persist.
6. Check historical one-hint feedback: 1 / 1, no reveal button. Historical two-hint feedback should stop at 2 / 2. Empty or malformed hints should show a safe no-hints state without a stack trace or another analysis request.
7. Try rapid clicks and browser Back: controls must recover and count must stay within available hints. A direct POST after the last hint must leave the count unchanged.
8. As another Student, or an Admin who does not own the submission, a valid-token reveal POST to the owner's ID must return 404. A missing-token POST must return 400. GET must not reveal anything.
9. Hint/summary/explanation text containing literal HTML must remain text. No image, script or alert should be created. CSS must preserve readable line breaks and wrap long text within the panel.
10. Review newly generated hint quality only if performing the optional one-call analysis: conceptual → diagnostic → strongest incomplete guidance. Summary/explanation must not give away later guidance. No complete solution, hidden values or instruction-following from source comments should appear. Offline tests establish boundaries and safeguards, not universal live model compliance.

## Learning Map

1. Open `/Progress`. Existing solved count, compilation count, successful-attempt percentage, average solving time and rating should remain above the map.
2. Check topic names, strength percentages, native progress bars, solved/published counts, completed attempts, completion/success rates and most frequent issue. Expand **Attempt breakdown** and **How to read your learning strength** using keyboard controls.
3. Verify zero-task/zero-attempt states remain readable. Labels must use Not started, Exploring, Strong, Developing or Needs practice. The wording describes recorded practice, without claims about personal ability.
4. Check the documented example: 2 of 4 tasks solved and 2 Accepted out of 5 completed gives 47%, Developing. Infrastructure failures must not reduce this score. The existing historical success percentage remains its original metric, with its original denominator.
5. Check official error patterns for Compilation, Wrong Answer / Logic, Runtime, Time Limit and Memory Limit. System failures must not appear as student mistakes; no raw diagnostics should appear in analytics.
6. AI-analyzed patterns must clearly say they represent only submissions with saved analysis. No-feedback state must remain useful. Revealing another hint must not change strength or error counts.
7. Record the map and recommendation, perform a temporary Custom Run, then refresh Progress. No counts, bars, labels or recommendation should change.

## Practice Next and journey

1. Check the recommended task is published and unsolved by the signed-in user. The reason should refer to practice priority and recorded results. **Practice this task** must open its normal Task Details page.
2. Check practice priority: Needs practice → Developing → Exploring → Not started → Strong. Within one group, lower strength wins, then topic ID; inside the topic, Easy precedes Medium/Hard, then task ID. Repeated refreshes should be stable while data is unchanged.
3. When all published tasks are solved, check the positive completion message and absence of a fabricated recommendation. With no published tasks, check the empty state instead.
4. Open the compact/full Attempt Journey. Saved feedback should retain its existing marker and show counts such as **AI hints: 1/3**. No hint text belongs on the timeline. Counts should update after reveal and page refresh.
5. Open **Compare with previous**; verify the existing read-only diff still works. Custom Run must remain absent from the journey.

## Layout, accessibility and CSP

- At every viewport, inspect the AI panel, reveal button, topic cards, pattern rows and recommendation. No text should overlap or disappear. Labels must remain useful without relying on color alone.
- Confirm keyboard focus is visible; forms, disclosure controls and links must work without a mouse. Hint dots are decorative; the text count must communicate progress.
- Resize an already-open page. At 390 px, cards must stack and buttons must remain reachable. Check `document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1` in Console.
- Inspect response CSP: Progress, Journey and Submission Details still use `style-src-attr 'none'`; only existing editor/diff pages get scoped Monaco permission. No new script `unsafe-inline` or `unsafe-eval` is allowed.
- Check Console after loading, revealing, navigating and resizing. Record all CSP/JavaScript errors. HTTP checks do not prove a clean browser console.
- Disable JavaScript and reload: hint reveal forms and the learning map should still work. The existing Monaco/custom-run features retain their documented JavaScript requirements.

Leave unperformed checks marked Pending. Record defects with URL, role, viewport, steps, expected/actual behavior and a screenshot without sensitive data.
