# Vẽ các ảnh logo minh họa (banner) dùng làm ảnh nhúng trong đề mẫu. Chạy: powershell -File make_images.ps1
Add-Type -AssemblyName System.Drawing
$dir = Join-Path $PSScriptRoot 'images'
New-Item -ItemType Directory -Force $dir | Out-Null

function New-Banner($file, $title, $subtitle, $c1, $c2) {
  $w = 640; $h = 140
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'
  $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
  $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 0.0
  $g.FillRectangle($brush, $rect)
  $white = [System.Drawing.Brushes]::White
  $f1 = New-Object System.Drawing.Font 'Segoe UI Semibold', 34, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
  $f2 = New-Object System.Drawing.Font 'Segoe UI', 17, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
  $g.DrawString($title, $f1, $white, 28, 30)
  $g.DrawString($subtitle, $f2, $white, 30, 84)
  $bmp.Save((Join-Path $dir $file), [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}

New-Banner 'tahara-air.png'  'TAHARA AIR'            'Delayed Luggage Form'                          ([System.Drawing.Color]::FromArgb(153,27,27))  ([System.Drawing.Color]::FromArgb(220,38,38))
New-Banner 'bdw-shipping.png' 'BDW SHIPPING'         'The Fast and Reliable Delivery Corporation'    ([System.Drawing.Color]::FromArgb(154,52,18))  ([System.Drawing.Color]::FromArgb(249,115,22))
New-Banner 'paga-expo.png'   'PAGA EXPO'             'Botanical and Horticultural Expo, July 10-13'  ([System.Drawing.Color]::FromArgb(21,128,61))  ([System.Drawing.Color]::FromArgb(74,222,128))
New-Banner 'golden-day.png'  'GOLDEN DAY'            'Stock Photographs'                             ([System.Drawing.Color]::FromArgb(161,98,7))   ([System.Drawing.Color]::FromArgb(250,204,21))
