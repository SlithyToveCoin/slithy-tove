Slithy Tove Linux

This folder contains the Linux command line tools and helper scripts.

Support: support@slithy.io

Main command:

  slithy

Raw tools:

  slithyd
  slithy-cli
  slithy-wallet
  slithy-tx

Install from the website:

  curl -fsSL https://slithy.io/install/linux/slithy-install.sh -o slithy-install.sh
  chmod +x slithy-install.sh
  sudo ./slithy-install.sh

Install from an extracted release package:

  sudo install/linux/slithy-install-local.sh .

After install:

  slithy
  slithy doctor

Start or stop the node service:

  sudo systemctl start slithy-node
  sudo systemctl stop slithy-node

Check updates:

  slithy updates

Report a bug:

  slithy report-bug

Mining:

  slithy start-mining ADDRESS 2
  slithy watch-mining
  slithy stop-mining

Wallets created through the menu ask for a password. If the password is left blank, the menu asks for confirmation before creating an unencrypted test wallet.

Current note:

The public build is still testnet until mainnet genesis and the production treasury wallet are final.
