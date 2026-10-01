# Enhancement 1: browser verification checklist

Status on 2026-10-01: **not browser-tested**. The native computer-use pipe was unavailable, no browser surfaces were exposed, and the in-app browser was unavailable. No screenshots were taken. **Actual viewports checked: none (0).** HTTP, source, bundle and simulated editor checks do not prove visual rendering or browser CSP enforcement.

## Preparation

1. Start SQL Server and Docker Desktop (Linux engine). Pull the pinned image from README, then run `dotnet run --launch-profile https`.
2. Open `https://localhost:7115` in a current Chromium browser. Use your normal Development Admin account and a separate Student account. Never copy User Secrets into this checklist or screenshots.
3. Open DevTools. Enable **Network → Disable cache** and **Console → Preserve log**. Open device emulation in **Responsive** mode, zoom 100%. Use each exact CSS viewport below and hard-reload every page. Repeat the editor check after resizing an already-open editor.
4. Record browser/version, date, screenshot filename and any issue in the result cells. All cells below start **pending**, not passed.

| Page / identity | 1440 × 900 desktop | 1024 × 768 laptop/tablet | 390 × 844 mobile |
|---|---|---|---|
| `/` guest and Student | Pending | Pending | Pending |
| `/Tasks` guest and Student | Pending | Pending | Pending |
| `/Tasks/sum-of-two-numbers` Student | Pending | Pending | Pending |
| `/Submissions/My` Student | Pending | Pending | Pending |
| `/Submissions/Details/{your-id}` Student | Pending | Pending | Pending |
| `/Progress` Student | Pending | Pending | Pending |
| `/Leaderboard` guest and Student | Pending | Pending | Pending |
| `/Account/Login` guest | Pending | Pending | Pending |
| `/Account/Register` guest | Pending | Pending | Pending |
| `/Account/Profile` Student and Admin | Pending | Pending | Pending |
| `/Admin` and Admin task/test-case forms | Pending | Pending | Pending |

## Every page and viewport

- Check the full page top to bottom. No content, buttons, labels or status badges should be clipped or overlap. Cards should stack at small widths. Horizontal scrolling should be inside wide tables/code panes, not the page.
- Run this read-only Console expression: `document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1`. Expect `true`; if false, inspect the overflowing element rather than concealing it with global `overflow-x: hidden`.
- At 1440 px the main navigation should be expanded. At 1024 and 390 px use **Toggle navigation**, follow links and close/reopen it. Confirm Login/Register or account/logout actions remain reachable. Use Tab/Shift+Tab and check focus and the skip link.
- Inspect task, submission, topic-progress, leaderboard and Admin tables. At 390 px swipe the table itself; headings and action buttons must remain accessible. Check long titles, display names, compiler output, source lines and test values.
- Submit empty/invalid Login/Register and an invalid Admin form. Check labels, focus and validation text. Error summaries should show real errors without a stray empty bullet. Correct values and verify errors clear normally.
- Check Console after navigation, validation and interactions. There must be no relevant CSP, JavaScript or worker errors. Record any blocked directive/resource, without exposing personal data.

## Monaco and CSP: repeat at all three widths

1. Sign in as Student and open `/Tasks/sum-of-two-numbers`. Confirm C++ highlighting (keywords, strings and comments differ) and a visible caret. Resize from desktop to mobile: the editor must fit the panel and allow internal code scrolling.
2. Type this solution, edit a value, then undo and redo:

   ```cpp
   #include <iostream>
   int main() {
       long long a, b;
       std::cin >> a >> b;
       std::cout << a + b;
   }
   ```

3. Read `document.getElementById('SourceCode').value` in Console and compare it with the editor. Reload in the same tab: the draft must remain. A different task must have its own draft. Server validation must preserve submitted text instead of restoring an older draft.
4. Trigger suggestions with Ctrl+Space after typing a repeated identifier. In Network inspect `code-editor.js`, C++ language chunks, `editor.worker.js`, its imports, CSS and icon fonts. They must load from the same origin successfully. Verify worker startup without CSP/module errors; if no worker request appears, record **not yet verified**, not passed.
5. Inspect the document response: one `Content-Security-Policy`; script/style nonces match. Read the `nonce` **property** of dynamically created `style` elements (browsers may hide nonce attributes). It should match `document.querySelector('meta[name="csp-nonce"]').content`. A fresh reload must produce a different nonce.
6. `style-src-attr 'unsafe-inline'` should appear only on the authenticated editor/invalid-submit response. On `/Progress` and Login expect `style-src-attr 'none'`. No page should allow script `unsafe-inline` or `unsafe-eval`.
7. Submit. Network → request form data → `SourceCode` must match the editor. The button disables and compiling status appears; the result must be **Accepted**. Use Back and confirm submission controls are usable again.
8. Submit `int main( {` and check readable compilation diagnostics without host paths. Try empty source: the editor remains usable and validation appears. Restore the correct solution afterwards.

## Results, AI, progress and Admin

- Review your result statuses, long source and visible/hidden test cards. Hidden input, expected/actual output and diagnostics must not appear, even in page HTML. Another Student opening your result URL must get 404.
- Review existing stored AI explanations/hints at each width; text must wrap and controls stay reachable. Reopening stored feedback must not call the model again. A new **Analyze with AI** action uses the configured paid service; it is unnecessary for this visual pass when stored feedback exists. Enhancement 1 verification made no paid AI calls.
- On `/Progress`, check metric cards and difficulty-bar widths agree with percentage/counts. Repeated Accepted attempts must not count a task twice. On `/Leaderboard`, check rank, score and current-user highlighting.
- As Admin, check navigation, CRUD forms, confirmations and table actions. As Student, verify Admin is denied. Use temporary rows for editing checks and remove them afterwards.

Record failures with page, role, viewport, steps, expected/actual behavior and a screenshot. This checklist remains pending until a person performs and records these browser checks.
