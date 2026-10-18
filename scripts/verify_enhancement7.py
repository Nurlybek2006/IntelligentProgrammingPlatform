"""Enhancement 7 HTTPS/SQL/Docker checks using UUID fixtures, removed in finally.

Default: zero paid AI calls, including saved-review reuse.
--live-review: exactly one explicitly authorized review attempt, recorded before POST.
Never automatically repeat the live mode. No browser rendering is claimed.
"""
import argparse
import html
import json
import os
import re
import secrets
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

from verify_phase2 import Client, check, is_redirect, sql
from verify_extra_tasks import sql_json
from verify_enhancement1 import Tags
from verify_enhancement2 import clean_runner
from verify_enhancement4 import page, switch, text
from verify_resources import CULTURES

ROOT = Path(__file__).resolve().parents[1]
GOOD = '#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b;}'
MARKER = '<img src=x onerror=enh7_marker()>'


def literal(value):
    # SQLCMD on Windows reparses quoted JSON arguments; a Unicode hex literal avoids that ambiguity.
    return "CONVERT(nvarchar(max),0x" + str(value).encode('utf-16le').hex() + ")"


def configuration():
    project = ET.parse(ROOT / 'IntelligentProgrammingPlatform.csproj')
    path = Path(os.environ['APPDATA']) / 'Microsoft/UserSecrets' / project.findtext('.//UserSecretsId') / 'secrets.json'
    return json.loads(path.read_text(encoding='utf-8-sig'))


def cells(body):
    return [(attrs['data-date'], int(attrs['data-submissions']), int(attrs['data-accepted']))
            for tag, attrs in Tags(body).tags if 'data-date' in attrs]


def api_starts(submission_id):
    data = (ROOT / 'obj/enhancement7-app.log').read_bytes()
    log = data.decode('utf-16' if data.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
    return log.count(f'Accepted review API request started for submission {submission_id}\n') + log.count(
        f'Accepted review API request started for submission {submission_id}\r\n')


def review_snapshot(submission_id):
    return sql(f"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM AcceptedCodeReviews WHERE SubmissionId={submission_id} FOR JSON PATH)),2)")


def main(live=False):
    settings = configuration()
    ledger_path = ROOT / 'obj/enhancement7-live-result.json'
    if live:
        assert not ledger_path.exists(), 'Live review already attempted; do not automatically repeat a paid call.'
        if not settings.get('OpenAI:ApiKey', settings.get('OpenAI', {}).get('ApiKey')):
            print('LIVE REVIEW SKIPPED: no configured User Secrets key. Real API calls: 0.')
            return
    prefix = 'enh7-' + uuid.uuid4().hex
    addresses = [prefix + suffix + '@example.test' for suffix in ('-owner', '-other')]
    owner, other, admin, guest = Client(timeout=180), Client(), Client(), Client()
    task_id = lesson_id = topic_id = None
    try:
        for client, email in zip((owner, other), addresses):
            password = 'Aa1!' + secrets.token_urlsafe(24)
            assert is_redirect(client.form('/Account/Register', {'DisplayName': 'Enhancement 7 fixture', 'Email': email,
                'Password': password, 'ConfirmPassword': password}))
        user_id = sql(f'SELECT Id FROM AspNetUsers WHERE Email={literal(addresses[0])}')
        other_id = sql(f'SELECT Id FROM AspNetUsers WHERE Email={literal(addresses[1])}')
        topic_id = int(sql(f'INSERT Topics(Name,Description) OUTPUT INSERTED.Id VALUES({literal(prefix)},N\'Temporary fixture\')'))
        task_id = int(sql(f"INSERT ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) OUTPUT INSERTED.Id VALUES({literal(prefix)},{literal(prefix)},N'Read two integers and print their sum.',0,{topic_id},2000,128,1,SYSUTCDATETIME())"))
        lesson_id = int(sql(f"INSERT Lessons(Title,Slug,Summary,Content,TopicId,[Order],IsPublished,CreatedAt) OUTPUT INSERTED.Id VALUES({literal(prefix)},{literal(prefix)},N'Fixture',N'Read then practice.',{topic_id},1,1,SYSUTCDATETIME())"))
        sql(f"INSERT TestCases(ProgrammingTaskId,Input,ExpectedOutput,[Order],IsHidden) VALUES({task_id},N'2 3',N'5',1,0),({task_id},N'712345 123456',N'835801',2,1)")
        cpp = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp'"))
        task_path, lesson_path = '/Tasks/' + prefix, '/Lessons/' + prefix
        if live:
            live_review(owner, user_id, task_id, cpp, task_path, ledger_path)
            return

        assert is_redirect(guest.request('/Tasks/Favorites'))
        for route, data, token_page in [('/Favorites/Add', {'taskId': task_id}, task_path),
            ('/LessonCompletions/Complete', {'lessonId': lesson_id}, lesson_path)]:
            assert is_redirect(guest.form(route, data, token_path=token_page))
            assert owner.request(route)[0] in (404, 405)
            assert owner.request(route, data)[0] == 400
        check(True, 'Favorites and completion require authentication, POST and antiforgery')
        for _ in range(2):
            response = owner.form('/Favorites/Add', {'taskId': task_id, 'UserId': other_id, 'returnUrl': 'https://example.test'}, token_path=task_path)
            assert is_redirect(response) and response[1]['Location'] == '/Tasks/Favorites'
        assert sql(f"SELECT COUNT(*) FROM TaskFavorites WHERE UserId='{user_id}' AND ProgrammingTaskId={task_id}") == '1'
        assert sql(f"SELECT COUNT(*) FROM TaskFavorites WHERE UserId='{other_id}'") == '0'
        assert prefix in owner.request('/Tasks/Favorites')[2] and prefix not in other.request('/Tasks/Favorites')[2]
        assert is_redirect(other.form('/Favorites/Remove', {'taskId': task_id, 'UserId': user_id}, token_path=task_path))
        assert sql(f"SELECT COUNT(*) FROM TaskFavorites WHERE UserId='{user_id}'") == '1'
        sql(f'UPDATE ProgrammingTasks SET IsPublished=0 WHERE Id={task_id}')
        assert prefix not in owner.request('/Tasks/Favorites')[2]
        assert other.form('/Favorites/Add', {'taskId': task_id}, token_path='/Tasks')[0] == 404
        sql(f'UPDATE ProgrammingTasks SET IsPublished=1 WHERE Id={task_id}')
        check(True, 'Favorite add/remove is owner scoped and idempotent; forged UserId ignored; unpublished tasks hidden')
        for _ in range(2):
            assert is_redirect(owner.form('/LessonCompletions/Complete', {'lessonId': lesson_id, 'UserId': other_id}, token_path=lesson_path))
        assert sql(f"SELECT COUNT(*) FROM LessonProgresses WHERE UserId='{user_id}' AND LessonId={lesson_id}") == '1'
        assert sql(f"SELECT COUNT(*) FROM LessonProgresses WHERE UserId='{other_id}'") == '0'
        assert 'id="lesson-completed"' in owner.request(lesson_path)[2]
        assert 'id="profile-xp">20</strong>' in owner.request('/Account/Profile')[2]
        assert is_redirect(admin.form('/Account/Login', {'Email': settings['SeedAdmin:Email'], 'Password': settings['SeedAdmin:Password']}))
        blocked = admin.form(f'/Admin/Lessons/Delete/{lesson_id}', {})
        assert blocked[0] == 200 and text('Admin', 'en-US', 'Lesson_HasProgress') in html.unescape(blocked[2])
        assert sql(f'SELECT COUNT(*) FROM Lessons WHERE Id={lesson_id}') == '1'
        sql(f'UPDATE Lessons SET IsPublished=0 WHERE Id={lesson_id}')
        assert other.form('/LessonCompletions/Complete', {'lessonId': lesson_id}, token_path='/Tasks')[0] == 404
        sql(f'UPDATE Lessons SET IsPublished=1 WHERE Id={lesson_id}')
        check(True, 'Lesson completion gives 20 XP once, ignores forged owner, rejects unpublished lesson and blocks Admin deletion')

        before_cells = cells(owner.request('/Account/Profile')[2])
        run = owner.form('/CustomRuns/Run', {'ProgrammingTaskId': task_id, 'RuntimeId': cpp, 'SourceCode': GOOD, 'CustomInput': '2 3'}, token_path=task_path)
        assert run[0] == 200 and json.loads(run[2])['status'] == 'Success'
        assert cells(owner.request('/Account/Profile')[2]) == before_cells
        assert 'id="profile-xp">20</strong>' in owner.request('/Account/Profile')[2]
        response = owner.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': cpp, 'SourceCode': GOOD}, token_path=task_path)
        assert is_redirect(response)
        accepted_id = int(response[1]['Location'].rstrip('/').split('/')[-1])
        assert sql(f'SELECT Status FROM Submissions WHERE Id={accepted_id}') == '3'
        profile = owner.request('/Account/Profile')[2]
        activity = cells(profile)
        assert len(activity) == 365 and sum(item[1] for item in activity) == 1 and sum(item[2] for item in activity) == 1
        assert 'id="profile-xp">70</strong>' in profile
        workspace = owner.request(task_path)[2]
        assert 'solved-badge' in workspace and text('Shared', 'en-US', 'Xp_Earned') in html.unescape(workspace)
        assert sql(f'SELECT COUNT(*) FROM AcceptedCodeReviews WHERE SubmissionId={accepted_id}') == '0'
        assert api_starts(accepted_id) == 0
        check(True, 'Actual Docker Accepted adds 50 XP/activity; Custom Run adds neither; no automatic AI call')

        detail = f'/Submissions/Details/{accepted_id}'
        route = f'/AcceptedCodeReviews/Analyze/{accepted_id}'
        assert owner.request(route)[0] in (404, 405) and owner.request(route, {})[0] == 400
        assert is_redirect(guest.form(route, {}, token_path='/Tasks'))
        assert other.form(route, {'UserId': user_id}, token_path='/Tasks')[0] == 404
        wrong_id = int(sql(f"INSERT Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,FinishedAt,PassedTests,TotalTests) OUTPUT INSERTED.Id VALUES('{user_id}',{task_id},{cpp},N'// offline WrongAnswer fixture',4,SYSUTCDATETIME(),SYSUTCDATETIME(),0,1)"))
        assert is_redirect(owner.form(f'/AcceptedCodeReviews/Analyze/{wrong_id}', {}, token_path=f'/Submissions/Details/{wrong_id}'))
        assert api_starts(accepted_id) == 0 and api_starts(wrong_id) == 0
        check(True, 'Review POST enforces CSRF, authenticated owner and Accepted status before any API call')

        # Deliberate stored offline fixture: exercising cache/display must never buy analysis.
        suggestions = json.dumps(['First offline suggestion', 'Second offline suggestion', 'Third offline suggestion'])
        sql(f"INSERT AcceptedCodeReviews(SubmissionId,Model,Summary,ReadabilityFeedback,NamingFeedback,EfficiencyFeedback,StyleFeedback,TimeComplexity,SpaceComplexity,ComplexityExplanation,SuggestionsJson,CreatedAt) VALUES({accepted_id},N'offline-http-fixture',{literal(MARKER)},N'Readable',N'Names',N'Efficient',N'Consistent',N'O(1)',N'O(1)',N'AI estimate, not a proof.',{literal(suggestions)},SYSUTCDATETIME())")
        stored = review_snapshot(accepted_id)
        for culture in CULTURES:
            switch(owner, culture)
            rendered = page(owner.request(detail), culture, ('Shared', 'Review_Caution'))
            assert MARKER not in owner.request(detail)[2] and MARKER in rendered
            assert '712345 123456' not in rendered and '835801' not in rendered
            assert is_redirect(owner.form(route, {}, token_path=detail))
            assert review_snapshot(accepted_id) == stored and api_starts(accepted_id) == 0
            page(owner.request('/Account/Profile'), culture, ('Shared', 'Activity_Title'))
            page(owner.request('/Tasks/Favorites'), culture, ('Shared', 'Favorites_Title'))
            page(owner.request(lesson_path), culture, ('Shared', 'Lesson_Completed'))
            workspace = page(owner.request(task_path), culture, ('Shared', 'Workspace_Reset'), editor=True)
            assert 'data-reset-confirm=' in owner.request(task_path)[2] and 'id="reset-code"' in workspace
        switch(admin, 'en-US')
        admin_profile = page(admin.request('/Admin/Users/Details/' + user_id), 'en-US', ('Shared', 'Xp_Title'))
        assert '70' in admin_profile and 'profile-xp' in admin_profile
        page(admin.request('/Admin/Users'), 'en-US', ('Shared', 'Xp_Total'))
        check(True, 'Saved review is reused across three cultures with zero AI calls; review XSS/hidden tests/CSP and XP/heatmap/favorite/workspace translations pass')
        for _ in range(2): assert is_redirect(owner.form('/Favorites/Remove', {'taskId': task_id}, token_path=task_path))
        assert sql(f"SELECT COUNT(*) FROM TaskFavorites WHERE UserId='{user_id}'") == '0'
        clean_runner()
        print('ENHANCEMENT 7 HTTPS/SQL/DOCKER CHECKS PASSED. Paid OpenAI calls: 0. Browser checks remain separate.')
    finally:
        selected = ','.join(map(literal, addresses))
        sql(f"DELETE p FROM LessonProgresses p JOIN AspNetUsers u ON u.Id=p.UserId WHERE u.Email IN ({selected}); "
            f"DELETE f FROM TaskFavorites f JOIN AspNetUsers u ON u.Id=f.UserId WHERE u.Email IN ({selected}); "
            f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({selected}); "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({selected}); "
            f"DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug={literal(prefix)}); "
            f"DELETE FROM ProgrammingTasks WHERE Slug={literal(prefix)}; DELETE FROM Lessons WHERE Slug={literal(prefix)}; "
            f"DELETE FROM Topics WHERE Name={literal(prefix)}; DELETE FROM AspNetUsers WHERE Email IN ({selected});")
        print('Removed only this run\'s Enhancement 7 fixtures, including reviews and learning history.')


def live_review(owner, user_id, task_id, runtime, task_path, ledger_path):
    response = owner.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime, 'SourceCode': GOOD}, token_path=task_path)
    assert is_redirect(response)
    submission_id = int(response[1]['Location'].rstrip('/').split('/')[-1])
    assert sql(f'SELECT Status FROM Submissions WHERE Id={submission_id}') == '3'
    detail = f'/Submissions/Details/{submission_id}'
    route = f'/AcceptedCodeReviews/Analyze/{submission_id}'
    switch(owner, 'kk-KZ')
    assert api_starts(submission_id) == 0
    ledger = {'submissionId': submission_id, 'attempted': True, 'success': False, 'realApiCalls': 0}
    ledger_path.write_text(json.dumps(ledger, indent=2), encoding='utf-8')
    try:
        result = owner.form(route, {}, token_path=detail)  # The only permitted new review request.
        ledger['realApiCalls'] = api_starts(submission_id)
        assert is_redirect(result) and ledger['realApiCalls'] == 1
        assert sql(f'SELECT COUNT(*) FROM AcceptedCodeReviews WHERE SubmissionId={submission_id}') == '1', 'Single live attempt did not produce a stored review; do not retry automatically.'
        saved = sql_json(f'SELECT Model,TimeComplexity,SpaceComplexity,Summary,ReadabilityFeedback,NamingFeedback,EfficiencyFeedback,StyleFeedback,ComplexityExplanation,SuggestionsJson,InputTokens,OutputTokens FROM AcceptedCodeReviews WHERE SubmissionId={submission_id}')[0]
        assert all(saved[key] for key in ('TimeComplexity', 'SpaceComplexity', 'Summary', 'ComplexityExplanation'))
        assert len(json.loads(saved['SuggestionsJson'])) == 3
        combined = '\n'.join(value for value in saved.values() if isinstance(value, str))
        assert '```' not in combined and '#include' not in combined and not re.search(r'\bint\s+main\s*\(', combined)
        stored = review_snapshot(submission_id)
        switch(owner, 'en-US')
        assert is_redirect(owner.form(route, {}, token_path=detail))
        assert review_snapshot(submission_id) == stored and api_starts(submission_id) == 1
        assert sql(f'SELECT Status FROM Submissions WHERE Id={submission_id}') == '3'
        page(owner.request(detail), 'en-US', ('Shared', 'Review_Caution'))
        ledger.update(success=True, reusedWithoutSecondCall=True, model=saved['Model'], timeComplexity=saved['TimeComplexity'],
            spaceComplexity=saved['SpaceComplexity'], inputTokens=saved['InputTokens'], outputTokens=saved['OutputTokens'])
        print('LIVE REVIEW PASSED: exactly one real OpenAI request, valid stored review/complexity, repeat and culture switch reuse it.')
    finally:
        ledger['realApiCalls'] = api_starts(submission_id)
        ledger_path.write_text(json.dumps(ledger, indent=2, ensure_ascii=False), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--live-review', action='store_true', help='Requires explicit authorization for ONE paid call; never run automatically.')
    main(parser.parse_args().live_review)
