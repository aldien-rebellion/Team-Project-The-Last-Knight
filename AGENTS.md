# Unity Project Guide

## Scope and structure

- This is a Unity project. Start targeted investigation in `Assets/`.
- Put editor-only tools in an `Editor/` folder.
- Use `Packages/manifest.json` to understand dependencies. Editable package source belongs in `Packages/`; do not edit `Library/PackageCache/`.
- Ignore generated `Library/` content unless a task specifically requires package-cache source. Treat `ProjectSettings/` changes as project-wide changes.

## Working approach

- Investigate only the files needed for the request. Prefer targeted searches over broad repository exploration.
- When relevant existing code is found, extend it unless the request or surrounding code indicates a fresh implementation is appropriate.
- Ask before a decision that materially changes the design, affects many unrelated assets, changes packages, or changes project-wide settings. Otherwise, complete normal implementation work autonomously.
- Preserve unrelated user changes in the working tree.

## Unity Editor automation

When executing C# through the Unity Editor command runner:

- Define an `internal class CommandScript : IRunCommand` with an `Execute(ExecutionResult result)` method.
- Register created objects with `result.RegisterObjectCreation`, register modifications before changing an object, and use `result.DestroyObject` for deletions.
- Use the supplied result logger for useful output.

## Completion and verification

- Complete the requested change, fix failures directly caused by it, and verify in proportion to risk.
- For scene, prefab, or visual changes, inspect the resulting view or screenshot when available.
- For code or editor-command changes, inspect relevant console errors and run affected local checks when available and safe.
- Fix clear, local issues found during verification. Ask when the correction needs missing product direction or materially expands scope.
