"""Real local HTTPS/SQL profile, avatar, curriculum and Admin checks; no paid AI.

First run the Enhancement6Checks console project to generate benign image fixtures.
All UUID users, submissions and uploaded images are removed in finally.
--capture-state / --check-state compare startup hashes without exposing row contents.
"""
import argparse
import html
import json
import re
import secrets
import urllib.error
import urllib.request
import uuid
from pathlib import Path

from verify_phase2 import BASE, Client, Inputs, check, is_redirect, sql
from verify_extra_tasks import snapshot, sql_json
from verify_enhancement4 import text
from verify_enhancement5a import links

ROOT = Path(__file__).resolve().parents[1]
SLUGS = ['python-basics', 'python-variables', 'python-input-output', 'python-conditions',
         'python-loops', 'python-lists', 'python-functions', 'python-strings', 'python-algorithms']


def raw(client, path, data=None, content_type=None):
    request = urllib.request.Request(BASE + path, data=data,
        headers={} if content_type is None else {'Content-Type': content_type})
    try:
        response = client.opener.open(request, timeout=client.timeout)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.headers, response.read()


def upload(client, data, name='avatar.png', mime='image/png', fields=None, csrf=True):
    values = {'DisplayName': 'Avatar owner', **(fields or {})}
    if csrf:
        values['__RequestVerificationToken'] = Inputs(client.request('/Account/EditProfile')[2]).values['__RequestVerificationToken']
    boundary = 'enh6-' + uuid.uuid4().hex
    chunks = []
    for key, value in values.items():
        chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode())
    chunks.append(f'--{boundary}\r\nContent-Disposition: form-data; name="Avatar"; filename="{name}"\r\nContent-Type: {mime}\r\n\r\n'.encode())
    chunks.extend([data, f'\r\n--{boundary}--\r\n'.encode()])
    status, headers, body = raw(client, '/Account/EditProfile', b''.join(chunks), 'multipart/form-data; boundary=' + boundary)
    return status, headers, body.decode('utf-8')


def managed(path):
    assert re.fullmatch(r'/uploads/avatars/[a-f0-9]{32}\.webp', path), path
    return ROOT / 'wwwroot' / path.lstrip('/')


def main():
    prefix = 'enh6-' + uuid.uuid4().hex
    emails = [prefix + suffix + '@example.test' for suffix in ('-owner', '-other', '-admin')]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    clients = [Client() for _ in emails]
    guest = Client()
    paths = set()
    fixture_root = ROOT / 'obj' / 'enh6-image-fixtures'
    images = {kind: (fixture_root / kind).read_bytes() for kind in ('jpeg', 'png', 'webp')}
    assert guest.request('/Account/EditProfile')[0] == 302
    assert upload(guest, images['png'], csrf=False)[0] in (302, 400)
    try:
        ids = []
        for i, (client, email) in enumerate(zip(clients, emails)):
            assert is_redirect(client.form('/Account/Register', {'DisplayName': 'Owner' if i == 0 else 'Other Student',
                'Email': email, 'Password': password, 'ConfirmPassword': password}))
            ids.append(sql(f"SELECT Id FROM AspNetUsers WHERE Email='{email}'"))
        owner, other, admin_id = ids
        student, stranger, admin = clients
        sql(f"INSERT INTO AspNetUserRoles(UserId,RoleId) SELECT '{admin_id}',Id FROM AspNetRoles WHERE Name='Admin'")
        assert is_redirect(admin.form('/Account/Logout', {}, token_path='/Account/Profile'))
        assert is_redirect(admin.form('/Account/Login', {'Email': emails[2], 'Password': password}))
        before = sql_json(f"SELECT Id,Email,CreatedAt FROM AspNetUsers WHERE Id='{owner}'")[0]
        assert student.request('/Admin/Users')[0] in (302,403)
        assert guest.request('/Admin/Users')[0] == 302
        assert student.request('/Admin/Users/RemoveAvatar/' + other, {})[0] in (302,403)
        assert upload(student, images['png'], csrf=False)[0] == 400
        assert student.request('/Account/RemoveAvatar')[0] in (404,405)
        assert student.request('/Account/RemoveAvatar', {})[0] == 400
        check(True, 'Anonymous/Student authorization, POST-only removal and antiforgery')

        def avatar():
            value = sql(f"SELECT COALESCE(AvatarPath,'') FROM AspNetUsers WHERE Id='{owner}'")
            if value: paths.add(value)
            return value

        previous = None
        for kind, data in images.items():
            response = upload(student, data, name='../../evil<img_onerror=alert(1)>.exe', mime='text/html', fields={
                'Id': other, 'UserId': other, 'Email': 'hacked@example.test', 'Role': 'Admin', 'Score': '99999',
                'CreatedAt': '1900-01-01', 'AvatarPath': '/uploads/avatars/../../sentinel.txt'})
            assert is_redirect(response), response[0]
            current = avatar()
            assert managed(current).exists() and current != previous
            status, headers, served = raw(guest, current)
            assert status == 200 and headers['Content-Type'] == 'image/webp'
            assert served[:4] == b'RIFF' and served[8:12] == b'WEBP' and 'immutable' in headers['Cache-Control']
            assert headers['X-Content-Type-Options'] == 'nosniff'
            if previous: assert not managed(previous).exists()
            previous = current
        assert sql_json(f"SELECT Id,Email,CreatedAt FROM AspNetUsers WHERE Id='{owner}'")[0] == before
        assert sql(f"SELECT COALESCE(AvatarPath,'') FROM AspNetUsers WHERE Id='{other}'") == ''
        assert sql(f"SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE ur.UserId='{owner}' AND r.Name='Admin'") == '0'
        check(True, 'JPEG/PNG/WebP uploads normalized; forged filename/MIME ignored; replacements clean old files; overposting cannot change owner/email/roles/date')

        for payload, filename in [(b'', 'empty.png'), (b'not an image', 'fake.jpg'),
            (b'<svg><script>alert(1)</script></svg>', 'avatar.svg'), (b'MZ executable', 'file.png'),
            (b'x' * (4 * 1024 * 1024 + 1), 'large.png')]:
            response = upload(student, payload, filename)
            assert response[0] in (200,400,413), response[0]
            assert avatar() == previous and managed(previous).exists()
        assert is_redirect(stranger.form('/Account/EditProfile', {'DisplayName': 'Other Student', 'UserId': owner, 'AvatarPath': previous}))
        assert is_redirect(stranger.form('/Account/RemoveAvatar', {'UserId': owner, 'AvatarPath': previous}, token_path='/Account/Profile'))
        assert avatar() == previous and managed(previous).exists()
        assert guest.request('/uploads/avatars/.gitkeep')[0] == 404
        check(True, 'Invalid/empty/oversize images preserve prior avatar; another user cannot replace or remove it')

        leaderboard = guest.request('/Leaderboard')[2]
        assert previous in leaderboard and 'data-initials="OS"' in leaderboard
        assert all(value not in leaderboard for value in [*emails, *ids, 'PasswordHash', 'SecurityStamp'])
        assert '<img_onerror' not in leaderboard
        assert admin.request('/Admin/Users/RemoveAvatar/' + owner)[0] in (404,405)
        assert admin.request('/Admin/Users/RemoveAvatar/' + owner, {})[0] == 400
        assert is_redirect(admin.form('/Admin/Users/RemoveAvatar/' + owner, {}, token_path='/Admin/Users/Details/' + owner))
        assert avatar() == '' and not managed(previous).exists()
        assert is_redirect(upload(student, images['png']))
        previous = avatar()
        assert is_redirect(student.form('/Account/RemoveAvatar', {'path': '../../sentinel.txt'}, token_path='/Account/Profile'))
        assert avatar() == '' and not managed(previous).exists()
        check(True, 'Public avatars/initials without email/ID leaks; Admin moderation and owner removal work')

        runtime = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='python'"))
        task = int(sql('SELECT TOP(1) Id FROM ProgrammingTasks WHERE Difficulty=0 ORDER BY Id'))
        for index in range(6):
            sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,PassedTests,TotalTests) VALUES('{owner}',{task},{runtime},'ENH6_SOURCE_MUST_NOT_LEAK',{3 if index < 2 else 4},DATEADD(minute,{index},'2026-01-01'),0,0)")
        # Missing summaries use the existing service rules after fixture history is inserted.
        sql(f"DELETE FROM Leaderboards WHERE UserId='{owner}'")
        for culture in ('kk-KZ','ru-RU','en-US'):
            # Reuse the same cookie jar while changing only the supported UI culture.
            assert is_redirect(student.form('/Culture/Set', {'culture': culture, 'returnUrl': '/Account/Profile'}, token_path='/Account/Profile'))
            assert is_redirect(admin.form('/Culture/Set', {'culture': culture, 'returnUrl': '/Admin/Users'}, token_path='/Admin/Users'))
            profile = student.request('/Account/Profile')[2]
            edit = student.request('/Account/EditProfile')[2]
            details = admin.request('/Admin/Users/Details/' + owner)[2]
            listing = admin.request('/Admin/Users')[2]
            assert text('Shared',culture,'Profile_Edit') in html.unescape(profile)
            assert text('Shared',culture,'Avatar_Help') in html.unescape(edit)
            assert text('Admin',culture,'User_Details') in html.unescape(details)
            assert text('Admin',culture,'Users') in html.unescape(listing)
            assert 'id="profile-solved">1<' in profile and 'id="profile-submissions">6<' in profile and 'id="profile-score">100<' in profile
            assert ('33.3%' if culture=='en-US' else '33,3%') in profile
            assert profile.count('status-badge ') == 5
            assert 'ENH6_SOURCE_MUST_NOT_LEAK' not in profile + details + listing
            assert 'PasswordHash' not in listing + details and 'SecurityStamp' not in listing + details
            invalid = student.form('/Account/EditProfile', {'DisplayName': '   '})
            assert invalid[0] == 200 and 'validation-summary-errors' in invalid[2]
        check(True, 'Three-language profile/edit/Admin pages; existing score/success rules and five projected recent submissions')

        for index in range(21):
            sql(f"INSERT INTO AspNetUsers(Id,UserName,NormalizedUserName,Email,NormalizedEmail,DisplayName,CreatedAt,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount) VALUES('{prefix}-page-{index}','{prefix}-page-{index}','{prefix.upper()}-PAGE-{index}','{prefix}-page-{index}@example.test','{prefix.upper()}-PAGE-{index}@EXAMPLE.TEST','Page fixture',GETUTCDATE(),0,0,0,0,0)")
        first = admin.request('/Admin/Users')[2]
        second = admin.request('/Admin/Users?page=2')[2]
        first_ids = set(re.findall(r'/Admin/Users/Details/([^"?]+)',first))
        second_ids = set(re.findall(r'/Admin/Users/Details/([^"?]+)',second))
        assert len(first_ids) == 20 and second_ids and not first_ids.intersection(second_ids)
        assert admin.request('/Admin/Users/Details/missing-' + prefix)[0] == 404
        check(True, 'Admin list has deterministic, disjoint pages of 20 users and safe missing-user handling')

        lessons = sql_json("SELECT Slug,[Order],CodeExample,Content,Summary,Title FROM Lessons WHERE CodeLanguage='python' ORDER BY [Order],Id")
        seeds = [lesson for lesson in lessons if lesson['Slug'] in SLUGS]
        assert [lesson['Slug'] for lesson in seeds] == SLUGS
        catalog = guest.request('/Lessons?language=python')[2]
        positions = [catalog.index('/Lessons/' + slug + '"') for slug in SLUGS]
        assert positions == sorted(positions)
        for lesson in seeds:
            detail = guest.request('/Lessons/' + lesson['Slug'])
            assert detail[0] == 200 and lesson['Title'] in html.unescape(detail[2])
            assert '<code>' in detail[2] and lesson['CodeExample'].replace('\r\n','\n') in html.unescape(detail[2]).replace('\r\n','\n')
            assert '/Lessons?language=python' in detail[2]
        for slug, related in [('python-lists',['count-even-numbers','array-sum','maximum-in-array']),
                              ('python-strings',['palindrome-check','count-vowels']), ('python-algorithms',['binary-search'])]:
            linked = links(guest.request('/Lessons/' + slug)[2])
            assert all('/Tasks/' + task in linked for task in related), (slug,linked)
        check(True, 'Nine Kazakh Python lessons in order, encoded code, course navigation and topic-based related tasks')
        print('ENHANCEMENT 6 HTTPS/SQL CHECKS PASSED. No browser or paid AI claims.', flush=True)
    finally:
        count = int(sql(f"SELECT COUNT(*) FROM AspNetUsers WHERE Email LIKE '{prefix}%@example.test' AND AvatarPath IS NOT NULL"))
        stored = sql_json(f"SELECT AvatarPath FROM AspNetUsers WHERE Email LIKE '{prefix}%@example.test' AND AvatarPath IS NOT NULL") if count else []
        for item in stored: paths.add(item['AvatarPath'])
        sql(f"DELETE e FROM ExecutionResults e JOIN Submissions s ON s.Id=e.SubmissionId JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email LIKE '{prefix}%@example.test'; "
            f"DELETE s FROM Submissions s JOIN AspNetUsers u ON u.Id=s.UserId WHERE u.Email LIKE '{prefix}%@example.test'; "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email LIKE '{prefix}%@example.test'; "
            f"DELETE FROM AspNetUsers WHERE Email LIKE '{prefix}%@example.test';")
        for path in paths: managed(path).unlink(missing_ok=True)
        check(all(not managed(path).exists() for path in paths), 'All this run\'s uploaded avatars and database fixtures removed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--capture-state', action='store_true')
    parser.add_argument('--check-state', action='store_true')
    args = parser.parse_args()
    if args.capture_state or args.check_state:
        path = ROOT / 'obj' / 'enhancement6-seeded-state.json'
        state = snapshot(True)
        if args.capture_state: path.write_text(json.dumps(state),encoding='utf-8')
        else: assert state == json.loads(path.read_text(encoding='utf-8')), 'Startup changed existing row hashes'
        check(True, 'Startup state ' + ('captured' if args.capture_state else 'unchanged, including all historical data and lessons'))
    else: main()
