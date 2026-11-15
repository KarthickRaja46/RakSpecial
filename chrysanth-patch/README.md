# Chrysanth Cheque Writer v27 – RAK patch (v27.3)

Patch scripts and installer for the company's **Chrysanth Cheque Writer 27.0**
install, used for **RAK Business** cheques until our own app (this repo) takes over.

> **No Chrysanth program files are in this repo.** Chrysanth Cheque Writer is
> proprietary software. The scripts here change a licensed copy that you
> already have, and the patched builds stay on the company PCs.

## Versions

| Version | Status | What it is |
|---|---|---|
| **v27.3** | **CURRENT** | v27.2 + confirm box before printing (Date / Payee / Amount – *Print this cheque now?*) |
| v27.2 | OLD | Bank list RAK only, Preview Cheque, font Size / Bold, per-field fine-tune (mm), Test print (prints cheque picture on plain A4) + warning, update check off |
| v27.1 | OLD | RAK (Biz) layout with RAK Business positions, decimals (fils), all banks |
| v27.0 | OLD | Original Chrysanth 27.0 from the accounts PC |
| v36.8 | OLD | Chrysanth Free 36.8 – **do not use on the accounts PC** (no fils, upgrades the data file so v27 can no longer open it) |

## What v27.3 changes (user view)

- Bank list: **RAK (Biz)**, Rak Bank Amal, Rak Bank (Personal) only. "More..." and the support-mail (Outlook) popup are removed.
- Print ▾ / Tools: **Preview Cheque** (replaces Print Envelope).
- Options › Font: **Size** (−2…+3), **Bold text**, **Fine-tune position (mm)** for Date, Payee, Amount, Amount in words (0.5 mm steps, ±4 mm).
- Options › General: **Test print** – prints the cheque picture too, for alignment checks on plain A4. You get a warning if you print with it ticked.
- **Confirm box before every print** showing Date / Payee / Amount. Not shown for Preview.
- Automatic update check off. The Arabic right-to-left option is removed, because its settings slot now stores the fine-tune values.

## Folder layout on the laptop

```
C:\Program Files (x86)\Chrysanth\Cheque Writer\
  CSCheckWriter.exe (= v27.3) + runtime DLLs, README.txt
  Builds\CURRENT - v27.3 ...\   Builds\OLD - v27.2 / v27.1 / v27.0 / v36.8 ...\   Builds\Tools\
  Share - Accounts PC\Cheque Writer v27.3 - Accounts PC update(.zip)
```

## Installing on the accounts PC

Copy `Share - Accounts PC\Cheque Writer v27.3 - Accounts PC update.zip`, extract it, and run
`INSTALL.bat`. The installer checks that version 27 is installed, backs up `DB.CSDB` and the
current exe, keeps the original exe, and installs v27.3. `UNDO.bat` restores the original.
The files are in [`installer/`](installer/).

## Rebuilding from an original 27.0 exe (Python 3 + `capstone`)

The v27.1 layout edit and the RAK-only bank list (three jumps in the bank-list builder,
"More..." removed) were done by hand. Their offsets are in the notes below. Then:

```
python tools/patch_v27.2_part1.py  <v27.1-rak-only.exe>  step1.exe   # Preview + Size
python tools/patch_v27.2_part2.py  step1.exe             v27.2.exe   # Bold, fine-tune, warning, .cave section
python tools/patch_v27.3.py        v27.2.exe             v27.3.exe   # confirm box
python tools/read_bank_layouts.py  CSCheckWriter.exe frxrptRakBank   # print a layout's field positions
```

Each script checks the bytes it expects and stops if they don't match.

### Technical notes (file offsets / VAs in the 27.0 exe)

- Bank list builder: jumps at file 0x3a3489→0x3a365b, 0x3a36e1→0x3a4222, 0x3a4265→0x3a4933. "More..." `Items.Add` NOPed at 0x345627, mailto handler skipped at 0x4ae2a8.
- Test print flag = setting 0x2723 (old "Disable auto update"), config `[[0x8e940c]]+0x54`. Sets `Picture1.Printable` (TfrxView+0x10C) at print.
- Preview: dispatcher `ecx` = preview flag → FastReport `ShowReport`.
- Settings are UPDATE-only, so existing rows are reused: 0x2711 (old envelope id) = size/bold/date+payee nudges, 0x2724 (old RTL) = amount+words nudges, bit 24 = valid marker.
- New code lives in an added PE section `.cave`.
