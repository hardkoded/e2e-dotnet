// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

// The tests drive a real window on an unlocked desktop, and the agent tests need a model login.
[assembly: Category("RealModel")]
[assembly: Category("Windows")]
// One app window at a time: keys and clicks go to the window in the foreground.
[assembly: NonParallelizable]
