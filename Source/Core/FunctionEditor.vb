' KOCH SpaceMouse - edit the catalog of functions (name, shortcut, category, icon)
' (c) 2026 KOCH Engineering - MIT License

Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class FunctionEditor
        Inherits Form

        Private ReadOnly _settings As Settings
        Private ReadOnly _list As ListBox
        Private ReadOnly _name As TextBox
        Private ReadOnly _shortcut As TextBox
        Private ReadOnly _category As ComboBox
        Private ReadOnly _icon As PictureBox
        Private ReadOnly _hint As Label
        Private _current As KeyFunction
        Private _loading As Boolean

        Public Sub New(settings As Settings)
            _settings = settings
            Text = "Edit functions"
            Font = New Font("Segoe UI", 9.0F)
            FormBorderStyle = FormBorderStyle.FixedDialog
            MaximizeBox = False
            MinimizeBox = False
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            AutoScaleMode = AutoScaleMode.Dpi
            ClientSize = New Size(620, 440)
            BackColor = Color.White

            _list = New ListBox With {.Location = New Point(16, 16), .Size = New Size(250, 340), .IntegralHeight = False}
            AddHandler _list.SelectedIndexChanged, AddressOf OnSelect
            Controls.Add(_list)

            Dim add As New Button With {.Text = "Add", .Location = New Point(16, 366), .Size = New Size(80, 28)}
            Dim del As New Button With {.Text = "Delete", .Location = New Point(102, 366), .Size = New Size(80, 28)}
            Dim imp As New Button With {.Text = "Import from Alibre…", .Location = New Point(16, 398), .Size = New Size(166, 28)}
            AddHandler add.Click, AddressOf OnAdd
            AddHandler del.Click, AddressOf OnDelete
            AddHandler imp.Click, AddressOf OnImportAlibre
            Controls.Add(add)
            Controls.Add(del)
            Controls.Add(imp)

            Dim x = 290
            Controls.Add(New Label With {.Text = "Name", .Location = New Point(x, 18), .AutoSize = True})
            _name = New TextBox With {.Location = New Point(x, 38), .Size = New Size(310, 24)}
            AddHandler _name.TextChanged, Sub()
                                              If _loading OrElse _current Is Nothing Then Return
                                              _current.Name = _name.Text
                                              RefreshItem()
                                          End Sub
            Controls.Add(_name)

            Controls.Add(New Label With {.Text = "Keyboard shortcut in Alibre (click the box and press the keys)",
                                         .Location = New Point(x, 74), .AutoSize = True})
            _shortcut = New TextBox With {.Location = New Point(x, 94), .Size = New Size(220, 24), .ReadOnly = True,
                                          .BackColor = Color.White, .ShortcutsEnabled = False}
            AddHandler _shortcut.KeyDown, AddressOf OnShortcutKey
            AddHandler _shortcut.PreviewKeyDown, Sub(s, e) e.IsInputKey = True
            Controls.Add(_shortcut)
            Dim clr As New Button With {.Text = "Clear", .Location = New Point(x + 226, 93), .Size = New Size(84, 26)}
            AddHandler clr.Click, Sub()
                                      If _current Is Nothing Then Return
                                      _current.Shortcut = ""
                                      ShowCurrent()
                                  End Sub
            Controls.Add(clr)

            Controls.Add(New Label With {.Text = "Category (sets the colour of a generated icon)", .Location = New Point(x, 130), .AutoSize = True})
            _category = New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Location = New Point(x, 150), .Size = New Size(160, 24)}
            _category.Items.AddRange(New Object() {"Part", "Assembly", "Sketch", "Drawing", "General"})
            AddHandler _category.SelectedIndexChanged, Sub()
                                                           If _loading OrElse _current Is Nothing Then Return
                                                           _current.Category = CStr(_category.SelectedItem)
                                                           ShowCurrent()
                                                       End Sub
            Controls.Add(_category)

            Controls.Add(New Label With {.Text = "Icon", .Location = New Point(x, 186), .AutoSize = True})
            _icon = New PictureBox With {.Location = New Point(x, 206), .Size = New Size(48, 48), .SizeMode = PictureBoxSizeMode.Zoom,
                                         .BorderStyle = BorderStyle.FixedSingle}
            Controls.Add(_icon)
            Dim pick As New Button With {.Text = "Pick icon…", .Location = New Point(x + 60, 206), .Size = New Size(110, 26)}
            Dim gen As New Button With {.Text = "Use generated", .Location = New Point(x + 60, 236), .Size = New Size(110, 26)}
            AddHandler pick.Click, Sub()
                                       If _current IsNot Nothing AndAlso ConfigForm.PickIcon(Me, _current) Then ShowCurrent()
                                   End Sub
            AddHandler gen.Click, Sub()
                                      If _current Is Nothing Then Return
                                      _current.Icon = ""
                                      ShowCurrent()
                                  End Sub
            Controls.Add(pick)
            Controls.Add(gen)

            _hint = New Label With {.Location = New Point(x, 280), .Size = New Size(310, 70), .ForeColor = Color.DimGray,
                                    .Text = "The shortcut must be the same as the one the function has in Alibre " &
                                            "(shown in brackets in Alibre's screen tips). The 3Dconnexion key sends these keys. " & "Tip: Import from Alibre reads them for you."}
            Controls.Add(_hint)

            Dim ok As New Button With {.Text = "Close", .Location = New Point(520, 398), .Size = New Size(84, 28), .DialogResult = DialogResult.OK}
            Controls.Add(ok)
            AcceptButton = ok

            FillList()
        End Sub

        Private Sub FillList()
            _list.BeginUpdate()
            _list.Items.Clear()
            For Each f In _settings.Functions.OrderBy(Function(q) q.Name, StringComparer.CurrentCultureIgnoreCase)
                _list.Items.Add(f)
            Next
            _list.EndUpdate()
            If _list.Items.Count > 0 Then _list.SelectedIndex = 0 Else ShowCurrent()
        End Sub

        Private Sub RefreshItem()
            Dim i = _list.SelectedIndex
            If i >= 0 Then _list.Items(i) = _list.Items(i)   ' redraw the text
        End Sub

        Private Sub OnSelect(sender As Object, e As EventArgs)
            _current = TryCast(_list.SelectedItem, KeyFunction)
            ShowCurrent()
        End Sub

        Private Sub ShowCurrent()
            _loading = True
            Dim has = _current IsNot Nothing
            _name.Enabled = has : _shortcut.Enabled = has : _category.Enabled = has
            _name.Text = If(has, _current.Name, "")
            _shortcut.Text = If(has, If(_current.Shortcut = "", "(none)", _current.Shortcut), "")
            _category.SelectedItem = If(has, CObj(_current.Category), Nothing)
            Dim old = _icon.Image
            _icon.Image = If(has, Icons.Render(_current, 48), Nothing)
            If old IsNot Nothing Then old.Dispose()
            _loading = False
        End Sub

        Private Sub OnShortcutKey(sender As Object, e As KeyEventArgs)
            e.SuppressKeyPress = True
            e.Handled = True
            If _current Is Nothing Then Return
            Dim s = KeyStroke.FromKeys(e.KeyData)
            If s = "" Then Return
            _current.Shortcut = s
            ShowCurrent()
        End Sub

        Private Sub OnAdd(sender As Object, e As EventArgs)
            Dim f As New KeyFunction With {.Id = _settings.NewId(), .Name = "New function", .Category = "General"}
            _settings.Functions.Add(f)
            FillList()
            _list.SelectedItem = f
            _name.Focus()
            _name.SelectAll()
        End Sub

        Private Sub OnImportAlibre(sender As Object, e As EventArgs)
            Using f As New ImportForm(_settings)
                If f.ShowDialog(Me) = DialogResult.OK AndAlso f.ImportedCount > 0 Then FillList()
            End Using
        End Sub

        Private Sub OnDelete(sender As Object, e As EventArgs)
            If _current Is Nothing Then Return
            If MessageBox.Show(Me, "Delete '" & _current.Name & "'? Keys using it go back to the 3DxWare default.", "Edit functions",
                               MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            For Each c In Contexts.AllLayoutKeys()
                Dim l = _settings.Layout(c)
                For i = 0 To l.Length - 1
                    If l(i) = _current.Id Then l(i) = ""
                Next
            Next
            _settings.Functions.Remove(_current)
            _current = Nothing
            FillList()
        End Sub
    End Class

End Namespace
