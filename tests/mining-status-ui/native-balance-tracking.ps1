param(
    [string]$BinaryDirectory = (Join-Path $PSScriptRoot '../../build/native/windows/bin')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$binaries = (Resolve-Path -LiteralPath $BinaryDirectory).Path
$daemon = Join-Path $binaries 'slithyd.exe'
$cli = Join-Path $binaries 'slithy-cli.exe'
if (!(Test-Path -LiteralPath $daemon) -or !(Test-Path -LiteralPath $cli)) {
    throw 'Build the Windows native tools before running this check.'
}

# Every file and RPC call belongs to this disposable, disconnected regtest node.
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = [IO.Path]::GetFullPath((Join-Path $tempParent ('slithy-native-balance-' + [Guid]::NewGuid().ToString('N'))))
if (!$root.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The test directory must be inside the temporary directory.'
}
New-Item -ItemType Directory -Path $root | Out-Null
$wallets = Join-Path $root 'wallets'
New-Item -ItemType Directory -Path $wallets | Out-Null
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$rpcPort = $listener.LocalEndpoint.Port
$listener.Stop()

$node = $null
$stdout = $null
$stderr = $null
$script:checks = 0
$script:methods = [Collections.Generic.List[string]]::new()

function Check([bool]$Passed, [string]$Message) {
    if (!$Passed) { throw "FAIL: $Message" }
    $script:checks++
    Write-Output "PASS: $Message"
}

function Invoke-Node([string]$Method, [string[]]$Arguments = @(), [string]$Wallet = '') {
    $script:methods.Add($Method)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $cli
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-regtest', "-datadir=$root", '-rpcconnect=127.0.0.1', "-rpcport=$rpcPort", '-rpcclienttimeout=45')) {
        $start.ArgumentList.Add($argument)
    }
    # slithy-cli reads this node's cookie from the isolated data directory.
    if ($Wallet) { $start.ArgumentList.Add("-rpcwallet=$Wallet") }
    $start.ArgumentList.Add($Method)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $command = [Diagnostics.Process]::Start($start)
    try {
        $outputTask = $command.StandardOutput.ReadToEndAsync()
        $errorTask = $command.StandardError.ReadToEndAsync()
        if (!$command.WaitForExit(60000)) {
            $command.Kill($true)
            $command.WaitForExit()
            throw "The disposable $Method request timed out."
        }
        $output = $outputTask.GetAwaiter().GetResult()
        $errorOutput = $errorTask.GetAwaiter().GetResult()
        if ($command.ExitCode -ne 0) {
            # Do not print credentials, wallet paths or arbitrary daemon output.
            throw "The disposable $Method request failed (exit $($command.ExitCode))."
        }
        return $output.Trim()
    }
    finally { $command.Dispose() }
}

try {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $daemon
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @(
        '-regtest', "-datadir=$root", "-walletdir=$wallets", '-server=1',
        '-listen=0', '-connect=0', '-dnsseed=0', '-discover=0', '-listenonion=0', '-networkactive=0',
        '-rpcbind=127.0.0.1', '-rpcallowip=127.0.0.1', "-rpcport=$rpcPort",
        '-fallbackfee=0.0001', '-printtoconsole=0', '-dbcache=32'
    )) { $start.ArgumentList.Add($argument) }
    $node = [Diagnostics.Process]::Start($start)
    $stdout = $node.StandardOutput.ReadToEndAsync()
    $stderr = $node.StandardError.ReadToEndAsync()
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if ($node.HasExited) { throw 'The disposable node exited during startup.' }
        try {
            $chain = Invoke-Node 'getblockchaininfo' | ConvertFrom-Json
            $ready = $chain.chain -eq 'regtest'
        }
        catch { Start-Sleep -Milliseconds 150 }
    } while (!$ready -and [DateTime]::UtcNow -lt $deadline)
    Check $ready 'The disposable node started on regtest.'
    $network = Invoke-Node 'getnetworkinfo' | ConvertFrom-Json
    Check (!$network.networkactive -and $network.connections -eq 0) 'P2P networking is off and there are no peers.'

    Invoke-Node 'createwallet' @('sender') | Out-Null
    Invoke-Node 'createwallet' @('receiver') | Out-Null
    $senderAddress = Invoke-Node 'getnewaddress' -Wallet 'sender'
    $receiverAddress = Invoke-Node 'getnewaddress' -Wallet 'receiver'
    Invoke-Node 'generatetoaddress' @('1', $senderAddress) | Out-Null
    $initial = Invoke-Node 'getbalances' -Wallet 'sender' | ConvertFrom-Json
    Check ([decimal]$initial.mine.immature -eq 9 -and [decimal]$initial.mine.trusted -eq 0) 'A new reward appears without a rescan.'
    Invoke-Node 'generatetoaddress' @('100', $senderAddress) | Out-Null
    $matured = Invoke-Node 'getbalances' -Wallet 'sender' | ConvertFrom-Json
    Check ([decimal]$matured.mine.trusted -gt 0) 'The reward becomes spendable after maturity without a rescan.'

    $height = [long](Invoke-Node 'getblockcount')
    $payment = Invoke-Node 'sendtoaddress' @($receiverAddress, '1.0') -Wallet 'sender'
    $pending = Invoke-Node 'getbalances' -Wallet 'receiver' | ConvertFrom-Json
    Check ([long](Invoke-Node 'getblockcount') -eq $height -and [decimal]$pending.mine.untrusted_pending -eq 1) 'An incoming payment appears at the same block height without a rescan.'
    Invoke-Node 'generatetoaddress' @('1', $senderAddress) | Out-Null
    $tip = Invoke-Node 'getbestblockhash'
    $confirmed = Invoke-Node 'getbalances' -Wallet 'receiver' | ConvertFrom-Json
    $transaction = Invoke-Node 'gettransaction' @($payment) -Wallet 'receiver' | ConvertFrom-Json
    Check ([decimal]$confirmed.mine.trusted -eq 1 -and [decimal]$confirmed.mine.untrusted_pending -eq 0 -and $transaction.confirmations -eq 1) 'The payment confirms and the balance updates without a rescan.'

    Invoke-Node 'invalidateblock' @($tip) | Out-Null
    $reverted = Invoke-Node 'getbalances' -Wallet 'receiver' | ConvertFrom-Json
    $transaction = Invoke-Node 'gettransaction' @($payment) -Wallet 'receiver' | ConvertFrom-Json
    Check ([long](Invoke-Node 'getblockcount') -eq $height -and [decimal]$reverted.mine.trusted -eq 0 -and [decimal]$reverted.mine.untrusted_pending -eq 1 -and $transaction.confirmations -eq 0) 'Disconnecting the disposable tip reverses confirmation and updates the balance.'
    Invoke-Node 'reconsiderblock' @($tip) | Out-Null
    $reconfirmed = Invoke-Node 'getbalances' -Wallet 'receiver' | ConvertFrom-Json
    $transaction = Invoke-Node 'gettransaction' @($payment) -Wallet 'receiver' | ConvertFrom-Json
    Check ([decimal]$reconfirmed.mine.trusted -eq 1 -and [decimal]$reconfirmed.mine.untrusted_pending -eq 0 -and $transaction.confirmations -eq 1) 'Reconsidering the disposable tip restores the confirmed balance.'
    Check (!$script:methods.Contains('rescanblockchain')) 'No check requested a wallet rescan.'
    $network = Invoke-Node 'getnetworkinfo' | ConvertFrom-Json
    Check (!$network.networkactive -and $network.connections -eq 0) 'The entire run remained disconnected from public networks.'
    Write-Output "Passed $script:checks native balance-tracking checks. No real wallet or installed node was used."
}
finally {
    if ($node) {
        if (!$node.HasExited) {
            try { Invoke-Node 'stop' | Out-Null } catch { }
            if (!$node.WaitForExit(20000)) {
                # This is the exact process created above, never an installed Slithy process.
                $node.Kill($true)
                $node.WaitForExit()
                Write-Warning 'The disposable node needed forced cleanup after its stop request.'
            }
        }
        if ($stdout) { $stdout.GetAwaiter().GetResult() | Out-Null }
        if ($stderr) { $stderr.GetAwaiter().GetResult() | Out-Null }
        $node.Dispose()
    }
    $cleanup = [IO.Path]::GetFullPath($root)
    if (!$cleanup.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or
        ![IO.Path]::GetFileName($cleanup).StartsWith('slithy-native-balance-', [StringComparison]::Ordinal)) {
        throw 'The test cleanup path is outside its disposable directory.'
    }
    if (Test-Path -LiteralPath $cleanup) { Remove-Item -LiteralPath $cleanup -Recurse -Force }
}
