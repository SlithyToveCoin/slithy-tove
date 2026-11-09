# Windows 0.1.50

Opening a wallet now waits for the local node to finish starting. The previous
15-second limit could report a failure even when the node was still checking
saved block headers.

The loading bar moves while the node works. The caption shows the current
step. Startup does not have a reliable percentage, so the app does not make
one up. Wallet scanning can show measured progress once that information is
available.

If the node shows no startup activity for 90 seconds, the app asks if you
want to wait longer. Continuing does not restart the node or ask you to
enter your wallet details again. A process exit or a refused connection
credential is reported separately. Cancel stops the wallet-opening request;
the local node can finish starting in the background.

This Windows update leaves wallet files, settings and the test chain in
place. Linux GUI 0.1.50-beta.1 remains the current Linux download.
