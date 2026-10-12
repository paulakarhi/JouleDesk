# Contributing

Bug reports, ideas and pull requests are welcome.

## Reporting a problem

Open an issue and tell which version of JouleDesk, which Windows version, processor and graphics card you have. For a
reading that is missing or wrong, open the Components page, press **Data sources** and paste the file into the issue.
It lists the components found and where each figure comes from, and nothing personal.

## Building

See [Building](README.md#building) in the README. The .NET 9 SDK is all that is needed.

## Pull requests

- Keep a change small and about one thing.
- Follow the style of the code around it: English names and comments, and short comments that say why, not what.
  The build treats warnings as errors, and `dotnet format --verify-no-changes` should report nothing.
- A text the user sees has to be added to all twelve languages in `Resources/locales.tsv`: the English text is the
  key, followed by one column per language in the order of the header.
- Say how you tried it: with which Windows version and hardware.
