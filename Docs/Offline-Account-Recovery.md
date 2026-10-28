# Offline sign-in and recovery

## Sign-in

Usernames are now case-sensitive: `owner` and `operator` must match their stored case. Passwords remain case-sensitive. Surrounding username whitespace is trimmed. Show/Hide reveals the current password only in the active input; nothing is saved to a password-history file.

## Setup

After successful sign-in, an account without a code is offered recovery setup. Choose Generate recovery code, copy the displayed value to paper or a separate safe USB and acknowledge it. The UI shows the code only in that dialog. The app stores a salted PBKDF2 hash, never the plaintext code. Skip for now is allowed, but an unconfigured account cannot use code-based recovery.

To set up or replace a code later: sign out, choose Forgot password, then I know my password — set up recovery. Verify the current password and generate a fresh code. A replacement immediately invalidates the previous code.

## Forgotten password

1. Choose Forgot password on the sign-in screen.
2. Enter the exact username, saved recovery code, new password and confirmation.
3. Reset password, then sign in again.
4. Generate and save a new recovery code. The old one was consumed by reset.

New passwords require 6–128 characters. The eye / crossed-eye icon toggles password visibility. Recovery changes invalidate that account's existing sessions. Five failed attempts trigger a 30-second cooldown retained across restarts. Successful password changes through existing services also invalidate recovery codes.

No email, OTP, security questions, embedded master password or privileged authentication bypass is used. Losing both password and code means no self-service reset; a verified backup with known credentials remains the recovery route. Restoring a backup restores its complete business/account state, not just the password. Keep current recovery material separately. Older backups may contain a code hash that was valid when that backup was taken.

## Migration and storage

Schema 5 adds `account_recovery` and takes a pre-upgrade backup for existing databases. Existing passwords and company records are retained; no recovery code is created automatically without the user explicitly generating one.

Login/password management does not encrypt the SQLite database. This feature is account recovery, not protection against direct file access on the USB.

## Interface changes

- Compact sign-in form, Show/Hide input and Forgot password entry point.
- Workspace Password button removed for both roles. Account recovery is reached from authentication.
- Original TradeVault vault/TV mark embedded in the executable, window icons, main header, login and Operator Reports.
- Operator Reports has search, review-status filter, real submission counts, selection-aware actions and a separate review dialog. Imported report payloads/checksums/revision logic are unchanged.
