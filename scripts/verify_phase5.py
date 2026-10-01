"""Phase 5 HTTPS/SQL checks. No OpenAI requests; fixture rows are always removed.

Checks server-rendered structure and contrast, not browser layout/Monaco behavior.
Run the separate explicitly opted-in live script only when paid checks are wanted.
"""
import html
import json
import os
import re
import secrets
import uuid
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from verify_phase2 import ROOT, Client, check, is_redirect, sql


class Page(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.tags = []
        self.feed(body)

    def handle_starttag(self, tag, attrs):
        self.tags.append((tag, dict(attrs)))


def inspect_page(response, expected=200):
    status, headers, body = response
    check(status == expected, f'Expected HTTP {expected}')
    for name, value in [('X-Content-Type-Options', 'nosniff'), ('X-Frame-Options', 'DENY'),
                        ('Referrer-Policy', 'strict-origin-when-cross-origin'),
                        ('Permissions-Policy', 'camera=(), microphone=(), geolocation=()')]:
        assert headers.get(name) == value, name
    page = Page(body)
    assert sum(tag == 'h1' for tag, attrs in page.tags) == 1, 'One clear page heading'
    assert any(tag == 'html' and attrs.get('lang') == 'en' for tag, attrs in page.tags)
    assert any(tag == 'meta' and attrs.get('name') == 'viewport' for tag, attrs in page.tags)
    assert 'Skip to main content' in body and 'id="main-content"' in body
    labels = {attrs.get('for') for tag, attrs in page.tags if tag == 'label'}
    for tag, attrs in page.tags:
        if tag in ('input', 'select', 'textarea') and attrs.get('type') not in ('hidden', 'submit'):
            assert attrs.get('id') in labels or attrs.get('aria-label'), f'Unlabelled {tag}'
        if tag == 'th':
            assert attrs.get('scope') == 'col', 'Table column has a header scope'
    check(True, 'Headers, language, viewport, skip link, heading, labels and table headers present')
    return body


def luminance(value):
    rgb = [int(value[i:i+2], 16) / 255 for i in (1, 3, 5)]
    linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
    return sum(v * weight for v, weight in zip(linear, (.2126, .7152, .0722)))


def main():
    for foreground, background in [('#eef2f8', '#181f2b'), ('#afbed0', '#181f2b'),
        ('#afbed0', '#211f33'), ('#19132b', '#b2a4ff'), ('#79dfb4', '#17352e'),
        ('#f5cf80', '#382f20'), ('#ffa2b2', '#392631'), ('#c1b6ff', '#181f2b')]:
        hi, lo = sorted([luminance(foreground), luminance(background)], reverse=True)
        assert (hi + .05) / (lo + .05) >= 4.5, 'Text contrast below 4.5:1'
    check(True, 'Main, muted, button, link and status text color pairs meet 4.5:1 contrast')

    run_id = uuid.uuid4().hex
    email = f'phase5-{run_id}@example.test'
    display = '<img src=x onerror=phase5Marker()>'
    password = 'Aa1!' + secrets.token_urlsafe(24)
    anon, student, admin = Client(), Client(), Client()
    try:
        for path in ['/', '/Tasks', '/Tasks/sum-of-two-numbers', '/Leaderboard', '/Account/Login', '/Account/Register']:
            print('PAGE:', path)
            inspect_page(anon.request(path))
        for path, status in [('/missing-phase5-' + run_id, 404), ('/Account/AccessDenied', 403), ('/Home/Error', 500)]:
            body = inspect_page(anon.request(path), status)
            assert not any(marker in body for marker in ['StackTrace', 'SqlException', 'C:\\', 'Development Mode', 'ConnectionStrings', 'DockerCodeRunner'])
        csrf = anon.request('/Account/Register', {})
        inspect_page(csrf, 400)
        check(True, '403/404/500 and CSRF error pages preserve status without internal details')
        response = student.form('/Account/Register', {'DisplayName': display, 'Email': email,
            'Password': password, 'ConfirmPassword': password})
        check(is_redirect(response), 'Student registers over HTTPS')
        auth_cookie = next(cookie for cookie in student.cookies if cookie.name == '.AspNetCore.Identity.Application')
        cookie_flags = next(value.split(';', 1)[1].lower() for value in response[1].get_all('Set-Cookie')
                            if value.startswith('.AspNetCore.Identity.Application='))
        check(auth_cookie.secure and '; httponly' in cookie_flags and 'samesite=lax' in cookie_flags,
              'Identity cookie is Secure, HttpOnly and SameSite=Lax')
        for path in ['/', '/Tasks', '/Tasks/sum-of-two-numbers', '/Submissions/My', '/Progress', '/Account/Profile']:
            print('STUDENT PAGE:', path)
            body = inspect_page(student.request(path))
            assert display not in body and '&lt;img' in body, 'DisplayName must be encoded'
        check("haven&#x27;t submitted" in student.request('/Submissions/My')[2] or "haven't submitted" in student.request('/Submissions/My')[2], 'History has a useful empty state')
        check('No tasks match your filters.' in student.request('/Tasks?search=' + run_id)[2], 'Filtered catalog has a useful empty state')
        user_id = sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}';")
        task_id = sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers';")
        runtime_id = sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp';")
        sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,StartedAt,FinishedAt,PassedTests,TotalTests,CompileSucceeded) VALUES('{user_id}',{task_id},{runtime_id},'// metadata-only fixture',3,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),3,3,1);")
        home = student.request('/')[2]
        check('id="home-solved">1</strong>' in home and 'id="home-score">100</strong>' in home, 'Home aggregates persisted Accepted history and score')
        catalog = student.request('/Tasks')[2]
        row = next(row for row in re.findall(r'<tr\b[^>]*>(.*?)</tr>', catalog, re.S) if 'sum-of-two-numbers' in row)
        check('Solved' in row, 'Catalog solved badge comes from persisted Accepted history')
        check('solved-badge' not in anon.request('/Tasks')[2], 'Anonymous catalog does not leak personal solved state')
        for path in ['/Tasks', '/']:
            assert 'no-store' in student.request(path)[1].get('Cache-Control', '')
        check(True, 'Personalized home and catalog cannot be stored by shared caches')
        hard = anon.request('/Tasks/longest-increasing-subsequence')[2]
        check('Hard' in hard and 'strictly increasing' in hard and '10 9 2 5 3 7 101 18' not in hard, 'Fourth seed task covers Hard without hidden input leakage')
        check(sql("SELECT COUNT(*) FROM ProgrammingTasks WHERE Slug='longest-increasing-subsequence';") == '1', 'New development seed remains idempotent')
        check(is_redirect(student.request('/Admin')), 'Student remains denied Admin access')
        board = anon.request('/Leaderboard')[2]
        check(email not in board and user_id not in board and display not in board, 'Public leaderboard excludes email/UserId and encodes names')
        project = ET.parse(ROOT / 'IntelligentProgrammingPlatform.csproj')
        secret_path = Path(os.environ['APPDATA']) / 'Microsoft' / 'UserSecrets' / project.findtext('.//UserSecretsId') / 'secrets.json'
        configuration = json.loads(secret_path.read_text(encoding='utf-8-sig'))
        assert is_redirect(admin.form('/Account/Login', {'Email': configuration['SeedAdmin:Email'], 'Password': configuration['SeedAdmin:Password']}))
        for path in ['/Admin', '/Admin/Topics', '/Admin/ProgrammingTasks', '/Admin/TestCases', '/Admin/Topics/Create', '/Admin/ProgrammingTasks/Create']:
            print('ADMIN PAGE:', path)
            inspect_page(admin.request(path))
        check(is_redirect(student.form('/Account/Logout', {}, token_path='/Account/Profile')), 'Student logs out')
        check(is_redirect(student.request('/Account/Profile')), 'Logout removes profile access')
    finally:
        sql(f"DELETE FROM Submissions WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email='{email}'); DELETE FROM Leaderboards WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email='{email}'); DELETE FROM AspNetUsers WHERE Email='{email}';")
    print('All Phase 5 HTTP/SQL checks passed. Browser visual checks remain separate.')


if __name__ == '__main__':
    main()
