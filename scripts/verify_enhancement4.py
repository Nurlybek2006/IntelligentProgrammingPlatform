"""Resource-backed HTTPS/SQL/Docker localization and Kazakh demonstration checks.

Run against the Development HTTPS app, after verify_resources.py. Creates only
UUID-scoped fixtures and cleans them in finally. Saved AI feedback is an explicit
offline fixture; this suite never requests analysis for a feedback-free attempt.
HTML/HTTP checks do not claim browser rendering, interaction or console coverage.
"""
import html
import json
import re
import secrets
import urllib.parse
import urllib.request
import uuid
from http.cookies import SimpleCookie
from html.parser import HTMLParser
from verify_phase2 import BASE, Client, Inputs, check, is_redirect, sql
from verify_enhancement1 import Tags, inspect as inspect_csp
from verify_enhancement2 import Page, clean_runner, snapshot
from verify_enhancement3_migration import literal, snapshot as feedback_metadata
from verify_resources import CULTURES, GROUPS, resources


TEXT = {(group, culture): resources(group, culture) for group in GROUPS for culture in CULTURES}
KEYS = {key for values in TEXT.values() for key in values if '_' in key}
MARKER = '</textarea></script><img src=x onerror=enh4_marker()>'
WRONG = '#include <iostream>\nint main(){std::cout<<0;}\n// ' + MARKER
GOOD = '#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b;}\n// ' + MARKER


def text(group, culture, key, *args):
    return TEXT[group + 'Resource', culture][key].format(*args)


class VisibleText(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.parts = []
        self.feed(body)

    def handle_data(self, data):
        self.parts.append(data)


def page(response, culture, expected=None, editor=False, status=200):
    body = inspect_csp(response, editor=editor, expected=status)
    decoded = html.unescape(body)
    tags = Tags(body).tags
    assert ('html', culture[:2]) in [(tag, attrs.get('lang')) for tag, attrs in tags]
    assert response[1].get('Content-Language') == culture
    assert not (set(re.findall(r'\b[A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+\b', ' '.join(VisibleText(body).parts))) & KEYS), 'Raw resource key in page text'
    for tag, attrs in tags:
        assert not (set(value for value in attrs.values() if value) & KEYS), 'Raw resource key in HTML attribute'
    if expected:
        group, key, *args = expected
        assert text(group, culture, key, *args) in decoded, (culture, group, key)
    # Semantic markup only: keyboard behavior, focus visibility and overflow need a real browser.
    labels = {attrs.get('for') for tag, attrs in tags if tag == 'label'}
    for tag, attrs in tags:
        if tag in ('input', 'select', 'textarea') and attrs.get('type') not in ('hidden', 'submit', 'button'):
            assert attrs.get('id') in labels or attrs.get('aria-label') or attrs.get('aria-labelledby'), ('Unlabelled control', attrs.get('id'))
        if tag == 'progress' or attrs.get('role') == 'progressbar':
            assert attrs.get('aria-label') or attrs.get('aria-labelledby'), 'Unlabelled progress'
        if tag == 'th':
            assert attrs.get('scope') in ('col', 'row'), 'Table header scope'
    return decoded


def all_database_hashes():
    # Hash every table in deterministic primary-key order. Credentials never leave SQL.
    metadata = sql("SELECT QUOTENAME(s.name)+'.'+QUOTENAME(t.name)+'|'+"
                   "STRING_AGG(CAST(QUOTENAME(c.name) AS nvarchar(max)),',') WITHIN GROUP(ORDER BY ic.key_ordinal) "
                   "FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id "
                   "JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1 "
                   "JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id "
                   "JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id "
                   "WHERE t.is_ms_shipped=0 GROUP BY s.name,t.name ORDER BY s.name,t.name;")
    tables = [line.strip().split('|') for line in metadata.splitlines() if line.strip()]
    assert len(tables) == int(sql('SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;')), 'Every table needs a deterministic key'
    return sql('SELECT ' + ','.join(
        f"CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM {table} ORDER BY {order} FOR JSON PATH,INCLUDE_NULL_VALUES)),2)"
        for table, order in tables) + ';')


def switch(client, culture, return_url='/Tasks'):
    result = client.form('/Culture/Set', {'culture': culture, 'returnUrl': return_url}, token_path='/')
    assert is_redirect(result) and result[1]['Location'] == return_url
    cookies = SimpleCookie()
    for header in result[1].get_all('Set-Cookie', []):
        cookies.load(header)
    cookie = cookies['.AspNetCore.Culture']
    assert urllib.parse.unquote(cookie.value) == f'c={culture}|uic={culture}'
    assert cookie['secure'] and cookie['httponly'] and cookie['samesite'].lower() == 'lax' and cookie['path'] == '/'
    assert cookie['expires']
    return result


def main():
    prefix = 'enhancement4-' + uuid.uuid4().hex
    emails = [prefix + f'-{index}@example.test' for index in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    student, admin, guest = Client(timeout=120, culture=None), Client(culture=None), Client(culture=None)
    title, description = 'Stored title <em>unchanged</em>', 'Stored task text <script>task_marker()</script>'
    task_path, next_path = '/Tasks/' + prefix + '-0', '/Tasks/' + prefix + '-1'
    journey_path = task_path + '/Journey'
    try:
        page(guest.request('/'), 'kk-KZ', ('Student', 'Home_Heading'))
        page(guest.request('/?culture=en-US&ui-culture=en-US'), 'kk-KZ', ('Student', 'Home_Heading'))
        request = urllib.request.Request(BASE + '/', headers={'Accept-Language': 'ru-RU'})
        with guest.opener.open(request, timeout=30) as result:
            page((result.status, result.headers, result.read().decode('utf-8')), 'kk-KZ')
        invalid_cookie = Client(culture='fr-FR')
        page(invalid_cookie.request('/'), 'kk-KZ')
        check(True, 'Default is Kazakh; query/header/unsupported-cookie values cannot select an unsupported culture')

        assert guest.request('/Culture/Set')[0] in (404, 405)
        assert guest.request('/Culture/Set', {'culture': 'ru-RU'})[0] == 400
        token = Inputs(guest.request('/')[2]).values['__RequestVerificationToken']
        for culture in ['', 'en', 'EN-US', 'kk-kz', 'ru', 'fr-FR', '../en-US', 'en-US\r\nX-Test: bad']:
            result = guest.request('/Culture/Set', {'culture': culture, 'returnUrl': '/', '__RequestVerificationToken': token})
            assert result[0] == 400 and not any('.AspNetCore.Culture=' in value for value in result[1].get_all('Set-Cookie', []))
        for return_url in ['https://untrusted.invalid/', '//untrusted.invalid/', '/\\untrusted.invalid/', '\\\\untrusted.invalid/', 'javascript:alert(1)', '///untrusted.invalid/']:
            result = guest.form('/Culture/Set', {'culture': 'kk-KZ', 'returnUrl': return_url}, token_path='/')
            assert is_redirect(result) and result[1]['Location'] == '/'
        switch(guest, 'kk-KZ', '/Tasks?Difficulty=0&Search=fixture')
        check(True, 'Culture endpoint is POST/CSRF protected, strictly allowlisted, uses secure cookie attributes and rejects open redirects')

        for client, email in [(student, emails[0]), (admin, emails[1])]:
            assert is_redirect(client.form('/Account/Register', {'DisplayName': 'Enh4 <b>fixture</b>', 'Email': email,
                'Password': password, 'ConfirmPassword': password}))
        owner, administrator = [sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}';") for email in emails]
        sql(f"INSERT INTO AspNetUserRoles(UserId,RoleId) SELECT '{administrator}',Id FROM AspNetRoles WHERE Name='Admin';")
        assert is_redirect(admin.form('/Account/Login', {'Email': emails[1], 'Password': password}))
        assert is_redirect(student.request('/Admin')) and is_redirect(guest.request('/Admin'))
        assert student.form('/Admin/Topics/Create', {'Name': prefix}, token_path='/Account/Profile')[0] in (302, 403)
        check(True, 'Admin authorization still rejects anonymous and Student requests')

        topic = int(sql(f"INSERT INTO Topics(Name,Description) VALUES('{prefix}','Stored topic description'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        tasks = []
        for index in range(2):
            tasks.append(int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES({literal(title + str(index))},'{prefix}-{index}',{literal(description)},0,{topic},500,32,1,'2026-01-02T03:04:05'); SELECT CAST(SCOPE_IDENTITY() AS int);")))
        runtime = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
        test_case = int(sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({tasks[0]},'2 3','5',0,1); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({tasks[0]},'82736412 16273849','99010261',1,2);")

        def submit(code, expected):
            response = student.form('/Submissions/Submit', {'ProgrammingTaskId': tasks[0], 'RuntimeId': runtime,
                'SourceCode': code}, token_path=task_path)
            assert is_redirect(response), response[0]
            identifier = int(response[1]['Location'].rsplit('/', 1)[1])
            assert sql(f'SELECT Status FROM Submissions WHERE Id={identifier};') == str(expected)
            return identifier

        # Complete the university demonstration through real HTTP/SQL/Docker in Kazakh.
        assert is_redirect(student.form('/Account/Logout', {}, token_path='/Account/Profile'))
        page(student.request('/'), 'kk-KZ', ('Student', 'Home_Heading'))
        page(student.request('/Account/Login'), 'kk-KZ', ('Student', 'Account_Welcome'))
        assert is_redirect(student.form('/Account/Login', {'Email': emails[0], 'Password': password}))
        page(student.request('/Tasks'), 'kk-KZ', ('Student', 'Tasks_Heading'))
        page(student.request(task_path), 'kk-KZ', ('Student', 'Task_YourSolution'), editor=True)
        student.request('/Leaderboard')  # Materialize all missing summaries before state-isolation checks.
        before_run = snapshot()
        response = student.form('/CustomRuns/Run', {'ProgrammingTaskId': tasks[0], 'RuntimeId': runtime,
            'SourceCode': WRONG, 'CustomInput': '2 3'}, token_path=task_path)
        result = json.loads(response[2])
        assert response[0] == 200 and result['status'] == 'Success' and result['output'] == '0'
        assert snapshot() == before_run
        wrong = submit(WRONG, 4)
        wrong_path = f'/Submissions/Details/{wrong}'
        page(student.request(wrong_path), 'kk-KZ', ('Shared', 'Status_WrongAnswer'))
        hints = ['FIRST_ENH4_HINT: Екі санды да оқыңыз. <img src=x onerror=hint_marker()>',
                 'SECOND_ENH4_HINT: Нәтижені кіріс мәндерінен есептеңіз.', 'THIRD_ENH4_HINT: Қосу амалын тексеріңіз.']
        summary = 'OFFLINE_AI_SUMMARY: Бұл тексеріске арналған сақталған кеңес.'
        sql(f"INSERT INTO AiFeedbacks(SubmissionId,UserId,Model,Summary,Explanation,HintsJson,RevealedHintCount,ErrorCategory,CreatedAt,InputTokens,OutputTokens) VALUES({wrong},'{owner}','offline-enh4-no-api',{literal(summary)},{literal('OFFLINE_AI_EXPLANATION: Кіріс мәндері өзгергенде нәтиже де өзгеруі тиіс.')},{literal(json.dumps(hints, ensure_ascii=False))},1,'Logic',SYSUTCDATETIME(),17,23);")
        stored_feedback = feedback_metadata(owner)
        assert sql(f'SELECT COUNT(*) FROM AiFeedbacks WHERE SubmissionId={wrong};') == '1'
        assert is_redirect(student.form(f'/Submissions/Analyze/{wrong}', {}, token_path=wrong_path))
        first_hint = page(student.request(wrong_path), 'kk-KZ', ('Shared', 'AiMessage_Existing'))
        assert 'FIRST_ENH4_HINT' in first_hint and 'SECOND_ENH4_HINT' not in first_hint and 'THIRD_ENH4_HINT' not in first_hint
        assert '<img src=x onerror=hint_marker()>' not in student.request(wrong_path)[2]
        assert is_redirect(student.form(f'/Submissions/{wrong}/RevealNextHint', {}, token_path=wrong_path))
        second_hint = page(student.request(wrong_path), 'kk-KZ', ('Student', 'Ai_RevealedCount', 2, 3))
        assert 'SECOND_ENH4_HINT' in second_hint and 'THIRD_ENH4_HINT' not in second_hint
        assert feedback_metadata(owner) == stored_feedback
        accepted = submit(GOOD, 3)
        accepted_path = f'/Submissions/Details/{accepted}'
        page(student.request(accepted_path), 'kk-KZ', ('Shared', 'Status_Accepted'))
        compare_path = f'/Submissions/Compare?olderId={wrong}&newerId={accepted}'
        comparison = student.request(compare_path)
        page(comparison, 'kk-KZ', ('Student', 'Diff_Title'), editor=True)
        assert Page(comparison[2]).sources == {'previous-source': WRONG, 'current-source': GOOD}
        journey = student.request(journey_path)
        page(journey, 'kk-KZ', ('Student', 'Journey_Title'))
        assert Page(journey[2]).attempts == [(1, wrong, 'WrongAnswer'), (2, accepted, 'Accepted')]
        progress = page(student.request('/Progress'), 'kk-KZ', ('Student', 'Map_Title'))
        assert f'data-topic-id="{topic}" data-strength="50.0" data-label="Exploring"' in progress
        assert f'href="{next_path}"' in progress and 'id="practice-next-link"' in progress
        page(student.request('/Leaderboard'), 'kk-KZ', ('Student', 'Common_Leaderboard'))
        clean_runner()
        check(True, 'Kazakh HTTP demo: login, custom Run without writes, real WrongAnswer, offline saved AI, hints 1/2, corrected Accepted, Diff, Journey, Learning Map, Practice Next and Leaderboard')

        # Stored source, content, feedback and aggregate rows must survive repeated culture switches verbatim.
        complete_before = all_database_hashes()
        for culture in ['kk-KZ', 'ru-RU', 'en-US', 'kk-KZ']:
            local_page = wrong_path + '?from=culture'
            switch(student, culture, local_page)
            shown = page(student.request(local_page), culture, ('Student', 'Ai_Title'))
            assert summary in shown and all(hint in shown for hint in hints[:2]) and 'THIRD_ENH4_HINT' not in shown
            page(student.request('/Account/Profile'), culture, ('Student', 'Account_Profile'))
            assert is_redirect(student.form(f'/Submissions/Analyze/{wrong}', {}, token_path=wrong_path))
            page(student.request(wrong_path), culture, ('Shared', 'AiMessage_Existing'))
            assert all_database_hashes() == complete_before
        check(True, 'kk -> ru -> en -> kk preserves authentication, safe local page, every database-table hash and originally stored AI text; existing Analyze never regenerates feedback')

        for culture in CULTURES:
            switch(student, culture)
            switch(admin, culture)
            switch(guest, culture)
            public_pages = [('/', 'Home_Heading'), ('/Tasks', 'Tasks_Heading'), ('/Leaderboard', 'Common_Leaderboard'),
                            ('/Account/Login', 'Account_Welcome'), ('/Account/Register', 'Account_StartJourney')]
            for route, key in public_pages:
                page(guest.request(route), culture, ('Student', key))
            for route, key, editor in [(task_path, 'Task_YourSolution', True), ('/Submissions/My', 'Common_MySubmissions', False),
                    (wrong_path, 'Ai_ProgressiveHints', False), (accepted_path, 'Submission_Title', False),
                    (journey_path, 'Journey_Title', False), (compare_path, 'Diff_Title', True),
                    ('/Progress', 'Map_PracticeNext', False), ('/Account/Profile', 'Account_Profile', False)]:
                expected = ('Student', key, accepted) if key == 'Submission_Title' else ('Student', key)
                page(student.request(route), culture, expected, editor=editor)
            for route, key in [('/Admin', 'Dashboard_Title'), ('/Admin/Topics', 'Topics'),
                    ('/Admin/Topics/Create', 'Topic_Create'), (f'/Admin/Topics/Edit/{topic}', 'Topic_Edit'),
                    (f'/Admin/Topics/Delete/{topic}', 'Topic_Delete'), ('/Admin/ProgrammingTasks', 'Tasks'),
                    ('/Admin/ProgrammingTasks/Create', 'Task_Create'), (f'/Admin/ProgrammingTasks/Edit/{tasks[0]}', 'Task_Edit'),
                    (f'/Admin/ProgrammingTasks/Delete/{tasks[0]}', 'Task_Delete'), (f'/Admin/ProgrammingTasks/Details/{tasks[0]}', 'Task_Back'),
                    (f'/Admin/TestCases?taskId={tasks[0]}', 'TestCases'), (f'/Admin/TestCases/Create?taskId={tasks[0]}', 'TestCase_Create'),
                    (f'/Admin/TestCases/Edit/{test_case}', 'TestCase_Edit'), (f'/Admin/TestCases/Delete/{test_case}', 'TestCase_Delete')]:
                page(admin.request(route), culture, ('Admin', key))
            denied = student.request('/Account/AccessDenied')
            page(denied, culture, ('Student', 'Account_AccessDenied'), status=403)
            page(guest.request('/does-not-exist-enhancement4'), culture, ('Shared', 'Error_NotFound'), status=404)
            page(guest.request('/Home/Error'), culture, ('Shared', 'Error_Server'), status=500)

            task_body = page(student.request(task_path), culture, ('Shared', 'Difficulty_Easy'), editor=True)
            assert title + '0' in task_body and description in task_body and '<script>task_marker()' not in student.request(task_path)[2]
            for key in ['Run_Button', 'Run_CustomInput', 'Submit_Button']:
                assert text('Student', culture, key) in task_body
            catalog = page(guest.request('/Tasks'), culture, ('Student', 'Tasks_Heading'))
            for difficulty in ['Easy', 'Medium', 'Hard']:
                assert text('Shared', culture, 'Difficulty_' + difficulty) in catalog
            assert '<option value="0"' in catalog and '<option value="1"' in catalog and '<option value="2"' in catalog
            result = page(student.request(wrong_path), culture, ('Shared', 'Status_WrongAnswer'))
            assert 'data-status="WrongAnswer"' in result and hints[0] in result and hints[1] in result and 'THIRD_ENH4_HINT' not in result
            map_body = page(student.request('/Progress'), culture, ('Shared', 'Strength_Exploring'))
            assert f'data-topic-id="{topic}" data-strength="50.0" data-label="Exploring"' in map_body
            assert ('50.0%' if culture == 'en-US' else '50,0%') in map_body
            for key in ['Map_ErrorPatterns', 'Map_AiPatterns', 'Map_PracticeNext']:
                assert text('Student', culture, key) in map_body
            for route in [task_path, wrong_path, accepted_path, journey_path, compare_path, '/Progress']:
                raw = student.request(route)[2]
                assert all(secret not in raw for secret in ['82736412 16273849', '99010261', 'THIRD_ENH4_HINT'])
                if route != wrong_path:
                    assert 'FIRST_ENH4_HINT' not in raw and 'SECOND_ENH4_HINT' not in raw
            assert admin.request(compare_path)[0] == 404 and admin.request(wrong_path)[0] == 404
            assert admin.form(f'/Submissions/{wrong}/RevealNextHint', {}, token_path='/Account/Profile')[0] == 404
            assert student.request(f'/Submissions/{wrong}/RevealNextHint', {})[0] == 400
            check(True, culture + ': all main/Admin pages, status/difficulty/map labels, semantic form labels, hidden data, hint boundaries, owner-only Diff and CSP pass')

            # Presentation validation must localize without touching any submission history.
            before_validation = snapshot()
            invalid_login = guest.form('/Account/Login', {'Email': prefix + '-missing@example.test', 'Password': password})
            page(invalid_login, culture, ('Shared', 'Account_InvalidLogin'))
            required = guest.form('/Account/Register', {'DisplayName': '', 'Email': '', 'Password': '', 'ConfirmPassword': ''})
            page(required, culture, ('Shared', 'Validation_Required', text('Shared', culture, 'Field_Email')))
            weak = guest.form('/Account/Register', {'DisplayName': 'Validation fixture', 'Email': prefix + '-weak@example.test',
                'Password': 'abcdefghijk', 'ConfirmPassword': 'abcdefghijk'})
            page(weak, culture, ('Shared', 'Identity_PasswordRequiresDigit'))
            duplicate = guest.form('/Account/Register', {'DisplayName': 'Duplicate', 'Email': emails[0],
                'Password': password, 'ConfirmPassword': password})
            page(duplicate, culture, ('Shared', 'Account_DuplicateEmail'))
            duplicate_topic = admin.form('/Admin/Topics/Create', {'Name': prefix})
            page(duplicate_topic, culture, ('Admin', 'Topic_Duplicate'))
            missing_topic = admin.form('/Admin/Topics/Create', {'Name': ''})
            page(missing_topic, culture, ('Admin', 'Validation_Required', text('Admin', culture, 'Field_Name')))
            task_data = {'Title': 'Duplicate', 'Slug': prefix + '-0', 'Description': 'Validation', 'Difficulty': '0',
                         'TopicId': topic, 'TimeLimitMs': '500', 'MemoryLimitMb': '32'}
            page(admin.form('/Admin/ProgrammingTasks/Create', task_data), culture, ('Admin', 'Task_DuplicateSlug'))
            page(admin.form('/Admin/ProgrammingTasks/Create', {**task_data, 'Slug': 'invalid slug', 'Difficulty': '99'}), culture, ('Admin', 'Validation_Slug'))
            duplicate_test = admin.form('/Admin/TestCases/Create', {'ProgrammingTaskId': tasks[0], 'Input': '', 'ExpectedOutput': '', 'Order': '1'},
                                        token_path=f'/Admin/TestCases/Create?taskId={tasks[0]}')
            page(duplicate_test, culture, ('Admin', 'TestCase_DuplicateOrder'))
            for code, key in [('', 'Validation_Required'), ('x' * 65537, 'Validation_SourceLimit'), ('//' + '\u044f' * 32768, 'Validation_SourceLimit')]:
                invalid = student.form('/Submissions/Submit', {'ProgrammingTaskId': tasks[0], 'RuntimeId': runtime, 'SourceCode': code}, token_path=task_path)
                expected = ('Shared', key, text('Shared', culture, 'Field_SourceCode')) if key == 'Validation_Required' else ('Shared', key)
                page(invalid, culture, expected, editor=True)
            invalid_run = student.form('/CustomRuns/Run', {'ProgrammingTaskId': tasks[0], 'RuntimeId': runtime, 'SourceCode': GOOD,
                'CustomInput': '\u044f' * 16385}, token_path=task_path)
            assert invalid_run[0] == 400 and json.loads(invalid_run[2])['error'] == text('Shared', culture, 'Run_Invalid')
            assert snapshot() == before_validation
            check(True, culture + ': required/login/password/duplicate topic-task-test/source and custom-input validation is localized without execution or saved attempts')

        assert feedback_metadata(owner) == stored_feedback
        assert sql(f'SELECT COUNT(*) FROM Submissions WHERE UserId=\'{owner}\';') == '2'
        assert sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE SubmissionId={wrong};') == '2'
        clean_runner()
        print('ENHANCEMENT 4 HTTPS/SQL/DOCKER CHECKS PASSED. Three languages tested over HTTP; zero browser viewports and zero live AI analyses.', flush=True)
    finally:
        addresses = ','.join("'" + email + "'" for email in [*emails, prefix + '-weak@example.test'])
        sql(f"DELETE f FROM AiFeedbacks f JOIN AspNetUsers u ON u.Id=f.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE t FROM TestCases t JOIN ProgrammingTasks p ON p.Id=t.ProgrammingTaskId WHERE p.Slug LIKE '{prefix}%'; "
            f"DELETE FROM ProgrammingTasks WHERE Slug LIKE '{prefix}%'; DELETE FROM Topics WHERE Name='{prefix}'; "
            f"DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed only this run\'s Enhancement 4 fixture data.', flush=True)


if __name__ == '__main__':
    main()
