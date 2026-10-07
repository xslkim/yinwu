param(
  [int]$Port = 5612,
  [string]$Token,
  [string]$Commands
)
$client = New-Object System.Net.Sockets.TcpClient("127.0.0.1", $Port)
$stream = $client.GetStream()
$reader = New-Object System.IO.StreamReader($stream)
$writer = New-Object System.IO.StreamWriter($stream)
$writer.AutoFlush = $true
function Read-Available {
  Start-Sleep -Milliseconds 500
  $out = ""
  while ($stream.DataAvailable) { $out += [char]$reader.Read() }
  return $out
}
$null = Read-Available
$writer.WriteLine("auth $Token")
$resp = Read-Available
Write-Host "AUTH-RESP: $resp"
foreach ($cmd in ($Commands -split ';')) {
  $writer.WriteLine($cmd)
  $r = Read-Available
  Write-Host "CMD [$cmd] => $r"
}
$writer.WriteLine("quit")
$client.Close()
