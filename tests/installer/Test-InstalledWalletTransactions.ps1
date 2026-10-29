#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstalledDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This fixture requires Windows.' }
$installed = [IO.Path]::GetFullPath($InstalledDirectory)
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$allowed = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
if (-not $evidence.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $evidence -PathType Container)) {
    throw 'Use the evidence directory created by the disposable installer test.'
}
$assemblyPath = Join-Path $installed 'Slithy Tove.dll'
$expectedAssemblyHash = '038D7339FDBE171D069E19BE34EE5AB704FBA031EBC9BD31C6A4EDCD99281CA3'
if ((Get-FileHash -LiteralPath $assemblyPath).Hash -ne $expectedAssemblyHash) {
    throw 'This transaction fixture is pinned to the signed 0.1.44 application assembly.'
}
$phases = [Collections.Generic.List[object]]::new()
$phase = 'load installed application'
$watch = [Diagnostics.Stopwatch]::StartNew()
$nodeProcess = $null
$node = $null
$sender = $null
$receiver = $null
$endpoint = $null
$work = Join-Path $evidence ('private-regtest-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work) | Out-Null

function Save-Result([bool]$Passed, [string]$ErrorMessage = '') {
    @{
        Passed = $Passed; Phase = $phase; Error = $ErrorMessage
        AssemblySha256 = $expectedAssemblyHash; Seconds = [math]::Round($watch.Elapsed.TotalSeconds, 2)
        Phases = $phases.ToArray(); Network = 'isolated regtest'; BlocksPerRequest = 10
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'transactions.json') -Encoding utf8
}

function Set-Phase([string]$Name) {
    $script:phase = $Name
    $phases.Add([pscustomobject]@{ Name = $Name; StartedUtc = [DateTimeOffset]::UtcNow })
    Write-Output "Transaction fixture: $Name"
    Save-Result $false 'Still running'
}

function Call-App($Client, [string]$Method, [object[]]$Values, [int]$DeadlineSeconds = 185) {
    $instance = $Client.PSObject.BaseObject
    $methodInfo = $instance.GetType().GetMethod($Method, [Reflection.BindingFlags]'Public,Instance')
    if ($null -eq $methodInfo) { throw "Installed app method is missing: $Method" }
    # Reflection needs the CLR objects, not PowerShell's wrappers around records.
    $arguments = [object[]]::new($Values.Count)
    for ($i = 0; $i -lt $Values.Count; $i++) {
        if ($null -ne $Values[$i]) { $arguments[$i] = $Values[$i].PSObject.BaseObject }
    }
    $task = [Threading.Tasks.Task]$methodInfo.Invoke($instance, $arguments)
    [void]$task.WaitAsync([TimeSpan]::FromSeconds($DeadlineSeconds)).GetAwaiter().GetResult()
    if ($methodInfo.ReturnType.IsGenericType) {
        return $task.GetType().GetProperty('Result').GetValue($task)
    }
}

try {
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
    $node = [Activator]::CreateInstance($assembly.GetType('Slithy_Tove.NodeRpcClient', $true), $true)
    $sender = [Activator]::CreateInstance($assembly.GetType('Slithy_Tove.WalletRpcClient', $true), $true)
    $receiver = [Activator]::CreateInstance($assembly.GetType('Slithy_Tove.WalletRpcClient', $true), $true)
    Set-Phase 'start isolated regtest node'
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $installed 'Binaries/slithyd.exe'))
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in @('-regtest', ('-datadir=' + $work), '-server=1', '-listen=0', '-connect=0',
            '-dnsseed=0', '-discover=0', '-rpcbind=127.0.0.1', '-rpcallowip=127.0.0.1',
            ('-rpcport=' + $port), '-fallbackfee=0.0001', '-printtoconsole=0')) {
        $start.ArgumentList.Add($argument)
    }
    $nodeProcess = [Diagnostics.Process]::Start($start)
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $ready = $false
    $cookiePath = Join-Path $work 'regtest/.cookie'
    while (-not $ready -and [DateTime]::UtcNow -lt $deadline) {
        if ($nodeProcess.HasExited) { throw "The fixture node exited with code $($nodeProcess.ExitCode)." }
        if (Test-Path -LiteralPath $cookiePath) {
            $cookie = [IO.File]::ReadAllText($cookiePath).Trim().Split(':', 2)
            $builder = [UriBuilder]::new('http', '127.0.0.1', $port)
            $builder.UserName = $cookie[0]
            $builder.Password = $cookie[1]
            $endpoint = $builder.Uri
            $cookie = $null
            try {
                $info = Call-App $node 'GetInfoAsync' @($endpoint, [TimeSpan]::FromSeconds(2)) 5
                if ($info.NetworkType -ne 'regtest') { throw 'The fixture reached the wrong chain.' }
                $ready = $true
            } catch { Start-Sleep -Milliseconds 250 }
        } else { Start-Sleep -Milliseconds 250 }
    }
    if (-not $ready) { throw 'The fixture node did not become ready within 45 seconds.' }

    Set-Phase 'create encrypted sender and receiver wallets'
    $senderPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    $receiverPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    Call-App $sender 'CreateWalletAsync' @($endpoint, 'sender', $senderPassword, $null)
    Call-App $receiver 'CreateWalletAsync' @($endpoint, 'receiver', $receiverPassword, $null)
    $senderSnapshot = Call-App $sender 'GetSnapshotAsync' @($endpoint)
    $receiverSnapshot = Call-App $receiver 'GetSnapshotAsync' @($endpoint)
    if (-not $senderSnapshot.Address.StartsWith('rslithy') -or -not $receiverSnapshot.Address.StartsWith('rslithy')) { throw 'Expected regtest addresses.' }

    # The bundled self-test requests 101 blocks within one 30-second RPC deadline.
    # Smaller requests use the same installed app method without that CPU-time assumption.
    for ($height = 0; $height -lt 101;) {
        $count = [math]::Min(10, 101 - $height)
        Set-Phase "mine fixture blocks $($height + 1)-$($height + $count)"
        Call-App $node 'GenerateBlocksAsync' @($endpoint, [string]$senderSnapshot.Address, [int]$count) 40
        $height += $count
    }
    $senderSnapshot = Call-App $sender 'GetSnapshotAsync' @($endpoint)
    if ($senderSnapshot.UnlockedBalance -lt 9) { throw 'Mining rewards did not mature in the fixture wallet.' }

    Set-Phase 'prepare and relay one coin using installed wallet code'
    $prepared = Call-App $sender 'PrepareTransferAsync' @($endpoint, [string]$receiverSnapshot.Address, [uint64]100000000)
    $sent = Call-App $sender 'RelayTransferAsync' @($endpoint, $prepared)
    if ($sent.TransactionHash -notmatch '^[a-fA-F0-9]{64}$' -or $prepared.FeeAtomic -eq 0 -or $sent.FeeAtomic -ne $prepared.FeeAtomic) {
        throw 'The transfer result or fee did not match the prepared transaction.'
    }
    Set-Phase 'confirm transfer and check recipient balance'
    Call-App $node 'GenerateBlocksAsync' @($endpoint, [string]$senderSnapshot.Address, [int]1) 40
    $receiverSnapshot = Call-App $receiver 'GetSnapshotAsync' @($endpoint)
    if ($receiverSnapshot.UnlockedBalance -lt 1) { throw 'The recipient did not receive the confirmed coin.' }

    Set-Phase 'back up the encrypted wallet, close it and reopen the backup'
    $backupDirectory = Join-Path $work 'backup-wallet'
    [IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
    Call-App $receiver 'BackupWalletAsync' @($endpoint, (Join-Path $backupDirectory 'wallet.dat'))
    Call-App $receiver 'LockWalletOnlyAsync' @($endpoint)
    $locked = Call-App $receiver 'NeedsSpendingPasswordAsync' @($endpoint)
    if (-not $locked) { throw 'The encrypted recipient wallet did not lock.' }
    Call-App $receiver 'CloseWalletAsync' @($endpoint)
    Call-App $receiver 'OpenWalletAsync' @($endpoint, $backupDirectory, $receiverPassword, $null)
    $restored = Call-App $receiver 'GetSnapshotAsync' @($endpoint)
    if ($restored.UnlockedBalance -ne $receiverSnapshot.UnlockedBalance) { throw 'The restored backup has a different spendable balance.' }

    Set-Phase 'close fixture wallets and stop the isolated node'
    Call-App $sender 'CloseWalletAsync' @($endpoint)
    Call-App $receiver 'CloseWalletAsync' @($endpoint)
    Call-App $node 'StopDaemonAsync' @($endpoint) 15
    if (-not $nodeProcess.WaitForExit(15000)) { throw 'The fixture node did not exit after its stop request.' }
    Save-Result $true
    Write-Output 'PASS Installed wallet code created, funded, sent, confirmed, backed up and restored real regtest wallets.'
} catch {
    $message = $_.Exception.GetBaseException().Message
    $message = $message -replace '(https?://)[^\s/@:]+:[^\s/@]+@', '$1[redacted]@'
    Save-Result $false $message
    throw "Transaction fixture phase '$phase' failed: $message"
} finally {
    foreach ($client in @($sender, $receiver, $node)) { if ($null -ne $client) { $client.Dispose() } }
    if ($null -ne $nodeProcess) {
        if (-not $nodeProcess.HasExited) { $nodeProcess.Kill($true); $nodeProcess.WaitForExit() }
        $nodeProcess.Dispose()
    }
}
