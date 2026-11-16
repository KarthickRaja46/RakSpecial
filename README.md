# RAK Special: RAK Bank cheque printing software for Windows (UAE)

<img src="docs/icon.png" width="96" alt="RAK Special cheque printing software icon">

**RAK Special** is a free Windows program that prints **RAK Bank (RAKBANK) cheques** on an ordinary
office printer. It prints the date, payee, amount in figures and **amount in words in AED with fils**. It supports
both RAK cheque books, prints **post-dated cheque (PDC) series** for rent and instalments, and keeps a cheque
register and a payee list.

Built for UAE businesses that write RAK Bank cheques every week and want them typed, consistent and correctly
spelled, without a subscription.

## Features

- **Both RAK Bank cheque books**, measured from real leaves:

  | Cheque book | Code | Size | Difference |
  |---|---|---|---|
  | RAK Business, new model | RAK-001 | 202 × 99 mm | amount box level with the first words line |
  | RAK Old, wider book | RAK-002 | 206 × 100 mm | amount box lower, level with the second words line |

- **Amount in words with fils**: `4523.20` prints as *Four Thousand Five Hundred Twenty Three And Fils Twenty Only*.
  Long amounts wrap onto the second words line automatically. You get a warning if the words do not fit.
- **PDC series (post-dated cheques)**: enter the payee, amount, first cheque number and first date once, choose
  for example 12 cheques every month, and print them one by one. Month ends stay correct (31 Jan → 28 Feb → 31 Mar),
  and dates that fall on a UAE weekend can move to Monday or to the Friday before.
- **Preview on a picture of the real cheque**, and plain-paper **test prints** with the cheque picture, so you can
  check alignment before using a real leaf.
- **Cheque register**: search, date filter, mark void, notes, reprint, and export to Excel (CSV).
  Warns about duplicate cheque numbers.
- **Payee list** with auto-complete. New payees are saved automatically.
- **Layout editor**: drag any field or move it 0.1 mm at a time, change font size or bold, and set a printer
  offset. You can also load your own scan of a blank cheque.
- **Confirm box before every print** (date, payee, amount, words).
- One `RAKSpecial.exe`: no .NET install and no administrator rights needed. Data is backed up as `.bak` on every save.

## Download and install

1. Download `RAK Special - Setup.zip`, then right-click it → **Extract All**.
2. Run **INSTALL.bat**. It installs for the current Windows user and adds a *RAK Special* shortcut on the Desktop
   and in the Start menu.
3. Open RAK Special, choose your cheque book (RAK Business or RAK Old) and click **Test print on plain paper**
   once. Hold the sheet against a real cheque in front of a light.
   - If everything is shifted the same way, change **Offset X / Offset Y** under
     *Tools → Layout editor → Cheque & printer settings*.
   - If only one field is off, drag that field.
4. Put the cheque in the top-left corner of an A4 landscape page against the paper guide, and press **Print cheque** (Ctrl+P).

## Where the data is kept

`%APPDATA%\RAK Special\rakspecial-data.json` is one JSON file holding the layouts, payees, register and settings.
For portable use, put an empty `portable.txt` next to `RAKSpecial.exe` and the data goes into a `Data` folder beside it.

## Building from source

Requires the .NET 8 SDK on Windows.

```bat
dotnet test     :: 54 unit tests (amount in words, wrapping, storage, PDC schedule, RAK books)
publish.bat     :: publish\RAKSpecial.exe + Share\RAK Special - Setup.zip
```

| Folder | What it is |
|---|---|
| `src/ChequeWriter.Core` | Models, RAK cheque books, amount in words, PDC schedule, JSON storage, CSV export (no UI) |
| `src/ChequeWriter` | WinForms app: main window, renderer, printing, PDC series, layout editor, register, payees |
| `setup` | INSTALL.bat / UNINSTALL.bat / READ ME for the setup ZIP |
| `tests/ChequeWriter.Tests` | xUnit tests |

The cheque pictures (`src/ChequeWriter/Images/rak-business.jpg`, `rak-old.jpg`) are not in this repository. Add
your own scans of blank, cancelled cheques with those names to build the pictures in, or leave them out: the
program still works and shows the cheque outline instead.

## Keywords

RAK Bank cheque printing software · RAKBANK cheque printer · cheque writer UAE · cheque printing software Dubai,
Abu Dhabi, Sharjah, Ras Al Khaimah · AED amount in words with fils · PDC post-dated cheque printing ·
rent cheque printing · free cheque printing software for Windows

## About

RAK Special is an independent project by Karthick Raja, not affiliated with or endorsed by
The National Bank of Ras Al Khaimah (RAKBANK).
