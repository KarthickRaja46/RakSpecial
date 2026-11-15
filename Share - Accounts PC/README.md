# Share · Accounts PC

| Item | On the laptop | In this repo |
|---|---|---|
| `Cheque Writer v27.3 - Accounts PC update.zip` | ✅ send this one | ❌ (contains the Chrysanth exe) |
| `Cheque Writer v27.3 - Accounts PC update/INSTALL.bat` | ✅ | ✅ |
| `Cheque Writer v27.3 - Accounts PC update/UNDO.bat` | ✅ | ✅ |
| `Cheque Writer v27.3 - Accounts PC update/READ ME FIRST.txt` | ✅ | ✅ |
| `Cheque Writer v27.3 - Accounts PC update/CSCheckWriter.exe` | ✅ | ❌ |

What `INSTALL.bat` does: it **checks that v27 is installed**, **backs up `DB.CSDB` and the current exe**
to the Desktop, keeps the original exe as `CSCheckWriter_v27_original.exe`, installs v27.3, and
**verifies the copy byte for byte**. `UNDO.bat` puts the original back.
