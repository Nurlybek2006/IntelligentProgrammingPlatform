"""Lessons regression over real HTTPS/SQL. Uses isolated fixtures; no code or paid AI runs.

Run with the Development app listening on https://localhost:7115 after AddLessons.
Checks HTTP semantics, encoding, resources and database constraints, not browser rendering.
"""
import secrets
import uuid
from verify_phase2 import Client, check, is_redirect, sql
from verify_enhancement1 import Tags
from verify_enhancement2 import snapshot
from verify_enhancement3_migration import literal
from verify_enhancement4 import page, switch, text
from verify_resources import CULTURES


MARKER = '</textarea></code></pre><script>lesson_marker()</script><img src=x onerror=lesson_marker()>'
SEEDS = ('programming-basics', 'cpp-basics', 'python-basics', 'arrays-basics', 'algorithm-basics')


def links(body):
    return [attrs.get('href', '') for tag, attrs in Tags(body).tags if tag == 'a']


def count(prefix):
    return int(sql(f"SELECT COUNT(*) FROM Lessons WHERE Slug LIKE '{prefix}%';"))


def constraint(query, expected):
    # Roll back even if the supposedly forbidden statement unexpectedly succeeds.
    result = sql(f"BEGIN TRY BEGIN TRANSACTION; {query}; ROLLBACK; SELECT 0; END TRY "
                 "BEGIN CATCH IF @@TRANCOUNT > 0 ROLLBACK; SELECT ERROR_NUMBER(); END CATCH;")
    assert int(result) in expected, ('Expected database constraint', result)


def main():
    prefix = 'enh5a-' + uuid.uuid4().hex
    emails = [prefix + f'-{index}@example.test' for index in range(2)]
    password = 'Aa1!' + secrets.token_urlsafe(24)
    guest, student, admin = Client(), Client(), Client()
    try:
        seeds = ','.join(literal(slug) for slug in SEEDS)
        assert sql(f"SELECT COUNT(*) FROM Lessons WHERE Slug IN ({seeds}) AND IsPublished=1 AND LEN(Content)>500 AND LEN(CodeExample)>40;") == '5'
        page(guest.request('/Lessons'), 'en-US', ('Student', 'Lessons_Title'))
        for slug in SEEDS:
            body = page(guest.request('/Lessons/' + slug), 'en-US', ('Student', 'Lessons_RelatedTasks'))
            assert any(link.startswith('/Tasks/') for link in links(body))
        assert guest.request('/Lessons/absent-' + prefix)[0] == 404
        check(True, 'Five useful published seeds have related task links; lesson examples remain display-only')

        for client, email in ((student, emails[0]), (admin, emails[1])):
            assert is_redirect(client.form('/Account/Register', {'DisplayName': prefix, 'Email': email,
                'Password': password, 'ConfirmPassword': password}))
        administrator = sql(f"SELECT Id FROM AspNetUsers WHERE Email='{emails[1]}';")
        sql(f"INSERT INTO AspNetUserRoles(UserId,RoleId) SELECT '{administrator}',Id FROM AspNetRoles WHERE Name='Admin';")
        assert is_redirect(admin.form('/Account/Login', {'Email': emails[1], 'Password': password}))
        topic = int(sql(f"INSERT INTO Topics(Name,Description) VALUES('{prefix}','Lessons fixture'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        other_topic = int(sql(f"INSERT INTO Topics(Name,Description) VALUES('{prefix}-other','Other fixture'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        lesson_only_topic = int(sql(f"INSERT INTO Topics(Name,Description) VALUES('{prefix}-protected','No tasks'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        data = {'Title': 'Lesson ' + MARKER, 'Slug': prefix + '-first', 'Summary': 'Summary ' + MARKER,
                'Content': 'Theory\n    preserved indentation\n' + MARKER,
                'CodeExample': '#include <iostream>\n' + MARKER, 'CodeLanguage': 'cpp',
                'TopicId': topic, 'Order': 1, 'IsPublished': 'true'}

        for client in (guest, student):
            for path in ('/Admin/Lessons', '/Admin/Lessons/Create', '/Admin/Lessons/Details/1',
                         '/Admin/Lessons/Edit/1', '/Admin/Lessons/Delete/1'):
                response = client.request(path)
                assert is_redirect(response) or response[0] == 403
            for path in ('/Admin/Lessons/Create', '/Admin/Lessons/Edit/1', '/Admin/Lessons/Delete/1'):
                assert client.form(path, data, token_path='/')[0] in (302, 403)
        assert count(prefix) == 0
        assert admin.request('/Admin/Lessons/Create', data)[0] == 400
        created = admin.form('/Admin/Lessons/Create', {**data, 'CreatedAt': '1900-01-01', 'Id': 2147483647})
        assert is_redirect(created), created[0]
        first = int(created[1]['Location'].rsplit('/', 1)[1])
        created_at = sql(f'SELECT CONVERT(varchar(33),CreatedAt,126) FROM Lessons WHERE Id={first};')
        assert sql(f'SELECT CASE WHEN ABS(DATEDIFF(second,CreatedAt,SYSUTCDATETIME()))<120 THEN 1 ELSE 0 END FROM Lessons WHERE Id={first};') == '1'
        assert first != 2147483647
        for action in ('Edit', 'Delete'):
            assert admin.request(f'/Admin/Lessons/{action}/{first}', data)[0] == 400
        check(True, 'Admin role, antiforgery and allowlisted form fields protect all mutations; CreatedAt is server assigned')

        invalid_cases = [
            ({'Slug': 'Upper-Case'}, 'Validation_LessonSlug', ()),
            ({'Slug': '../unsafe'}, 'Validation_LessonSlug', ()),
            ({'Slug': 'two--hyphens'}, 'Validation_LessonSlug', ()),
            ({'Slug': '-leading'}, 'Validation_LessonSlug', ()),
            ({'Slug': 'trailing-'}, 'Validation_LessonSlug', ()),
            ({'Slug': data['Slug']}, 'Lesson_DuplicateSlug', ()),
            ({'Order': 1}, 'Lesson_DuplicateOrder', ()),
            ({'Order': 0}, 'Validation_Range', ('Field_Order', 1, 2147483647)),
            ({'TopicId': 2147483647}, 'Validation_Topic', ()),
            ({'CodeLanguage': 'sh; arbitrary-command'}, 'Validation_CodeLanguage', ()),
        ]
        for culture in CULTURES:
            switch(admin, culture, '/Admin/Lessons')
            for path in ('/Admin/Lessons', '/Admin/Lessons/Create', f'/Admin/Lessons/Details/{first}',
                         f'/Admin/Lessons/Edit/{first}', f'/Admin/Lessons/Delete/{first}'):
                response = admin.request(path)
                page(response, culture)
                assert MARKER not in response[2], 'Admin text and textarea content must be encoded'
            for override, key, args in invalid_cases:
                values = {**data, 'Slug': prefix + '-invalid', 'Order': 10, **override}
                resolved = [text('Admin', culture, arg) if isinstance(arg, str) and arg.startswith('Field_') else arg for arg in args]
                page(admin.form('/Admin/Lessons/Create', values), culture, ('Admin', key, *resolved))
            for field, label, limit in (('Title', 'Field_Title', 200), ('Slug', 'Field_Slug', 200),
                    ('Summary', 'Field_LessonSummary', 1000), ('Content', 'Field_LessonContent', 50000),
                    ('CodeExample', 'Field_CodeExample', 16000)):
                label_text = text('Admin', culture, label)
                values = {**data, 'Slug': prefix + '-invalid', 'Order': 10, field: 'x' * (limit + 1)}
                page(admin.form('/Admin/Lessons/Create', values), culture, ('Admin', 'Validation_StringLength', label_text, limit))
                if field != 'CodeExample':
                    page(admin.form('/Admin/Lessons/Create', {**data, 'Order': 10, field: ' '}), culture,
                         ('Admin', 'Validation_Required', label_text))
            assert count(prefix) == 1
            check(True, culture + ': CRUD UI, required/length/slug/order/topic/language validation are localized')
        switch(admin, 'en-US')
        assert admin.form(f'/Admin/Lessons/Edit/{first}', {**data, 'Order': 0})[0] == 200
        assert sql(f'SELECT [Order] FROM Lessons WHERE Id={first};') == '1'

        # Deliberately insert out of order to ensure neighbors use Order, not insertion order.
        identifiers = {}
        for name, order, published, parent in [('last', 4, 1, topic), ('draft', 2, 0, topic),
                ('middle', 3, 1, topic), ('other', 1, 1, other_topic), ('protected', 1, 1, lesson_only_topic)]:
            result = admin.form('/Admin/Lessons/Create', {**data, 'Slug': prefix + '-' + name,
                'Title': name + ' lesson', 'Order': order, 'IsPublished': str(bool(published)).lower(),
                'TopicId': parent, 'CodeExample': '', 'CodeLanguage': ''})
            assert is_redirect(result), (name, result[0])
            identifiers[name] = int(result[1]['Location'].rsplit('/', 1)[1])
        draft_path = '/Lessons/' + prefix + '-draft'
        for client in (guest, student, admin):
            assert client.request(draft_path)[0] == 404
        for i in range(5):
            task = int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Related {i}','{prefix}-task-{i}','Task fixture',0,{topic},500,32,{1 if i<4 else 0},SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);"))
            sql(f"INSERT INTO TestCases(ProgrammingTaskId,Input,ExpectedOutput,IsHidden,[Order]) VALUES({task},'LESSON_HIDDEN_INPUT','LESSON_HIDDEN_OUTPUT',1,1);")
        sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Other task','{prefix}-task-other','Task fixture',0,{other_topic},500,32,1,SYSUTCDATETIME());")

        student.request('/Leaderboard')  # Materialize summaries before checking read-only lesson requests.
        unchanged = snapshot()
        stored = sql(f"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM Lessons WHERE Slug LIKE '{prefix}%' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)")
        for culture in CULTURES:
            switch(guest, culture, '/Lessons')
            catalog = page(guest.request(f'/Lessons?topicId={topic}'), culture, ('Student', 'Lessons_Title'))
            catalog_links = links(catalog)
            assert draft_path not in catalog_links and '/Lessons/' + prefix + '-other' not in catalog_links
            assert catalog_links.index('/Lessons/' + prefix + '-middle') < catalog_links.index('/Lessons/' + prefix + '-last')
            assert any(attrs.get('value') == str(topic) and 'selected' in attrs for tag, attrs in Tags(catalog).tags if tag == 'option')
            response = guest.request('/Lessons/' + data['Slug'])
            body = page(response, culture, ('Student', 'Lessons_Theory'))
            assert MARKER in body and MARKER not in response[2]
            assert not any(tag in ('script', 'img') and ('lesson_marker' in str(attrs) or attrs.get('src') == 'x') for tag, attrs in Tags(response[2]).tags)
            assert '<script>lesson_marker()' not in response[2] and '<pre class="source-code" tabindex="0"' in response[2]
            assert 'Theory\n    preserved indentation\n' in body
            related = [link for link in links(body) if link.startswith('/Tasks/' + prefix)]
            assert related == ['/Tasks/' + prefix + f'-task-{i}' for i in range(3)], related
            assert 'LESSON_HIDDEN_' not in body
            assert '/Lessons/' + prefix + '-middle' in links(body) and draft_path not in links(body)
            middle = page(guest.request('/Lessons/' + prefix + '-middle'), culture)
            assert '/Lessons/' + data['Slug'] in links(middle) and '/Lessons/' + prefix + '-last' in links(middle)
            last = page(guest.request('/Lessons/' + prefix + '-last'), culture)
            assert '/Lessons/' + prefix + '-middle' in links(last) and text('Student', culture, 'Lessons_Next') not in last
            page(guest.request('/Lessons/' + prefix + '-protected'), culture, ('Student', 'Lessons_NoTasks'))
            page(guest.request('/Lessons?topicId=2147483647'), culture, ('Student', 'Lessons_Empty'))
            check(True, culture + ': published catalog/filter, safe text/code, same-topic ordered neighbors and three safe related tasks')
        assert snapshot() == unchanged
        assert stored == sql(f"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM Lessons WHERE Slug LIKE '{prefix}%' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2)")
        check(True, 'Reading and language switching preserve lesson bytes, submissions, results, AI and leaderboard data')

        assert is_redirect(admin.form(f'/Admin/Lessons/Edit/{first}', {**data, 'IsPublished': 'false', 'CreatedAt': '1900-01-01'}))
        assert guest.request('/Lessons/' + data['Slug'])[0] == 404
        assert sql(f'SELECT CONVERT(varchar(33),CreatedAt,126) FROM Lessons WHERE Id={first};') == created_at
        assert is_redirect(admin.form(f'/Admin/Lessons/Edit/{first}', {**data, 'IsPublished': 'true'}))
        assert guest.request('/Lessons/' + data['Slug'])[0] == 200
        page(admin.form(f'/Admin/Topics/Delete/{lesson_only_topic}', {}), 'en-US', ('Admin', 'Topic_HasLessons'))
        assert sql(f'SELECT COUNT(*) FROM Topics WHERE Id={lesson_only_topic};') == '1'
        constraint(f'DELETE FROM Topics WHERE Id={lesson_only_topic}', {547})
        constraint(f'UPDATE Lessons SET [Order]=0 WHERE Id={first}', {547})
        constraint(f"UPDATE Lessons SET Slug={literal(data['Slug'])} WHERE Id={identifiers['last']}", {2601, 2627})
        constraint(f"UPDATE Lessons SET [Order]=1 WHERE Id={identifiers['last']}", {2601, 2627})
        constraint(f'UPDATE Lessons SET TopicId=2147483647 WHERE Id={first}', {547})
        page(admin.form(f"/Admin/Lessons/Edit/{identifiers['last']}", data), 'en-US', ('Admin', 'Lesson_DuplicateSlug'))
        page(admin.form(f"/Admin/Lessons/Edit/{identifiers['last']}", {**data, 'Slug': prefix + '-last'}),
             'en-US', ('Admin', 'Lesson_DuplicateOrder'))
        moved = {**data, 'Title': 'Moved lesson', 'Slug': prefix + '-moved', 'TopicId': other_topic, 'Order': 2,
                 'Summary': 'Updated summary', 'Content': 'Updated theory\n  indentation',
                 'CodeLanguage': 'python', 'CodeExample': 'print(42)', 'IsPublished': 'true'}
        assert is_redirect(admin.form(f'/Admin/Lessons/Edit/{first}', moved))
        assert guest.request('/Lessons/' + data['Slug'])[0] == 404
        moved_body = page(guest.request('/Lessons/' + moved['Slug']), 'en-US')
        assert 'Moved lesson' in moved_body and 'Updated theory\n  indentation' in moved_body and 'print(42)' in moved_body
        assert '/Lessons/' + prefix + '-other' in links(moved_body) and '/Lessons/' + prefix + '-middle' not in links(moved_body)
        assert '/Tasks/' + prefix + '-task-other' in links(moved_body)
        assert sql(f'SELECT CONVERT(varchar(33),CreatedAt,126) FROM Lessons WHERE Id={first};') == created_at
        assert admin.request(f"/Admin/Lessons/Delete/{identifiers['protected']}")[0] == 200
        assert sql(f"SELECT COUNT(*) FROM Lessons WHERE Id={identifiers['protected']};") == '1'
        assert is_redirect(admin.form(f"/Admin/Lessons/Delete/{identifiers['protected']}", {}))
        assert is_redirect(admin.form(f'/Admin/Topics/Delete/{lesson_only_topic}', {}))
        for action in ('Details', 'Edit', 'Delete'):
            assert admin.request(f'/Admin/Lessons/{action}/2147483647')[0] == 404
        check(True, 'Publishing/editing/deleting work; GET never deletes; topic FK, unique slug/order and positive order hold in SQL')
        print('ENHANCEMENT 5A HTTPS/SQL CHECKS PASSED. No execution, paid AI calls or browser claims.', flush=True)
    finally:
        addresses = ','.join(literal(email) for email in emails)
        sql(f"DELETE FROM Lessons WHERE Slug LIKE '{prefix}%'; "
            f"DELETE t FROM TestCases t JOIN ProgrammingTasks p ON p.Id=t.ProgrammingTaskId WHERE p.Slug LIKE '{prefix}%'; "
            f"DELETE FROM ProgrammingTasks WHERE Slug LIKE '{prefix}%'; DELETE FROM Topics WHERE Name LIKE '{prefix}%'; "
            f"DELETE l FROM Leaderboards l JOIN AspNetUsers u ON u.Id=l.UserId WHERE u.Email IN ({addresses}); "
            f"DELETE FROM AspNetUsers WHERE Email IN ({addresses});")
        print('Removed only this run\'s Lessons fixture data.', flush=True)


if __name__ == '__main__':
    main()
