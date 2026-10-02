"""Fail on missing/duplicate/empty translations without relying on runtime fallback."""
import re
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CULTURES = ("kk-KZ", "ru-RU", "en-US")
GROUPS = ("SharedResource", "StudentResource", "AdminResource")


def resources(group, culture):
    path = ROOT / "Resources" / f"{group}.{culture}.resx"
    nodes = ET.parse(path).findall("data")
    result = {node.attrib["name"]: node.findtext("value", "") for node in nodes}
    assert len(nodes) == len(result), f"Duplicate key: {path.name}"
    assert all(value.strip() and ("_" not in key or key != value) for key, value in result.items()), f"Empty/raw translation: {path.name}"
    return result


def main():
    groups = {}
    for group in GROUPS:
        translations = {culture: resources(group, culture) for culture in CULTURES}
        expected = set(translations["en-US"])
        for culture, values in translations.items():
            assert set(values) == expected, f"{group}/{culture}: missing={expected-set(values)}, extra={set(values)-expected}"
            for key, value in values.items():
                placeholders = lambda text: sorted(re.findall(r"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})", text))
                assert placeholders(value) == placeholders(translations["en-US"][key]), f"Placeholder mismatch: {group}/{culture}/{key}"
        groups[group] = expected
        print(f"PASS: {group}: {len(expected)} matching, nonempty keys per culture; placeholders match")

    # Check literal localizer lookups and annotation keys against the correct resource group.
    for folder in ["Views", "Areas", "Controllers", "ViewModels"]:
        for path in (ROOT / folder).rglob("*"):
            if path.suffix not in (".cs", ".cshtml"):
                continue
            source = path.read_text(encoding="utf-8-sig")
            for symbol, key in re.findall(r'\b(T|L|A|_text)\["([A-Za-z][A-Za-z0-9_]+)"', source):
                group = {"T": "SharedResource", "L": "StudentResource", "A": "AdminResource"}.get(symbol,
                    "AdminResource" if "Admin" in path.parts else "SharedResource")
                assert key in groups[group], f"Missing {group}/{key} referenced by {path.relative_to(ROOT)}"
            for key in re.findall(r'(?:ErrorMessage|Name)\s*=\s*"((?:Validation|Field)_[A-Za-z0-9_]+)"', source):
                group = "AdminResource" if "Admin" in path.parts else "SharedResource"
                assert key in groups[group], f"Missing annotation {group}/{key}"
    # Dynamic display families must cover every invariant semantic value.
    required = {
        "Status": "Pending Compiling Running Accepted Passed WrongAnswer CompilationError RuntimeError TimeLimitExceeded MemoryLimitExceeded InternalError Skipped Success",
        "Difficulty": "Easy Medium Hard",
        "Strength": "NotStarted Exploring NeedsPractice Developing Strong",
        "ErrorPattern": "Compilation WrongAnswer Runtime TimeLimit MemoryLimit None",
        "AiCategory": "Compilation Logic Runtime TimeLimit Memory OutputFormat Unknown",
        "Identity": "PasswordTooShort PasswordRequiresNonAlphanumeric PasswordRequiresDigit PasswordRequiresLower PasswordRequiresUpper PasswordRequiresUniqueChars"
    }
    for prefix, names in required.items():
        assert all(prefix + "_" + name in groups["SharedResource"] for name in names.split()), prefix
    print(f"RESOURCE CHECKS PASSED: {len(GROUPS)*len(CULTURES)} files, {sum(map(len, groups.values()))} keys per language; no missing-key fallback used")


if __name__ == "__main__":
    main()
