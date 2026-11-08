# Woot! Native UWP app

Woot! browser for Windows 10 Mobile is a native Universal Windows Platform app
for browsing Woot! daily deals. It uses a touch-friendly layout with swipeable
category pivots, native deal cards, per-category sorting, Woot green accents,
light/dark appearance options, and native offer details.

## Supported feeds

Featured, All, Clearance, Computers, Electronics, Home, Gourmet, Shirts, Sports,
Tools, and Wootoff.

The app loads feed data from `https://developer.woot.com/feed/{feedname}` and
renders titles, subtitles, prices, state, featured status, and images with UWP
controls. Offer links open externally through the system browser; catalog HTML
is not rendered in a WebView.

## Sorting

The toolbar sort selector reorders the deals in the current category. Available
orders are default feed order, price low to high, price high to low, biggest
discount, newest first, ending soonest, available first, featured first, title
A to Z, and title Z to A.

Each category keeps its own sort, saved locally under `WootSort_<FeedName>` and
restored on the next launch. Sorting runs on the device against the deals
already loaded, because the Woot feed endpoint takes no sort parameter; changing
the sort therefore costs no network request.

Deals that are missing the value a sort needs are listed last, and ties keep
their original feed order. Woot does not populate every field on every feed, so
when no deal in a category carries the required value the status line adds
"(not available for this feed)" next to the sort name.

## Filtering

When Woot returns subcategories for the deals in a category, the selector beneath
the toolbar lists them, and choosing one narrows the list. The selector is always
present, offering "All subcategories" on its own before a category has loaded or
for one that has none, so moving between categories never shifts the layout.

Woot tags each deal with a flat list that mixes the sites it appears on with the
paths beneath them, such as
`[ "HOME", "TOOLS", "HOME/Lighting & Fans", "TOOLS/Lighting & Fans" ]`. The bare
site names duplicate the category pivot, so only the entries carrying a path are
offered, and the leading site is dropped from them: a deal cross-posted to
several sites would otherwise appear as two choices meaning the same thing.

Filtering runs on the device against the deals already loaded, so it costs no
network request, and it applies before sorting. The filter is kept per category
for as long as the app is running but is not saved between launches, because
Woot's deals turn over daily. A filter that no longer matches anything after a
refresh is cleared automatically rather than leaving an unexplained empty list.

## Empty categories

Woot answers for every category it documents, but some are routinely empty. A
Woot-Off is an occasional event, so that category has nothing in it most of the
time, and Gourmet no longer carries deals of its own. Both report why in the
status line instead of simply showing an empty list.

## Offline and background behaviour

The deals returned for each category are saved locally and shown straight away on
the next launch, so the app opens on content rather than an empty list while the
network answers, and a phone with no signal still shows the last deals it saw.
Saved deals are always labelled with their age in the status line and are
replaced as soon as Woot responds. A refresh that fails keeps them on screen and
says so instead of reporting an error over an empty list.

The live tile is refreshed by a timer-triggered background task roughly every
thirty minutes, so it stays current while the app is closed. It runs in the app's
own process, which needs no separate component and no manifest declaration, and
it only wakes when a connection is available. One request every half hour is 48 a
day, well inside Woot's limit.

Deal photos are decoded at the size they are drawn rather than at Woot's full
resolution, which is what makes eleven categories of images affordable on a phone
with 512 MB of memory.

## Settings

Settings stores the preferred startup category and appearance locally with
`ApplicationData.Current.LocalSettings`. The About dialog identifies the app,
version, and ZuneTracks.

API access uses the `x-api-key` request header. For private development builds,
create an ignored `Woot.Uwp\LocalBuildConfiguration.cs` from
`Woot.Uwp\LocalBuildConfiguration.cs.example`; do not commit private keys.
When no private build key is present, the service can use a locally stored
configuration value where supported by the build.

## Build prerequisites

Install Visual Studio 2017 with the **Universal Windows Platform development**
workload and Windows 10 SDK **10.0.15063.0**. Open `Woot.Uwp.sln`, select
`Release | ARM`, then build and deploy to a physical Windows 10 Mobile device.
ARM is the package architecture for Windows 10 Mobile.

The project uses the classic UWP project system and targets Windows Mobile
`10.0.15063.0`. The `Store | ARM` configuration creates a Store-upload
candidate and requires a real Store association before packaging.

## Development signing

Development packages use the `CN=WootDevelopment` certificate. Install the
public `WootDevelopment.cer` on a test device before installing a signed AppX.
Never redistribute the private PFX file. Replace the development identity and
certificate with production signing configuration before distribution.

## Live tile

After a successful Featured feed request or offer visit, the app updates a
foreground live tile with the latest available deal title, price, and Woot
branding. The tile does not poll in the background.

## Repository layout

- `Woot.Uwp\` - native UWP application
- `Woot.Uwp\Models\` - feed and offer models
- `Woot.Uwp\Services\` - API, API-key, and tile services
- `Woot.Uwp\Views\` - Settings and offer-detail pages
