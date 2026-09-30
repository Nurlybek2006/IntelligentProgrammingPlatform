"""Real HTTPS/Identity/SQL/Docker checks. Run against the development app on 7115.

All submitted C++ runs through the application inside Docker. Test accounts and
their submissions are removed in finally; existing demo tasks are never edited.
Requires the same local SQL/Docker access as the app. No third-party Python deps.
"""
import concurrent.futures
import http.client
import html
import json
import os
import re
import secrets
import subprocess
import ssl
import sys
import time
import urllib.parse
import uuid
from pathlib import Path

from verify_phase2 import Client, Inputs, check, is_redirect, sql

GOOD = '#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b<<"\\r\\n ";}\n// <script>phase3-marker</script>'
LOOP = '#include <iostream>\nint main(){for(;;){asm volatile("");}}'
STATES = {3: 'Accepted', 4: 'WrongAnswer', 5: 'CompilationError', 6: 'RuntimeError',
          7: 'TimeLimitExceeded', 8: 'MemoryLimitExceeded', 9: 'InternalError'}


def docker(*args):
    result = subprocess.run(['docker', *args], capture_output=True, text=True, timeout=15)
    if result.returncode:
        raise RuntimeError(result.stderr.strip())
    return result.stdout.strip()


def running_containers():
    names = docker('ps', '-a', '--filter', 'label=ipp.runner=phase3', '--format', '{{.Names}}').splitlines()
    if not names:
        return []
    # A short-lived container may disappear between ps and inspect.
    result = subprocess.run(['docker', 'inspect', *names], capture_output=True, text=True, timeout=15)
    return json.loads(result.stdout or '[]')


def inspect_restrictions(container):
    host = container['HostConfig']
    config = container['Config']
    assert host['NetworkMode'] == 'none'
    assert host['ReadonlyRootfs'] and not host['Privileged']
    assert host['CapDrop'] == ['ALL'] and host['PidsLimit'] == 64
    assert 'no-new-privileges' in host['SecurityOpt']
    assert host['Memory'] == host['MemorySwap'] and host['Memory'] > 0
    assert config['User'] == '65534:65534'
    assert config['Entrypoint'] == ['/usr/bin/timeout']
    assert 'noexec' in host['Tmpfs']['/tmp']
    assert host['LogConfig']['Type'] == 'none'
    compiling = container['Name'].startswith('/ipp-compile-')
    assert host['NanoCpus'] == (1000000000 if compiling else 500000000)
    mounts = container['Mounts']
    expected = {'/source': False, '/build': True} if compiling else {'/app': False}
    assert {m['Destination']: m['RW'] for m in mounts} == expected
    for mount in mounts:
        source = mount['Source'].replace('\\', '/')
        assert re.search(r'IntelligentProgrammingPlatformRunner/[a-f0-9]{32}/(source|build)$', source)
    return 'compile' if compiling else 'run'


def main():
    run_id = uuid.uuid4().hex
    emails = [f'phase3-{run_id}-{i}@example.test' for i in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    clients = [Client(), Client()]
    anon = Client()
    owned = []
    observed = set()
    runtime_id = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
    task_id = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers' AND IsPublished=1;"))
    task_path = '/Tasks/sum-of-two-numbers'
    fixture_slug = 'phase3-' + run_id

    def submit(client, source, expected, selected_task=task_id):
        response = client.form('/Submissions/Submit', {
            'ProgrammingTaskId': selected_task, 'RuntimeId': runtime_id, 'SourceCode': source,
            'UserId': 'forged-owner', 'Status': 3, 'PassedTests': 999, 'CompilerOutput': 'forged-output'
        }, token_path=task_path)
        assert is_redirect(response), f'Submit failed HTTP {response[0]}: {response[2][-1500:]}'
        location = response[1]['Location']
        submission_id = int(location.rsplit('/', 1)[1])
        owned.append(submission_id)
        status, _, body = client.request(location)
        actual = sql(f'SELECT Status FROM Submissions WHERE Id={submission_id};')
        check(actual == str(expected), f'{STATES[expected]} persisted (submission {submission_id}, actual {actual})')
        check(status == 200 and f'>{STATES[expected]}</dd>' in body, 'Owner sees stored result')
        check(sql(f'SELECT COUNT(*) FROM Submissions WHERE Id={submission_id} AND StartedAt IS NOT NULL AND FinishedAt>=StartedAt AND PassedTests<=TotalTests AND UserId<>\'forged-owner\' AND ISNULL(CompilerOutput,\'\')<>\'forged-output\';') == '1', 'Server owns timestamps, counters, owner and compiler result')
        check(sql(f'SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={submission_id} AND MemoryUsedKb IS NOT NULL;') == '0', 'Unmeasured memory remains NULL')
        if selected_task == task_id:
            check(all(value not in body for value in ['123456', '654321', '777777']), 'Hidden input, expected and actual output absent from full HTML')
        return submission_id, body

    try:
        check(docker('info', '--format', '{{.OSType}}') == 'linux', 'Docker Linux engine available')
        for client, email in zip(clients, emails):
            check(is_redirect(client.form('/Account/Register', {'DisplayName': 'Phase 3 verification', 'Email': email,
                  'Password': password, 'ConfirmPassword': password})), 'Isolated student registered and signed in')
        student, other = clients
        check('submission-form' not in anon.request(task_path)[2], 'Anonymous task page has no submission form')
        check(is_redirect(anon.request('/Submissions/My')) and is_redirect(anon.request('/Submissions/Submit', {})), 'Anonymous history and submission require login')
        check(student.request('/Submissions/Submit', {})[0] == 400, 'Submit requires antiforgery token')
        page = student.request(task_path)[2]
        check('id="SourceCode"' in page and 'code-editor.js' in page and 'code-editor' in page, 'Authenticated form includes local editor and source textarea')
        for asset in ['code-editor.js', 'editor.worker.js', 'code-editor.css']:
            check(anon.request('/js/editor/' + asset)[0] == 200, 'Local editor asset served: ' + asset)
        for source, selected_runtime in [('', runtime_id), ('// ' + 'x' * 65536, runtime_id), ('// ' + '\u049b' * 33000, runtime_id), ('// preserved-source', 2147483647)]:
            response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': selected_runtime, 'SourceCode': source}, token_path=task_path)
            check(response[0] == 200 and ('field-validation-error' in response[2] or 'validation-summary-errors' in response[2]), 'Invalid source/runtime rejected with validation')
            if 'preserved-source' in source:
                check('preserved-source' in response[2], 'Source preserved after failed validation')
        check(sql(f"SELECT COUNT(*) FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{emails[0]}';") == '0', 'Invalid forms create no submissions')

        accepted, body = submit(student, GOOD, 3)
        check('&lt;script&gt;phase3-marker&lt;/script&gt;' in body and '<script>phase3-marker</script>' not in body, 'Stored source is HTML encoded')
        check(other.request(f'/Submissions/Details/{accepted}')[0] == 404, 'A second student cannot read another submission')
        check(f'/Submissions/Details/{accepted}' not in other.request('/Submissions/My')[2], 'Other student history excludes owner submission')
        check(f'/Submissions/Details/{accepted}' in student.request('/Submissions/My')[2], 'Owner history contains submission link')
        submit(student, '#include <iostream>\nint main(){std::cout<<0;}', 4)
        bad, body = submit(student, 'int main( { syntax error;', 5)
        check('main.cpp' in body and 'IntelligentProgrammingPlatformRunner' not in body and 'C:\\Users' not in body, 'Compiler diagnostics do not expose temporary host paths')
        check(sql(f'SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={bad} AND Status<>6;') == '0', 'Compilation error skips all tests')

        with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
            future = pool.submit(submit, student, LOOP, 7)
            while not future.done():
                for container in running_containers():
                    observed.add(inspect_restrictions(container))
                started = time.monotonic()
                assert anon.request('/Tasks')[0] == 200
                assert time.monotonic() - started < 2, 'App stalled during infinite loop'
                time.sleep(0.15)
            future.result()
        check(observed == {'compile', 'run'}, f'Live compiler and run containers enforce all security restrictions (observed {sorted(observed)})')
        check(True, 'Application remains responsive during infinite loop; timeout terminates it')
        submit(student, 'int main(){return 42;}', 6)
        flooded, _ = submit(student, '#include <cstdio>\nint main(){for(;;)puts("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");}', 6)
        check(sql(f"SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={flooded} AND ErrorMessage='Output limit exceeded.' AND DATALENGTH(ActualOutput)<=131072;") == sql(f'SELECT TotalTests FROM Submissions WHERE Id={flooded};'), 'Endless output is terminated and stored output bounded to 64 KiB per stream')
        submit(student, '#include <cstdlib>\nint main(){for(;;){volatile char* p=(char*)malloc(1024*1024);if(!p)return 2;for(int i=0;i<1024*1024;i+=4096)p[i]=1;}}', 8)
        submit(student, GOOD.replace('std::cout<<a+b', 'std::cout<<" "<<a+b'), 4)

        # Independent fixture checks limits without altering published demo tasks.
        fixture_id = int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) SELECT 'Phase3 fixture','{fixture_slug}','Verification only',0,TopicId,1000,32,1,SYSUTCDATETIME() FROM ProgrammingTasks WHERE Id={task_id}; SELECT CAST(SCOPE_IDENTITY() AS int);"))
        sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({fixture_id},'','',0,1);")
        # Three concurrent jobs must share exactly two execution slots.
        concurrent_clients = []
        for _ in range(3):
            client = Client()
            assert is_redirect(client.form('/Account/Login', {'Email': emails[0], 'Password': password}))
            concurrent_clients.append(client)
        max_containers = 0
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            futures = [pool.submit(submit, client, LOOP, 7, fixture_id) for client in concurrent_clients]
            while not all(future.done() for future in futures):
                count = len(running_containers())
                max_containers = max(max_containers, count)
                assert count <= 2, 'Concurrency limit exceeded'
                time.sleep(0.1)
            for future in futures:
                future.result()
        check(max_containers == 2, 'Three concurrent submissions use at most two active containers')

        # Abort an actual in-flight HTTP request; the app must still persist and clean up.
        token = Inputs(student.request(task_path)[2]).values['__RequestVerificationToken']
        payload = urllib.parse.urlencode({'ProgrammingTaskId': fixture_id, 'RuntimeId': runtime_id,
                  'SourceCode': LOOP, '__RequestVerificationToken': token})
        connection = http.client.HTTPSConnection('localhost', 7115, context=ssl._create_unverified_context())
        connection.request('POST', '/Submissions/Submit', body=payload, headers={
            'Content-Type': 'application/x-www-form-urlencoded',
            'Cookie': '; '.join(cookie.name + '=' + cookie.value for cookie in student.cookies)})
        deadline = time.monotonic() + 15
        saw_running = False
        while time.monotonic() < deadline:
            if any(c['Name'].startswith('/ipp-run-') and c['State']['Running'] for c in running_containers()):
                saw_running = True
                break
            time.sleep(0.1)
        connection.close()
        assert saw_running, 'Cancellation test did not reach a running container'
        cancelled = int(sql(f"SELECT MAX(s.Id) FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{emails[0]}';"))
        owned.append(cancelled)
        deadline = time.monotonic() + 15
        while time.monotonic() < deadline:
            if sql(f'SELECT COUNT(*) FROM Submissions WHERE Id={cancelled} AND FinishedAt IS NOT NULL AND Status=9;') == '1':
                break
            time.sleep(0.2)
        check(sql(f'SELECT Status FROM Submissions WHERE Id={cancelled};') == '9', 'Cancelled request persists InternalError and completes cleanup')

        # Runtime command/image strings are metadata, even if changed in the DB.
        metadata = json.loads(sql(f'SELECT CompileCommand,RunCommand,DockerImage FROM Runtimes WHERE Id={runtime_id} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;'))
        try:
            sql(f"UPDATE Runtimes SET CompileCommand='untrusted-command',RunCommand='untrusted-command',DockerImage='untrusted-image-do-not-run' WHERE Id={runtime_id};")
            submit(student, GOOD, 3)
            check(True, 'Changed runtime command/image metadata cannot change the trusted runner')
        finally:
            assignments = ','.join(key + '=' + ("N'" + value.replace("'", "''") + "'" if value is not None else 'NULL') for key, value in metadata.items())
            sql(f'UPDATE Runtimes SET {assignments} WHERE Id={runtime_id};')

        sql(f'UPDATE ProgrammingTasks SET IsPublished=0 WHERE Id={fixture_id};')
        check(student.form('/Submissions/Submit', {'ProgrammingTaskId': fixture_id, 'RuntimeId': runtime_id, 'SourceCode': GOOD}, token_path=task_path)[0] == 404, 'Unpublished task cannot be submitted by forged ID')
        sql(f"INSERT INTO Runtimes(Name,LanguageKey,Version,FileExtension,CompileCommand,RunCommand,DockerImage,IsEnabled) VALUES('Phase3 fixture','{fixture_slug}','test','.x','untrusted','untrusted','untrusted',1);")
        foreign_runtime = int(sql(f"SELECT Id FROM Runtimes WHERE LanguageKey='{fixture_slug}';"))
        response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': foreign_runtime, 'SourceCode': GOOD}, token_path=task_path)
        check(response[0] == 200 and 'Select an enabled C++ runtime.' in html.unescape(response[2]), 'Enabled unsupported runtime is rejected')
        sql(f'UPDATE Runtimes SET IsEnabled=0 WHERE Id={foreign_runtime};')
        response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': foreign_runtime, 'SourceCode': GOOD}, token_path=task_path)
        check(response[0] == 200 and 'Select an enabled C++ runtime.' in html.unescape(response[2]), 'Disabled runtime is rejected')
        check(docker('ps', '-a', '--filter', 'label=ipp.runner=phase3', '--format', '{{.Names}}') == '', 'No runner containers remain after success, failures and timeouts')
        temporary_root = Path(os.environ['TEMP']) / 'IntelligentProgrammingPlatformRunner'
        check(not temporary_root.exists() or not any(temporary_root.iterdir()), 'No per-submission temporary directories remain')
        history = student.request('/Submissions/My')[2]
        links = [int(value) for value in re.findall(r'href="/Submissions/Details/(\d+)"', history)]
        check(links == sorted(links, reverse=True), 'History lists submissions newest first')
        print(f'PHASE 3 VERIFICATION PASSED: {len(owned)} real submissions; fixtures will be removed.', flush=True)
    finally:
        # Only this run's unique accounts and fixtures are removed, in FK order.
        addresses = ','.join("'" + email + "'" for email in emails)
        sql(f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug='{fixture_slug}'); DELETE FROM ProgrammingTasks WHERE Slug='{fixture_slug}'; DELETE FROM Runtimes WHERE LanguageKey='{fixture_slug}'; DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed this run\'s verification accounts, submissions and fixture rows.', flush=True)


def verify_unavailable():
    """Use only with an app launched with DOCKER_HOST pointing to a nonexistent pipe."""
    email = 'phase3-unavailable-' + uuid.uuid4().hex + '@example.test'
    password = 'Aa1!' + secrets.token_urlsafe(24)
    student = Client()
    try:
        assert is_redirect(student.form('/Account/Register', {'DisplayName': 'Unavailable verification',
            'Email': email, 'Password': password, 'ConfirmPassword': password}))
        runtime_id = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
        task_id = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers';"))
        for attempt in range(2):
            response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id,
                'SourceCode': GOOD}, token_path='/Tasks/sum-of-two-numbers')
            assert is_redirect(response)
            submission_id = int(response[1]['Location'].rsplit('/', 1)[1])
            body = student.request(response[1]['Location'])[2]
            check(sql(f'SELECT COUNT(*) FROM Submissions WHERE Id={submission_id} AND Status=9 AND FinishedAt IS NOT NULL AND CompileSucceeded IS NULL;') == '1', f'Unavailable Docker persists InternalError without compiling (attempt {attempt + 1})')
            check('Execution service is temporarily unavailable.' in body and all(value not in body for value in
                  ['ipp-unavailable-verification', 'npipe:', 'System.InvalidOperationException', 'C:\\Users']), 'Infrastructure details stay in server logs')
        check(docker('ps', '-a', '--filter', 'label=ipp.runner=phase3', '--format', '{{.Names}}') == '', 'Unavailable runner starts no containers or host compiler')
        check(student.request('/Tasks')[0] == 200, 'Application remains available without Docker')
        print('DOCKER-UNAVAILABLE VERIFICATION PASSED (including cached failure).', flush=True)
    finally:
        sql(f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{email}'; DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{email}'; DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email='{email}'; DELETE FROM AspNetUsers WHERE Email='{email}';")
        print('Removed unavailable-check fixture data.', flush=True)


if __name__ == '__main__':
    if sys.argv[1:] == ['--unavailable']:
        verify_unavailable()
    elif len(sys.argv) == 1:
        main()
    else:
        raise SystemExit('Usage: python scripts/verify_phase3.py [--unavailable]')
