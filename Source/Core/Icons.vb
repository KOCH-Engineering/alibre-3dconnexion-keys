' KOCH SpaceMouse - icons: user PNGs, or a generated initials icon coloured by category
' (c) 2026 KOCH Engineering - MIT License

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Drawing.Text
Imports System.IO
Imports System.Linq

Namespace KochSpaceMouse

    Public Module Icons

        Public Const LcdSize As Integer = 24

        Public Function CategoryColor(category As String) As Color
            Select Case category
                Case "Part" : Return Color.FromArgb(&H37, &H8A, &HDD)
                Case "Assembly" : Return Color.FromArgb(&H7F, &H77, &HDD)
                Case "Sketch" : Return Color.FromArgb(&H63, &H99, &H22)
                Case "Drawing" : Return Color.FromArgb(&HBA, &H75, &H17)
                Case Else : Return Color.FromArgb(&H5F, &H5E, &H5A)
            End Select
        End Function

        Public Function Initials(name As String) As String
            Dim words = name.Split(New Char() {" "c, "-"c, "_"c, "/"c}, StringSplitOptions.RemoveEmptyEntries)
            If words.Length = 0 Then Return "?"
            If words.Length = 1 Then
                Dim w = words(0)
                Return If(w.Length >= 2, w.Substring(0, 1).ToUpperInvariant() & w.Substring(1, 1).ToLowerInvariant(), w.ToUpperInvariant())
            End If
            Return (words(0).Substring(0, 1) & words(1).Substring(0, 1)).ToUpperInvariant()
        End Function

        ''' <summary>The function's icon at the given size: its PNG if it has one, otherwise generated.</summary>
        Public Function Render(f As KeyFunction, size As Integer) As Bitmap
            Dim bmp As New Bitmap(size, size, PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
                Dim src = LoadUserIcon(f)
                If src IsNot Nothing Then
                    Using src
                        g.DrawImage(src, New Rectangle(0, 0, size, size))
                    End Using
                Else
                    Dim r As New RectangleF(0.5F, 0.5F, size - 1.0F, size - 1.0F)
                    Using path = RoundRect(r, size * 0.18F), brush As New SolidBrush(CategoryColor(f.Category))
                        g.FillPath(brush, path)
                    End Using
                    Dim text = Initials(f.Name)
                    Using font As New Font("Segoe UI", size * 0.38F, FontStyle.Bold, GraphicsUnit.Pixel),
                          sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                        g.DrawString(text, font, Brushes.White, New RectangleF(0, 0, size, size + 1), sf)
                    End Using
                End If
            End Using
            Return bmp
        End Function

        ''' <summary>24x24 PNG bytes for the 3DxWare profile.</summary>
        Public Function LcdPng(f As KeyFunction) As Byte()
            Using bmp = Render(f, LcdSize), ms As New MemoryStream()
                bmp.Save(ms, ImageFormat.Png)
                Return ms.ToArray()
            End Using
        End Function

        Private Function LoadUserIcon(f As KeyFunction) As Image
            If String.IsNullOrEmpty(f.Icon) Then Return Nothing
            Dim p = Path.Combine(Settings.IconFolder, f.Icon)
            If Not File.Exists(p) Then Return Nothing
            Try
                ' load without locking the file
                Return Image.FromStream(New MemoryStream(File.ReadAllBytes(p)))
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>Copies a PNG/JPG/BMP chosen by the user into the icon folder; returns the stored file name.</summary>
        Public Function ImportIcon(sourceFile As String, f As KeyFunction) As String
            Directory.CreateDirectory(Settings.IconFolder)
            Dim name = f.Id & ".png"
            Using img = Image.FromStream(New MemoryStream(File.ReadAllBytes(sourceFile))),
                  bmp As New Bitmap(64, 64, PixelFormat.Format32bppArgb)
                Using g = Graphics.FromImage(bmp)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(img, New Rectangle(0, 0, 64, 64))
                End Using
                bmp.Save(Path.Combine(Settings.IconFolder, name), ImageFormat.Png)
            End Using
            Return name
        End Function

        Private Function RoundRect(r As RectangleF, radius As Single) As GraphicsPath
            Dim d = radius * 2
            Dim p As New GraphicsPath()
            p.AddArc(r.X, r.Y, d, d, 180, 90)
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
            p.CloseFigure()
            Return p
        End Function
    End Module

End Namespace
