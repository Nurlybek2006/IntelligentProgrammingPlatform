"""Phase 4 HTTP/SQL checks against the development app. Never calls live OpenAI.

Uses UUID-scoped fixture history plus one real Docker submission. The existing
admin password is read only in memory. All fixture rows are removed in finally.
Run offline C# SDK/storage checks separately: dotnet run --project tests/Phase4Checks
"""
import html
import json
import os
import re
import secrets
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

from verify_phase2 import ROOT, Client, check, is_redirect, sql


def cell_rows(body):
    rows = []
    for row in re.findall(r'<tr\b[^>]*>(.*?)</tr>', body, re.S):
        cells = [html.unescape(re.sub(r'<[^>]+>', '', cell)).strip()
                 for cell in re.findall(r'<td\b[^>]*>(.*?)</td>', row, re.S)]
        if cells:
            rows.append(cells)
    return rows


def main():
    run_id = uuid.uuid4().hex
    emails = [f'phase4-{run_id}-{index}@example.test' for index in range(3)]
    names = [f'Phase4 {run_id} student {index}' for index in range(3)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    prefix = 'phase4-' + run_id
    project = ET.parse(ROOT / 'IntelligentProgrammingPlatform.csproj')
    secret_path = Path(os.environ['APPDATA']) / 'Microsoft' / 'UserSecrets' / project.findtext('.//UserSecretsId') / 'secrets.json'
    configuration = json.loads(secret_path.read_text(encoding='utf-8-sig'))
    anon, admin = Client(), Client()
    students = [Client(), Client(), Client()]
    try:
        check(is_redirect(anon.request('/Progress')), 'Anonymous progress requires login')
        check(anon.request('/Leaderboard')[0] == 200, 'Leaderboard is public')
        for client, name, email in zip(students, names, emails):
            check(is_redirect(client.form('/Account/Register', {'DisplayName': name, 'Email': email,
                'Password': password, 'ConfirmPassword': password})), 'Temporary student can register')
        student, other, third = students
        ids = [sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}';") for email in emails]
        empty = student.request('/Progress')
        check(empty[0] == 200 and 'id="success-rate">0.0%' in empty[2] and 'Not yet available' in empty[2], 'Empty progress page handles zero attempts')
        runtime_id = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp';"))
        topic_id = int(sql(f"INSERT INTO Topics(Name) VALUES('{prefix}'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        task_ids = []
        for difficulty in range(3):
            task_ids.append(int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Phase4 task {difficulty}','{prefix}-{difficulty}','Verification only',{difficulty},{topic_id},1000,64,1,SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);")))

        def fixture(user_id, task_id, status, minutes):
            finished = 'NULL' if status < 3 else f"DATEADD(minute,{minutes},'2026-01-01T08:00:01')"
            return int(sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,StartedAt,FinishedAt,PassedTests,TotalTests,CompileSucceeded) VALUES('{user_id}',{task_id},{runtime_id},'// fixture code',{status},DATEADD(minute,{minutes},'2026-01-01T08:00:00'),DATEADD(minute,{minutes},'2026-01-01T08:00:00'),{finished},0,0,1); SELECT CAST(SCOPE_IDENTITY() AS bigint);"))

        wrong = fixture(ids[0], task_ids[0], 4, 0)
        fixture(ids[0], task_ids[0], 3, 10)
        fixture(ids[0], task_ids[0], 3, 20)
        compile_id = fixture(ids[0], task_ids[1], 5, 5)
        fixture(ids[0], task_ids[1], 3, 35)
        pending = fixture(ids[0], task_ids[2], 2, 0)
        fixture(ids[1], task_ids[2], 3, 0)
        fixture(ids[2], task_ids[1], 3, 0)
        status, _, progress = student.request('/Progress')
        check(status == 200, 'Progress renders against real fixture submission history')
        for metric, value in [('solved-tasks', '2'), ('total-submissions', '5'), ('accepted-submissions', '3'),
                              ('compilation-errors', '1'), ('success-rate', '60.0%'),
                              ('average-solve-time', '20.0 minutes'), ('rating-score', '300')]:
            check(f'id="{metric}">{value}</strong>' in progress, 'Progress metric matches fixture: ' + metric)
        check(any(row == [prefix, '2', '3'] for row in cell_rows(progress)), 'Topic progress shows 2 of 3 published fixture tasks')
        other_progress = other.request('/Progress?userId=' + ids[0])[2]
        check('id="solved-tasks">1</strong>' in other_progress, 'Progress ignores a forged userId query parameter')
        check(student.request('/Submissions/Analyze/' + str(wrong))[0] in (404, 405), 'AI analysis is POST only')
        check(student.request('/Submissions/Analyze/' + str(wrong), {})[0] == 400, 'AI analysis requires antiforgery')
        check(other.form('/Submissions/Analyze/' + str(wrong), {}, token_path='/Account/Profile')[0] == 404,
              'Another student cannot request AI analysis for the owner')
        response = student.form('/Submissions/Analyze/' + str(pending), {}, token_path='/Account/Profile')
        check(is_redirect(response) and 'Wait until the submission has finished' in student.request(response[1]['Location'])[2], 'Unfinished submission cannot trigger AI')
        details = student.request('/Submissions/Details/' + str(compile_id))[2]
        if 'AI feedback is not configured.' in details:
            response = student.form('/Submissions/Analyze/' + str(compile_id), {}, token_path='/Account/Profile')
            check(is_redirect(response) and 'AI feedback is not configured.' in student.request(response[1]['Location'])[2], 'Missing key gives a safe message and leaves the app working')
            check(sql(f'SELECT COUNT(*) FROM AiFeedbacks WHERE SubmissionId={compile_id};') == '0', 'Missing key does not create fake feedback')
        else:
            print('SKIP: Missing-key HTTP check; app has AI configured. No live request made.', flush=True)

        # A deliberately synthetic feedback row verifies encoded rendering and cache-only POST.
        sql(f"INSERT INTO AiFeedbacks(SubmissionId,UserId,Model,Summary,Explanation,HintsJson,ErrorCategory,CreatedAt) VALUES({wrong},'{ids[0]}','offline-ui-fixture','AI fixture <script>unsafe()</script>','Review the expression.','['+NCHAR(34)+'Check the visible example.'+NCHAR(34)+']','Logic',SYSUTCDATETIME());")
        details = student.request('/Submissions/Details/' + str(wrong))[2]
        check('&lt;script&gt;unsafe()&lt;/script&gt;' in details and '<script>unsafe()</script>' not in details, 'AI feedback uses Razor encoding')
        response = student.form('/Submissions/Analyze/' + str(wrong), {}, token_path='/Account/Profile')
        check(is_redirect(response) and 'Showing the existing AI feedback' in student.request(response[1]['Location'])[2], 'Existing feedback is returned without another analysis')
        check(sql(f'SELECT COUNT(*) FROM AiFeedbacks WHERE SubmissionId={wrong};') == '1', 'Feedback remains unique per submission')
        check(other.request('/Submissions/Details/' + str(wrong))[0] == 404, 'Feedback details preserve submission ownership')

        board = anon.request('/Leaderboard')[2]
        check(all(email not in board for email in emails) and all(user_id not in board for user_id in ids), 'Public leaderboard exposes neither email nor UserId')
        fixture_rows = [row for row in cell_rows(board) if row[1] in names]
        check([row[1] for row in fixture_rows] == names, 'Equal scores use solved-count tie-breaker and lower scores follow')
        check([row[-1] for row in fixture_rows] == ['300', '300', '200'], 'Missing leaderboard rows are automatically calculated')
        check('table-primary' in student.request('/Leaderboard')[2], 'Current user leaderboard row is highlighted')
        check(is_redirect(student.form('/Admin/Leaderboard/Rebuild', {}, token_path='/Account/Profile')), 'Student cannot rebuild leaderboard')
        check(is_redirect(admin.form('/Account/Login', {'Email': configuration['SeedAdmin:Email'], 'Password': configuration['SeedAdmin:Password']})), 'Configured admin logs in')
        check(admin.request('/Admin/Leaderboard/Rebuild')[0] in (404, 405), 'Admin rebuild is POST only')
        check(admin.request('/Admin/Leaderboard/Rebuild', {})[0] == 400, 'Admin rebuild requires antiforgery')
        sql(f"UPDATE Leaderboards SET Score=99999 WHERE UserId='{ids[0]}';")
        check(is_redirect(admin.form('/Admin/Leaderboard/Rebuild', {}, token_path='/Admin')), 'Admin rebuild completes')
        check(sql(f"SELECT Score FROM Leaderboards WHERE UserId='{ids[0]}';") == '300', 'Rebuild repairs stale score from source history')
        check(sql(f"SELECT COUNT(*) FROM Submissions WHERE UserId='{ids[0]}';") == '6', 'Rebuild does not delete submission history')

        # A real completion verifies the business-layer update hook (no AI request).
        demo_id = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers';"))
        response = student.form('/Submissions/Submit', {'ProgrammingTaskId': demo_id, 'RuntimeId': runtime_id,
            'SourceCode': '#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b;}'}, token_path='/Tasks/sum-of-two-numbers')
        check(is_redirect(response), 'Real Docker submission completes with Phase 4 services registered')
        actual_id = int(response[1]['Location'].rsplit('/', 1)[1])
        check(sql(f'SELECT Status FROM Submissions WHERE Id={actual_id};') == '3', 'Real submission is Accepted')
        check(sql(f"SELECT COUNT(*) FROM Leaderboards WHERE UserId='{ids[0]}' AND Score=400 AND SolvedTasks=3 AND SuccessfulSubmissions=4 AND TotalSubmissions=6;") == '1', 'Finished submission automatically recalculates leaderboard correctly')
        check(sql(f'SELECT COUNT(*) FROM AiFeedbacks WHERE SubmissionId={actual_id};') == '0', 'Submission never automatically invokes AI')
        print('PHASE 4 HTTP CHECKS PASSED. No live OpenAI request was made.', flush=True)
    finally:
        addresses = ','.join("'" + email + "'" for email in emails)
        sql(f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug LIKE '{prefix}%'); DELETE FROM ProgrammingTasks WHERE Slug LIKE '{prefix}%'; DELETE FROM Topics WHERE Name='{prefix}'; DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed only this run\'s Phase 4 HTTP fixtures.', flush=True)


if __name__ == '__main__':
    main()
