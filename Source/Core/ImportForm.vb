' KOCH SpaceMouse - pick Alibre commands to add to the catalog
' (c) 2026 KOCH Engineering - MIT License

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Module Diag
        Public Log As Action(Of String) = Sub(m)
                                          End Sub
    End Module

    Public Class ImportForm
        Inherits Form

        Private ReadOnly _settings As Settings
        Private ReadOnly _all As List(Of FoundCommand)
        Private ReadOnly _list As ListView
        Private ReadOnly _filter As TextBox
        Private ReadOnly _onlyShortcut As CheckBox
        Private ReadOnly _images As New ImageList With {.ImageSize = New Size(24, 24), .ColorDepth = ColorDepth.Depth32Bit}
        Private ReadOnly _checked As New HashSet(Of FoundCommand)
        Private _filling As Boolean

        Public Property ImportedCount As Integer

        Public Sub New(settings As Settings)
            _settings = settings
            Text = "Import functions from Alibre"
            Font = New Font("Segoe UI", 9.0F)
            FormBorderStyle = FormBorderStyle.Sizable
            MinimizeBox = False
            StartPosition = FormStartPosition.CenterParent
            ShowInTaskbar = False
            AutoScaleMode = AutoScaleMode.Dpi
            ClientSize = New Size(720, 560)
            MinimumSize = New Size(560, 400)
            BackColor = Color.White

            Cursor = Cursors.WaitCursor
            _all = AlibreCommands.Scan()
            Cursor = Cursors.Default
            For Each line In AlibreCommands.LastReport
                Diag.Log("Import: " & line)
            Next

            Dim info As New Label With {.Location = New Point(12, 12), .Size = New Size(696, 34), .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
                                        .Text = "These are the commands in Alibre's open windows. Tick the ones you want on your 3Dconnexion keys. " &
                                                "Open a part, an assembly, a sketch and a drawing first to see all of them."}
            Controls.Add(info)
            Controls.Add(New Label With {.Text = "Search", .Location = New Point(12, 56), .AutoSize = True})
            _filter = New TextBox With {.Location = New Point(64, 52), .Size = New Size(240, 24)}
            AddHandler _filter.TextChanged, Sub() Fill()
            Controls.Add(_filter)
            _onlyShortcut = New CheckBox With {.Text = "Only commands with a keyboard shortcut", .Location = New Point(320, 54), .AutoSize = True, .Checked = True}
            AddHandler _onlyShortcut.CheckedChanged, Sub() Fill()
            Controls.Add(_onlyShortcut)

            _list = New ListView With {.Location = New Point(12, 84), .Size = New Size(696, 420), .View = View.Details, .CheckBoxes = True,
                                       .FullRowSelect = True, .SmallImageList = _images, .HideSelection = False,
                                       .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right}
            _list.Columns.Add("Command", 260)
            _list.Columns.Add("Shortcut", 130)
            _list.Columns.Add("Ribbon", 280)
            AddHandler _list.ItemChecked, AddressOf OnChecked
            Controls.Add(_list)

            Dim note As New Label With {.Location = New Point(12, 516), .Size = New Size(440, 34), .ForeColor = Color.DimGray,
                                        .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left,
                                        .Text = "Icons are copied from Alibre into your own settings folder only."}
            Controls.Add(note)
            Dim ok As New Button With {.Text = "Import ticked", .Size = New Size(120, 30), .Location = New Point(492, 518),
                                       .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
            Dim cancel As New Button With {.Text = "Cancel", .Size = New Size(84, 30), .Location = New Point(624, 518),
                                           .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right, .DialogResult = DialogResult.Cancel}
            AddHandler ok.Click, AddressOf OnImport
            Controls.Add(ok)
            Controls.Add(cancel)
            CancelButton = cancel

            For i = 0 To _all.Count - 1
                Dim c = _all(i)
                _images.Images.Add(CStr(i), If(c.Image, Icons.Render(New KeyFunction With {.Name = c.Caption, .Category = AlibreCommands.GuessCategory(c)}, 24)))
            Next
            Fill()
            If _all.Count = 0 Then
                MessageBox.Show(Me, "No Alibre commands were found in the open windows. Details are in the add-on log.", Text)
            End If
        End Sub

        Private Sub Fill()
            _filling = True
            _list.BeginUpdate()
            _list.Items.Clear()
            Dim q = _filter.Text.Trim()
            Dim have = New HashSet(Of String)(_settings.Functions.Select(Function(f) f.Name.ToLowerInvariant()))
            For i = 0 To _all.Count - 1
                Dim c = _all(i)
                If _onlyShortcut.Checked AndAlso c.Shortcut = "" Then Continue For
                If q <> "" AndAlso c.Caption.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 Then Continue For
                Dim li As New ListViewItem(c.Caption, CStr(i)) With {.Tag = c, .Checked = _checked.Contains(c)}
                li.SubItems.Add(If(c.Shortcut = "", "-", c.Shortcut))
                li.SubItems.Add(c.Page)
                If have.Contains(c.Caption.ToLowerInvariant()) Then li.ForeColor = Color.Gray
                _list.Items.Add(li)
            Next
            _list.EndUpdate()
            _filling = False
        End Sub

        Private Sub OnChecked(sender As Object, e As ItemCheckedEventArgs)
            If _filling Then Return
            Dim c = CType(e.Item.Tag, FoundCommand)
            If e.Item.Checked Then _checked.Add(c) Else _checked.Remove(c)
        End Sub

        Private Sub OnImport(sender As Object, e As EventArgs)
            Directory.CreateDirectory(Settings.IconFolder)
            Dim n = 0
            For Each c In _checked
                Dim id = "alibre_" & Regex.Replace(c.Id, "[^A-Za-z0-9_]", "_")
                Dim f = _settings.Find(id)
                If f Is Nothing Then
                    f = New KeyFunction With {.Id = id}
                    _settings.Functions.Add(f)
                End If
                f.Name = c.Caption
                f.Shortcut = c.Shortcut
                f.Category = AlibreCommands.GuessCategory(c)
                If c.Image IsNot Nothing Then
                    Try
                        Dim file = id & ".png"
                        c.Image.Save(Path.Combine(Settings.IconFolder, file), ImageFormat.Png)
                        f.Icon = file
                    Catch
                    End Try
                End If
                n += 1
            Next
            ImportedCount = n
            Diag.Log("Imported " & n & " functions from Alibre")
            DialogResult = DialogResult.OK
        End Sub
    End Class

End Namespace
