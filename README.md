# otp

[![build](https://github.com/mjcheetham/otp/actions/workflows/build.yml/badge.svg)](https://github.com/mjcheetham/otp/actions/workflows/build.yml)
[![Mjcheetham.Otp.Tool](https://img.shields.io/nuget/v/Mjcheetham.Otp.Tool?label=Mjcheetham.Otp.Tool)](https://www.nuget.org/packages/Mjcheetham.Otp.Tool)
[![Mjcheetham.Otp](https://img.shields.io/nuget/v/Mjcheetham.Otp?label=Mjcheetham.Otp)](https://www.nuget.org/packages/Mjcheetham.Otp)

> Create and manage one-time passwords (TOTP and HOTP) from the command line —
> plus a small .NET library to do the same in your own apps.

`otp` generates the same time-based (TOTP) and counter-based (HOTP) one-time
passwords used by authenticator apps. It stores your accounts locally, prints
current codes, and imports/exports the standard `otpauth://` format — including
scannable QR codes.

## Features

- **TOTP & HOTP** — [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238) time-based
  and [RFC 4226](https://www.rfc-editor.org/rfc/rfc4226) counter-based codes.
- **Hash-agile** — SHA-1, SHA-256, and SHA-512, with configurable digit count
  and time period.
- **Import & export** — read and write `otpauth://` URIs and render QR codes.
- **Interactive or scriptable** — guided prompts on a terminal; `--format json`
  or `-z` (NUL-delimited) for scripts.
- **Local storage** — accounts kept in `~/.otp/store.json`, restricted to your
  user.
- **Two distribution channels** — a cross-platform `dotnet tool` and
  self-contained native binaries.
- **Reusable library** — the `Mjcheetham.Otp` package exposes the same engine.

## Install

### As a .NET tool

Requires the [.NET SDK](https://dotnet.microsoft.com/download):

```sh
dotnet tool install --global Mjcheetham.Otp.Tool
```

Invoke it as `otp`, and update with
`dotnet tool update --global Mjcheetham.Otp.Tool`.

### As a native binary (no .NET required)

Download the archive for your platform from the
[latest release](https://github.com/mjcheetham/otp/releases/latest), extract it,
and put `otp` on your `PATH`. Binaries are published for:

| OS      | Architectures                         |
| ------- | ------------------------------------- |
| Linux   | x64, arm64, arm                       |
| macOS   | x64 (Intel), arm64 (Apple silicon)    |
| Windows | x64, arm64                            |

## Quick start

```sh
# Add a time-based (TOTP) account from its Base32 secret
otp add github --secret JBSWY3DPEHPK3PXP --issuer GitHub

# ...or import an otpauth:// URI (e.g. decoded from a QR code)
otp add --uri "otpauth://totp/GitHub:me@example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub"

# ...or just run `add` on a terminal for guided prompts
otp add

# Generate the current code ('code' is an alias for 'get')
otp get github
otp code github

# List, inspect (secret masked by default), and remove
otp list
otp show github
otp remove github
```

## Usage

Run `otp --help` or `otp <command> --help` for full details; `otp --version`
prints the version.

| Command  | Aliases | Description                                                          |
| -------- | ------- | ------------------------------------------------------------------- |
| `add`    |         | Add an account from options, an `otpauth://` URI, or interactively. |
| `get`    | `code`  | Generate the current code for a stored account.                     |
| `list`   | `ls`    | List stored accounts.                                               |
| `show`   |         | Show an account's details, `otpauth://` URI, or QR code.            |
| `remove` | `rm`    | Remove a stored account.                                            |

### `add`

```
otp add <name> [options]
otp add --uri <otpauth://...>
otp add --interactive
```

| Option                | Description                                                | Default |
| --------------------- | ---------------------------------------------------------- | ------- |
| `<name>`              | Label to store the account under (unless `--uri`).         |         |
| `--secret`, `-s`      | Shared secret, Base32-encoded (unless `--uri`).            |         |
| `--type`, `-t`        | `totp` (time-based) or `hotp` (counter-based).             | `totp`  |
| `--issuer`            | Issuer / provider name.                                    |         |
| `--digits`, `-d`      | Number of digits in the code.                              | `6`     |
| `--period`, `-p`      | Time step in seconds (TOTP only).                          | `30`    |
| `--counter`, `-c`     | Initial counter value (HOTP only).                         | `0`     |
| `--algorithm`, `-a`   | `sha1`, `sha256`, or `sha512`.                             | `sha1`  |
| `--uri`               | Import from an `otpauth://` URI (excludes the options above). |      |
| `--interactive`, `-i` | Prompt for each field.                                     |         |

Running `otp add` with no arguments on an interactive terminal starts the guided
prompts automatically.

### `get` (alias `code`)

```
otp get <name> [--counter <n>] [--format text|json|nul] [-z]
```

Prints the current code. For TOTP accounts the human view also shows how long the
code stays valid. `--counter` generates a single HOTP code at a specific counter
without advancing the stored value.

### `list` (alias `ls`)

```
otp list [--type totp|hotp] [--format text|json|nul] [-z]
```

### `show`

```
otp show <name> [--show-secret] [--uri | --qr] [--format text|json|nul] [-z]
```

The secret is masked (`••••••••`) by default.

| Option          | Description                                                          |
| --------------- | ------------------------------------------------------------------- |
| `--show-secret` | Reveal the secret (and the secret embedded in the URI).             |
| `--uri`         | Print only the `otpauth://` URI (contains the secret — for backup). |
| `--qr`          | Print the `otpauth://` URI as a QR code to scan into another app.   |

### `remove` (alias `rm`)

```
otp remove <name> [--yes]
```

Prompts for confirmation on a terminal; pass `--yes`/`-y` to skip (required when
input is not a terminal).

## Output formats

Commands that emit data (`get`, `list`, `show`) accept `--format`/`-f`:

- `text` — human-readable (default), coloured when the terminal supports it.
- `json` — a single compact JSON value, ideal for `jq` and friends.
- `nul` — NUL-delimited records in the style of `git … -z`; `-z` is shorthand.

```sh
otp show github --format json | jq .type
otp list -z | xargs -0 -n1 otp get
```

## Storage

Accounts are kept in a single JSON file:

- Default: `~/.otp/store.json` — the directory is created `0700`, the file `0600`.
- Override the location with the `OTP_STORE` environment variable.

> [!WARNING]
> Secrets are stored **in plaintext**. Rely on filesystem permissions (as `otp`
> does) and/or full-disk encryption to protect them. The storage backend sits
> behind an interface and is intended to be swappable (e.g. an OS keychain) in
> the future.

## Environment variables

| Variable    | Effect                                                    |
| ----------- | -------------------------------------------------------- |
| `OTP_STORE` | Path to the store file (default `~/.otp/store.json`).    |
| `NO_COLOR`  | Disable coloured output.                                  |
| `NO_ANSI`   | Disable ANSI escape sequences entirely.                  |

## Library

Install the [`Mjcheetham.Otp`](https://www.nuget.org/packages/Mjcheetham.Otp)
package to generate codes in your own .NET app:

```sh
dotnet add package Mjcheetham.Otp
```

```csharp
using Mjcheetham.Otp;

// Build a TOTP from a Base32 secret and generate the current code.
byte[] secret = Base32.Decode("JBSWY3DPEHPK3PXP");
var totp = new TimeBasedOtp("GitHub", secret, period: 30, digits: 6, algorithm: OtpAlgorithm.Sha1);
string code = totp.GetCode();

// Counter-based (HOTP): generate the code for a specific counter.
var hotp = new HmacOtp("Backup", secret);
string next = hotp.GetCode(counter: 42);

// Parse and format the standard otpauth:// URI.
IOneTimePassword imported = OtpAuthUri.Parse(
    "otpauth://totp/GitHub:me@example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub");
string uri = OtpAuthUri.Format(imported);

// Persist accounts in the same file store the CLI uses.
IOtpStore store = new FileOtpStore(FileOtpStore.GetDefaultPath());
await store.AddAsync(totp);
```

The library targets .NET 10, is trim-/AOT-compatible, and ships a `.snupkg`
symbol package.

## Building from source

Requires the .NET SDK pinned in [`global.json`](global.json).

```sh
dotnet build                              # build the solution
dotnet run --project src/otp -- --help    # run the CLI
dotnet publish src/otp -c Release -r <rid>  # native AOT binary for a runtime
```

The root [`VERSION`](VERSION) file is the single source of truth for the
`major.minor.patch` number: local builds report `<VERSION>-dev+<sha>`, and a
release is cut by pushing a tag that matches `VERSION`.

## Standards

- [RFC 4226](https://www.rfc-editor.org/rfc/rfc4226) — HOTP
- [RFC 6238](https://www.rfc-editor.org/rfc/rfc6238) — TOTP
- [Key URI Format](https://github.com/google/google-authenticator/wiki/Key-Uri-Format)
  — `otpauth://` URIs (an `issuer` parameter takes precedence over an issuer
  label prefix)

## License

[MIT](LICENSE) © Matthew John Cheetham
