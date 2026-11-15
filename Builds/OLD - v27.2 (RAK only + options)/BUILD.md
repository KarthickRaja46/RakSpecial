# v27.2 · OLD

> The `.exe` is **not** in this repo, because Chrysanth Cheque Writer is proprietary.
> It is kept on the laptop at
> `C:\Program Files (x86)\Chrysanth\Cheque Writer\Builds\OLD - v27.2 (RAK only + options)\`

## What it is
- Bank list: RAK (Biz), Rak Bank Amal, Rak Bank (Personal) only; "More..." and the Outlook support-mail popup removed
- Preview Cheque (replaces Print Envelope)
- Options › Font: Size −2…+3, Bold text, Fine-tune position (mm) per field
- Options › General: Test print (prints the cheque picture) + warning when printing with it on
- Automatic update check off; Arabic right-to-left option removed

**Note:** Previous release.

**Made with:** `patch_v27.2_part1.py` and then `patch_v27.2_part2.py`, applied to v27.1 with the RAK-only bank list

## Files on the laptop

| File | Size | SHA-256 |
|---|---|---|
| `Chrysanth Cheque Writer v27.2 (OLD).exe` | 10,025,984 bytes | `CF26FF2838299FB0DC85F59FC01699BE4234D1791CFD45DC8BF85AB5D53D868E` |

Check a copy in PowerShell:
```powershell
Get-FileHash "<file>" -Algorithm SHA256
```
