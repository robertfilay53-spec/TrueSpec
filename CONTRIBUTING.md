# Contributing to TrueSpec

Thanks for helping. TrueSpec exists to give buyers the truth about a machine, so the rules below protect that trust.

## Ground rules

1. **Never invent a number.** Every value is measured (name the source), estimated (label it ESTIMATE), or shown as "Not reported by hardware".
2. **Stay offline.** No telemetry, no analytics, no account, no hidden network calls. Optional features that need the internet must be off by default and opt-in.
3. **English only** in the UI, reports, alerts, code comments and commit messages.
4. **No secrets** in the repository. No API keys, tokens or personal data.
5. **One collector, one job.** New hardware support is a new `ICollector` implementation, not a change to the core.
6. **Explain every score.** The scoring engine must keep its inputs and weights visible.

## Development setup

Requirements: .NET 10 SDK. Avalonia builds on Linux and Windows.

```bash
dotnet restore TrueSpec.sln
dotnet build TrueSpec.sln
dotnet test TrueSpec.sln
dotnet run --project src/TrueSpec.App
```

## Making a change

1. Fork the repository and create a branch: `feature/short-name` or `fix/short-name`.
2. Keep pull requests small and focused.
3. Add or update tests for any change in `TrueSpec.Core`.
4. Run `dotnet build` and `dotnet test` before pushing.
5. Open a pull request describing what changed, why, and how you tested it.

## Code style

- C# with nullable reference types enabled.
- Prefer records for immutable data.
- Keep UI logic in view models, not code-behind, except small view-only interactions like clipboard copy.
- Use `async` and `CancellationToken` for anything that touches hardware or disk.
- Comments explain why, not what. Plain English, no jargon.

## Areas that need care

- **Collectors:** declare `RequiresAdministrator` honestly. Never swallow an access error and return fake data.
- **Scoring:** a component with no sensor must be excluded, and its weight redistributed. Do not pad the score.
- **Buyer checks:** a failed check must say what it means and what to ask the seller.
- **Snapshots:** schema changes need a migration path. Existing local history must not break.

## Reporting bugs

Open an issue with your Windows version, the TrueSpec version, what you expected, what happened, and the steps to reproduce. Do not attach a report that contains personal data such as serial numbers unless you are comfortable sharing it.

## License

By contributing you agree that your work is released under the MIT license in [LICENSE](LICENSE).
