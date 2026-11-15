CHRYSANTH CHEQUE WRITER - RAK SETUP                (updated 15-Nov-2026)
========================================================================

INSTALLED NOW:  v27.3  (CURRENT)
------------------------------------------------------------------------
  CSCheckWriter.exe in this folder = Builds\CURRENT - v27.3 ...
  Based on Chrysanth Cheque Writer 27.0 (decimals / fils work), plus:
    - Bank list: RAK only - "RAK (Biz)", "Rak Bank Amal", "Rak Bank (Personal)".
      Use "RAK (Biz)" for company cheques (calibrated layout).
    - Print button arrow (or Tools menu) > "Preview Cheque".
      ("Print Envelope" removed.)
    - Options > Font: Size (-2..+3), Bold text,
      Fine-tune position (mm) for Date / Payee / Amount / Amount in words.
    - Before every print: confirm box with Date / Payee / Amount
      ("Print this cheque now?" - No cancels). Not shown for Preview.
    - Options > General: "Test print" = prints the cheque picture too, for
      alignment checks on plain A4. Warning shown if you print with it on.
      UNTICK IT FOR REAL CHEQUES.
    - Automatic update check is off (Help > Check for Update does nothing).
    - Arabic "Right to Left Printing" option removed (its slot stores the
      fine-tune values).
  Do not move or delete the .dll / firebird.msg / manifest files here -
  the program needs them.

VERSIONS (Builds folder)
------------------------------------------------------------------------
  v27.3  CURRENT   v27.2 + confirm box before printing
  v27.2  OLD       RAK only + Preview, Size, Bold, Fine-tune, Test print
  v27.1  OLD       RAK (Biz) layout + decimals, all banks   <- undo copy
  v27.1  OLD       test 1 / test 2 (early builds from 09-Nov, not needed)
  v27.0  OLD       original program from the accounts computer (untouched)
  v36.8  OLD       Chrysanth Free 36.8 - DO NOT USE on the accounts computer
                   (no fils, and it upgrades the data file so v27 can no
                   longer open it)

  TO SWITCH VERSION: close Cheque Writer, copy the .exe from the version's
  folder into this folder, and rename it to  CSCheckWriter.exe
  (replace the existing one).

FOLDERS
------------------------------------------------------------------------
  Builds\CURRENT - v27.3 (RAK only + options + confirm)
  Builds\OLD - v27.2 (RAK only + options)
  Builds\OLD - v27.1 (RAK Biz, all banks - undo copy)
  Builds\OLD - v27.1 test builds
  Builds\OLD - v27.0 (original, accounts PC)
        also holds a zip of the whole original program folder
  Builds\OLD - v36.8 (free - DO NOT USE on accounts PC)
  Builds\Tools
        Python scripts used to build v27.2 / v27.3 (only to rebuild):
        patch_v27.2_part1.py  (v27.1 + RAK only build -> Preview + Size)
        patch_v27.2_part2.py  (part1 output -> all other options)
        patch_v27.3.py        (v27.2 -> confirm box before printing)
        read_bank_layouts.py  (prints field positions of bank layouts)
  Share - Accounts PC
        Cheque Writer v27.3 - Accounts PC update.zip   <- send this
        Cheque Writer v27.3 - Accounts PC update\      (same, unzipped)
        Instructions: "READ ME FIRST.txt" inside it.

DATA FILE
------------------------------------------------------------------------
  %APPDATA%\Chrysanth\Data\DB.CSDB  (payees, accounts, cheque history).
  Back it up before any change. INSTALL.bat in the share package does this
  automatically.

IMPORTANT
  Never install version 36.8 or newer on the accounts computer.
