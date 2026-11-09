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

The tests start the app in process on a free loopback port (`Fixtures/BenchmarkTest.cs`). Upstream's `e2e.config.ts` starts `pnpm run start` through `app.command`; the NUnit adapter does not start `app.command`. They need a Playwright browser, like every web test of the repository, and no model login.

```bash
dotnet test apps/WebBenchmark.E2ETests
```

## Not ported yet

Tracked by #1149; each follows in a later pull request.

- The other 34 `tests/*.e2e.ts` suites (`aria-hidden-flow`, `async-states`, `canvas-only`, `canvas-whiteboard`, `cart-totals`, `context-menu-trap`, `css-content-ui`, `date-picker`, `debounced-search`, `deferred-dialog`, `div-soup`, `drag-and-drop`, `filter-deep-link`, `gift-card-purchase`, `iframe-form`, `image-only-ui`, `infinite-scroll`, `kanban-board`, `lying-labels`, `native-dialogs`, `newsletter-signup`, `onboarding-wizard`, `order-history`, `otp-auto-advance`, `playbook-cleanup`, `price-sorting`, `promo-storefront`, `rich-text-editor`, `saved-addresses`, `shadow-dom-form`, `stale-dom`, `sticky-chrome`, `virtualized-table`, `wishlist`), `support.ts`, and `canvas.ts`, with the scenarios they drive.
- `tests-agent/`: `login-form`, `planted-bugs`, `scenarios`, and `unique-cache` (real-model tests; `planted-bugs` and `scenarios` replay committed cache recordings).
