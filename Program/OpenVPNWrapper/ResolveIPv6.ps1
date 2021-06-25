# Address of the DDNSv6 server
$ddnsName = "example.myfritz.net"
# Size of the routing prefix
$netSize  = 56
# Subnet of the device 
$subnet   = "01"
# Interface identifier of the device to connect to
$itfIdent = "0123:4567:89ab:cdef"
# Location of the configuration file
$ovpnFile = "C:\Program Files\OpenVPN\config\NAS01.ovpn"

$fullAddress = (Resolve-DnsName $ddnsName).IPAddress
for ($i = 4; $i -gt 0; $i--) { $fullAddress = $fullAddress.Substring(0, $fullAddress.LastIndexOf(":")) }
$index = $fullAddress.LastIndexOf(":") + 1
switch ($netSize) {
	48 { $fullAddress.Substring(0, [math]::Max([math]::Min($index + 0, $fullAddress.Length - 4), $index)) + $subnet + ":" + $itfIdent}
	52 { $fullAddress.Substring(0, [math]::Max([math]::Min($index + 1, $fullAddress.Length - 3), $index)) + $subnet + ":" + $itfIdent}
	56 { $fullAddress.Substring(0, [math]::Max([math]::Min($index + 2, $fullAddress.Length - 2), $index)) + $subnet + ":" + $itfIdent}
	60 { $fullAddress.Substring(0, [math]::Max([math]::Min($index + 3, $fullAddress.Length - 1), $index)) + $subnet + ":" + $itfIdent}
	64 { $fullAddress.Substring(0, [math]::Max([math]::Min($index + 4, $fullAddress.Length - 0), $index)) + $subnet + ":" + $itfIdent}
}
