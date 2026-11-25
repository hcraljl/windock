$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$icon=Join-Path $root 'SuperDock/Assets/logo.ico'
$stream=[IO.File]::OpenRead($icon)
try{
 $decoder=[System.Windows.Media.Imaging.IconBitmapDecoder]::new($stream,[System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
 $sizes=@($decoder.Frames|ForEach-Object {$_.PixelWidth})
 foreach($size in @(16,20,24,32,40,48,64,96,128,256)){if($sizes -notcontains $size){throw "Missing icon size $size"}}
}finally{$stream.Dispose()}
$logo=[System.Windows.Media.Imaging.BitmapImage]::new([Uri]::new((Join-Path $root 'SuperDock/Assets/logo.png')))
if($logo.PixelWidth -ne 512 -or $logo.PixelHeight -ne 512){throw 'Incorrect settings logo size'}
$rgba=[System.Windows.Media.Imaging.FormatConvertedBitmap]::new($logo,[System.Windows.Media.PixelFormats]::Bgra32,$null,0)
$pixel=New-Object byte[] 4
foreach($corner in @(@(0,0),@(511,0),@(0,511),@(511,511))){$rgba.CopyPixels([System.Windows.Int32Rect]::new($corner[0],$corner[1],1,1),$pixel,4,0);if($pixel[3] -ne 0){throw 'Logo corners are not transparent'}}
$published=Join-Path $root 'artifacts/publish/windock-win-x64/windock-icon-v2.ico'
if((Get-FileHash -LiteralPath $published).Hash -ne (Get-FileHash -LiteralPath $icon).Hash){throw 'Published shortcut icon differs from source'}
Write-Output ('PASS: ten ICO frames ('+($sizes -join ', ')+'), transparent 512px settings logo, matching published shortcut icon.')
