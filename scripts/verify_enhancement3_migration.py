"""Upgrade-only legacy-feedback fixtures. No database reset and no AI calls.

Run --prepare before applying AddProgressiveHintReveal, then --verify afterward.
--verify removes the exact fixture scope in finally; --cleanup recovers an interrupted run.
"""
import json
import sys
import uuid
from pathlib import Path
from verify_phase2 import sql, check

STATE = Path(__file__).resolve().parents[1] / 'obj' / 'enhancement3-migration-fixture.json'


def literal(value):
    # sqlcmd -Q on Windows: keep literal JSON quotes out of the process argument.
    return "N'" + value.replace("'", "''").replace('"', "'+NCHAR(34)+N'") + "'"


def snapshot(owner):
    return sql("SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(" +
        "SELECT Id,SubmissionId,UserId,Model,Summary,Explanation,HintsJson,ErrorCategory,CreatedAt,InputTokens,OutputTokens " +
        f"FROM AiFeedbacks WHERE UserId='{owner}' ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),2);")


def cleanup(state):
    owner = state['owner']
    assert owner.startswith('enh3-migration-') and len(owner) == len('enh3-migration-') + 32
    sql(f"DELETE FROM Submissions WHERE UserId='{owner}'; DELETE FROM Leaderboards WHERE UserId='{owner}'; "
        f"DELETE FROM ProgrammingTasks WHERE Slug='{owner}'; DELETE FROM Topics WHERE Name='{owner}'; "
        f"DELETE FROM AspNetUsers WHERE Id='{owner}';")
    STATE.unlink(missing_ok=True)


def prepare():
    assert not STATE.exists(), 'Recover the previous migration fixture with --cleanup first.'
    assert sql("SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('AiFeedbacks') AND name='RevealedHintCount';") == '0'
    owner = 'enh3-migration-' + uuid.uuid4().hex
    state = {'owner': owner, 'feedback': []}
    STATE.write_text(json.dumps(state), encoding='utf-8')
    try:
        sql(f"INSERT INTO AspNetUsers(Id,UserName,DisplayName,CreatedAt,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount) VALUES('{owner}','{owner}','Migration fixture',SYSUTCDATETIME(),0,0,0,0,0);")
        topic = int(sql(f"INSERT INTO Topics(Name) VALUES('{owner}'); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        task = int(sql(f"INSERT INTO ProgrammingTasks(Title,Slug,Description,Difficulty,TopicId,TimeLimitMs,MemoryLimitMb,IsPublished,CreatedAt) VALUES('Migration fixture','{owner}','Temporary migration test',0,{topic},500,32,0,SYSUTCDATETIME()); SELECT CAST(SCOPE_IDENTITY() AS int);"))
        runtime = int(sql("SELECT Id FROM Runtimes WHERE LanguageKey='cpp';"))
        for hints, expected in [('[]', 0), ('["legacy one"]', 1), ('["legacy one","legacy two"]', 1),
                                ('["legacy one","legacy two","legacy three"]', 1), ('{broken', 0), ('{}', 0), ('[null]', 0), ('[""]', 0)]:
            submission = int(sql(f"INSERT INTO Submissions(UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,FinishedAt,PassedTests,TotalTests) VALUES('{owner}',{task},{runtime},'// migration fixture',4,SYSUTCDATETIME(),SYSUTCDATETIME(),0,0); SELECT CAST(SCOPE_IDENTITY() AS bigint);"))
            feedback = int(sql(f"INSERT INTO AiFeedbacks(SubmissionId,UserId,Model,Summary,Explanation,HintsJson,ErrorCategory,CreatedAt,InputTokens,OutputTokens) VALUES({submission},'{owner}','legacy-offline-fixture','Legacy summary','Legacy explanation',{literal(hints)},'Logic',SYSUTCDATETIME(),11,22); SELECT CAST(SCOPE_IDENTITY() AS bigint);"))
            state['feedback'].append([feedback, expected])
        state['hash'] = snapshot(owner)
        STATE.write_text(json.dumps(state), encoding='utf-8')
        check(True, 'Eight isolated legacy rows prepared before migration; no real feedback changed')
    except Exception:
        cleanup(state)
        raise


def verify():
    state = json.loads(STATE.read_text(encoding='utf-8'))
    try:
        check(snapshot(state['owner']) == state['hash'], 'Migration preserves every existing feedback field and row')
        for feedback, expected in state['feedback']:
            check(sql(f'SELECT RevealedHintCount FROM AiFeedbacks WHERE Id={feedback};') == str(expected),
                  'Legacy reveal state matches available hints')
        check(sql("SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddProgressiveHintReveal';") == '1',
              'AddProgressiveHintReveal applied exactly once')
        print('ENHANCEMENT 3 MIGRATION CHECKS PASSED')
    finally:
        cleanup(state)
        print('Removed only this migration fixture.')


if __name__ == '__main__':
    if sys.argv[1:] == ['--prepare']:
        prepare()
    elif sys.argv[1:] == ['--verify']:
        verify()
    elif sys.argv[1:] == ['--cleanup']:
        cleanup(json.loads(STATE.read_text(encoding='utf-8')))
    else:
        raise SystemExit('Use --prepare before migration, --verify after migration, or --cleanup for recovery.')
