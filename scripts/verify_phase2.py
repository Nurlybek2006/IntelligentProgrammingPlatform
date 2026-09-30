"""Exercise the running Phase 2 app over HTTPS with real cookies and antiforgery tokens.

Run from the project root after dotnet run --launch-profile https.
Uses only the Python standard library and sqlcmd. Reads the admin password from
.NET User Secrets in memory; never prints or copies it. All test-created database
rows have a unique run ID and are removed in finally. Student code is never run.
"""
import html
import http.cookiejar
import json
import os
import re
import secrets
import ssl
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "https://localhost:7115"
DATABASE = "IntelligentProgrammingPlatformDb"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Inputs(HTMLParser):
    def __init__(self, source):
        super().__init__()
        self.values = {}
        self.feed(source)

    def handle_starttag(self, tag, attrs):
        fields = dict(attrs)
        if tag == "input" and fields.get("name"):
            self.values[fields["name"]] = fields.get("value", "")


class Client:
    def __init__(self):
        self.cookies = http.cookiejar.CookieJar()
        # Only the loopback development certificate is accepted without CA validation.
        context = ssl._create_unverified_context()
        self.opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}), NoRedirect(),
            urllib.request.HTTPCookieProcessor(self.cookies),
            urllib.request.HTTPSHandler(context=context))

    def request(self, path, data=None):
        assert path.startswith("/") and not path.startswith("//")
        request = urllib.request.Request(
            BASE + path,
            data=None if data is None else urllib.parse.urlencode(data).encode(),
            headers={"Content-Type": "application/x-www-form-urlencoded"} if data is not None else {})
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            return response.status, response.headers, response.read().decode("utf-8")

    def form(self, path, data, token_path=None):
        status, _, body = self.request(token_path or path)
        assert status == 200, f"Form GET failed: {token_path or path}: {status}"
        token = Inputs(body).values.get("__RequestVerificationToken")
        assert token, f"Missing antiforgery token: {path}"
        return self.request(path, {**data, "__RequestVerificationToken": token})


def sql(query):
    result = subprocess.run(
        ["sqlcmd", "-S", "localhost", "-E", "-C", "-I", "-d", DATABASE,
         "-b", "-h", "-1", "-W", "-Q", "SET NOCOUNT ON; SET XACT_ABORT ON; " + query],
        capture_output=True, text=True, check=False)
    if result.returncode:
        # These fixture queries never contain or select credentials.
        raise RuntimeError("Database verification query failed: " + (result.stderr or result.stdout).strip())
    return result.stdout.strip()


def check(condition, label):
    if not condition:
        raise AssertionError(label)
    print("PASS: " + label, flush=True)


def is_redirect(response):
    return response[0] in (302, 303)


def main():
    project = ET.parse(ROOT / "IntelligentProgrammingPlatform.csproj")
    secret_id = project.findtext(".//UserSecretsId")
    secret_path = Path(os.environ["APPDATA"]) / "Microsoft" / "UserSecrets" / secret_id / "secrets.json"
    configuration = json.loads(secret_path.read_text(encoding="utf-8-sig"))
    admin_email = configuration["SeedAdmin:Email"]
    admin_password = configuration["SeedAdmin:Password"]

    run_id = uuid.uuid4().hex
    email = "phase2-" + run_id + "@example.test"
    password = "Aa1!" + secrets.token_urlsafe(24)
    topic_name = "Phase2 verification " + run_id
    slug = "phase2-" + run_id
    runtime_key = "check-" + run_id
    anon, student, admin = Client(), Client(), Client()
    try:
        status, _, body = anon.request("/Tasks")
        check(status == 200 and all(title in body for title in
              ["Sum of Two Numbers", "Maximum in Array", "Fibonacci Number"]),
              "Anonymous catalog lists all three published demo tasks")
        check(sql("SELECT COUNT(*) FROM AspNetRoles WHERE Name IN ('Student','Admin');") == "2",
              "Student and Admin roles exist")
        check(sql("SELECT COUNT(*) FROM ProgrammingTasks WHERE Slug IN ('sum-of-two-numbers','maximum-in-array','fibonacci-number');") == "3"
              and sql("SELECT COUNT(*) FROM Topics WHERE Name IN ('Basics','Arrays','Algorithms');") == "3"
              and sql("SELECT COUNT(*) FROM TestCases t JOIN ProgrammingTasks p ON p.Id=t.ProgrammingTaskId WHERE p.Slug IN ('sum-of-two-numbers','maximum-in-array','fibonacci-number');") == "8",
              "Restarted development seed has no duplicate demo rows")
        for task_slug, visible, hidden in [
            ("sum-of-two-numbers", "2 3", ["123456", "654321", "777777"]),
            ("maximum-in-array", "1 9 3 2 4", ["-17", "-29", "-41"]),
            ("fibonacci-number", "13", ["24157817"])]:
            status, _, body = anon.request("/Tasks/" + task_slug)
            check(status == 200 and visible in body and all(value not in body for value in hidden),
                  "Visible examples present and hidden data absent from full HTML: " + task_slug)
        response = anon.request("/Admin")
        check(is_redirect(response) and "/Account/Login" in response[1]["Location"],
              "Anonymous Admin request requires login")
        check(is_redirect(anon.request("/Account/Profile")), "Anonymous profile requires login")
        check(anon.request("/Account/Register", {})[0] == 400, "Registration rejects a missing antiforgery token")

        registration = {"DisplayName": "Verification Student", "Email": email,
                        "Password": password, "ConfirmPassword": password, "Role": "Admin",
                        "CreatedAt": "2000-01-01", "PasswordHash": "overpost"}
        response = student.form("/Account/Register", registration)
        check(is_redirect(response) and "/Account/Profile" in response[1]["Location"],
              "Registration succeeds and signs in")
        status, _, body = student.request("/Account/Profile")
        check(status == 200 and "Verification Student" in body and email in body and "Student" in body,
              "Authenticated profile displays display name, email, date, and role")
        check(sql(f"SELECT r.Name FROM AspNetUserRoles ur JOIN AspNetUsers u ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.Email='{email}';") == "Student",
              "Registration always assigns Student and ignores posted Admin role")
        check(sql(f"SELECT COUNT(*) FROM AspNetUsers WHERE Email='{email}' AND CreatedAt>'2020-01-01' AND PasswordHash<>'overpost' AND PasswordHash IS NOT NULL;") == "1",
              "Registration uses server timestamp and Identity password storage")

        for path in ["/Admin", "/Admin/Topics", "/Admin/ProgrammingTasks", "/Admin/TestCases"]:
            response = student.request(path)
            check(is_redirect(response) and "/Account/AccessDenied" in response[1]["Location"],
                  "Student denied server-side: " + path)
        check(student.request("/Account/AccessDenied")[0] == 403, "AccessDenied page returns HTTP 403")
        response = student.form("/Admin/Topics/Create", {"Name": "Unauthorized"}, token_path="/Account/Profile")
        check(is_redirect(response) and "/Account/AccessDenied" in response[1]["Location"],
              "Student cannot bypass Admin protection with a direct POST")
        check(student.request("/Account/Logout")[0] in (404, 405), "Logout is not available via GET")
        check(student.request("/Account/Logout", {})[0] == 400, "Logout requires antiforgery token")
        check(is_redirect(student.form("/Account/Logout", {}, token_path="/Account/Profile")),
              "POST logout succeeds")
        check(is_redirect(student.request("/Account/Profile")), "Logout removes authenticated profile access")
        response = student.form("/Account/Register", registration)
        check(response[0] == 200 and "already registered" in response[2], "Duplicate email gives a friendly validation error")

        response = student.form("/Account/Login", {"Email": email, "Password": "incorrect"})
        check(response[0] == 200 and "Invalid email or password." in response[2], "Wrong password gives a generic login error")
        response = anon.form("/Account/Login", {"Email": "missing-" + email, "Password": "incorrect"})
        check(response[0] == 200 and "Invalid email or password." in response[2], "Unknown email gives the same generic login error")
        response = student.form("/Account/Login?returnUrl=https%3A%2F%2Fexample.com",
                                {"Email": email, "Password": password, "RememberMe": "true"})
        check(is_redirect(response) and response[1]["Location"] == "/Tasks", "Login succeeds and external returnUrl is rejected")
        check(any(cookie.name == ".AspNetCore.Identity.Application" and cookie.secure and cookie.expires
                  for cookie in student.cookies), "RememberMe issues a persistent secure Identity cookie over HTTPS")
        response = admin.form("/Account/Login", {"Email": admin_email, "Password": admin_password})
        check(is_redirect(response) and admin.request("/Admin")[0] == 200, "Configured development admin logs in and accesses Admin")
        check("Admin" in admin.request("/Account/Profile")[2], "Admin profile displays its role")
        check(admin.request("/Admin/Topics/Create", {"Name": topic_name})[0] == 400,
              "Admin CRUD rejects a missing antiforgery token")

        response = admin.form("/Admin/Topics/Create", {"Name": topic_name, "Description": "Verification"})
        check(is_redirect(response), "Admin creates a topic")
        topic_id = int(sql(f"SELECT Id FROM Topics WHERE Name='{topic_name}';"))
        response = admin.form("/Admin/Topics/Create", {"Name": topic_name.upper()})
        check(response[0] == 200 and "already exists" in response[2], "Duplicate topic is validated using SQL collation")
        response = admin.form(f"/Admin/Topics/Edit/{topic_id}", {"Name": topic_name, "Description": "Updated description"})
        check(is_redirect(response) and sql(f"SELECT Description FROM Topics WHERE Id={topic_id};") == "Updated description",
              "Admin edits a topic")

        task_data = {"Title": "Verification Task " + run_id, "Slug": slug.upper(),
                     "Description": "<script>unsafeMarkup()</script>\nTask statement",
                     "Difficulty": "1", "TopicId": str(topic_id), "TimeLimitMs": "2000",
                     "MemoryLimitMb": "256", "IsPublished": "false", "CreatedAt": "2000-01-01"}
        response = admin.form("/Admin/ProgrammingTasks/Create", task_data)
        check(is_redirect(response), "Admin creates an unpublished task")
        task_id = int(sql(f"SELECT Id FROM ProgrammingTasks WHERE Slug='{slug}';"))
        check(sql(f"SELECT Slug FROM ProgrammingTasks WHERE Id={task_id};") == slug, "Task slug is normalized to lowercase")
        check(sql(f"SELECT COUNT(*) FROM ProgrammingTasks WHERE Id={task_id} AND CreatedAt>'2020-01-01';") == "1",
              "Task CreatedAt cannot be overposted")
        check(anon.request("/Tasks/" + slug)[0] == 404 and task_data["Title"] not in anon.request("/Tasks")[2]
              and student.request("/Tasks/" + slug)[0] == 404, "Unpublished tasks are absent and return 404 to anonymous users and students")
        response = admin.form("/Admin/ProgrammingTasks/Create", task_data)
        check(response[0] == 200 and "slug already exists" in response[2], "Duplicate task slug is validated cleanly")
        invalid_task = {**task_data, "Slug": slug + "-invalid", "TopicId": "2147483647"}
        response = admin.form("/Admin/ProgrammingTasks/Create", invalid_task)
        check(response[0] == 200 and "Select an existing topic" in response[2], "Invalid TopicId is rejected")
        response = admin.form("/Admin/ProgrammingTasks/Create", {**task_data, "Slug": "bad slug", "Difficulty": "99",
                              "TimeLimitMs": "0", "MemoryLimitMb": "-1"})
        check(response[0] == 200 and "single hyphens" in response[2] and "between 100 and 30000" in response[2],
              "Task slug, enum, and resource-limit validation reject invalid values")
        task_data.update({"IsPublished": "true", "Title": "Edited verification task " + run_id})
        response = admin.form(f"/Admin/ProgrammingTasks/Edit/{task_id}", task_data)
        check(is_redirect(response), "Admin edits and publishes a task")
        status, _, body = anon.request("/Tasks/" + slug)
        check(status == 200 and task_data["Title"] in body and "<script>unsafeMarkup()" not in body
              and "&lt;script&gt;" in body, "Published task is public and its description is HTML encoded")
        check(task_data["Title"] in anon.request("/Tasks?search=" + run_id)[2]
              and task_data["Title"] in anon.request(f"/Tasks?topicId={topic_id}&difficulty=1")[2],
              "Catalog title, topic, and difficulty filters work")
        response = admin.form(f"/Admin/Topics/Delete/{topic_id}", {})
        check(response[0] == 200 and "contains programming tasks" in response[2], "Topic deletion is blocked when it contains tasks")

        visible_input = "visible-input-" + run_id
        visible_output = "visible-output-" + run_id
        hidden_input = "hidden-input-" + run_id
        hidden_output = "hidden-output-" + run_id
        case_data = {"ProgrammingTaskId": str(task_id), "Input": visible_input,
                     "ExpectedOutput": visible_output, "IsHidden": "false", "Order": "1"}
        response = admin.form("/Admin/TestCases/Create", case_data,
                              token_path=f"/Admin/TestCases/Create?taskId={task_id}")
        check(is_redirect(response), "Admin creates a visible test case")
        case_id = int(sql(f"SELECT Id FROM TestCases WHERE ProgrammingTaskId={task_id} AND [Order]=1;"))
        response = admin.form("/Admin/TestCases/Create", case_data,
                              token_path=f"/Admin/TestCases/Create?taskId={task_id}")
        check(response[0] == 200 and "already has a test case with this order" in response[2],
              "Duplicate test order is validated cleanly")
        response = admin.form(f"/Admin/TestCases/Edit/{case_id}", {**case_data, "ExpectedOutput": visible_output + "-edited"})
        check(is_redirect(response), "Admin edits a test case")
        hidden_data = {**case_data, "Input": hidden_input, "ExpectedOutput": hidden_output, "IsHidden": "true", "Order": "2"}
        response = admin.form("/Admin/TestCases/Create", hidden_data,
                              token_path=f"/Admin/TestCases/Create?taskId={task_id}")
        check(is_redirect(response), "Admin creates a hidden test case")
        hidden_id = int(sql(f"SELECT Id FROM TestCases WHERE ProgrammingTaskId={task_id} AND [Order]=2;"))
        response = admin.form("/Admin/TestCases/Create", {**case_data, "Input": "", "ExpectedOutput": "", "Order": "3"},
                              token_path=f"/Admin/TestCases/Create?taskId={task_id}")
        check(is_redirect(response), "Empty input and expected output are valid test data")
        for client, name in [(anon, "Anonymous"), (student, "Student")]:
            status, _, body = client.request("/Tasks/" + slug)
            check(status == 200 and visible_input in body and visible_output + "-edited" in body
                  and hidden_input not in body and hidden_output not in body,
                  name + " task HTML contains visible examples and no hidden data")
        admin_html = admin.request(f"/Admin/ProgrammingTasks/Details/{task_id}")[2]
        check(hidden_input in admin_html and hidden_output in admin_html, "Only authorized Admin details show hidden cases")
        check("no-store" in admin.request(f"/Admin/ProgrammingTasks/Details/{task_id}")[1].get("Cache-Control", ""),
              "Admin pages prevent response caching")
        response = student.request(f"/Admin/TestCases/Edit/{hidden_id}")
        check(is_redirect(response) and "/Account/AccessDenied" in response[1]["Location"],
              "Student cannot fetch a hidden case through an Admin URL")
        response = admin.form(f"/Admin/TestCases/Edit/{hidden_id}", {**hidden_data, "ProgrammingTaskId": str(task_id + 10000)})
        check(response[0] == 400, "Test-case parent reassignment is rejected")
        response = admin.form(f"/Admin/TestCases/Edit/{hidden_id}", {**hidden_data, "Order": "1"})
        check(response[0] == 200 and "already has a test case with this order" in response[2], "Duplicate test order is also rejected on edit")
        response = admin.form(f"/Admin/ProgrammingTasks/Delete/{task_id}", {})
        check(response[0] == 200 and "test cases first" in response[2], "Task with test cases is not silently deleted")

        # A bounded historical-data fixture verifies deletion guards; this never executes code.
        sql(f"""
            DECLARE @userId nvarchar(450)=(SELECT Id FROM AspNetUsers WHERE Email='{email}');
            INSERT INTO Runtimes (Name,LanguageKey,Version,FileExtension,RunCommand,IsEnabled)
                VALUES ('Verification fixture','{runtime_key}','1','.txt','',0);
            DECLARE @runtimeId int=SCOPE_IDENTITY();
            INSERT INTO Submissions (UserId,ProgrammingTaskId,RuntimeId,SourceCode,Status,CreatedAt,PassedTests,TotalTests)
                VALUES (@userId,{task_id},@runtimeId,'Verification data only',0,SYSUTCDATETIME(),0,1);
            DECLARE @submissionId bigint=SCOPE_IDENTITY();
            INSERT INTO ExecutionResults (SubmissionId,TestCaseId,Status) VALUES (@submissionId,{hidden_id},0);
        """)
        response = admin.form(f"/Admin/ProgrammingTasks/Delete/{task_id}", {})
        check(response[0] == 200 and "has submissions" in response[2], "Historical submissions block task deletion")
        response = admin.form(f"/Admin/TestCases/Delete/{hidden_id}", {})
        check(response[0] == 200 and "has execution results" in response[2], "Historical results block test-case deletion")
        sql(f"DELETE FROM Submissions WHERE RuntimeId IN (SELECT Id FROM Runtimes WHERE LanguageKey='{runtime_key}'); DELETE FROM Runtimes WHERE LanguageKey='{runtime_key}';")
        case_ids = sql(f"SELECT Id FROM TestCases WHERE ProgrammingTaskId={task_id};").split()
        for item in case_ids:
            check(is_redirect(admin.form("/Admin/TestCases/Delete/" + item, {})), "Admin deletes an unused test case")
        check(is_redirect(admin.form(f"/Admin/ProgrammingTasks/Delete/{task_id}", {})), "Admin deletes a task safely after dependents are removed")
        check(is_redirect(admin.form(f"/Admin/Topics/Delete/{topic_id}", {})), "Admin deletes an unused topic")
        check(anon.request("/Tasks/" + slug)[0] == 404, "Deleted task is no longer public")
        check(is_redirect(admin.form("/Account/Logout", {}, token_path="/Account/Profile")), "Admin can log out")
    finally:
        # Scoped to this run's unpredictable fixture identifiers, never demo or user data.
        sql(f"""
            BEGIN TRANSACTION;
            DELETE FROM ExecutionResults WHERE SubmissionId IN
                (SELECT Id FROM Submissions WHERE RuntimeId IN (SELECT Id FROM Runtimes WHERE LanguageKey='{runtime_key}'));
            DELETE FROM Submissions WHERE RuntimeId IN (SELECT Id FROM Runtimes WHERE LanguageKey='{runtime_key}');
            DELETE FROM Runtimes WHERE LanguageKey='{runtime_key}';
            DELETE FROM TestCases WHERE ProgrammingTaskId IN (SELECT Id FROM ProgrammingTasks WHERE Slug='{slug}');
            DELETE FROM ProgrammingTasks WHERE Slug='{slug}';
            DELETE FROM Topics WHERE Name='{topic_name}';
            DELETE FROM AspNetUsers WHERE Email='{email}';
            COMMIT;
        """)
        print("Removed this run's temporary verification data.", flush=True)
    print("All Phase 2 HTTP and database checks passed.", flush=True)


if __name__ == "__main__":
    main()
