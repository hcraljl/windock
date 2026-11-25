$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Join-Path (Split-Path $PSScriptRoot -Parent) 'SuperDock\Assets'
$source=[System.Windows.Media.Imaging.BitmapImage]::new()
$source.BeginInit();$source.CacheOption=[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$source.UriSource=[Uri]::new((Join-Path $root 'logo-master.png'));$source.EndInit();$source.Freeze()
$frames=@()
foreach($size in @(16,20,24,32,40,48,64,96,128,256,512)){
 $visual=[System.Windows.Media.DrawingVisual]::new();[System.Windows.Media.RenderOptions]::SetBitmapScalingMode($visual,[System.Windows.Media.BitmapScalingMode]::HighQuality)
 $drawing=$visual.RenderOpen();$drawing.DrawImage($source,[System.Windows.Rect]::new(0,0,$size,$size));$drawing.Close()
 $bitmap=[System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($visual)
 $encoder=[System.Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap));$stream=[IO.MemoryStream]::new();$encoder.Save($stream);$bytes=$stream.ToArray()
 if($size -le 256){$frames+=,@{Size=$size;Bytes=$bytes}}
 if($size -eq 512){[IO.File]::WriteAllBytes((Join-Path $root 'logo.png'),$bytes)}
 $stream.Dispose()
}
$output=[IO.File]::Create((Join-Path $root 'logo.ico'));$writer=[IO.BinaryWriter]::new($output)
try{
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count);$offset=6+16*$frames.Count
 foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
}finally{$writer.Dispose()}
Write-Output ('Created logo.png (512px), logo.ico ('+$frames.Count+' sizes, 16-256px).')