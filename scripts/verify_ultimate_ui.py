"""Inspect real rendered Razor, secure culture POST, local assets and role menus.

No browser rendering, database fixtures, AI calls or content mutations. Admin
credentials stay in memory, as in the existing regression suites.
"""
import html
import re
from pathlib import Path
from verify_phase2 import Client, check, is_redirect
from verify_enhancement1 import Tags, inspect
from verify_enhancement4 import switch
from verify_enhancement7 import configuration
from verify_resources import CULTURES, resources


def main():
    guest, admin = Client(), Client()
    for culture in CULTURES:
        switch(guest, culture, '/Lessons?language=python')
        body = inspect(guest.request('/Lessons?language=python'))
        tags = Tags(body).tags
        buttons = [attrs for tag, attrs in tags if tag == 'button' and attrs.get('name') == 'culture']
        assert {attrs['value'] for attrs in buttons} == set(CULTURES)
        assert [attrs['value'] for attrs in buttons if attrs.get('aria-current')] == [culture]
        assert all(attrs.get('type') == 'submit' for attrs in buttons)
        assert re.search(r'<form[^>]+method="post"[^>]*action="/Culture/Set"|<form[^>]+action="/Culture/Set"[^>]*method="post"', body)
        assert 'Culture_Apply' not in body and 'culture-choice' not in body
        assert 'class="lesson-roadmap"' in body
        check(True, culture + ': localized culture submit buttons, current language and connected roadmap markup')
    assert guest.request('/Culture/Set?culture=en-US')[0] in (404, 405)
    assert guest.request('/Culture/Set', {'culture': 'en-US', 'returnUrl': '/'})[0] == 400
    bad = guest.form('/Culture/Set', {'culture': 'bad-culture', 'returnUrl': 'https://evil.example'}, token_path='/')
    assert bad[0] == 400
    external = guest.form('/Culture/Set', {'culture': 'kk-KZ', 'returnUrl': 'https://evil.example'}, token_path='/')
    assert is_redirect(external) and not external[1]['Location'].startswith('http')
    check(True, 'Language changes remain POST-only, antiforgery protected, allowlisted and local-return-only')
    for path in ['/', '/Lessons', '/Tasks', '/Leaderboard', '/Account/Login', '/Account/Register', '/Home/Privacy']:
        body = inspect(guest.request(path))
        for css in ['site', 'layout', 'components', 'learning', 'workspace', 'social']:
            assert f'/css/{css}.css?v=' in body
        assert '<style ' not in body
        assert 'data-nav-toggle' in body and 'nav-utilities' in body
    for asset in ['css/site.css', 'css/layout.css', 'css/components.css', 'css/learning.css', 'css/workspace.css', 'css/social.css', 'js/site.js', 'js/workspace.js']:
        response = guest.request('/' + asset)
        assert response[0] == 200
        assert response[2].lstrip('\ufeff').replace('\r\n', '\n') == Path('wwwroot', asset).read_text(encoding='utf-8-sig'), asset
    check(True, 'All redesigned public pages use strict CSP and serve the exact local CSS/JS files')
    settings = configuration()
    assert is_redirect(admin.form('/Account/Login', {'Email': settings['SeedAdmin:Email'], 'Password': settings['SeedAdmin:Password']}))
    try:
        for culture in CULTURES:
            switch(admin, culture)
            shared = resources('SharedResource', culture)
            home = html.unescape(inspect(admin.request('/')))
            assert 'dashboard-metrics' in home and 'dashboard-next' in home and shared['Ui_ContinueLearning'] in home
            assert 'class="hero"' not in home
            assert 'user-control' in home and 'community-nav' in home and '/Admin' in home and '/Favorites' in home
            assert 'data-social-count="unreadMessages"' in home
            for path in ['/Progress', '/Account/Profile', '/Friends', '/Messages', '/Admin', '/Admin/Users', '/Admin/Topics', '/Admin/Lessons', '/Admin/ProgrammingTasks', '/Admin/TestCases']:
                inspect(admin.request(path))
            task = inspect(admin.request('/Tasks/sum-of-two-numbers'), editor=True)
            assert 'workspace-shell' in task and '/js/workspace.js?v=' in task
            assert task.index('class="panel problem-pane"') < task.index('<h1>') < task.index('class="panel editor-panel solution-pane"')
            assert 'data-workspace-tabs' in task and 'id="custom-input-panel"' in task
        check(True, 'Authenticated dashboard, menu roles, progress XP, task workspace and all Admin screens render in three cultures')
    finally:
        assert is_redirect(admin.form('/Account/Logout', {}, token_path='/'))
    print('ULTIMATE UI HTTPS CHECKS PASSED. Paid AI calls: 0. Browser screenshots: 0.')


if __name__ == '__main__':
    main()
