"""HTTPS/SQL hardening regressions. No browser, no OpenAI requests.

Use the running HTTPS Development app. Unique fixture account is removed in finally.
"""
import base64
import html
import re
import secrets
import urllib.error
import urllib.request
import uuid
from html.parser import HTMLParser
from verify_phase2 import BASE, Client, check, is_redirect, sql


class Tags(HTMLParser):
    def __init__(self, body):
        super().__init__()
        self.tags = []
        self.feed(body)

    def handle_starttag(self, tag, attrs):
        self.tags.append((tag, dict(attrs)))


nonces = set()


def inspect(response, editor=False, expected=200):
    status, headers, body = response
    assert status == expected, status
    policies = headers.get_all('Content-Security-Policy')
    assert len(policies) == 1
    directives = dict(part.strip().split(' ', 1) for part in policies[0].split(';'))
    nonce = re.search(r"'nonce-([^']+)'", directives['script-src']).group(1)
    assert len(base64.b64decode(nonce, validate=True)) == 32 and nonce not in nonces
    nonces.add(nonce)
    assert directives['script-src'] == f"'self' 'nonce-{nonce}'"
    assert directives['style-src'] == f"'self' 'nonce-{nonce}'"
    assert directives['script-src-attr'] == "'none'"
    assert directives['style-src-attr'] == ("'unsafe-inline'" if editor else "'none'")
    for directive in ['default-src', 'object-src', 'frame-src', 'frame-ancestors', 'base-uri']:
        assert directives[directive] == "'none'"
    for directive in ['connect-src', 'worker-src', 'font-src', 'form-action']:
        assert directives[directive] == "'self'"
    assert directives['img-src'] == "'self' data:"
    assert 'unsafe-eval' not in policies[0] and headers['Cache-Control'] == 'no-store'
    for tag, attrs in Tags(body).tags:
        assert not any(key.startswith('on') for key in attrs)
        assert 'style' not in attrs, 'Authored HTML should not need style attributes'
        if tag == 'style' or tag == 'script' and 'src' not in attrs:
            assert attrs.get('nonce') == nonce, 'Inline block nonce must match response'
        if tag == 'meta' and attrs.get('name') == 'csp-nonce':
            assert attrs['content'] == nonce
    assert headers['X-Content-Type-Options'] == 'nosniff' and headers['X-Frame-Options'] == 'DENY'
    return body


def main():
    guest, student = Client(), Client(timeout=60)
    email = 'enhancement1-' + uuid.uuid4().hex + '@example.test'
    password = 'Aa1!' + secrets.token_urlsafe(24)
    try:
        for path in ['/', '/Tasks', '/Tasks/sum-of-two-numbers', '/Leaderboard', '/Account/Login', '/Account/Register']:
            inspect(guest.request(path))
        inspect(guest.request('/does-not-exist-enhancement1'), expected=404)
        inspect(guest.request('/Home/Error'), expected=500)
        inspect(guest.request('/Account/Login', {'Email': email}), expected=400)
        check(True, 'Anonymous, error and CSRF responses use unique nonces and strict CSP')
        for host, expected in [('localhost', 200), ('127.0.0.1', 200), ('untrusted-host.invalid', 400)]:
            request = urllib.request.Request(BASE + '/', headers={'Host': host})
            try:
                response = guest.opener.open(request, timeout=30)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                check(response.status == expected, f'Host filtering: {host} -> {expected}')

        assert is_redirect(student.form('/Account/Register', {'DisplayName': 'CSP verification', 'Email': email, 'Password': password, 'ConfirmPassword': password}))
        for path in ['/', '/Progress', '/Account/Profile', '/Submissions/My', '/Leaderboard']:
            inspect(student.request(path))
        task_path = '/Tasks/sum-of-two-numbers'
        inspect(student.request(task_path), editor=True)
        task_id = int(sql("SELECT Id FROM ProgrammingTasks WHERE Slug='sum-of-two-numbers';"))
        runtime_id = int(sql("SELECT TOP (1) Id FROM Runtimes WHERE LanguageKey='cpp' AND IsEnabled=1;"))
        invalid = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id, 'SourceCode': ''}, token_path=task_path)
        inspect(invalid, editor=True)
        code = 'int main() { syntax error } // <script>alert("xss")</script>'
        response = student.form('/Submissions/Submit', {'ProgrammingTaskId': task_id, 'RuntimeId': runtime_id, 'SourceCode': code}, token_path=task_path)
        assert is_redirect(response)
        submission_id = int(response[1]['Location'].rsplit('/', 1)[1])
        assert sql(f'SELECT Status FROM Submissions WHERE Id={submission_id};') == '5'
        body = inspect(student.request(response[1]['Location']))
        assert 'main.cpp' in body and 'IntelligentProgrammingPlatformRunner' not in body
        assert '<script>alert(' not in body and code in html.unescape(body)
        check(True, 'Editor and invalid-submit responses scope styles; real syntax submission remains encoded')

        sql(f"INSERT INTO AspNetUserRoles(UserId,RoleId) SELECT u.Id,r.Id FROM AspNetUsers u CROSS JOIN AspNetRoles r WHERE u.Email='{email}' AND r.Name='Admin';")
        assert is_redirect(student.form('/Account/Login', {'Email': email, 'Password': password}))
        inspect(student.request('/Admin'))
        check(True, 'Authenticated, Progress and Admin markup match their CSP nonces')
        for path in ['/js/editor/code-editor.js', '/js/editor/editor.worker.js', '/js/editor/code-editor.css']:
            status, _, asset = guest.request(path)
            assert status == 200 and asset
            for chunk in re.findall(r'(?:from|import)\s*["\'](\./[^"\']+\.js)["\']', asset):
                assert guest.request('/js/editor/' + chunk[2:])[0] == 200
        check(True, 'Local editor, worker, CSS and imported chunks are served')
        print('ENHANCEMENT 1 HTTP CHECKS PASSED; no real browser was used.', flush=True)
    finally:
        sql(f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{email}'; DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email='{email}'; DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email='{email}'; DELETE FROM AspNetUsers WHERE Email='{email}';")
        print('Removed enhancement verification fixtures.', flush=True)


if __name__ == '__main__':
    main()
