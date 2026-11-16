# Contributing to RAK Special

Thank you for helping. RAK Special is used to print real cheques, so mistakes cost people money. Please keep
changes small, tested and easy to check.

## Reporting a problem

[Open an issue](https://github.com/KarthickRaja46/RakSpecial/issues/new/choose) and include the following. For quick
help instead, email karthickraja232205@gmail.com or WhatsApp +971 50 256 7444 (UAE) / +91 63832 68445 (India).

- the RAK Special version (*Help → About*) and your Windows version
- the cheque book (RAK Business or RAK Old) and the printer model
- what you typed, what was printed, and what you expected. A photo of a **test print on plain paper** helps
  a lot. **Never post a photo of a real cheque, an account number or a signature.**

## Setting up

1. Install the tools once:
   ```bat
   winget install Microsoft.DotNet.SDK.8
   winget install JRSoftware.InnoSetup   :: only needed to build the setup wizard
   ```
2. Clone the repository (`git clone https://github.com/KarthickRaja46/RakSpecial.git`), then:
   ```bat
   dotnet test                              :: all tests must pass
   dotnet run --project src\ChequeWriter    :: start the app
   ```
3. The cheque pictures (`src/ChequeWriter/Images/*.jpg`) are built into the program. If you replace one, keep it at
   the exact cheque proportions (e.g. 202 × 99 mm) or the preview will not line up with the printed text.

While you work, use a portable data folder so your real cheque register is not touched: create an empty
`portable.txt` next to the built `RAKSpecial.exe` (in `src/ChequeWriter/bin/Debug/net8.0-windows/`).

## Sending a change

- One topic per pull request, with a short description of what changed and why.
- Add or update tests in `tests/ChequeWriter.Tests` for anything in `ChequeWriter.Core` (amount in words, dates,
  layouts, storage). Amount-in-words changes need tests for fils, rounding and very large amounts.
- Changing a built-in layout (`DefaultLayouts.cs`) changes where text lands on real cheques. Say which cheque
  book and printer you checked it on, with a plain-paper test print held against a real leaf.
- Keep the saved data file readable by older versions where possible. If you must change its shape, add a
  migration in `DataStore`.
- Don't add anything that sends data off the user's PC. The only network call is the update check
  (`Updates.cs`), which fetches the latest release number from GitHub and can be switched off in Settings.

## Making a release (maintainers)

1. Raise `<Version>` in `src/ChequeWriter/ChequeWriter.csproj`. The update check compares this number with the
   newest release tag, so every release needs a higher version. `AppInfo.GitHubRepo` (in `ChequePictures.cs`) must
   name the repository, e.g. `owner/rak-special`.
2. Add a `## <version>` section at the top of `CHANGELOG.md` with short, plain lines for users. It becomes the
   release notes on GitHub and in the app's *Update available* window.
3. Commit, then tag and push: `git tag v2.1.0 && git push --tags`.
4. GitHub Actions runs the tests, builds `RAKSpecial-Setup-2.1.0.exe` and `RAKSpecial-Portable-2.1.0.zip`, and
   publishes them as a release, together with fixed-name copies (`RAKSpecial-Setup.exe`, `RAKSpecial-Portable.zip`)
   that the README's download buttons link to. Edit the release notes on GitHub if needed.

To build locally instead, run `publish.bat`; the files appear in `Share\`.
