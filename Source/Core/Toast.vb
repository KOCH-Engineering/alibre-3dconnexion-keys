' KOCH SpaceMouse - a short message near the mouse pointer ("Boss Extrude is not available here")
' (c) 2026 KOCH Engineering - MIT License

Imports System.Drawing
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class Toast
        Inherits Form

        Private Shared _current As Toast

        Public Shared Shadows Sub Show(text As String)
            Try
                Media.SystemSounds.Beep.Play()
                If _current IsNot Nothing AndAlso Not _current.IsDisposed Then _current.Close()
                _current = New Toast(text)
                _current.Visible = True
            Catch
            End Try
        End Sub

        Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
            Get
                Return True     ' never take the focus from Alibre
            End Get
        End Property

        Protected Overrides ReadOnly Property CreateParams As CreateParams
            Get
                Dim cp = MyBase.CreateParams
                cp.ExStyle = cp.ExStyle Or &H8 Or &H80 Or &H8000000   ' topmost, tool window, no activate
                Return cp
            End Get
        End Property

        Private Sub New(text As String)
            FormBorderStyle = FormBorderStyle.None
            ShowInTaskbar = False
            StartPosition = FormStartPosition.Manual
            TopMost = True
            BackColor = Color.FromArgb(&H33, &H33, &H33)
            Dim lbl As New Label With {.Text = text, .AutoSize = True, .ForeColor = Color.White,
                                       .Font = New Font("Segoe UI", 9.5F), .Location = New Point(10, 7)}
            Controls.Add(lbl)
            Dim sz = TextRenderer.MeasureText(text, lbl.Font)
            Size = New Size(sz.Width + 22, sz.Height + 14)
            Dim p = Cursor.Position
            Location = New Point(p.X + 16, p.Y + 18)
            Dim t As New Timer With {.Interval = 2200}
            AddHandler t.Tick, Sub()
                                   t.Stop()
                                   t.Dispose()
                                   Close()
                               End Sub
            t.Start()
        End Sub
    End Class

End Namespace
