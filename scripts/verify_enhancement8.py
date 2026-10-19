"""Real HTTPS/SQL/SignalR tests with UUID users, no AI requests, and finally cleanup.

Run with the local app on https://localhost:7115. WebSocket cookies only travel in
memory to the localhost-only Node probe. This is not a browser rendering test.
"""
import html
import json
import re
import secrets
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

from verify_phase2 import BASE, Client, Inputs, check, is_redirect, sql
from verify_enhancement1 import Tags
from verify_enhancement4 import switch
from verify_enhancement7 import configuration, literal
from verify_resources import CULTURES, resources


def text(source):
    return html.unescape(re.sub(r'<[^>]*>', '', source))


class Probe:
    def __init__(self, clients):
        self.process = subprocess.Popen(['node', 'scripts/social_realtime_probe.mjs'], stdin=subprocess.PIPE,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8')
        cookies = ['; '.join(cookie.name + '=' + cookie.value for cookie in client.cookies) for client in clients]
        assert self.command(op='connect', cookies=cookies)['connected'] == len(clients)

    def command(self, **command):
        self.process.stdin.write(json.dumps(command) + '\n')
        self.process.stdin.flush()
        result = json.loads(self.process.stdout.readline())
        assert 'error' not in result, result
        return result

    def close(self):
        try:
            if self.process.poll() is None:
                try:
                    self.command(op='close')
                except (BrokenPipeError, json.JSONDecodeError, AssertionError):
                    pass
                try:
                    self.process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    self.process.kill()
        finally:
            if self.process.poll() is None:
                self.process.kill()


def raw(client, path, data=None, headers=None):
    request = urllib.request.Request(BASE + path, data=None if data is None else urllib.parse.urlencode(data).encode(),
        headers={'Accept': 'application/json', **({'Content-Type': 'application/x-www-form-urlencoded'} if data is not None else {}), **(headers or {})})
    try:
        response = client.opener.open(request, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.headers, response.read().decode()


def main():
    prefix = 'enh8-' + uuid.uuid4().hex
    emails = [prefix + suffix + '@example.test' for suffix in ('-a', '-b', '-c')]
    clients = [Client(), Client(), Client()]
    a, b, outsider = clients
    guest, admin = Client(), Client()
    ids, tokens = [], []
    probe = None
    marker = '<img src=x onerror=enh8_xss()>'
    hostile = '<script>enh8_xss()</script>\n<img src=x onerror=enh8_xss()>\n**plain text**'
    try:
        asset = guest.request('/js/social/social.js')
        assert asset[0] == 200 and asset[2] == Path('wwwroot/js/social/social.js').read_text(encoding='utf-8')
        for index, (client, email) in enumerate(zip(clients, emails)):
            password = 'Aa1!' + secrets.token_urlsafe(24)
            assert is_redirect(client.form('/Account/Register', {'DisplayName': prefix + str(index), 'Email': email,
                'Password': password, 'ConfirmPassword': password}))
            ids.append(sql(f'SELECT Id FROM AspNetUsers WHERE Email={literal(email)}'))
            tokens.append(Inputs(client.request('/Friends')[2]).values['__RequestVerificationToken'])
        sql(f'UPDATE AspNetUsers SET DisplayName={literal(marker)} WHERE Id={literal(ids[0])}')
        settings = configuration()
        assert is_redirect(admin.form('/Account/Login', {'Email': settings['SeedAdmin:Email'], 'Password': settings['SeedAdmin:Password']}))
        admin_token = Inputs(admin.request('/Account/Profile')[2]).values['__RequestVerificationToken']

        def post(client, path, values, token=None):
            selected = tokens[clients.index(client)] if client in clients else admin_token
            return raw(client, path, {**values, '__RequestVerificationToken': token or selected})

        def friend(client, action, target):
            return post(client, '/Friends/' + action, {'targetUserId': target})

        def counts(client):
            response = raw(client, '/Social/Counts')
            assert response[0] == 200
            return json.loads(response[2])

        for route in ['/Friends', '/Messages', '/Messages/ThreadData?id=1', '/Social/Counts']:
            assert guest.request(route)[0] in (302, 401)
        for route in ['/Friends/Request', '/Friends/Accept', '/Friends/Decline', '/Friends/Cancel', '/Friends/Remove',
                      '/Friends/Block', '/Friends/Unblock', '/Messages/Open', '/Messages/Send', '/Messages/Read']:
            assert a.request(route)[0] in (404, 405), route
            assert raw(a, route, {'targetUserId': ids[1]})[0] == 400, route
        assert raw(guest, '/hubs/chat/negotiate?negotiateVersion=1', {})[0] in (302, 401)
        assert raw(a, '/hubs/chat/negotiate?negotiateVersion=1', {}, {'Origin': 'https://evil.example'})[0] == 403
        assert raw(a, '/hubs/chat/negotiate?negotiateVersion=1', {}, {'Sec-Fetch-Site': 'cross-site'})[0] == 403
        check(True, 'Authentication, POST-only, CSRF and hub same-origin restrictions hold')

        for culture in CULTURES:
            switch(guest, culture)
            response = guest.request('/Users/' + ids[0])
            assert response[0] == 200 and 'no-store' in response[1].get('Cache-Control', '')
            rendered = text(response[2])
            values = resources('SharedResource', culture)
            assert values['Social_PublicProfile'] in rendered and values['Social_Login'] in rendered
            assert marker not in response[2] and marker in rendered
            assert all(value not in response[2] for value in [*emails, 'PasswordHash', 'SecurityStamp', 'SourceCode', 'data-activity-heatmap', 'TaskFavorites'])
            assert all(value not in rendered for value in ids)
        assert 'data-social-state="Self"' in a.request('/Users/' + ids[0])[2]
        assert friend(a, 'Request', ids[0])[0] == 403 and friend(a, 'Block', ids[0])[0] == 403
        assert post(a, '/Messages/Open', {'targetUserId': ids[0]})[0] == 403
        assert post(a, '/Messages/Open', {'targetUserId': ids[1]})[0] == 403
        assert '/Users/' + ids[1] not in a.request('/Friends?section=search&q=' + urllib.parse.quote(emails[1]))[2]
        search = a.request('/Friends?section=search&q=' + prefix.upper())[2]
        assert '/Users/' + ids[1] in search and '/Users/' + ids[2] in search
        check(True, 'Three-language public profiles encode names, expose only safe summaries, and search by name only')

        probe = Probe(clients)
        assert is_redirect(post(a, '/Friends/Request', {'targetUserId': ids[1], 'UserId': ids[2], 'RequestedByUserId': ids[2]}))
        assert is_redirect(friend(a, 'Request', ids[1])) and is_redirect(friend(b, 'Request', ids[0]))
        assert sql(f"SELECT COUNT(*) FROM Friendships WHERE RequestedByUserId='{ids[0]}' AND Status=0") == '1'
        assert counts(b)['incomingRequests'] == 1 and counts(a)['incomingRequests'] == 0
        received = probe.command(op='wait', client=1, event='FriendRequestReceived', count=1)['events']
        assert not received[2]
        assert friend(a, 'Accept', ids[1])[0] == 403 and friend(outsider, 'Accept', ids[0])[0] == 403
        assert friend(a, 'Decline', ids[1])[0] == 403
        assert is_redirect(friend(b, 'Accept', ids[0]))
        probe.command(op='wait', client=0, event='FriendRequestAccepted', count=1)
        assert counts(b)['incomingRequests'] == 0
        assert 'data-social-state="Friends"' in a.request('/Users/' + ids[1])[2]
        opened = post(a, '/Messages/Open', {'targetUserId': ids[1]})
        assert is_redirect(opened)
        conversation = int(re.search(r'(\d+)$', opened[1]['Location'])[1])
        assert post(b, '/Messages/Open', {'targetUserId': ids[0]})[1]['Location'] == opened[1]['Location']
        check(True, 'Explicit recipient acceptance, reverse-duplicate prevention, lazy single conversation and real-time friend events work')

        client_id = str(uuid.uuid4())
        data = {'ConversationId': conversation, 'ClientMessageId': client_id, 'Body': hostile, 'SenderUserId': ids[2]}
        first = post(a, '/Messages/Send', data)
        assert first[0] == 200, first[0:1]
        message_id = json.loads(first[2])['id']
        assert json.loads(post(a, '/Messages/Send', data)[2])['id'] == message_id
        received = probe.command(op='wait', client=1, event='NewMessage', count=1)['events']
        probe.command(op='wait', client=0, event='NewMessage', count=1)
        assert not received[2]
        assert all('body' not in json.dumps(item).lower() for group in received for item in group)
        assert sql(f"SELECT COUNT(*) FROM DirectMessages WHERE ConversationId={conversation} AND SenderUserId='{ids[0]}'") == '1'
        for method in ['SendMessage', 'JoinGroup']:
            result = probe.command(op='invoke', client=2, id=method, method=method, args=['social-user:' + ids[0]])
            assert result['completion'].get('error')
        assert counts(b)['unreadMessages'] == 1 and counts(a)['unreadMessages'] == 0
        page = b.request('/Messages/Conversation/' + str(conversation))
        config = next(attrs for tag, attrs in Tags(page[2]).tags if 'data-social-config' in attrs)
        assert config['data-conversation-url'] == '/Messages/Conversation', 'Conversation URL must not inherit current route ID'
        assert page[0] == 200 and hostile not in page[2] and '<script>enh8_xss()' not in page[2]
        assert hostile in text(page[2]) and counts(b)['unreadMessages'] == 1
        assert post(a, '/Messages/Read', {'conversationId': conversation, 'throughId': message_id})[0] == 200
        assert counts(b)['unreadMessages'] == 1
        assert post(b, '/Messages/Read', {'conversationId': conversation, 'throughId': message_id})[0] == 200
        assert counts(b)['unreadMessages'] == 0
        check(True, 'Three real authenticated WebSockets: sender/recipient receive event, outsider gets none, DB row once; unread/read correct')

        for stranger in [outsider, admin]:
            for path in [f'/Messages/Conversation/{conversation}', f'/Messages/ThreadData?id={conversation}']:
                response = stranger.request(path)
                assert response[0] == 404 and 'enh8_xss' not in response[2]
            assert post(stranger, '/Messages/Read', {'conversationId': conversation, 'throughId': message_id})[0] == 404
            assert post(stranger, '/Messages/Send', {**data, 'ClientMessageId': str(uuid.uuid4())})[0] == 404
            assert 'enh8_xss' not in stranger.request('/Messages')[2]
        for path in ['/Admin/Users', '/Admin/Users/Details/' + ids[0], '/Users/' + ids[0], '/Leaderboard']:
            page = admin.request(path)[2]
            assert '**plain text**' not in page and '&lt;script&gt;enh8_xss' not in page
        check(True, 'Conversation IDOR and Admin bypass fail for history, JSON, sending and read mutations; private bodies stay out of profiles/Admin')

        for body in ['', ' \n\t', 'x' * 2001]:
            assert post(a, '/Messages/Send', {**data, 'Body': body, 'ClientMessageId': str(uuid.uuid4())})[0] == 400
        assert post(a, '/Messages/Send', {**data, 'Body': 'x' * 2000, 'ClientMessageId': str(uuid.uuid4())})[0] == 200
        for culture in CULTURES:
            switch(b, culture)
            for path in ['/Friends', '/Friends?section=incoming', '/Friends?section=outgoing', '/Friends?section=search',
                         '/Friends?section=blocked', '/Messages', '/Messages/Conversation/' + str(conversation)]:
                response = b.request(path)
                assert response[0] == 200 and 'Social_' not in response[2], (culture, path)
                assert "'unsafe-eval'" not in response[1].get('Content-Security-Policy', '')
                assert "script-src 'self'" in response[1].get('Content-Security-Policy', '')
        check(True, 'Plain-text XSS, 1–2000 length validation, all social pages in KZ/RU/EN and existing CSP pass')

        assert is_redirect(friend(a, 'Remove', ids[1]))
        assert post(a, '/Messages/Send', {**data, 'ClientMessageId': str(uuid.uuid4())})[0] == 403
        assert a.request('/Messages/Conversation/' + str(conversation))[0] == 200
        assert not json.loads(raw(a, f'/Messages/ThreadData?id={conversation}')[2])['canSend']
        assert is_redirect(friend(b, 'Request', ids[0])) and is_redirect(friend(a, 'Accept', ids[1]))
        assert post(a, '/Messages/Open', {'targetUserId': ids[1]})[1]['Location'] == opened[1]['Location']
        assert is_redirect(friend(b, 'Block', ids[0]))
        for client, target in [(a, ids[1]), (b, ids[0])]:
            assert friend(client, 'Request', target)[0] == 403
            assert post(client, '/Messages/Send', {**data, 'ClientMessageId': str(uuid.uuid4())})[0] == 403
        assert 'data-social-state="Unavailable"' in a.request('/Users/' + ids[1])[2]
        assert '/Users/' + ids[1] not in a.request('/Friends?section=search&q=' + prefix)[2]
        assert 'data-social-state="Blocked"' in b.request('/Friends?section=blocked')[2]
        assert is_redirect(friend(a, 'Unblock', ids[1]))
        assert sql(f"SELECT COUNT(*) FROM UserBlocks WHERE BlockerUserId='{ids[1]}'") == '1'
        assert is_redirect(friend(b, 'Unblock', ids[0]))
        assert sql(f"SELECT COUNT(*) FROM Friendships WHERE UserAId IN ('{ids[0]}','{ids[1]}') AND UserBId IN ('{ids[0]}','{ids[1]}')") == '0'
        assert post(a, '/Messages/Send', {**data, 'ClientMessageId': str(uuid.uuid4())})[0] == 403
        assert is_redirect(friend(b, 'Request', ids[0])) and is_redirect(friend(a, 'Decline', ids[1]))
        assert is_redirect(friend(b, 'Request', ids[0])) and is_redirect(friend(b, 'Cancel', ids[0]))
        check(True, 'Unfriend/block retain history, neutral blocking and own-only unblock work; friendship is never implicitly restored')

        # Fixed fixture rows exercise history pagination without bypassing the production POST limiter.
        sql(f";WITH n AS (SELECT TOP(55) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) n FROM sys.all_objects) INSERT DirectMessages(ConversationId,SenderUserId,ClientMessageId,Body,CreatedAt) SELECT {conversation},'{ids[0]}',NEWID(),CONCAT(N'Older fixture ',n),SYSUTCDATETIME() FROM n;")
        thread = json.loads(raw(b, f'/Messages/ThreadData?id={conversation}')[2])
        assert len(thread['messages']) == 50 and thread['hasOlder']
        before = thread['messages'][0]['id']
        older = b.request(f'/Messages/Conversation/{conversation}?beforeId={before}')[2]
        assert len([1 for tag, attrs in Tags(older).tags if 'data-message-id' in attrs]) == 7
        assert b.request(f'/Messages/Conversation/{conversation}?beforeId=9223372036854775807')[0] == 404
        assert is_redirect(friend(b, 'Request', ids[0])) and is_redirect(friend(a, 'Accept', ids[1]))
        # Close SignalR first: persistence must work without a single connected recipient.
        probe.close(); probe = None
        assert post(b, '/Messages/Send', {**data, 'Body': 'Offline transport still persists', 'ClientMessageId': str(uuid.uuid4())})[0] == 200
        assert 'Offline transport still persists' in a.request('/Messages/Conversation/' + str(conversation))[2]
        check(True, 'Stable latest/older history pagination and HTTP delivery with all hubs disconnected pass')

        # Exhaust only fixture user buckets; no real user's limiter is affected.
        responses = [post(b, '/Messages/Send', {**data, 'Body': 'Rate fixture', 'ClientMessageId': str(uuid.uuid4())}) for _ in range(31)]
        assert any(item[0] == 429 for item in responses)
        rejected = next(item for item in responses if item[0] == 429)
        assert rejected[1].get('Retry-After') == '60' and 'error' in json.loads(rejected[2])
        requests = [friend(outsider, 'Request', ids[0]) for _ in range(11)]
        assert any(item[0] == 429 for item in requests)
        check(True, 'Per-user 30/min message and 10/min friend-request limits return safe localized 429')
        print('ENHANCEMENT 8 HTTPS/SQL/REAL SIGNALR CHECKS PASSED. Paid AI calls: 0. Browser rendering not claimed.')
    finally:
        if probe:
            probe.close()
        where = 'Email IN (' + ','.join(literal(email) for email in emails) + ')'
        scope = 'SELECT Id FROM AspNetUsers WHERE ' + where
        sql(f"DELETE DirectConversations WHERE UserAId IN ({scope}) OR UserBId IN ({scope}); DELETE Friendships WHERE UserAId IN ({scope}) OR UserBId IN ({scope}); DELETE UserBlocks WHERE BlockerUserId IN ({scope}) OR BlockedUserId IN ({scope}); DELETE Leaderboards WHERE UserId IN ({scope}); DELETE AspNetUsers WHERE {where};")
        assert sql(f'SELECT COUNT(*) FROM AspNetUsers WHERE {where}') == '0'
        print('Removed only this run\'s temporary social users and dependent rows.')


if __name__ == '__main__':
    main()
