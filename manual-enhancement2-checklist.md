# Enhancement 2 — manual browser checklist

**Status: pending. Actual browser viewports checked: 0.** Browser discovery returned no apps/browsers, and creating an in-app browser returned “Browser is not available: iab”. No visual success, screenshots, real Monaco interaction or clean browser console is claimed. HTTP/SQL/Docker and JavaScript integration tests are separate evidence.

## Setup

1. Start SQL Server and Docker Desktop with Linux containers and the pinned GCC image described in README.
2. Run `dotnet run --launch-profile https`. Open `https://localhost:7115` in a current Chromium browser; sign in as a Student. Use another Student in a separate browser profile for ownership checks.
3. Open DevTools, enable Network → Disable cache and Console → Preserve log. Use Responsive emulation, 100% browser zoom, at the exact sizes below. Hard-reload each page, then also resize an already-open editor.
4. Record the date, browser/version and screenshot filenames in the cells. Do not include passwords, cookies, User Secrets or personal data in screenshots.

| Check | 1440 × 900 | 1024 × 768 | 390 × 844 |
|---|---|---|---|
| Task editor and custom input panel | Pending | Pending | Pending |
| Run loading/result/error controls | Pending | Pending | Pending |
| Compact attempt timeline | Pending | Pending | Pending |
| Full journey and pagination | Pending | Pending | Pending |
| Compare links and read-only diff | Pending | Pending | Pending |
| Narrow-screen layout / page overflow | Pending | Pending | Pending |
| Worker, CSP console and JavaScript errors | Pending | Pending | Pending |

## Run and Submit

1. Open `/Tasks/sum-of-two-numbers`. Confirm C++ highlighting, keyboard input, cursor, undo/redo and draft restoration still work. Enter:

   ```cpp
   #include <iostream>
   int main() {
       long long a, b;
       std::cin >> a >> b;
       std::cout << a + b;
   }
   ```

2. Put `2 3` in **Custom input**, then click **Run**. The task page must remain open, show “Running…”, disable Run/Submit during execution, and then show Success, output `5` and measured execution time. Controls must become usable again. The panel should state that no attempt was saved and output was not judged.
3. In Network, inspect the POST to `/CustomRuns/Run`: source must match Monaco, custom input must match the textarea, and the antiforgery token must be present. Check the hidden textarea through `document.getElementById('SourceCode').value`; it must match the editor. Do not share the token.
4. Open My submissions, Progress and your journey before/after Run. No new attempt, solved task or score should appear. Return to the task: the source draft should remain. Custom input/results are temporary, not persistent history.
5. Try blank custom input with `int main(){}`: a successful program may produce empty output. Try `int main( {`: show Compilation error and readable compiler output. Try `int main(){for(;;){}}`: it must terminate with a time limit and restore controls.
6. Test client validation with empty source and UTF-8 input over 32 KiB. No Run should start. Submit remains the official judge action and must not use Custom input as official test data.
7. Disconnect the network temporarily using DevTools, click Run, then restore it. Show a useful error and restore controls. Navigate away during a Run and return: the editor and controls must remain usable.
8. To inspect untrusted output safely, run a C++ program that prints the literal text `<img src=x onerror=alert(1)>`. It must appear as text in Output; there must be no image, alert or script execution. Check compiler/stderr panels similarly.
9. Click **Submit solution** with the correct sum program. It should navigate to an Accepted result, save an official attempt, and update Progress/Leaderboard normally.

## Attempt journey and replay

1. For a fresh student/task, create three official attempts: syntax error, a program that always prints zero, and the correct sum solution. Open **View attempt journey** from the result page, or `/Tasks/sum-of-two-numbers/Journey`.
2. Verify Attempt #1/#2/#3, CompilationError → WrongAnswer → Accepted, chronological UTC timestamps, passed/total counts and measured/not-measured times. The Accepted node should be distinguishable with the existing status color.
3. Click an attempt: it must open the existing Submission Details page, including its existing AI Tutor section. A timeline AI marker appears only when saved feedback exists. It does not claim that AI caused improvement.
4. Prefer an existing attempt with saved feedback for this visual check. Requesting new **Analyze with AI** feedback is a paid action using your configured service; the automated Enhancement 2 checks did not make paid calls.
5. With more than five attempts, the task page should show the latest five in chronological order, retaining their full-history numbers, plus **View full journey**. With more than twenty, check Earlier/Later page links. The first item of a later page must still compare with its predecessor on the previous page.
6. Log in as the other Student and open the same journey URL. Only that student's own attempts or a clean empty state may appear. Adding `?userId=...` must not select another user.

## Compare attempts

1. Click **Compare with previous** on attempt #2 or later. The page must display Previous/Current status, timestamp and execution time, then Monaco Diff Editor. Back to journey and Back to submission must work.
2. At 1440 px confirm side-by-side rendering, removed/added/changed lines and correct Previous/Current source. At widths below the configured 800 px editor breakpoint, confirm usable inline comparison. At 390 px verify internal scrolling and that the page itself does not scroll horizontally.
3. Try typing, paste and Delete on both sides. Both sources must stay read-only. Reload: stored submissions must be unchanged. Identical sources should show no invented changes. A leading blank line should be preserved.
4. Compare source containing `</script>` and `</textarea><img src=x onerror=alert(1)>` in comments. It must be plain source text, with no new DOM element or script execution.
5. As the other Student, opening the comparison URL must give 404. Comparing your attempts from two different tasks, missing IDs or the same ID twice must also fail safely. Admin role does not grant cross-owner comparison access.

## Layout, accessibility and CSP

- Inspect every panel at all three sizes. Labels, buttons, status badges, timeline links and diagnostics must remain readable and reachable. Long code should scroll inside Monaco/pre/textarea containers.
- Run `document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1` in Console: expect `true`. Record an overflowing element if false; do not hide the problem with global overflow clipping.
- Test keyboard Tab/Shift+Tab, visible focus and Monaco navigation. Run should announce its changing status without moving keyboard focus unexpectedly.
- In Network confirm local `code-editor.js`, `custom-run.js`, editor CSS, language chunks and `editor.worker.js` load successfully. Trigger Monaco suggestions/diff calculation to exercise worker startup.
- Inspect document CSP: task and diff pages have the scoped Monaco style-attribute allowance; the full journey uses `style-src-attr 'none'`. Scripts must not gain `unsafe-inline` or `unsafe-eval`. The response nonce must match import maps and dynamically inserted styles; reload must change it.
- Check Console after Run, Submit, diff loading, resizing and navigation. Record any CSP, worker or JavaScript error. Successful HTTP responses alone do not establish a clean console.
- With JavaScript disabled, comparison textareas should remain readable and read-only. Monaco and asynchronous Run require JavaScript; normal Submit retains its textarea fallback.

Record each defect with URL, role, viewport, steps, expected/actual behavior and screenshot. Leave unperformed checks marked Pending.
