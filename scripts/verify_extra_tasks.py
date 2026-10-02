"""Verify the two Kazakh seeds over real HTTPS, SQL and Docker; no paid AI calls.

Before the first updated startup: --capture-baseline.
After startup: --check-added, --capture-seeded; restart, then --check-restart.
Default runs the six submissions using one UUID account removed in finally.
SQL snapshots contain counts/hashes, never credentials or submitted source.
"""
import argparse
import html
import json
import re
import secrets
import subprocess
import uuid
from html.parser import HTMLParser
from pathlib import Path

from verify_phase2 import Client, check, is_redirect, sql
from verify_enhancement2 import clean_runner, progress
from verify_enhancement5a import links


ROOT = Path(__file__).resolve().parents[1]
SLUGS = "'array-sum','count-vowels'"
TITLES = {
    'array-sum': 'Массив элементтерінің қосындысы',
    'count-vowels': 'Жолдағы дауысты әріптер саны',
}
SOLUTIONS = {
    'array-sum': {
        'cpp': '#include <iostream>\nint main(){int n;long long x,total=0;std::cin>>n;while(n--){std::cin>>x;total+=x;}std::cout<<total;}',
        'python': 'import sys\ndata=list(map(int,sys.stdin.buffer.read().split()))\nprint(sum(data[1:1+data[0]]))',
    },
    'count-vowels': {
        'cpp': '#include <iostream>\n#include <string>\nint main(){std::string s,v="aeiouAEIOU";std::getline(std::cin,s);int total=0;for(char c:s)if(v.find(c)!=std::string::npos)++total;std::cout<<total;}',
        'python': 'line=input()\nprint(sum(c in "aeiouAEIOU" for c in line))',
    },
}


def sql_json(query):
    # Explicit UTF-8 stdout preserves Kazakh; unlimited width preserves large tests.
    result = subprocess.run(['sqlcmd', '-S', 'localhost', '-E', '-C', '-I',
        '-d', 'IntelligentProgrammingPlatformDb', '-b', '-f', '65001', '-y', '0', '-w', '65535',
        '-Q', 'SET NOCOUNT ON; ' + query + ' FOR JSON PATH, INCLUDE_NULL_VALUES;'],
        capture_output=True, timeout=30)
    assert result.returncode == 0, 'SQL JSON read failed'
    return json.loads(''.join(result.stdout.decode('utf-8-sig').splitlines()).strip())


def snapshot(include_new=False):
    excluded = '' if include_new else f' WHERE Slug NOT IN ({SLUGS})'
    test_filter = '' if include_new else f' WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks{excluded})'
    queries = {'tasks': 'SELECT * FROM ProgrammingTasks' + excluded,
        'tests': 'SELECT * FROM TestCases' + test_filter,
        **{table: 'SELECT * FROM ' + table for table in
           ('Topics', 'Lessons', 'Runtimes', 'Submissions', 'ExecutionResults', 'AiFeedbacks', 'Leaderboards')},
        'users': 'SELECT Id,CreatedAt FROM AspNetUsers'}
    return {name: sql(f"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',({query} ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)")
            for name, query in queries.items()}


def state(mode):
    seeded = mode in ('capture-seeded', 'check-restart')
    path = ROOT / 'obj' / ('extra-tasks-seeded.json' if seeded else 'extra-tasks-baseline.json')
    current = {'hashes': snapshot(seeded),
        'tasks': int(sql('SELECT COUNT(*) FROM ProgrammingTasks')),
        'tests': int(sql('SELECT COUNT(*) FROM TestCases'))}
    if mode.startswith('capture-'):
        if not seeded:
            assert sql(f'SELECT COUNT(*) FROM ProgrammingTasks WHERE Slug IN ({SLUGS})') == '0'
        path.parent.mkdir(exist_ok=True)
        path.write_text(json.dumps(current, indent=2), encoding='utf-8')
        check(True, mode + ': saved row counts and SHA-256 hashes')
    else:
        previous = json.loads(path.read_text(encoding='utf-8'))
        assert current['hashes'] == previous['hashes'], 'Existing rows changed: ' + ', '.join(
            key for key in current['hashes'] if current['hashes'][key] != previous['hashes'][key])
        assert current['tasks'] == previous['tasks'] + (0 if seeded else 2)
        assert current['tests'] == previous['tests'] + (0 if seeded else 19)
        check(True, mode + ': exact task/test counts; existing content and history preserved')


class TestMarkup(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.values, self.buffer, self.cards = [], None, 0
        self.feed(body)

    def handle_starttag(self, tag, attrs):
        classes = dict(attrs).get('class', '').split()
        if tag == 'pre' and 'test-data' in classes:
            self.buffer = []
        if tag == 'section' and 'test-result' in classes:
            self.cards += 1

    def handle_data(self, value):
        if self.buffer is not None:
            self.buffer.append(value)

    def handle_endtag(self, tag):
        if tag == 'pre' and self.buffer is not None:
            self.values.append(''.join(self.buffer).replace('\r\n', '\n'))
            self.buffer = None


def main():
    tasks = sql_json(f'SELECT p.Id,p.Slug,p.Title,p.Description,p.Difficulty,p.TimeLimitMs,p.MemoryLimitMb,p.IsPublished,t.Name AS Topic FROM ProgrammingTasks p JOIN Topics t ON t.Id=p.TopicId WHERE p.Slug IN ({SLUGS}) ORDER BY p.Slug')
    assert len(tasks) == 2 and {t['Slug'] for t in tasks} == set(TITLES)
    guest, student = Client(culture='kk-KZ'), Client(timeout=240, culture='kk-KZ')
    catalog = guest.request('/Tasks')
    assert catalog[0] == 200 and 'lang="kk"' in catalog[2]
    tests = {}
    for task in tasks:
        slug = task['Slug']
        assert task['Title'] == TITLES[slug] and task['IsPublished'] and task['Difficulty'] == 0
        assert task['TimeLimitMs'] == 2000 and task['MemoryLimitMb'] == 128
        assert task['Topic'] == ('Arrays' if slug == 'array-sum' else 'Basics')
        assert task['Title'] in html.unescape(catalog[2]) and '/Tasks/' + slug in links(catalog[2])
        rows = sql_json(f"SELECT [Order],IsHidden,Input,ExpectedOutput FROM TestCases WHERE ProgrammingTaskId={task['Id']} ORDER BY [Order]")
        tests[slug] = rows
        assert len(rows) == (8 if slug == 'array-sum' else 11)
        assert [r['Order'] for r in rows] == list(range(1, len(rows) + 1))
        assert [r['IsHidden'] for r in rows] == [False, False] + [True] * (len(rows) - 2)
        for row in rows:
            assert len(row['Input'].encode()) <= 1024 * 1024
            if slug == 'array-sum':
                values = list(map(int, row['Input'].split()))
                assert values[0] == len(values) - 1 and 1 <= values[0] <= 100000
                assert all(-1000000 <= value <= 1000000 for value in values[1:])
                expected = sum(values[1:])
            else:
                value = row['Input']
                assert 1 <= len(value) <= 100000 and re.fullmatch('[a-zA-Z ]+', value)
                expected = sum(value.count(c) for c in 'aeiouAEIOU')
            assert row['ExpectedOutput'] == str(expected), (slug, row['Order'])
        response = guest.request('/Tasks/' + slug)
        assert response[0] == 200 and task['Description'] in html.unescape(response[2])
        assert all(label in task['Description'] for label in ('Кіріс пішімі', 'Шығыс пішімі', 'Шектеулер', 'Мысал'))
        assert TestMarkup(response[2]).values == [value for r in rows[:2] for value in (r['Input'], r['ExpectedOutput'])]
        lesson_slug = 'arrays-basics' if slug == 'array-sum' else 'programming-basics'
        lesson = guest.request('/Lessons/' + lesson_slug)
        assert lesson[0] == 200 and '/Tasks/' + slug in links(lesson[2])
        check(True, slug + ': Kazakh catalog/description, two visible examples, hidden tests, constraints and related lesson')

    prefix = 'extra-tasks-' + uuid.uuid4().hex
    email = prefix + '@example.test'
    password = 'Aa1!' + secrets.token_urlsafe(24)
    before = snapshot(True)
    runtimes = {key: int(sql(f"SELECT Id FROM Runtimes WHERE LanguageKey='{key}' AND IsEnabled=1")) for key in ('cpp', 'python')}
    try:
        assert is_redirect(student.form('/Account/Register', {'DisplayName': prefix, 'Email': email,
            'Password': password, 'ConfirmPassword': password}))
        owner = sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}'")
        for task in tasks:
            slug, rows = task['Slug'], tests[task['Slug']]
            for language, source, expected in [
                ('cpp', SOLUTIONS[slug]['cpp'], 3), ('python', SOLUTIONS[slug]['python'], 3),
                ('python', 'print(-999999999)', 4)]:
                response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task['Id'],
                    'RuntimeId': runtimes[language], 'SourceCode': source}, token_path='/Tasks/' + slug)
                assert is_redirect(response), ('Submit HTTP', response[0])
                identifier = int(response[1]['Location'].rsplit('/', 1)[1])
                assert sql(f'SELECT Status FROM Submissions WHERE Id={identifier}') == str(expected), (slug, language, identifier)
                assert sql(f"SELECT COUNT(*) FROM Submissions WHERE Id={identifier} AND UserId='{owner}' AND RuntimeId={runtimes[language]} AND CompileSucceeded=1 AND TotalTests={len(rows)} AND FinishedAt>=StartedAt") == '1'
                assert sql(f'SELECT COUNT(*) FROM ExecutionResults WHERE SubmissionId={identifier} AND Status={1 if expected == 3 else 2}') == str(len(rows))
                detail = student.request(response[1]['Location'])
                markup = TestMarkup(detail[2])
                assert detail[0] == 200 and markup.cards == len(rows)
                expected_values = [value for r in rows[:2] for value in
                    (r['Input'], r['ExpectedOutput'], r['ExpectedOutput'] if expected == 3 else '-999999999')]
                assert [v.rstrip() for v in markup.values] == [v.rstrip() for v in expected_values]
                decoded = html.unescape(detail[2])
                assert all(marker not in decoded for marker in
                    (['6\n-1000000 999999 1 -8 8 42'] if slug == 'array-sum' else ['rhythms', 'ApPlE OrAnGe']))
                check(True, f'{slug}: {language} {"Accepted" if expected == 3 else "WrongAnswer"}; all test results and hidden-data protection')
        metrics = dict(progress(student))
        assert {k: metrics[k] for k in ('solved-tasks', 'total-submissions', 'accepted-submissions', 'compilation-errors', 'rating-score')} == {
            'solved-tasks': '2', 'total-submissions': '6', 'accepted-submissions': '4', 'compilation-errors': '0', 'rating-score': '200'}
        assert metrics['success-rate'].replace(',', '.').rstrip('%') == '66.7'
        board = student.request('/Leaderboard')
        assert board[0] == 200 and prefix in board[2]
        assert sql(f"SELECT COUNT(*) FROM Leaderboards WHERE UserId='{owner}' AND SolvedTasks=2 AND Score=200 AND SuccessfulSubmissions=4 AND TotalSubmissions=6") == '1'
        check(True, 'Progress/Leaderboard: two unique solved tasks, four Accepted, six attempts, score 200, success 66.7%')
        clean_runner()
    finally:
        assert re.fullmatch(r'extra-tasks-[a-f0-9]{32}@example\.test', email)
        sql(f"BEGIN TRANSACTION; DELETE FROM Submissions WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email='{email}'); "
            f"DELETE FROM Leaderboards WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email='{email}'); "
            f"DELETE FROM AspNetUsers WHERE Email='{email}'; COMMIT;")
        assert sql(f"SELECT COUNT(*) FROM AspNetUsers WHERE Email='{email}'") == '0'
        assert snapshot(True) == before, 'Pre-existing rows must remain unchanged after fixture cleanup'
        check(True, 'Only this UUID account/submissions/summary removed; all pre-existing rows unchanged')
    print('EXTRA TASKS HTTPS/SQL/DOCKER CHECKS PASSED. No paid AI calls.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_mutually_exclusive_group()
    for mode in ('capture-baseline', 'check-added', 'capture-seeded', 'check-restart'):
        modes.add_argument('--' + mode, dest='mode', action='store_const', const=mode)
    args = parser.parse_args()
    state(args.mode) if args.mode else main()
