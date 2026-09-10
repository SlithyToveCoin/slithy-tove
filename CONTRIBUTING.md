# Contributing

Build from a fresh checkout using docs/BUILDING.md before opening a pull request.
Describe the problem, your change and the tests you ran. Include steps to reproduce
UI or wallet issues without sharing a real wallet or recovery words.

Use regtest and disposable wallets for mining and payment tests. Never run a test
against the public network unless it explicitly calls for that network and you
intend to spend its test coins. Do not stop unrelated node processes.

Windows controls belong in the Visual Studio Designer. Keep comments direct:
explain a reason, constraint or side effect that is not apparent from the code.
Preserve upstream copyright and license headers.

Protocol changes need tests for invalid blocks, treasury outputs and competing
chains. Do not change network parameters as part of an unrelated UI fix.

Never commit wallet files, private keys, passwords, real logs or visitor data.
Public release verification keys are expected in the updater source.

By submitting original code, you agree to license your contribution under the
project's applicable source license. Identify copied code and its original license.

Report a vulnerability privately using SECURITY.md, not a public issue.
