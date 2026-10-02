"""Enhancement 3 real HTTPS/SQL/Docker checks. No paid AI calls or browser claims.

Synthetic feedback is inserted only into UUID-scoped fixtures. Analysis POST is
used only for already-stored feedback; offline C# checks count the initial call.
"""
import concurrent.futures
import html
import json
import re
import secrets
import uuid
from verify_phase2 import Client, Inputs, check, is_redirect, sql
from verify_enhancement1 import inspect as inspect_csp
from verify_enhancement2 import snapshot, progress, clean_runner
from verify_enhancement3_migration import literal, snapshot as feedback_metadata


def learning_map(body):
    return body[body.index('<section id="learning-map"'):body.index('<section aria-labelledby="recent-heading"')]


def main():
    prefix = 'enhancement3-' + uuid.uuid4().hex
    emails = [prefix + f'-{i}@example.test' for i in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    student, other, anon = Client(timeout=120), Client(), Client()
    owner = None
    try:
        for client, email in zip([student, other], emails):
            assert is_redirect(client.form('/Account/Register', {'Email': email, 'DisplayName': 'Enhancement 3 fixture',
                'Password': password, 'ConfirmPassword': password}))
        owner, other_id = [sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}';") for email in emails]
        topic = int(sql(f"INSERT INTO Topics(Name) VALUES('{prefix}'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        runtime = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
        tasks = []
        for i in range(5):
            tasks.append(int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Hint fixture {i}','{prefix}-{i}','Read input and reflect on the result.',{1 if i == 3 else 0},{topic},500,32,{0 if i == 4 else 1},SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")))

        def submission(task=tasks[0], status=4, user=owner):
            return int(sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,FinishedAt,PassedTests,TotalTests) VALUES('{user}',{task},{runtime},'// fixture: no live analysis',{status},SYSUTCDATETIME(),{'NULL' if status < 3 else 'SYSUTCDATETIME()'},0,0); SELECT CAST(SCOPE_IDENTITY() AS bigint);"))

        def feedback(submission_id, hints, count=1, user=owner):
            return int(sql(f"INSERT INTO AiFeedbacks(SubmissionId,UserId,Model,Summary,Explanation,HintsJson,RevealedHintCount,ErrorCategory,CreatedAt,InputTokens,OutputTokens) VALUES({submission_id},'{user}','offline-http-fixture','Summary <script>summary_marker()</script>','Explanation <img src=x onerror=explanation_marker()>',{literal(hints)},{count},'Logic',SYSUTCDATETIME(),17,23); SELECT CAST(SCOPE_IDENTITY() AS bigint);"))

        def reveal(submission_id, client=student, extra=None):
            return client.form(f'/Submissions/{submission_id}/RevealNextHint', extra or {}, token_path='/Account/Profile')

        def details(submission_id):
            return inspect_csp(student.request(f'/Submissions/Details/{submission_id}'))

        hints = ['FIRST_HINT_MARKER <img src=x onerror=hint_marker()>',
                 'SECOND_HINT_MARKER </script><script>second_marker()</script>', 'THIRD_HINT_MARKER strongest incomplete direction']
        wrong = submission()
        stored = feedback(wrong, json.dumps(hints))
        missing = submission(status=2)
        foreign = submission(user=other_id)
        feedback(foreign, '["private owner hint"]', user=other_id)
        path = f'/Submissions/{wrong}/RevealNextHint'
        check(is_redirect(anon.request(path, {})), 'Reveal requires authentication')
        check(student.request(path)[0] in (404, 405), 'Reveal is POST only')
        check(student.request(path, {})[0] == 400, 'Reveal rejects a missing antiforgery token')
        check(reveal(wrong, other)[0] == 404 and reveal(foreign)[0] == 404, 'Reveal rejects either cross-owner submission')
        check(all(reveal(item)[0] == 404 for item in [0, -1, 9223372036854775807, missing]), 'Missing submission or feedback returns 404')
        check(sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE Id={stored};') == '1', 'Rejected requests do not change reveal state')
        before = feedback_metadata(owner)
        journey_path = f'/Tasks/{prefix}-0/Journey'
        for visible in range(1, 4):
            body = details(wrong)
            check(f'Hints revealed: {visible} / 3' in body and all(hint in html.unescape(body) for hint in hints[:visible]),
                  f'Owned details renders the first {visible} hint(s)')
            check(all(marker not in body for marker in ['SECOND_HINT_MARKER', 'THIRD_HINT_MARKER'][visible - 1:]),
                  'Future hint markers absent from the entire HTML, including scripts/attributes/comments')
            assert '<script>summary_marker()' not in body and '<img src=x onerror=hint_marker()' not in body
            assert '<img src=x onerror=explanation_marker()' not in body and '<script>second_marker()' not in body
            journey = inspect_csp(student.request(journey_path))
            check(f'AI hints: {visible}/3' in journey and not any(hint.split()[0] in journey for hint in hints),
                  'Journey contains counts only, never revealed or unrevealed hint text')
            if visible < 3:
                label = 'Reveal next hint' if visible == 1 else 'Reveal final hint'
                assert label in body and f'action="{path}"' in body
                assert is_redirect(reveal(wrong, extra={'UserId': other_id, 'RevealedHintCount': 3}))
            else:
                assert 'All available hints revealed.' in body and f'action="{path}"' not in body
        assert is_redirect(reveal(wrong))
        assert sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE Id={stored};') == '3'
        check(feedback_metadata(owner) == before, 'Reveal changes only count: same row, text, model, CreatedAt and token metadata')
        assert is_redirect(student.form(f'/Submissions/Analyze/{wrong}', {}, token_path='/Account/Profile'))
        check(feedback_metadata(owner) == before and 'Showing the existing AI feedback' in details(wrong), 'Repeated Analyze reuses saved feedback and does not reset reveal state')
        renewed = Client()
        assert is_redirect(renewed.form('/Account/Login', {'Email': emails[0], 'Password': password}))
        check('Hints revealed: 3 / 3' in renewed.request(f'/Submissions/Details/{wrong}')[2], 'Reveal progress survives a new authenticated browser session')

        sql(f'UPDATE AiFeedbacks SET RevealedHintCount=1 WHERE Id={stored};')
        token = Inputs(student.request('/Account/Profile')[2]).values['__RequestVerificationToken']
        clients = [Client() for _ in range(12)]
        for client in clients:
            for cookie in student.cookies:
                client.cookies.set_cookie(cookie)
        with concurrent.futures.ThreadPoolExecutor(max_workers=12) as pool:
            responses = list(pool.map(lambda client: client.request(path, {'__RequestVerificationToken': token}), clients))
        check(all(is_redirect(response) for response in responses)
              and int(sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE Id={stored};')) in [2, 3], 'Fast concurrent HTTP reveals stay bounded without duplicate rows')
        assert is_redirect(reveal(wrong))

        for legacy, available, count in [('[]', 0, 0), ('["legacy one"]', 1, 1), ('["legacy one","legacy two"]', 2, 1),
                                         ('["legacy one","legacy two","legacy three"]', 3, 0), ('{broken', 0, 1),
                                         ('null', 0, 1), ('{}', 0, 1), ('[null]', 0, 1), ('[1]', 0, 1), ('[""]', 0, 1)]:
            legacy_id = submission(tasks[4])
            feedback(legacy_id, legacy, count)
            body = details(legacy_id)
            assert f'Hints revealed: {min(count, available)} / {available}' in body
            if available <= count:
                assert f'action="/Submissions/{legacy_id}/RevealNextHint"' not in body
            for _ in range(3):
                assert is_redirect(reveal(legacy_id))
            assert f'Hints revealed: {available} / {available}' in details(legacy_id)
            assert sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE SubmissionId={legacy_id};') == str(available)
        check(True, 'Historical 0/1/2/3 and malformed hints render safely and reveal only available items')
        mismatch = submission(tasks[4])
        feedback(mismatch, '["mismatched private hint"]', user=other_id)
        check(reveal(mismatch)[0] == 404 and reveal(mismatch, other)[0] == 404
              and 'mismatched private hint' not in details(mismatch), 'Feedback owner and submission owner must both match')
        sql(f"INSERT INTO AspNetUserRoles(UserId,RoleId) SELECT '{other_id}',Id FROM AspNetRoles WHERE Name='Admin';")
        assert is_redirect(other.form('/Account/Login', {'Email': emails[1], 'Password': password}))
        check(reveal(wrong, other)[0] == 404, 'Admin role provides no cross-owner reveal bypass')

        accepted = submission(status=3)
        submission(tasks[1], 3)
        submission(tasks[2], 4)
        submission(tasks[2], 4)
        response = student.request('/Progress')
        body = inspect_csp(response)
        check(f'data-topic-id="{topic}" data-strength="47.0" data-label="Developing"' in body,
              'Progress learning card renders the 47-point formula example')
        assert f'href="/Tasks/{prefix}-2"' in learning_map(body)
        check('AI-analyzed patterns' in body and 'Only submissions with saved AI analysis' in body,
              'AI category profile clearly states its analyzed-only scope')
        assert 'id="solved-tasks">2</strong>' in body and 'id="rating-score">200</strong>' in body
        assert f'data-topic-id="{topic}" data-strength="0.0" data-label="Exploring"' in other.request('/Progress?userId=' + owner)[2]
        check(True, 'Existing required Progress metrics remain intact; forged UserId cannot change map ownership')
        hidden = int(sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({tasks[0]},'ENH3_HIDDEN_INPUT','ENH3_HIDDEN_EXPECTED',1,1); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        sql(f"INSERT INTO ExecutionResults(SubmissionId,TestCaseId,Status,ActualOutput,ErrorMessage) VALUES({wrong},{hidden},2,'ENH3_HIDDEN_ACTUAL','ENH3_HIDDEN_DIAGNOSTIC');")
        for route, editor in [(f'/Submissions/Details/{wrong}', False), (journey_path, False), (f'/Tasks/{prefix}-0', True),
                              (f'/Submissions/Compare?olderId={wrong}&newerId={accepted}', True), ('/Progress', False)]:
            page = inspect_csp(student.request(route), editor=editor)
            assert 'ENH3_HIDDEN_' not in page
            if '/Details/' not in route:
                assert all(hint.split()[0] not in page for hint in hints)
        check(True, 'Hidden values and hint texts are absent from unrelated pages; task/diff CSP stays scoped')
        student.request('/Leaderboard')  # Materialize missing rows before the no-side-effects snapshot.
        before_run, before_progress, before_map = snapshot(), progress(student), learning_map(student.request('/Progress')[2])
        response = student.form('/CustomRuns/Run', {'ProgrammingTaskId': tasks[0], 'RuntimeId': runtime,
            'SourceCode': '#include <iostream>\nint main(){int a,b;std::cin>>a>>b;std::cout<<a+b;}', 'CustomInput': '2 3'}, token_path=f'/Tasks/{prefix}-0')
        result = json.loads(response[2])
        check(response[0] == 200 and result['status'] == 'Success' and result['output'] == '5', 'Real Docker Custom Run still succeeds')
        check(snapshot() == before_run and progress(student) == before_progress and learning_map(student.request('/Progress')[2]) == before_map,
              'Custom Run leaves all row hashes, learning scores, errors, recommendation, Progress and Leaderboard unchanged')
        clean_runner()

        submission(tasks[2], 3)
        check(f'href="/Tasks/{prefix}-3"' in learning_map(student.request('/Progress')[2]), 'Solved task is never recommended; next remaining difficulty is selected')
        sql(f'UPDATE ProgrammingTasks SET IsPublished=0 WHERE Id={tasks[3]};')
        check(f'href="/Tasks/{prefix}-3"' not in learning_map(student.request('/Progress')[2]), 'Unpublished task is never recommended')
        sql(f'UPDATE ProgrammingTasks SET IsPublished=1 WHERE Id={tasks[3]};')
        sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,FinishedAt,PassedTests,TotalTests) SELECT '{owner}',t.Id,{runtime},'// completion fixture',3,SYSUTCDATETIME(),SYSUTCDATETIME(),0,0 FROM ProgrammingTasks t WHERE t.IsPublished=1 AND NOT EXISTS(SELECT 1 FROM Submissions s WHERE s.ProgrammingTaskId=t.Id AND s.UserId='{owner}' AND s.Status=3);")
        complete = learning_map(student.request('/Progress')[2])
        check('You have solved every published task.' in complete and 'practice-next-link' not in complete, 'All-solved HTML shows a positive completion state')
        print('ENHANCEMENT 3 HTTP/SQL/DOCKER CHECKS PASSED. No browser or paid AI request was used.')
    finally:
        if owner:
            # Include mismatch feedback deliberately owned by the other isolated fixture account.
            sql(f"DELETE f FROM AiFeedbacks f JOIN AspNetUsers u ON u.Id=f.UserId WHERE u.Email IN ('{emails[0]}','{emails[1]}');")
        sql(f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ('{emails[0]}','{emails[1]}'); "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ('{emails[0]}','{emails[1]}'); "
            f"DELETE t FROM TestCases t JOIN ProgrammingTasks p ON p.Id=t.ProgrammingTaskId WHERE p.Slug LIKE '{prefix}%'; "
            f"DELETE FROM ProgrammingTasks WHERE Slug LIKE '{prefix}%'; DELETE FROM Topics WHERE Name='{prefix}'; "
            f"DELETE FROM AspNetUsers WHERE Email IN ('{emails[0]}','{emails[1]}');")
        print('Removed only this run\'s Enhancement 3 HTTP fixtures.')


if __name__ == '__main__':
    main()
