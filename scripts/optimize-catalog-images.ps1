$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceDirectory = Join-Path $PSScriptRoot '../frontend/public/images/products'
$outputDirectory = Join-Path $sourceDirectory 'optimized'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$jpegCodec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.png' -File) {
    $sourceImage = [System.Drawing.Image]::FromFile($sourceFile.FullName)
    try {
        foreach ($maxWidth in @(640, 1024)) {
            $width = [Math]::Min($maxWidth, $sourceImage.Width)
            $height = [int][Math]::Round($sourceImage.Height * $width / $sourceImage.Width)
            $bitmap = New-Object System.Drawing.Bitmap $width, $height
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
            try {
                $graphics.Clear([System.Drawing.Color]::White)
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($sourceImage, 0, 0, $width, $height)
                $parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]85)
                $bitmap.Save((Join-Path $outputDirectory ($sourceFile.BaseName + '-' + $maxWidth + '.jpg')), $jpegCodec, $parameters)
            } finally { $parameters.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $sourceImage.Dispose() }
}
Get-ChildItem -LiteralPath $outputDirectory -File | Measure-Object -Property Length -Sum
