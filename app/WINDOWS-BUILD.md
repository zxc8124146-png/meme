# MT4 黃金儀表板 — Windows build and update

This folder is the Windows widget. Trading stays read-only: `MT4Recorder.mq4` only reads the account and writes files. It does not place, change, or delete orders.

The widget was cross-compiled on Linux with the .NET 8 SDK (`win-x64`, self-contained, single file). Version 1.1.1 fixes a startup stack overflow in 1.1.0 (`this.CreateParams` / `this.OnLoad` / `this.WndProc` called themselves). That crash is `0xc00000fd` and does not reach `error.log` unless the new startup lines are written first. This environment has no Windows session and no MetaEditor, so those two tools were not run here. `error.log` now records `startup main`, `startup ctor`, `startup ctor-ready`, `startup run`, `startup onload`, and `startup onload-ready`. A reentered `CreateParams`, `OnLoad`, or `WndProc` throws `InvalidOperationException` and is appended to that log instead of overflowing the stack.

## What the update does

While the program is open it still records the broker day from the live `todayRealized` snapshot (closed-trade profit + swap + commission). On startup it also reads `mt4realized.csv`, written by the EA from the account history MT4 has loaded, and inserts missing broker dates into that account's ledger.

- Earnings are account-wide closed buy/sell deals, same scope as the existing today figure. Deposits, withdrawals, credit, and pending orders are not earnings.
- A day inside the loaded history span with no closed deals is stored as `0.00`.
- Dates before the first loaded history record are left blank. That blank is not a zero. MT4 only returns the range currently loaded in Account History.
- Rows already saved keep their equity, balance, and realized numbers. Days filled by backfill have a blank closing equity. The program does not invent past floating-equity curves.
- Ledgers are split by account: `%AppData%\MT4Widget\history_<account>.csv`.
- The old `history.csv` is not edited. The first account that connects is given a copy of it. A byte copy is also kept as `history.legacy.original.csv.bak`. Before new dates are inserted, the account file is copied under `backups\`.

## Install without losing data

Do this on the machine that already runs the widget. Do not delete `%AppData%\MT4Widget`.

1. Close `MT4Widget.exe`.
2. Replace the old exe with `app/MT4Widget.exe` from this update. Leave `settings.json` and `history.csv` where they are (`%AppData%\MT4Widget`).
3. In MetaEditor, open `app/MT4Recorder.mq4` (copy it into the terminal `MQL4\Experts` folder if that is where the old EA lives) and compile it. Reattach the compiled EA to any chart. No inputs need to change.
4. In MT4, open the Account History tab, right-click, and choose **All History** (全部歷史). Wait until the history download finishes. If you leave the terminal on "last month" or "last 3 days", older days stay blank on purpose.
5. Start `MT4Widget.exe`. The first successful read creates `history_<帳號>.csv` and, when needed, `history.legacy.original.csv.bak`.
6. Open **收益紀錄**. The note at the bottom states the loaded span. A dash in the equity column means that day was backfilled and was not sampled live.

Repeat startup with the same history file does not add duplicate days and does not write another backup.

## Build the exe on Windows

Requires the .NET 8 SDK.

```bat
cd app\src\MT4Widget
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o ..\..\publish
```

The project is `net8.0-windows` because it is a WinForms app. `EnableWindowsTargeting` is set so the same project can also be published from Linux.

Run the offline tests from the repo root:

```bat
dotnet test app\src\MT4Widget.Tests\MT4Widget.Tests.csproj -c Release
```

Those tests use synthetic account `90000001` / `90000002` only. They do not start MT4.

## Files the EA writes

Both files are under the MT4 common data folder:

`%AppData%\MetaQuotes\Terminal\Common\Files\`

- `mt4dash.json` — live snapshot, including a short `realizedExport` block (file name, account, span, stamp).
- `mt4realized.csv` — read-only history export. Rewritten at most every few seconds when the loaded history changes, otherwise about once a minute.

No passwords or tokens are written.
