// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WebBenchmark;

// `dotnet run --project apps/WebBenchmark` serves the app on upstream's port 4280; `--urls` names another.
var app = BenchmarkApp.Create(args.Length == 0 ? ["--urls", BenchmarkApp.DefaultUrl] : args);
await app.RunAsync();
