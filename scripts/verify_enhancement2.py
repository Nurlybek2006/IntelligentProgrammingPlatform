"""Real HTTPS/SQL/Docker regression. Disposable fixtures; no paid AI calls.

Default: normal Development app on 7115. --unavailable: a separate app process
whose DOCKER_HOST points to the nonexistent ipp-unavailable-verification pipe.
No browser rendering is claimed by this script.
"""
import concurrent.futures
import html
import http.client
import json
import os
import re
import secrets
import ssl
import sys
import time
import urllib.parse
import uuid
from html.parser import HTMLParser
from pathlib import Path
from verify_phase2 import Client, Inputs, check, is_redirect, sql
from verify_phase3 import docker, inspect_restrictions, running_containers
from verify_enhancement1 import inspect as inspect_csp

MARKER = '</textarea></script><img src=x onerror=alert(1)>'
GOOD = '#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b;}\n// ' + MARKER
LOOP = 'int main(){for(;;){asm volatile("");}}'
ECHO = '#include <iostream>\nint main(){std::cout<<std::cin.rdbuf();}'


class Page(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.attempts, self.sources = [], {}
        self.current = None
        self.feed(body)
        for key, value in self.sources.items():
            value = value.replace('\r\n', '\n').replace('\r', '\n')
            self.sources[key] = value[1:] if value.startswith('\n') else value

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'li' and 'data-attempt-number' in attrs:
            self.attempts.append((int(attrs['data-attempt-number']), int(attrs['data-submission-id']), attrs['data-status']))
        if tag == 'textarea' and attrs.get('id') in ['previous-source', 'current-source']:
            self.current = attrs['id']
            self.sources[self.current] = ''

    def handle_endtag(self, tag):
        if tag == 'textarea':
            self.current = None

    def handle_data(self, data):
        if self.current:
            self.sources[self.current] += data


def snapshot():
    # Hash complete, deterministically ordered rows inside SQL; never print source or feedback.
    return sql('SELECT ' + ', '.join(
        f"CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM {table} ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),2)"
        for table in ['Submissions', 'ExecutionResults', 'AiFeedbacks', 'Leaderboards']) + ';')


def progress(client):
    body = client.request('/Progress')[2]
    return re.findall(r'id="(solved-tasks|total-submissions|accepted-submissions|compilation-errors|rating-score|success-rate)">([^<]+)', body)


def clean_runner():
    assert docker('ps', '-a', '--filter', 'label=ipp.runner=phase3', '--format', '{{.Names}}') == ''
    root = Path(os.environ['TEMP']) / 'IntelligentProgrammingPlatformRunner'
    assert not root.exists() or not any(root.iterdir())


def main(unavailable=False):
    suffix = uuid.uuid4().hex
    slug = 'enhancement2-' + suffix
    emails = [f'{slug}-{i}@example.test' for i in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    student, other, anon = Client(timeout=120), Client(timeout=120), Client()
    task_path = '/Tasks/' + slug
    journey_path = task_path + '/Journey'
    try:
        for client, email in zip([student, other], emails):
            assert is_redirect(client.form('/Account/Register', {'Email': email, 'DisplayName': 'Journey verification',
                'Password': password, 'ConfirmPassword': password}))
        owner_id = sql(f"SELECT Id FROM AspNetUsers WHERE Email='{emails[0]}';")
        topic_id = int(sql('SELECT TOP (1) Id FROM Topics ORDER BY Id;'))
        sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt,TopicId) VALUES('Journey fixture','{slug}','Read two integers and print the sum.',0,500,32,1,SYSUTCDATETIME(),{topic_id});")
        task_id = int(sql(f"SELECT Id FROM ProgrammingTasks WHERE Slug='{slug}';"))
        sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({task_id},'2 3','5',0,1),({task_id},'82736412 16273849','99010261',1,2);")
        runtime_id = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
        sql(f"INSERT INTO Runtimes(Name,LanguageKey,Version,FileExtension,CompileCommand,RunCommand,DockerImage,IsEnabled) VALUES('{slug}','{slug}','fixture','.x','untrusted-compiler','untrusted-command','untrusted-image',0);")
        foreign_runtime = int(sql(f"SELECT Id FROM Runtimes WHERE Name='{slug}';"))

        def run(code, input_text='', expected='Success', extra=None, expected_http=200):
            response = student.form('/CustomRuns/Run', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id,
                'SourceCode': code, 'CustomInput': input_text, **(extra or {})}, token_path=task_path)
            assert response[0] == expected_http, ('Custom Run HTTP', response[0], expected_http)
            if expected_http != 200:
                return response
            data = json.loads(response[2])
            assert data['status'] == expected, (data['status'], expected)
            assert all(value not in (data.get('error') or '') + (data.get('compilerOutput') or '') for value in
                       ['C:\\Users', 'C:\\ENT', 'IntelligentProgrammingPlatformRunner', 'docker.exe', 'npipe:', 'System.InvalidOperationException'])
            check(True, 'Custom Run ' + expected)
            return data

        before = snapshot()
        initial_progress = progress(student)
        if unavailable:
            for _ in range(2):
                result = run(GOOD, '2 3', 'InternalError')
                assert result['compileSucceeded'] is None and 'temporarily unavailable' in result['error']
            assert snapshot() == before and progress(student) == initial_progress
            clean_runner()
            check(True, 'Unavailable/cached Custom Run makes no database changes and leaves no resources')
            return

        assert anon.request('/CustomRuns/Run', {})[0] in [302, 401]
        assert anon.request(journey_path)[0] in [302, 401]
        assert anon.request('/Submissions/Compare?olderId=1&newerId=2')[0] in [302, 401]
        assert student.request('/CustomRuns/Run', {})[0] == 400
        assert student.request('/CustomRuns/Run')[0] in [404, 405]
        assert 'run-code' not in anon.request(task_path)[2]
        assert 'No official attempts yet' in student.request(journey_path)[2]
        check(True, 'Run/Journey/Compare require authentication; Run is POST and CSRF protected')

        for code, input_text in [('', ''), ('x' * 65537, ''), ('//' + '\u044f' * 32768, ''), (GOOD, 'x' * 32769), (GOOD, '\u044f' * 16385)]:
            run(code, input_text, expected_http=400)
        run(GOOD, '2 3', extra={'ProgrammingTaskId': 2147483647}, expected_http=404)
        run(GOOD, '2 3', extra={'RuntimeId': 2147483647}, expected_http=400)
        run(GOOD, '2 3', extra={'RuntimeId': foreign_runtime}, expected_http=400)
        sql(f'UPDATE Runtimes SET IsEnabled=1 WHERE Id={foreign_runtime};')
        run(GOOD, '2 3', extra={'RuntimeId': foreign_runtime}, expected_http=400)
        sql(f'UPDATE ProgrammingTasks SET IsPublished=0 WHERE Id={task_id};')
        response = student.form('/CustomRuns/Run', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id,
            'SourceCode': GOOD, 'CustomInput': '2 3'}, token_path='/Account/Profile')
        assert response[0] == 404
        sql(f'UPDATE ProgrammingTasks SET IsPublished=1 WHERE Id={task_id};')
        check(True, 'Byte limits, disabled/unsupported runtimes and missing/unpublished tasks reject execution')

        result = run(GOOD, '2 3', extra={'UserId': 'forged-owner', 'ExpectedOutput': '999', 'Status': 'Accepted', 'ExecutionTimeMs': -1})
        assert result['output'] == '5' and result['exitCode'] == 0 and result['compileSucceeded'] is True and result['executionTimeMs'] >= 0
        assert run(ECHO, MARKER)['output'] == MARKER
        assert run(ECHO, 'x' * 32768)['output'] == 'x' * 32768
        assert run('int main(){}' + ' ' * (65536 - len('int main(){}')))['output'] == ''
        result = run('int main( {', expected='CompilationError')
        assert result['compileSucceeded'] is False and 'main.cpp' in result['compilerOutput']
        run(LOOP, expected='TimeLimitExceeded')
        result = run('#include <iostream>\nint main(){std::cerr<<"custom stderr";return 7;}', expected='RuntimeError')
        assert result['exitCode'] == 7 and 'custom stderr' in result['error']
        assert run('#include <iostream>\nint main(){std::cerr<<"warning only";}')['error'] == 'warning only'
        result = run('#include <iostream>\nint main(){for(;;)std::cout<<"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\\n";}', expected='RuntimeError')
        assert len(result['output'].encode()) <= 65536 and 'Output limit exceeded' in result['error']
        run('#include <cstdlib>\n#include <cstring>\n#include <vector>\nint main(){std::vector<void*> p;for(;;){auto q=malloc(1024*1024);if(!q)return 1;memset(q,1,1024*1024);p.push_back(q);}}', expected='MemoryLimitExceeded')
        assert snapshot() == before and progress(student) == initial_progress
        assert 'No official attempts yet' in student.request(journey_path)[2]
        clean_runner()
        check(True, 'All custom results leave Submission/ExecutionResult/AI/leaderboard counts, scores and Progress unchanged')

        def submit(client, code, expected, selected_task=task_id):
            response = client.form('/Submissions/Submit', {'ProgrammingTaskId': selected_task, 'RuntimeId': runtime_id,
                'SourceCode': code}, token_path=task_path)
            assert is_redirect(response), response[0]
            identifier = int(response[1]['Location'].rsplit('/', 1)[1])
            assert sql(f'SELECT Status FROM Submissions WHERE Id={identifier};') == str(expected)
            return identifier

        first = submit(student, 'int main( {\n// ' + MARKER, 5)
        second = submit(student, '#include <iostream>\nint main(){std::cout<<0;}\n// ' + MARKER, 4)
        third = submit(student, GOOD, 3)
        foreign = submit(other, GOOD, 3)
        sql(f'DECLARE @sameTime datetime2 = (SELECT CreatedAt FROM Submissions WHERE Id={first}); UPDATE Submissions SET CreatedAt=@sameTime WHERE Id IN ({first},{second},{third});')
        body = inspect_csp(student.request(journey_path))
        assert Page(body).attempts == [(1, first, 'CompilationError'), (2, second, 'WrongAnswer'), (3, third, 'Accepted')]
        assert 'AI Hint Used</span>' not in body and str(foreign) not in [str(item[1]) for item in Page(body).attempts]
        sql(f"INSERT INTO AiFeedbacks(SubmissionId,UserId,Model,Summary,Explanation,HintsJson,ErrorCategory,CreatedAt) VALUES({second},'{owner_id}','offline-fixture-no-call','Check your output.','Read both values.',CONCAT('[',NCHAR(34),'Consider the sum.',NCHAR(34),']'),'Logic',SYSUTCDATETIME());")
        body = student.request(journey_path + '?userId=forged-owner')[2]
        assert body.count('AI Hint Used</span>') == 1 and 'offline-fixture-no-call' not in body
        assert Page(other.request(journey_path + '?userId=' + owner_id)[2]).attempts == [(1, foreign, 'Accepted')]
        check(True, 'Real official attempts form the ordered journey; AI marker follows only a persisted feedback row; owner comes from Identity')

        hidden_marker = 'HIDDEN_DIAGNOSTIC_' + suffix
        sql(f"UPDATE e SET ActualOutput='{hidden_marker}',ErrorMessage='{hidden_marker}' FROM ExecutionResults e JOIN TestCases t ON t.Id=e.TestCaseId WHERE t.ProgrammingTaskId={task_id} AND t.IsHidden=1;")
        compare = f'/Submissions/Compare?olderId={second}&newerId={third}'
        body = inspect_csp(student.request(compare), editor=True)
        parsed = Page(body)
        assert parsed.sources['current-source'] == GOOD and MARKER in parsed.sources['previous-source']
        assert MARKER not in body and 'code-diff' in body and 'Back to journey' in body
        assert Page(student.request(f'/Submissions/Compare?olderId={third}&newerId={second}')[2]).sources == parsed.sources
        assert other.request(compare)[0] == 404
        for a, b in [(second, foreign), (0, third), (second, 9223372036854775807), (second, second)]:
            assert student.request(f'/Submissions/Compare?olderId={a}&newerId={b}')[0] == 404
        different_task = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers';"))
        cross_task = submit(student, GOOD, 3, different_task)
        assert student.request(f'/Submissions/Compare?olderId={third}&newerId={cross_task}')[0] == 404
        for content in [body, student.request(journey_path)[2], student.request(task_path)[2], student.request(f'/Submissions/Details/{third}')[2]]:
            assert all(secret not in content for secret in ['82736412 16273849', '99010261', hidden_marker])
        check(True, 'Diff enforces both owners/same task, encodes hostile source, keeps CSP scoped and exposes no hidden contents')

        for index in range(22):
            sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,FinishedAt,PassedTests,TotalTests,CompileSucceeded) VALUES('{owner_id}',{task_id},{runtime_id},'BATCH_SOURCE_MUST_NOT_APPEAR',5,DATEADD(SECOND,{22-index},DATEADD(DAY,1,SYSUTCDATETIME())),SYSUTCDATETIME(),0,0,0);")
        expected = [int(value) for value in sql(f"SELECT Id FROM Submissions WHERE UserId='{owner_id}' AND ProgrammingTaskId={task_id} ORDER BY CreatedAt,Id;").splitlines()]
        for page, offset in [(1, 0), (2, 20)]:
            body = student.request(journey_path + f'?page={page}')[2]
            assert [(n, identifier) for n, identifier, _ in Page(body).attempts] == list(enumerate(expected[offset:offset+20], offset+1))
            assert 'BATCH_SOURCE_MUST_NOT_APPEAR' not in body and hidden_marker not in body
        body = student.request(journey_path + '?page=2')[2]
        assert f'olderId={expected[19]}&newerId={expected[20]}' in html.unescape(body)
        body = student.request(task_path)[2]
        assert [identifier for _, identifier, _ in Page(body).attempts] == expected[-5:]
        assert f'olderId={expected[-6]}&newerId={expected[-5]}' in html.unescape(body) and 'View full journey' in body
        check(True, 'Journey pagination and compact latest five keep chronological numbers and cross-page predecessor links')
        populated_snapshot, populated_progress = snapshot(), progress(student)
        run(GOOD, '2 3')
        assert snapshot() == populated_snapshot and progress(student) == populated_progress
        check(True, 'Custom Run also leaves existing attempts, AI feedback and nonzero statistics unchanged')

        sql(f'UPDATE ProgrammingTasks SET TimeLimitMs=3000 WHERE Id={task_id};')
        observed, max_containers = set(), 0
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            futures = [pool.submit(run, LOOP, '', 'TimeLimitExceeded') for _ in range(2)]
            futures.append(pool.submit(submit, student, LOOP, 7))
            while not all(future.done() for future in futures):
                containers = running_containers()
                max_containers = max(max_containers, len(containers))
                assert len(containers) <= 2
                for container in containers:
                    observed.add(inspect_restrictions(container))
                    assert container['Config']['Image'] == docker('image', 'inspect', 'gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15', '--format', '{{.Id}}')
                assert anon.request('/Tasks')[0] == 200
                time.sleep(0.1)
            for future in futures:
                future.result()
        assert max_containers == 2 and observed == {'compile', 'run'}
        check(True, 'Mixed Run/Submit shares a two-container gate; live containers retain every restriction and the pinned image')

        cancellation_snapshot = snapshot()
        token = Inputs(student.request(task_path)[2]).values['__RequestVerificationToken']
        payload = urllib.parse.urlencode({'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id, 'SourceCode': LOOP,
            'CustomInput': '', '__RequestVerificationToken': token})
        connection = http.client.HTTPSConnection('localhost', 7115, context=ssl._create_unverified_context())
        connection.request('POST', '/CustomRuns/Run', body=payload, headers={'Content-Type': 'application/x-www-form-urlencoded',
            'Cookie': '; '.join(cookie.name + '=' + cookie.value for cookie in student.cookies)})
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if any(c['Name'].startswith('/ipp-run-') and c['State']['Running'] for c in running_containers()):
                break
            time.sleep(0.1)
        else:
            raise AssertionError('Cancellation fixture did not reach running code')
        connection.close()
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline and running_containers():
            time.sleep(0.2)
        # Container deletion precedes workspace cleanup by a short finally block.
        time.sleep(0.3)
        clean_runner()
        assert snapshot() == cancellation_snapshot
        check(True, 'Cancelled custom request cleans containers/workspace without writing an attempt')
        print('ENHANCEMENT 2 CHECKS PASSED. No browser or paid AI request was used.', flush=True)
    finally:
        addresses = ','.join("'" + email + "'" for email in emails)
        sql(f"DELETE a FROM AiFeedbacks a JOIN AspNetUsers u ON u.Id=a.UserId WHERE u.Email IN ({addresses}); DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug='{slug}'); DELETE FROM ProgrammingTasks WHERE Slug='{slug}'; DELETE FROM Runtimes WHERE Name='{slug}'; DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed only this run\'s Enhancement 2 fixture data.', flush=True)


if __name__ == '__main__':
    if sys.argv[1:] not in [[], ['--unavailable']]:
        raise SystemExit('Usage: python scripts/verify_enhancement2.py [--unavailable]')
    main(unavailable=bool(sys.argv[1:]))
