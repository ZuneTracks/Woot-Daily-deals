# Changelog

## Unreleased - Windows 10 Mobile Store install fix

- Fixed the app terminating immediately after the splash screen when installed
  from the Microsoft Store on Windows 10 Mobile. The package declared a
  `Microsoft.VCLibs.140.00` dependency with `MinVersion 14.0.33519.0`, which
  Windows 10 Mobile cannot obtain: Mobile stopped receiving framework-package
  updates at OS build 15254 and its Store can only provision VCLibs up to
  roughly `14.0.24217.0`. The dependency was therefore never satisfied even
  though the Store install itself reported success. Sideloading was unaffected
  because the sideload output ships its own VCLibs payload that is installed by
  hand, which is why the defect was not caught by on-device testing.
- Upgraded `Microsoft.NETCore.UniversalWindowsPlatform` from `5.2.9` to
  `6.2.14`, which resolves `Microsoft.Net.Native.Compiler 1.7.6`. The package
  now declares `Microsoft.VCLibs.140.00 MinVersion 14.0.22929.0` plus
  `Microsoft.NET.Native.Framework.1.7` / `Microsoft.NET.Native.Runtime.1.7`
  instead of `Microsoft.NET.CoreRuntime.1.1`.
- Enabled `UseDotNetNativeToolchain` for the `Release|ARM` and `Store|ARM`
  configurations so the .NET Native framework references are actually applied
  at packaging time.
- `TargetPlatformMinVersion` deliberately remains `10.0.15063.0`. Raising it to
  16299 would drop Windows 10 Mobile support entirely, since Mobile's last build
  is 15254.
- Removed the `ExcludeSdkWinmdsFromStorePackage` target, which stripped
  `WinMetadata\Windows.winmd` from every configuration including Store uploads.
  The Store recompiles uploaded MSIL in the cloud and cannot resolve Windows
  Runtime types without that file. The root-level duplicate `Windows.winmd` that
  the target was added to remove is already handled by `<Private>false</Private>`
  on the `Windows` reference, so the Store payload now contains exactly one copy.

## v1.1.2 - Live tile reliability

- Fixed live-tile text ordering so the previously viewed deal is displayed before
  the static Woot! Deals label.
- Added large-square tile content for devices that use the 310x310 tile layout.
- Sanitized deal text before generating tile XML to prevent malformed API text from
  crashing the app.
- Removed the synchronous tile refresh during startup so the app can progress past
  the splash screen reliably.
- Updated the package version to `1.1.2.0`.
- Published a signed ARM Release AppX.

## v1.1.0.2 - About dialog disclaimer

- Added a disclaimer to both About dialogs stating that the app is not affiliated
  with Woot!, Amazon, or any of their affiliates.
- Updated the package version to `1.1.0.2`.
- Published a signed ARM AppX with the matching `WootDevelopment.cer`.

## v1.1.0.1 - Live tile and About updates

- Improved Windows 10 Mobile live-tile notification compatibility with supported
  square and wide text templates plus legacy fallbacks.
- Added diagnostics around live-tile XML generation and notification submission.
- Updated the About dialog in MainPage and SettingsPage to read the installed
  package version dynamically from the app manifest.
- Added the current Woot! app description to the About dialog.
- Published a signed ARM AppX with the matching `WootDevelopment.cer`.

## v1.0.9.0 - Store packaging preparation

- Added an explicit ARM Store-upload configuration that creates an unsigned
  `.appxupload` candidate with public symbols.
- Added a build-time guard requiring Visual Studio Store association metadata
  before Store packaging.
- Updated the package version to use a Store-compatible fourth version field of
  `0`.

## v1.0.7.4 - First public release

- Added a Windows 10 Mobile 15063+ ARM UWP app with a Metro-inspired YourTube UI.
- Added public YouTube Data API v3 search, regional trending, details, channels,
  categories, and inline category expansion.
- Added an official YouTube mobile watch page in-app viewing route with a browser
  fallback and no direct media extraction.
- Added foreground Trending Now live-tile updates.
- Added OAuth 2.0 authorization code plus PKCE architecture with system-browser
  sign-in and Credential Locker storage.
- Removed retained legacy credentials from the recovered source and excluded the
  recovered WP8 projects from the public release repository.

## Security and distribution notes

- No API key, OAuth client ID, OAuth client secret, access token, refresh token, or
  private signing certificate is included.
- The included AppX is for Developer Mode sideloading and is signed with a temporary
  development certificate. It is not a production or Microsoft Store package.
