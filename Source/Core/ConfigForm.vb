' KOCH SpaceMouse - the "3Dconnexion keys" dialog
' (c) 2026 KOCH Engineering - MIT License

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Linq
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class ConfigForm
        Inherits Form

        Private ReadOnly _settings As Settings
        Private _context As String
        Private _selected As Integer = 0
        Private _keyboard As Boolean          ' False = SpaceMouse Enterprise, True = Keyboard Pro
        Private ReadOnly _previewLabel As Label

        Private ReadOnly _tabs As New List(Of RadioButton)
        Private ReadOnly _lcd As LcdPreview
        Private ReadOnly _combos(Contexts.KeyCount - 1) As FunctionPicker
        Private ReadOnly _rows(Contexts.KeyCount - 1) As Panel
        Private ReadOnly _auto As CheckBox
        Private _loading As Boolean
        ' Alibre commands found in its ribbons that are not (yet) in the catalog
        Private ReadOnly _alibre As New List(Of KeyFunction)
        Private ReadOnly _alibreImages As New Dictionary(Of KeyFunction, FoundCommand)

        Private Shared ReadOnly DefaultItem As String = "(3DxWare default)"
        Private Shared ReadOnly Red As Color = Color.FromArgb(&HC6, &H28, &H28)
        Private Shared ReadOnly Accent As Color = Color.FromArgb(&H18, &H5F, &HA5)
        Private Shared ReadOnly AccentBg As Color = Color.FromArgb(&HE6, &HF1, &HFB)

        Public Sub New(settings As Settings, startContext As String)
            _settings = settings
            _context = If(Contexts.All.Contains(startContext), startContext, "Part")

            Text = "3Dconnexion keys"
            Font = New Font("Segoe UI", 9.0F)
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            AutoScaleMode = AutoScaleMode.Dpi
            ClientSize = New Size(760, 676)
            BackColor = Color.White

            ' header
            Dim title As New Label With {.Text = "3Dconnexion keys", .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
                                         .Location = New Point(20, 14), .AutoSize = True}
            ' credits block, always visible (top right, centred)
            Const bw = 220
            Dim bx = ClientSize.Width - 20 - bw
            Dim by = 8
            Dim brandTip As New ToolTip()
            Dim openLinkedIn As LinkLabelLinkClickedEventHandler =
                Sub(snd, ev)
                    Try
                        Diagnostics.Process.Start("https://www.linkedin.com/in/henrikjkoch/")
                    Catch
                    End Try
                End Sub
            For Each ln In {"KOCH Engineering", "Henrik J. Koch", "Denmark © 2026"}
                Dim bold = (ln = "KOCH Engineering")
                Dim f = If(bold, New Font("Segoe UI", 9.0F, FontStyle.Bold), New Font("Segoe UI", 8.25F))
                Dim h = TextRenderer.MeasureText(ln, f).Height
                Dim lk As New LinkLabel With {.Text = ln, .Font = f, .AutoSize = False, .TextAlign = ContentAlignment.MiddleCenter,
                                              .Location = New Point(bx, by), .Size = New Size(bw, h), .LinkBehavior = LinkBehavior.HoverUnderline,
                                              .LinkColor = If(bold, Color.FromArgb(&H33, &H33, &H33), Accent), .ActiveLinkColor = Accent,
                                              .ForeColor = Color.Gray}
                Select Case ln
                    Case "KOCH Engineering"
                        AddHandler lk.LinkClicked, Sub()
                                                       Using ab As New AboutForm()
                                                           ab.ShowDialog(Me)
                                                       End Using
                                                   End Sub
                        brandTip.SetToolTip(lk, "About KOCH 3Dconnexion Keys")
                    Case "Henrik J. Koch"
                        AddHandler lk.LinkClicked, openLinkedIn
                        brandTip.SetToolTip(lk, "Open LinkedIn")
                    Case Else
                        lk.LinkArea = New LinkArea(0, 0)
                End Select
                Controls.Add(lk)
                by += h
            Next
            Controls.Add(title)

            ' context tabs
            Dim x = 20
            For Each c In Contexts.All
                Dim rb As New RadioButton With {.Text = c, .Appearance = Appearance.Button, .FlatStyle = FlatStyle.Flat,
                                                .TextAlign = ContentAlignment.MiddleCenter, .Size = New Size(96, 30),
                                                .Location = New Point(x, 50), .Tag = c, .Checked = (c = _context)}
                rb.FlatAppearance.BorderColor = Color.Silver
                rb.FlatAppearance.CheckedBackColor = AccentBg
                AddHandler rb.CheckedChanged, AddressOf OnTab
                _tabs.Add(rb)
                Controls.Add(rb)
                x += 100
            Next

            ' device selector (only the devices 3DxWare knows)
            Dim hasMouse, hasKeyboard As Boolean
            DxWareProfile.DetectDevices(hasMouse, hasKeyboard)
            If Not hasMouse AndAlso Not hasKeyboard Then hasMouse = True : hasKeyboard = True
            _keyboard = Not hasMouse
            Dim dx = 20
            For Each dev In {"SpaceMouse Enterprise", "Keyboard Pro"}
                Dim isKb = (dev = "Keyboard Pro")
                If (isKb AndAlso Not hasKeyboard) OrElse (Not isKb AndAlso Not hasMouse) Then Continue For
                Dim rb As New RadioButton With {.Text = dev, .Appearance = Appearance.Button, .FlatStyle = FlatStyle.Flat,
                                                .TextAlign = ContentAlignment.MiddleCenter, .Size = New Size(160, 26),
                                                .Location = New Point(dx, 90), .Tag = isKb, .Checked = (isKb = _keyboard)}
                rb.FlatAppearance.BorderColor = Color.Silver
                rb.FlatAppearance.CheckedBackColor = AccentBg
                AddHandler rb.CheckedChanged, AddressOf OnDevice
                Controls.Add(rb)
                dx += 164
            Next

            ' key preview
            _previewLabel = New Label With {.ForeColor = Color.DimGray, .Location = New Point(20, 126), .AutoSize = True}
            Controls.Add(_previewLabel)
            _lcd = New LcdPreview(Me) With {.Location = New Point(20, 146), .Size = New Size(720, 150)}
            Controls.Add(_lcd)

            ' 12 rows in two columns: keys 1-6 left, 7-12 right
            Controls.Add(New Label With {.Text = "Function per key", .ForeColor = Color.DimGray,
                                         .Location = New Point(20, 308), .AutoSize = True})
            For i = 0 To Contexts.KeyCount - 1
                Dim col = If(i < 6, 0, 1)
                Dim rowIx = i Mod 6
                Dim row As New Panel With {.Location = New Point(20 + col * 364, 328 + rowIx * 38), .Size = New Size(356, 36)}
                Dim lbl As New Label With {.Text = "Key", .Location = New Point(4, 10), .AutoSize = True, .ForeColor = Color.DimGray}
                Dim num As New Label With {.Text = CStr(i + 1), .Location = New Point(30, 10), .AutoSize = True,
                                           .ForeColor = Red, .Font = New Font(Font, FontStyle.Bold)}
                Dim keyIx = i
                Dim cb As New FunctionPicker With {.Location = New Point(54, 6), .Size = New Size(258, 24), .Tag = i}
                cb.Drawer = Sub(g, r, item, sel, inList, fnt) DrawChoice(g, r, item, sel, inList, fnt, keyIx)
                AddHandler cb.SelectionChanged, AddressOf OnCombo
                AddHandler cb.Enter, Sub(s, e) SelectKey(CInt(CType(s, Control).Tag), False)
                Dim icon As New Button With {.Text = "", .Size = New Size(32, 26), .Location = New Point(318, 5), .Tag = i,
                                             .FlatStyle = FlatStyle.Flat}
                icon.FlatAppearance.BorderColor = Color.Silver
                AddHandler icon.Click, AddressOf OnPickIcon
                Dim tip As New ToolTip()
                tip.SetToolTip(icon, "Pick an icon for this function")
                row.Controls.Add(lbl) : row.Controls.Add(num) : row.Controls.Add(cb) : row.Controls.Add(icon)
                _combos(i) = cb
                _rows(i) = row
                Controls.Add(row)
            Next

            _auto = New CheckBox With {.Text = "Switch layout automatically with the active Alibre window",
                                       .Location = New Point(22, 562), .AutoSize = True, .Checked = _settings.AutoSwitch}
            Controls.Add(_auto)

            Dim profile As New Label With {.Text = "3DxWare profile: " & DxWareProfile.FindProfile(), .ForeColor = Color.Gray,
                                           .Location = New Point(22, 588), .Size = New Size(720, 18), .AutoEllipsis = True}
            Controls.Add(profile)

            ' bottom buttons
            Dim sep As New Label With {.BorderStyle = BorderStyle.Fixed3D, .Location = New Point(20, 614), .Size = New Size(720, 2)}
            Controls.Add(sep)
            Dim edit = MakeButton("Edit functions…", 20, 628, 120)
            Dim imp = MakeButton("Import layout…", 146, 628, 112)
            Dim exp = MakeButton("Export layout…", 264, 628, 112)
            Dim cancel = MakeButton("Cancel", 500, 628, 90)
            Dim save = MakeButton("Save to devices", 596, 628, 144)
            save.BackColor = Color.FromArgb(&H19, &H19, &H19)
            save.ForeColor = Color.White
            save.FlatStyle = FlatStyle.Flat
            AddHandler edit.Click, AddressOf OnEditFunctions
            AddHandler imp.Click, AddressOf OnImport
            AddHandler exp.Click, AddressOf OnExport
            AddHandler cancel.Click, Sub() DialogResult = DialogResult.Cancel
            AddHandler save.Click, AddressOf OnSave
            CancelButton = cancel

            ScanAlibre()
            LoadContext()
        End Sub

        Private Sub ScanAlibre()
            Try
                Cursor = Cursors.WaitCursor
                Dim found = AlibreCommands.ScanCached()
                For Each line In AlibreCommands.LastReport
                    Diag.Log("Scan: " & line)
                Next
                Dim names As New HashSet(Of String)(_settings.Functions.Select(Function(f) f.Name.ToLowerInvariant()))
                For Each c In found
                    If names.Contains(c.Caption.ToLowerInvariant()) Then Continue For
                    names.Add(c.Caption.ToLowerInvariant())
                    Dim kf As New KeyFunction With {
                        .Id = "alibre_" & System.Text.RegularExpressions.Regex.Replace(c.Id, "[^A-Za-z0-9_]", "_"),
                        .Name = c.Caption, .Shortcut = c.Shortcut, .Category = AlibreCommands.GuessCategory(c)}
                    _alibre.Add(kf)
                    _alibreImages(kf) = c
                Next
            Catch ex As Exception
                Diag.Log("Scan failed: " & ex.ToString())
            Finally
                Cursor = Cursors.Default
            End Try
        End Sub

        ''' <summary>Icon for a catalog function or a not-yet-imported Alibre command.</summary>
        Friend Function IconFor(f As KeyFunction, size As Integer) As Bitmap
            Dim fc As FoundCommand = Nothing
            Dim img As Bitmap = Nothing
            If _alibreImages.TryGetValue(f, fc) Then img = fc.Image
            If img IsNot Nothing Then
                Dim b As New Bitmap(size, size)
                Using g = Graphics.FromImage(b)
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic
                    g.DrawImage(img, New Rectangle(0, 0, size, size))
                End Using
                Return b
            End If
            Return Icons.Render(f, size)
        End Function

        ' Moves an Alibre command into the catalog (with its icon) when it is chosen
        Private Sub Adopt(f As KeyFunction)
            If Not _alibre.Contains(f) Then Return
            _alibre.Remove(f)
            Dim fc As FoundCommand = Nothing
            If _alibreImages.TryGetValue(f, fc) AndAlso fc.Image IsNot Nothing Then
                Dim img = fc.Image
                Try
                    IO.Directory.CreateDirectory(Settings.IconFolder)
                    img.Save(IO.Path.Combine(Settings.IconFolder, f.Id & ".png"), Imaging.ImageFormat.Png)
                    f.Icon = f.Id & ".png"
                Catch
                End Try
                _alibreImages.Remove(f)
            End If
            If _settings.Find(f.Id) Is Nothing Then _settings.Functions.Add(f)
        End Sub

        Private Sub DrawChoice(g As Graphics, r As Rectangle, item As Object, selected As Boolean, inList As Boolean, fnt As Font, keyIx As Integer)
            Dim f = TryCast(item, KeyFunction)
            Dim fore = If(selected, SystemColors.HighlightText, SystemColors.WindowText)
            Dim dim_ = If(selected, SystemColors.HighlightText, Color.Gray)
            ' in the list, functions already on another key in this layout get a light amber background and the key number
            Dim usedOn = -1
            If f IsNot Nothing AndAlso inList Then
                Dim l = CurrentLayout
                For k = 0 To l.Length - 1
                    If k <> keyIx AndAlso l(k) = f.Id Then
                        usedOn = k
                        Exit For
                    End If
                Next
            End If
            If usedOn >= 0 AndAlso Not selected Then
                Using bg As New SolidBrush(Color.FromArgb(&HFA, &HEE, &HDA))
                    g.FillRectangle(bg, r)
                End Using
            End If
            If f Is Nothing Then
                TextRenderer.DrawText(g, CStr(item), fnt, New Rectangle(r.X + 26, r.Y, r.Width - 26, r.Height), dim_,
                                      TextFormatFlags.VerticalCenter Or TextFormatFlags.Left)
                Return
            End If
            Using icon = IconFor(f, 18)
                g.DrawImage(icon, r.X + 3, r.Y + (r.Height - 18) \ 2)
            End Using
            Dim noKey = String.IsNullOrWhiteSpace(f.Shortcut)
            Dim right = If(noKey, "(no shortcut)", f.Shortcut)
            If usedOn >= 0 Then right = "key " & (usedOn + 1) & "  ·  " & right
            Dim rw = TextRenderer.MeasureText(right, fnt).Width + 6
            TextRenderer.DrawText(g, f.Name, fnt, New Rectangle(r.X + 26, r.Y, r.Width - 26 - rw, r.Height),
                                  If(noKey, dim_, fore), TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis)
            TextRenderer.DrawText(g, right, fnt, New Rectangle(r.Right - rw, r.Y, rw - 2, r.Height), dim_,
                                  TextFormatFlags.VerticalCenter Or TextFormatFlags.Right)
        End Sub

        Private Function MakeButton(text As String, x As Integer, y As Integer, w As Integer) As Button
            Dim b As New Button With {.Text = text, .Location = New Point(x, y), .Size = New Size(w, 30)}
            Controls.Add(b)
            Return b
        End Function

        ' ---------------------------------------------------------------- state

        Friend ReadOnly Property CurrentLayout As String()
            Get
                Return _settings.Layout(If(_keyboard, _context & Contexts.KeyboardSuffix, _context))
            End Get
        End Property

        Friend ReadOnly Property KeyboardMode As Boolean
            Get
                Return _keyboard
            End Get
        End Property

        Private Sub OnDevice(sender As Object, e As EventArgs)
            Dim rb = CType(sender, RadioButton)
            If Not rb.Checked Then Return
            _keyboard = CBool(rb.Tag)
            _selected = 0
            LoadContext()
        End Sub

        Friend ReadOnly Property Settings As Settings
            Get
                Return _settings
            End Get
        End Property

        Friend ReadOnly Property SelectedKey As Integer
            Get
                Return _selected
            End Get
        End Property

        Private Sub LoadContext()
            _loading = True
            Dim items As New List(Of Object) From {DefaultItem}
            Dim all As New List(Of KeyFunction)(_settings.Functions)
            all.AddRange(_alibre)
            ' functions with a shortcut first, then alphabetical
            items.AddRange(all.OrderBy(Function(f) If(String.IsNullOrWhiteSpace(f.Shortcut), 1, 0)) _
                              .ThenBy(Function(f) f.Name, StringComparer.CurrentCultureIgnoreCase))
            _previewLabel.Text = If(_keyboard, "Keyboard Pro keys - no display on the keyboard, the icons are for your overview (click a key to edit it)",
                                    "LCD preview (click a key to edit it)")
            Dim layout = CurrentLayout
            For i = 0 To Contexts.KeyCount - 1
                Dim cb = _combos(i)
                cb.Items = New List(Of Object)(items)
                Dim f = _settings.Find(layout(i))
                cb.SelectedItem = If(f Is Nothing, CObj(DefaultItem), CObj(f))
                UpdateIconButton(i)
            Next
            _loading = False
            SelectKey(_selected, False)
            _lcd.Invalidate()
        End Sub

        Private Sub UpdateIconButton(i As Integer)
            Dim btn = CType(_rows(i).Controls(3), Button)
            Dim f = _settings.Find(CurrentLayout(i))
            Dim old = btn.Image
            btn.Image = If(f Is Nothing, Nothing, Icons.Render(f, 20))
            btn.Enabled = f IsNot Nothing
            If old IsNot Nothing Then old.Dispose()
        End Sub

        Friend Sub SelectKey(i As Integer, focusCombo As Boolean)
            _selected = i
            For k = 0 To Contexts.KeyCount - 1
                _rows(k).BackColor = If(k = i, AccentBg, Color.White)
            Next
            _lcd.Invalidate()
            If focusCombo Then
                _combos(i).Focus()
                _combos(i).OpenPopup("")
            End If
        End Sub

        ' ---------------------------------------------------------------- events

        Private Sub OnTab(sender As Object, e As EventArgs)
            Dim rb = CType(sender, RadioButton)
            If Not rb.Checked Then Return
            _context = CStr(rb.Tag)
            LoadContext()
        End Sub

        Private Sub OnCombo(sender As Object, e As EventArgs)
            If _loading Then Return
            Dim cb = CType(sender, FunctionPicker)
            Dim i = CInt(cb.Tag)
            Dim f = TryCast(cb.SelectedItem, KeyFunction)
            If f IsNot Nothing Then Adopt(f)
            CurrentLayout(i) = If(f Is Nothing, "", f.Id)
            UpdateIconButton(i)
            SelectKey(i, False)
        End Sub

        Private Sub OnPickIcon(sender As Object, e As EventArgs)
            Dim i = CInt(CType(sender, Control).Tag)
            Dim f = _settings.Find(CurrentLayout(i))
            If f Is Nothing Then Return
            If PickIcon(Me, f) Then
                For k = 0 To Contexts.KeyCount - 1
                    UpdateIconButton(k)
                Next
                _lcd.Invalidate()
            End If
        End Sub

        Friend Shared Function PickIcon(owner As IWin32Window, f As KeyFunction) As Boolean
            Using dlg As New OpenFileDialog With {.Title = "Pick an icon for " & f.Name,
                                                  .Filter = "Images (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"}
                If dlg.ShowDialog(owner) <> DialogResult.OK Then Return False
                Try
                    f.Icon = Icons.ImportIcon(dlg.FileName, f)
                    Return True
                Catch ex As Exception
                    MessageBox.Show(owner, "Could not use that image: " & ex.Message, "3Dconnexion keys")
                    Return False
                End Try
            End Using
        End Function

        Private Sub OnEditFunctions(sender As Object, e As EventArgs)
            Using ed As New FunctionEditor(_settings)
                ed.ShowDialog(Me)
            End Using
            LoadContext()
        End Sub

        Private Sub OnImport(sender As Object, e As EventArgs)
            Using dlg As New OpenFileDialog With {.Title = "Import 3Dconnexion layout", .Filter = "3Dconnexion layout (*.xml)|*.xml"}
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    _settings.ImportLayout(dlg.FileName)
                    LoadContext()
                Catch ex As Exception
                    MessageBox.Show(Me, "Could not import: " & ex.Message, "3Dconnexion keys")
                End Try
            End Using
        End Sub

        Private Sub OnExport(sender As Object, e As EventArgs)
            Using dlg As New SaveFileDialog With {.Title = "Export 3Dconnexion layout", .Filter = "3Dconnexion layout (*.xml)|*.xml",
                                                  .FileName = "3Dconnexion layout.xml"}
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                Try
                    _settings.ExportLayout(dlg.FileName)
                Catch ex As Exception
                    MessageBox.Show(Me, "Could not export: " & ex.Message, "3Dconnexion keys")
                End Try
            End Using
        End Sub

        Private Sub OnSave(sender As Object, e As EventArgs)
            _settings.AutoSwitch = _auto.Checked
            ' warn about functions without a shortcut
            Dim missing As New List(Of String)
            For Each c In Contexts.AllLayoutKeys()
                For Each id In _settings.Layout(c)
                    Dim f = _settings.Find(id)
                    If f IsNot Nothing AndAlso String.IsNullOrWhiteSpace(f.Shortcut) AndAlso Not missing.Contains(f.Name) Then missing.Add(f.Name)
                Next
            Next
            If missing.Count > 0 Then
                Dim r = MessageBox.Show(Me, "These functions have no keyboard shortcut yet, so their keys will show the name and icon but do nothing:" &
                                        vbCrLf & vbCrLf & String.Join(vbCrLf, missing) & vbCrLf & vbCrLf &
                                        "Give them a shortcut in Alibre and in 'Edit functions…'. Save anyway?",
                                        "3Dconnexion keys", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If r <> DialogResult.Yes Then Return
            End If
            DialogResult = DialogResult.OK
        End Sub

        ' ---------------------------------------------------------------- LCD preview

        Private Class LcdPreview
            Inherits Control

            Private ReadOnly _owner As ConfigForm

            Public Sub New(owner As ConfigForm)
                _owner = owner
                DoubleBuffered = True
                SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.Selectable, True)
                Cursor = Cursors.Hand
            End Sub

            Private Function KeyRect(i As Integer) As Rectangle
                Const pad = 10, gap = 6, groupGap = 18
                If _owner.KeyboardMode Then
                    ' Keyboard Pro: one row of 12 keys in three groups of 4
                    Dim kw = (Width - 2 * pad - 9 * gap - 2 * groupGap) \ 12
                    Dim kh = Math.Min(Height - 2 * pad, kw + 18)
                    Dim grp = i \ 4
                    Dim kx = pad + i * (kw + gap) + grp * (groupGap - gap)
                    Return New Rectangle(kx, (Height - kh) \ 2, kw, kh)
                End If
                Dim w = (Width - 2 * pad - 4 * gap - groupGap) \ 6
                Dim h = (Height - 2 * pad - gap) \ 2
                Dim col = i Mod 6
                Dim row = i \ 6
                Dim x = pad + col * (w + gap) + If(col >= 3, groupGap - gap, 0)
                Return New Rectangle(x, pad + row * (h + gap), w, h)
            End Function

            Protected Overrides Sub OnPaint(e As PaintEventArgs)
                Dim g = e.Graphics
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit
                Using bg As New SolidBrush(AccentBg)
                    g.FillRectangle(bg, ClientRectangle)
                End Using
                Dim layout = _owner.CurrentLayout
                Using numFont As New Font("Segoe UI", 8.0F, FontStyle.Bold),
                      nameFont As New Font("Segoe UI", 8.0F),
                      redBrush As New SolidBrush(Red),
                      selPen As New Pen(Accent, 2)
                    For i = 0 To Contexts.KeyCount - 1
                        Dim r = KeyRect(i)
                        g.FillRectangle(Brushes.White, r)
                        If i = _owner.SelectedKey Then g.DrawRectangle(selPen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2)
                        g.DrawString(CStr(i + 1), numFont, redBrush, r.X + 3, r.Y + 2)
                        Dim f = _owner.Settings.Find(layout(i))
                        If f Is Nothing Then
                            TextRenderer.DrawText(g, "default", nameFont, r, Color.Gray,
                                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                        Else
                            Dim isz = If(_owner.KeyboardMode, 22, 28)
                            Using icon = _owner.IconFor(f, isz)
                                g.DrawImage(icon, r.X + (r.Width - isz) \ 2, r.Y + 12)
                            End Using
                            Dim tr As New Rectangle(r.X + 2, r.Y + 14 + isz, r.Width - 4, r.Height - 16 - isz)
                            TextRenderer.DrawText(g, f.Name, nameFont, tr, Color.Black,
                                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.Top Or
                                                  TextFormatFlags.WordBreak Or TextFormatFlags.EndEllipsis)
                        End If
                    Next
                End Using
            End Sub

            Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
                MyBase.OnMouseClick(e)
                For i = 0 To Contexts.KeyCount - 1
                    If KeyRect(i).Contains(e.Location) Then
                        _owner.SelectKey(i, e.Button = MouseButtons.Left)
                        If e.Button = MouseButtons.Right Then
                            Dim f = _owner.Settings.Find(_owner.CurrentLayout(i))
                            If f IsNot Nothing AndAlso PickIcon(_owner, f) Then _owner.LoadContextPublic()
                        End If
                        Exit For
                    End If
                Next
            End Sub
        End Class

        Friend Sub LoadContextPublic()
            LoadContext()
        End Sub
    End Class

End Namespace
