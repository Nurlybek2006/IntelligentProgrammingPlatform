"""Real multi-language HTTPS/SQL/Docker checks; submitted Python ONLY runs in Docker.

Default uses the normal Development app. --unavailable requires an app whose
DOCKER_HOST points to the nonexistent test pipe. UUID fixtures are cleaned in finally.
No browser rendering or paid AI calls are claimed. Run DB suites sequentially.
"""
import concurrent.futures
import html
import http.client
import json
import re
import secrets
import ssl
import sys
import time
import urllib.parse
import uuid
from verify_phase2 import Client, Inputs, check, is_redirect, sql
from verify_phase3 import docker, running_containers, inspect_restrictions
from verify_enhancement1 import Tags
from verify_enhancement2 import snapshot, clean_runner, Page, progress
from verify_enhancement3_migration import literal
from verify_enhancement4 import page, switch
from verify_resources import CULTURES

IMAGE = 'python@sha256:5024f48ba9441d4b13a95d3945abc6365538e3a31109833367a1923523c6efed'
MARKER = '</textarea></script><img src=x onerror=python_marker()>'
GOOD = 'import sys, math\na, b = map(int, sys.stdin.read().split())\nprint(a + b, end="\\r\\n ")\n# ' + MARKER
CPP_WAIT = '#include <iostream>\n#include <thread>\n#include <chrono>\nint main(){std::this_thread::sleep_for(std::chrono::seconds(2));std::cout<<42;}'
TASK_SOLUTIONS = {
    'count-even-numbers': (
        '#include <iostream>\nint main(){int n,x,c=0;std::cin>>n;while(n--){std::cin>>x;if(x%2==0)++c;}std::cout<<c;}',
        'import sys\nv=list(map(int,sys.stdin.read().split()))\nprint(sum(x%2==0 for x in v[1:1+v[0]]))'),
    'palindrome-check': (
        '#include <iostream>\n#include <string>\n#include <algorithm>\nint main(){std::string s;std::cin>>s;auto r=s;std::reverse(r.begin(),r.end());std::cout<<(s==r?"YES":"NO");}',
        'word=input().strip()\nprint("YES" if word==word[::-1] else "NO")'),
    'binary-search': (
        '#include <iostream>\n#include <vector>\nint main(){int n,t;std::cin>>n;std::vector<int>a(n);for(int&x:a)std::cin>>x;std::cin>>t;int l=0,r=n-1,ans=-1;while(l<=r){int m=l+(r-l)/2;if(a[m]==t){ans=m;break;}if(a[m]<t)l=m+1;else r=m-1;}std::cout<<ans;}',
        'import sys\nv=list(map(int,sys.stdin.read().split()))\nn=v[0];a=v[1:n+1];t=v[n+1]\nl=0;r=n-1;ans=-1\nwhile l<=r:\n    m=(l+r)//2\n    if a[m]==t:\n        ans=m\n        break\n    if a[m]<t:\n        l=m+1\n    else:\n        r=m-1\nprint(ans)')
}


def inspect_python(container, image_id):
    if container['Image'] != image_id:
        inspect_restrictions(container)
        return 'cpp'
    host, config = container['HostConfig'], container['Config']
    assert host['NetworkMode'] == 'none' and host['ReadonlyRootfs'] and not host['Privileged']
    assert host['CapDrop'] == ['ALL'] and host['PidsLimit'] == 64
    assert 'no-new-privileges' in host['SecurityOpt'] and host['Memory'] == host['MemorySwap'] > 0
    assert host['PidMode'] != 'host' and config['User'] == '65534:65534'
    assert config['Entrypoint'] == ['/usr/bin/timeout'] and host['LogConfig']['Type'] == 'none'
    assert 'noexec' in host['Tmpfs']['/tmp'] and {'-I', '-S', '-B'} <= set(config['Cmd'])
    assert host['NanoCpus'] == (1000000000 if container['Name'].startswith('/ipp-compile-') else 500000000)
    assert {m['Destination']: m['RW'] for m in container['Mounts']} == {'/app': False}
    for mount in container['Mounts']:
        assert re.search(r'IntelligentProgrammingPlatformRunner/[a-f0-9]{32}/source$', mount['Source'].replace('\\', '/'))
    assert {value.split('=', 1)[0] for value in config['Env']} <= {'PATH', 'GPG_KEY', 'PYTHON_VERSION', 'PYTHON_SHA256', 'LANG'}
    return 'python'


def main(unavailable=False):
    prefix = 'enh5b-' + uuid.uuid4().hex
    emails = [prefix + f'-{i}@example.test' for i in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    student, other, guest = Client(timeout=180), Client(timeout=180), Client()
    runtimes = {key: int(sql(f"SELECT Id FROM Runtimes WHERE LanguageKey='{key}' AND IsEnabled=1")) for key in ('cpp', 'python')}
    sum_id = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers'"))
    image_id = docker('image', 'inspect', IMAGE, '--format', '{{.Id}}')
    try:
        for client, email in ((student, emails[0]), (other, emails[1])):
            assert is_redirect(client.form('/Account/Register', {'Email': email, 'DisplayName': 'Python checks',
                'Password': password, 'ConfirmPassword': password}))
        owner = sql(f"SELECT Id FROM AspNetUsers WHERE Email='{emails[0]}'")
        topic = int(sql(f"INSERT INTO Topics(Name,Description) VALUES('{prefix}','Multi-language fixture'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        task_id = int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Python sandbox fixture','{prefix}','Print 42.',0,{topic},8000,64,1,SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({task_id},'','42',0,1)")
        student.request('/Leaderboard')

        def submit(source, expected, language='python', selected=sum_id, slug='sum-of-two-numbers'):
            response = student.form('/Submissions/Submit', {'ProgrammingTaskId': selected, 'RuntimeId': runtimes[language],
                'SourceCode': source, 'LanguageKey': 'untrusted-language', 'UserId': 'forged', 'Status': 3}, token_path='/Tasks/' + slug)
            assert is_redirect(response), ('Submit HTTP', response[0])
            identifier = int(response[1]['Location'].rsplit('/', 1)[1])
            assert sql(f'SELECT Status FROM Submissions WHERE Id={identifier}') == str(expected), ('Unexpected status', identifier, expected)
            assert sql(f"SELECT COUNT(*) FROM Submissions WHERE Id={identifier} AND RuntimeId={runtimes[language]} AND UserId='{owner}' AND FinishedAt>=StartedAt") == '1'
            assert sql(f'SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={identifier} AND MemoryUsedKb IS NOT NULL') == '0'
            response = student.request(response[1]['Location'])
            body = page(response, 'en-US')
            assert 'Python 3' in body if language == 'python' else 'C++ 20' in body
            assert MARKER not in response[2]
            if selected == sum_id:
                assert all(hidden not in body for hidden in ('123456', '654321', '777777'))
            check(True, f'{language}: Submit status {expected}, results, owner/runtime, hidden data and unmeasured memory verified')
            return identifier

        def run(source, input='', expected='Success', language='python', selected=task_id, slug=prefix):
            response = student.form('/CustomRuns/Run', {'ProgrammingTaskId': selected, 'RuntimeId': runtimes[language],
                'SourceCode': source, 'CustomInput': input, 'LanguageKey': 'cpp'}, token_path='/Tasks/' + slug)
            assert response[0] == 200, response[0]
            result = json.loads(response[2])
            assert result['status'] == expected, (expected, result)
            return result

        if unavailable:
            before = snapshot()
            for language in ('cpp', 'python', 'python'):
                result = run('print(42)' if language == 'python' else CPP_WAIT, expected='InternalError', language=language)
                assert result['error'] == 'Execution service is temporarily unavailable.'
                assert all(value not in json.dumps(result) for value in ('npipe:', 'C:\\Users', 'ipp-unavailable'))
            assert before == snapshot()
            for _ in range(2): submit(GOOD, 9)
            clean_runner()
            check(True, 'Python/C++ unavailable and cached failures are safe; no host fallback or leftover resources')
            return

        task_html = student.request('/Tasks/sum-of-two-numbers')
        page(task_html, 'en-US', editor=True)
        options = [attrs for tag, attrs in Tags(task_html[2]).tags if tag == 'option' and attrs.get('data-language')]
        assert {(item['data-language'], int(item['value'])) for item in options} == set(runtimes.items())
        assert all('solution here' in item['data-starter'] for item in options)
        invalid = student.form('/Submissions/Submit', {'ProgrammingTaskId': sum_id, 'RuntimeId': runtimes['python'], 'SourceCode': ' '}, token_path='/Tasks/sum-of-two-numbers')
        assert invalid[0] == 200 and any(attrs.get('value') == str(runtimes['python']) and 'selected' in attrs for tag, attrs in Tags(invalid[2]).tags if tag == 'option')
        assert student.request('/CustomRuns/Run', {'ProgrammingTaskId': sum_id, 'RuntimeId': runtimes['python'], 'SourceCode': GOOD})[0] == 400

        accepted = submit(GOOD, 3)
        wrong = submit('print(0)', 4)
        syntax = submit('def broken(:\n    pass', 5)
        assert sql(f'SELECT CAST(CompileSucceeded AS int) FROM Submissions WHERE Id={syntax}') == '0'
        assert sql(f'SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={syntax} AND Status<>6') == '0'
        syntax_text = html.unescape(student.request(f'/Submissions/Details/{syntax}')[2])
        assert 'SyntaxError' in syntax_text and '/app/main.py' not in syntax_text
        submit('raise ValueError("fixture exception")', 6)
        submit('while True:\n    pass', 7)
        submit('chunks=[]\nwhile True:\n    chunks.append(bytearray(8*1024*1024))', 8)
        submit('print(" 5")', 4)

        before = snapshot()
        assert run('import sys, math\nprint(math.isqrt(int(sys.stdin.read())))', '81')['output'].strip() == '9'
        assert run('import sys\nsys.stdout.write(sys.stdin.read())', ' leading\r\nline ')['output'] == ' leading\r\nline '
        assert run('def broken(:', expected='CompilationError')['compileSucceeded'] is False
        assert 'ValueError' in run('raise ValueError("fixture exception")', expected='RuntimeError')['error']
        run('while True:\n    print("x"*4096)', expected='RuntimeError')
        run('while True:\n    pass', expected='TimeLimitExceeded')
        run('chunks=[]\nwhile True:\n    chunks.append(bytearray(4*1024*1024))', expected='MemoryLimitExceeded')
        filesystem = run('import errno\ntry:\n    open("/etc/ipp-write-probe", "w").write("probe")\nexcept OSError as e:\n    assert e.errno in (errno.EROFS,errno.EACCES)\n    print("READ_ONLY")\nelse:\n    raise RuntimeError("write unexpectedly allowed")')
        assert filesystem['output'].strip() == 'READ_ONLY'
        network = run('import socket\ns=socket.socket()\ns.settimeout(0.5)\ntry:\n    s.connect(("1.1.1.1",443))\nexcept OSError:\n    print("NETWORK_BLOCKED")\nelse:\n    raise RuntimeError("network unexpectedly allowed")\nfinally:\n    s.close()')
        assert network['output'].strip() == 'NETWORK_BLOCKED'
        environment = run('import os,sys,importlib.util\nassert os.getuid()==65534\nassert not any(any(word in key.lower() for word in ("openai","connectionstrings","seedadmin","aspnetcore")) for key in os.environ)\nassert importlib.util.find_spec("pip") is None\nassert "/app" not in sys.path\nprint("ENV_ISOLATED")')
        assert environment['output'].strip() == 'ENV_ISOLATED'
        assert before == snapshot()
        check(True, 'Python Run: success, imports/stdin, syntax/runtime/time/OOM/output limits, read-only root, blocked network and isolated environment; zero database changes')

        fields = ('CompileCommand', 'RunCommand', 'DockerImage', 'FileExtension')
        metadata = {field: sql(f'SELECT {field} FROM Runtimes WHERE Id={runtimes["python"]}') for field in fields}
        try:
            sql(f"UPDATE Runtimes SET CompileCommand='do-not-execute',RunCommand='do-not-execute',DockerImage='do-not-pull',FileExtension='.cmd' WHERE Id={runtimes['python']}")
            assert run('print(42)')['output'].strip() == '42'
            sql(f"UPDATE Runtimes SET IsEnabled=0 WHERE Id={runtimes['python']}")
            rejected = student.form('/CustomRuns/Run', {'ProgrammingTaskId': sum_id, 'RuntimeId': runtimes['python'], 'SourceCode': GOOD}, token_path='/Tasks/sum-of-two-numbers')
            assert rejected[0] == 400
            rejected = student.form('/Submissions/Submit', {'ProgrammingTaskId': sum_id, 'RuntimeId': runtimes['python'], 'SourceCode': GOOD}, token_path='/Tasks/sum-of-two-numbers')
            assert rejected[0] == 200 and 'Select an enabled C++ or Python runtime.' in html.unescape(rejected[2])
            assert not any(attrs.get('data-language') == 'python' for tag, attrs in Tags(student.request('/Tasks/sum-of-two-numbers')[2]).tags if tag == 'option')
        finally:
            sql('UPDATE Runtimes SET IsEnabled=1,' + ','.join(field+'='+literal(value) for field,value in metadata.items()) + f" WHERE Id={runtimes['python']}")
        check(True, 'Trusted server commands/image/filename ignore Runtime metadata; disabled Python is rejected by Run and Submit')

        for slug, solutions in TASK_SOLUTIONS.items():
            selected = int(sql(f"SELECT Id FROM ProgrammingTasks WHERE Slug='{slug}' AND IsPublished=1"))
            orders = [int(value) for value in sql(f'SELECT [Order] FROM TestCases WHERE ProgrammingTaskId={selected} ORDER BY [Order]').splitlines()]
            assert orders == list(range(1, len(orders)+1)) and len(orders) in (7, 8)
            for language, source in zip(('cpp', 'python'), solutions):
                submit(source, 3, language, selected, slug)
            submit('print("incorrect")', 4, selected=selected, slug=slug)
            check(True, slug + ': Accepted in C++ and Python; WrongAnswer; deterministic visible/hidden tests')

        # Two languages, Custom Run and Submit must compete for the same two execution slots.
        observed, maximum = set(), 0
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            futures = [pool.submit(run, 'import time\ntime.sleep(2)\nprint(42)'),
                       pool.submit(run, CPP_WAIT, language='cpp'),
                       pool.submit(run, 'import time\ntime.sleep(2)\nprint(42)'),
                       pool.submit(submit, CPP_WAIT, 3, 'cpp', task_id, prefix)]
            while not all(future.done() for future in futures):
                containers = running_containers()
                maximum = max(maximum, len(containers))
                assert len(containers) <= 2, 'Global gate was bypassed'
                for container in containers:
                    observed.add(inspect_python(container, image_id))
                time.sleep(0.03)
            for future in futures: future.result()
        assert observed == {'cpp', 'python'} and maximum == 2, (observed, maximum)
        clean_runner()
        check(True, 'Live C++/Python Run/Submit containers share exactly two slots and all Docker restrictions')

        before = snapshot()
        token = Inputs(student.request('/Tasks/' + prefix)[2]).values['__RequestVerificationToken']
        payload = urllib.parse.urlencode({'ProgrammingTaskId': task_id, 'RuntimeId': runtimes['python'],
            'SourceCode': 'while True:\n    pass', 'CustomInput': '', '__RequestVerificationToken': token})
        connection = http.client.HTTPSConnection('localhost', 7115, context=ssl._create_unverified_context())
        try:
            connection.request('POST', '/CustomRuns/Run', body=payload, headers={'Content-Type': 'application/x-www-form-urlencoded',
                'Cookie': '; '.join(cookie.name+'='+cookie.value for cookie in student.cookies)})
            deadline = time.monotonic()+15
            while time.monotonic()<deadline:
                if any(c['Image']==image_id and c['Name'].startswith('/ipp-run-') and c['State']['Running'] for c in running_containers()): break
                time.sleep(0.05)
            else: raise AssertionError('Python cancellation test never reached execution')
        finally: connection.close()
        deadline = time.monotonic()+15
        while time.monotonic()<deadline and running_containers(): time.sleep(0.1)
        time.sleep(0.3)
        clean_runner()
        assert before == snapshot()
        check(True, 'Cancelled Python Run cleans containers/workspace and saves no attempt')

        mixed = submit('#include <iostream>\nint main(){long long a,b;std::cin>>a>>b;std::cout<<a+b;}', 3, 'cpp')
        later_python = submit(GOOD, 3)
        journey = student.request('/Tasks/sum-of-two-numbers/Journey')
        journey_text = page(journey, 'en-US')
        assert 'Python 3' in journey_text and 'C++ 20' in journey_text
        previous_python = int(sql(f"SELECT TOP (1) Id FROM Submissions WHERE UserId='{owner}' AND ProgrammingTaskId={sum_id} AND RuntimeId={runtimes['python']} AND Id<{later_python} ORDER BY CreatedAt DESC,Id DESC"))
        assert f'olderId={previous_python}&newerId={later_python}' in journey_text
        python_diff = f'/Submissions/Compare?olderId={accepted}&newerId={wrong}'
        mixed_diff = f'/Submissions/Compare?olderId={mixed}&newerId={later_python}'
        assert 'data-language="python"' in student.request(python_diff)[2]
        assert 'data-language="plaintext"' in student.request(mixed_diff)[2]
        assert Page(student.request(python_diff)[2]).sources['previous-source'] == GOOD
        assert other.request(python_diff)[0] == 404 and other.request(f'/Submissions/Details/{accepted}')[0] == 404
        assert is_redirect(guest.request(python_diff))
        assert 'Python 3' in student.request('/Submissions/My')[2]
        stats = dict(progress(student))
        assert int(stats['solved-tasks']) == 5 and int(stats['rating-score']) == 700, stats
        assert sql(f"SELECT Score FROM Leaderboards WHERE UserId='{owner}'") == '700'
        assert 'Map_' not in student.request('/Progress')[2]
        for lesson, task in (('arrays-basics','count-even-numbers'),('algorithm-basics','binary-search')):
            assert '/Tasks/'+task in guest.request('/Lessons/'+lesson)[2]
        for culture in CULTURES:
            switch(student, culture)
            page(student.request('/Tasks/sum-of-two-numbers'), culture, ('Student','Editor_LanguageHelp'), editor=True)
            page(student.request(mixed_diff), culture, ('Student','Diff_MixedLanguages'), editor=True)
            page(student.request(python_diff), culture, editor=True)
            page(student.request('/Tasks/sum-of-two-numbers/Journey'), culture)
        clean_runner()
        check(True, 'History/Journey/same-language comparisons, mixed plaintext diff, ownership, Progress/Map/Leaderboard, Lessons and three-language CSP pages pass')
        print('ENHANCEMENT 5B HTTPS/SQL/DOCKER CHECKS PASSED. No paid AI calls or browser claims.', flush=True)
    finally:
        addresses = ','.join(literal(email) for email in emails)
        sql(f"DELETE a FROM AiFeedbacks a JOIN AspNetUsers u ON u.Id=a.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug='{prefix}'); "
            f"DELETE FROM ProgrammingTasks WHERE Slug='{prefix}'; DELETE FROM Topics WHERE Name='{prefix}'; "
            f"DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed only this run\'s Enhancement 5B fixture data.', flush=True)


if __name__ == '__main__':
    if sys.argv[1:] not in ([], ['--unavailable']): raise SystemExit('Usage: verify_enhancement5b.py [--unavailable]')
    main(unavailable=bool(sys.argv[1:]))
