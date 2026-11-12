# WebBenchmark tests

The tests that drive [`apps/WebBenchmark`](../WebBenchmark) with the web engine. They port upstream's `apps/web-benchmark/tests`, laid out as upstream lays them out: each upstream test file is a directory, each `describe` a class, each `it` a test.

| Directory | Upstream | What it runs |
| --- | --- | --- |
| `Smoke` | `smoke.e2e.ts` | The home page lists the scenarios and opens one. No model. |
| `LoginForm` | `login-form.e2e.ts` | `describe('login form')`: the three rejections and sign in / log out. |
| `OverlayTrap` | `overlay-trap.e2e.ts` | The banner, then the transparent overlay's chip, then Continue. |
| `HoverMenu` | `hover-menu.e2e.ts` | `describe('hover menu')`: a decoy leaf, and Account, Billing, Redeem voucher by hovering. |
| `CheckoutReview` | `checkout-review.e2e.ts` | The planted raw greeting token and `undefined` delivery date, then the order is placed. |
| `ProductReviews` | `product-reviews.e2e.ts` | The planted coffee-grinder reviews on the running-shoe page. |
| `AriaHiddenFlow` | `aria-hidden-flow.e2e.ts` | `describe('aria hidden flow')`: pick the Pro plan and confirm it, and a wrong plan keeps the picker open, by visible text only. |
| `DivSoup` | `div-soup.e2e.ts` | `describe('div soup')`: a wrong PIN, and the right one after the delete tile fixes a typo. |
| `CartTotals` | `cart-totals.e2e.ts` | `describe('cart totals')`: the planted stale order summary, and the quantity floor of one. |
| `FilterDeepLink` | `filter-deep-link.e2e.ts` | `describe('filter deep link')`: filtering writes `?q=` to the URL, and a shared link restores the filter. |
| `StickyChrome` | `sticky-chrome.e2e.ts` | The Accept terms button is scrolled clear of the sticky footer before the tap. |
| `InfiniteScroll` | `infinite-scroll.e2e.ts` | The feed is scrolled until the Golden Ticket loads, then claimed. |
| `DeferredDialog` | `deferred-dialog.e2e.ts` | One double-clicked submit opens one confirm, which opens on a timer after the click. |
| `DebouncedSearch` | `debounced-search.e2e.ts` | `describe('debounced search')`: type into the combobox, pick from the portal listbox (a decoy, then the exact item). |
| `NewsletterSignup` | `newsletter-signup.e2e.ts` | The planted silent failure: no feedback, a console `TypeError`. |
| `NativeDialogs` | `native-dialogs.e2e.ts` | `describe('native dialogs')`: the confirm, prompt, and alert chain, a dismissed confirm, a wrong code. |
| `PriceSorting` | `price-sorting.e2e.ts` | `describe('price sorting')`: the featured order, and the planted sort by original price. |
| `OrderHistory` | `order-history.e2e.ts` | `describe('order history')`: overview and settings, and the Orders tab stuck loading (planted). |
| `ShadowDomForm` | `shadow-dom-form.e2e.ts` | `describe('shadow DOM form')`: a wrong access code is rejected, and the code from the hint inside the root grants access. |
| `IframeForm` | `iframe-form.e2e.ts` | `describe('iframe form')`: a wrong code is refused by the host page, and the code read from one frame applies through the other. |
| `ImageOnlyUi` | `image-only-ui.e2e.ts` | `describe('image only ui')`: the bare svg locator is ambiguous, a wrong icon keeps the icons, and star then heart verifies them. |
| `CssContentUi` | `css-content-ui.e2e.ts` | `describe('css content ui')`: the labels are CSS `::before` content, read by a poll over the computed style. |
| `LyingLabels` | `lying-labels.e2e.ts` | `describe('lying labels')`: visible text beats the accessible names, the text match is ambiguous, and label-sibling fields take the payment. |
| `RichTextEditor` | `rich-text-editor.e2e.ts` | `describe('rich text editor')`: bolding exactly "approved" passes; bold that spills into "release" fails. |
| `StaleDom` | `stale-dom.e2e.ts` | The moving target is tapped three times across list rebuilds. |
| `VirtualizedTable` | `virtualized-table.e2e.ts` | The windowed table is scrolled to the Golden Row and claimed (about two minutes). |
| `CanvasOnly` | `canvas-only.e2e.ts` | `describe('canvas only')`: the square, circle, and diamond taps at pixel coordinates, and the out-of-order reset. |
| `CanvasWhiteboard` | `canvas-whiteboard.e2e.ts` | `describe('canvas whiteboard')`: drag both shapes into their slots and save, and the early save refusal. |
| `DragAndDrop` | `drag-and-drop.e2e.ts` | `describe('drag and drop')`: the out-of-order reset, and Banana then Cherry. |
| `KanbanBoard` | `kanban-board.e2e.ts` | `describe('kanban board')`: pointer drags, a swipe drag, and the untouched board rejected. |
| `DatePicker` | `date-picker.e2e.ts` | `describe('date picker')`: navigate three months and book September 17, and the error without the exact date. |
| `OtpAutoAdvance` | `otp-auto-advance.e2e.ts` | `describe('otp auto-advance')`: focus advance and verify, backspace refocus, and a wrong code clearing every box. |
| `AsyncStates` | `async-states.e2e.ts` | `describe('async states')`: the skeleton wait, the toast code read off the screen, and a wrong code after the toast is gone. |
| `ContextMenuTrap` | `context-menu-trap.e2e.ts` | `describe('context menu trap')`: right-click does nothing, the row menu renames the file, and the decoy items refuse. |
| `OnboardingWizard` | `onboarding-wizard.e2e.ts` | `describe('onboarding wizard')`: the five-step brief through bare-div inputs, dropdowns, the calendar, steppers, and toggles. |
| `PlaybookCleanup` | `playbook-cleanup.e2e.ts` | `describe('playbook cleanup')`: delete a draft and confirm an exact-name search no longer finds it; cancel keeps it. |
| `SavedAddresses` | `saved-addresses.e2e.ts` | `describe('saved addresses')`: add an address, ignore an empty add, and the planted delete that keeps the address. |
| `Wishlist` | `wishlist.e2e.ts` | `describe('wishlist')`: saving toggles the button and badge; the planted empty wishlist tab. |
| `PromoStorefront` | `promo-storefront.e2e.ts` | Buys a deal once the promo chrome (cookie banner, chat widget, skeleton reviews) is out of the way. |
| `GiftCardPurchase` | `gift-card-purchase.e2e.ts` | `describe('gift card purchase')`: amount and email validation, then the gift card is sent despite the aria traps. |
| `TestsAgent/LoginForm` | `tests-agent/login-form.e2e.ts` | Real model: the act signs in with the benchmark account. |
| `TestsAgent/PlantedBugs` | `tests-agent/planted-bugs.e2e.ts` | Real model: the agent's judgment catches the planted cart-totals, checkout-review, and wishlist bugs. |
| `TestsAgent/Scenarios` | `tests-agent/scenarios.e2e.ts` | Real model: the act completes each scenario, and the filter deep link. |
| `TestsAgent/UniqueCache` | `tests-agent/unique-cache.e2e.ts` | Real model: the act adds an address with a run-unique label. |

The tests start the app in process on a free loopback port (`Fixtures/BenchmarkTest.cs`). Upstream's `e2e.config.ts` starts `pnpm run start` through `app.command`; the NUnit adapter does not start `app.command`. They need a Playwright browser, like every web test of the repository, and no model login.

```bash
dotnet test apps/WebBenchmark.E2ETests
```

## Not ported

Every upstream `tests/*.e2e.ts` suite is ported, with its helpers: `support.ts` is `Fixtures/Support.cs` and `canvas.ts` is `Fixtures/Canvas.cs`. The four `tests-agent/` files are ported under `TestsAgent/` as real-model tests (`[Category("RealModel")]`). Two parts of them do not match upstream yet:

- The replay recordings in upstream's `apps/web-benchmark/.e2e/cache` are not copied: they are keyed on the project id `dev.e2e.web-benchmark-agent`, and the .NET agent does not read a project id, so the recordings do not replay here (see COMPATIBILITY.md).
- The scenario tests pass a five-minute act timeout, as upstream's agent runs without a step deadline; the port's default act deadline is 30 seconds with no configuration setting.

