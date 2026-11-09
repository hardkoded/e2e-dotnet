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

The tests start the app in process on a free loopback port (`Fixtures/BenchmarkTest.cs`). Upstream's `e2e.config.ts` starts `pnpm run start` through `app.command`; the NUnit adapter does not start `app.command`. They need a Playwright browser, like every web test of the repository, and no model login.

```bash
dotnet test apps/WebBenchmark.E2ETests
```

## Not ported yet

Tracked by #1149; each follows in a later pull request.

- The other 28 `tests/*.e2e.ts` suites (`async-states`, `canvas-only`, `canvas-whiteboard`, `context-menu-trap`, `css-content-ui`, `date-picker`, `debounced-search`, `deferred-dialog`, `drag-and-drop`, `gift-card-purchase`, `iframe-form`, `image-only-ui`, `kanban-board`, `lying-labels`, `native-dialogs`, `newsletter-signup`, `onboarding-wizard`, `order-history`, `otp-auto-advance`, `playbook-cleanup`, `price-sorting`, `promo-storefront`, `rich-text-editor`, `saved-addresses`, `shadow-dom-form`, `stale-dom`, `virtualized-table`, `wishlist`), `support.ts`, and `canvas.ts`, with the scenarios they drive.
- `tests-agent/`: `login-form`, `planted-bugs`, `scenarios`, and `unique-cache` (real-model tests; `planted-bugs` and `scenarios` replay committed cache recordings).
