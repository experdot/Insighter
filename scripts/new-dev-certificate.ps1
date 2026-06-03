param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPfxPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputCerPath,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$Subject = 'CN=Insighter Dev'
)

$ErrorActionPreference = 'Stop'

$securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
$certificate = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName 'Insighter Dev Certificate' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3') `
    -NotAfter (Get-Date).AddYears(2)

Export-PfxCertificate -Cert $certificate -FilePath $OutputPfxPath -Password $securePassword | Out-Null
Export-Certificate -Cert $certificate -FilePath $OutputCerPath | Out-Null
