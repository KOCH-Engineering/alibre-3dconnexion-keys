' KOCH SpaceMouse - a combo-box-like picker with a search box (type to filter)
' (c) 2026 KOCH Engineering - MIT License

Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Namespace KochSpaceMouse

    ''' <summary>Draws one entry (string or KeyFunction) in a rectangle.</summary>
    Public Delegate Sub DrawEntry(g As Graphics, r As Rectangle, item As Object, selected As Boolean, inList As Boolean, font As Font)

    Public Class FunctionPicker
        Inherits Control

        Public Property Items As New List(Of Object)
        Public Property Drawer As DrawEntry
        Public Event SelectionChanged As EventHandler

        Private _selected As Object

        Public Property SelectedItem As Object
            Get
                Return _selected
            End Get
            Set(value As Object)
                _selected = value
                Invalidate()
            End Set
        End Property

        Public Sub New()
            SetStyle(ControlStyles.Selectable Or ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            TabStop = True
            Cursor = Cursors.Hand
            BackColor = Color.White
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            Dim r = ClientRectangle
            e.Graphics.Clear(BackColor)
            Dim borderColor = If(Focused, Color.FromArgb(&H18, &H5F, &HA5), Color.FromArgb(&HB4, &HB4, &HB4))
            Using p As New Pen(borderColor)
                e.Graphics.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1)
            End Using
            ' arrow
            Dim ax = r.Right - 16
            Dim ay = r.Height \ 2 - 2
            Using b As New SolidBrush(Color.DimGray)
                e.Graphics.FillPolygon(b, New Point() {New Point(ax, ay), New Point(ax + 8, ay), New Point(ax + 4, ay + 4)})
            End Using
            If Drawer IsNot Nothing AndAlso _selected IsNot Nothing Then
                Drawer.Invoke(e.Graphics, New Rectangle(1, 1, r.Width - 22, r.Height - 2), _selected, False, False, Font)
            End If
        End Sub

        Protected Overrides Sub OnGotFocus(e As EventArgs)
            MyBase.OnGotFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnLostFocus(e As EventArgs)
            MyBase.OnLostFocus(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            If e.Button = MouseButtons.Left Then OpenPopup("")
        End Sub

        Protected Overrides Function IsInputKey(keyData As Keys) As Boolean
            Select Case keyData And Keys.KeyCode
                Case Keys.Down, Keys.Up, Keys.Enter, Keys.F4
                    Return True
            End Select
            Return MyBase.IsInputKey(keyData)
        End Function

        Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
            MyBase.OnKeyDown(e)
            If e.KeyCode = Keys.Down OrElse e.KeyCode = Keys.F4 OrElse e.KeyCode = Keys.Enter OrElse (e.Alt AndAlso e.KeyCode = Keys.Down) Then
                e.Handled = True
                OpenPopup("")
            End If
        End Sub

        Protected Overrides Sub OnKeyPress(e As KeyPressEventArgs)
            MyBase.OnKeyPress(e)
            If Not Char.IsControl(e.KeyChar) Then
                e.Handled = True
                OpenPopup(e.KeyChar.ToString())
            End If
        End Sub

        ''' <summary>Opens the search list under the control.</summary>
        Public Sub OpenPopup(initialText As String)
            Dim popup As New PickerPopup(Me, initialText)
            popup.Show(Me, New Point(0, Height))
        End Sub

        Friend Sub Choose(item As Object)
            If Equals(item, _selected) Then Return
            _selected = item
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        ' ------------------------------------------------------------------ popup

        Private Class PickerPopup
            Inherits ToolStripDropDown

            Private ReadOnly _owner As FunctionPicker
            Private ReadOnly _search As TextBox
            Private ReadOnly _list As ListBox
            Private ReadOnly _panel As Panel

            Public Sub New(owner As FunctionPicker, initialText As String)
                _owner = owner
                AutoSize = False
                Padding = Padding.Empty
                Margin = Padding.Empty
                DropShadowEnabled = True

                _panel = New Panel With {.Size = New Size(Math.Max(owner.Width, 380), 380), .BackColor = Color.White, .Padding = New Padding(4)}
                _search = New TextBox With {.Dock = DockStyle.Top, .Font = owner.Font}
                _list = New ListBox With {.Dock = DockStyle.Fill, .DrawMode = DrawMode.OwnerDrawFixed, .ItemHeight = 22,
                                          .IntegralHeight = False, .BorderStyle = BorderStyle.None, .Font = owner.Font}
                Dim hint As New Label With {.Dock = DockStyle.Top, .Height = 18, .ForeColor = Color.Gray, .Font = owner.Font,
                                            .Text = "Type to search · Enter to choose · Esc to close"}
                _panel.Controls.Add(_list)
                _panel.Controls.Add(hint)
                _panel.Controls.Add(_search)

                AddHandler _list.DrawItem, AddressOf OnDrawItem
                AddHandler _search.TextChanged, Sub() Filter()
                AddHandler _search.KeyDown, AddressOf OnSearchKey
                AddHandler _list.MouseClick, Sub(s, e)
                                                 Dim i = _list.IndexFromPoint(e.Location)
                                                 If i >= 0 Then Pick(_list.Items(i))
                                             End Sub
                AddHandler _list.KeyDown, AddressOf OnSearchKey

                Dim host As New ToolStripControlHost(_panel) With {.AutoSize = False, .Size = _panel.Size, .Margin = Padding.Empty, .Padding = Padding.Empty}
                Items.Add(host)
                Size = New Size(_panel.Width + 2, _panel.Height + 2)

                _search.Text = initialText
                _search.SelectionStart = _search.Text.Length
                Filter()
            End Sub

            Protected Overrides Sub OnOpened(e As EventArgs)
                MyBase.OnOpened(e)
                _search.Focus()
                _search.SelectionStart = _search.Text.Length
            End Sub

            Private Sub Filter()
                Dim q = _search.Text.Trim()
                Dim words = q.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                _list.BeginUpdate()
                _list.Items.Clear()
                For Each it In _owner.Items
                    Dim text = If(TypeOf it Is KeyFunction, CType(it, KeyFunction).Name & " " & CType(it, KeyFunction).Shortcut, CStr(it))
                    If words.All(Function(w) text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) Then _list.Items.Add(it)
                Next
                _list.EndUpdate()
                Dim sel = _list.Items.IndexOf(_owner.SelectedItem)
                If q = "" AndAlso sel >= 0 Then
                    _list.SelectedIndex = sel
                ElseIf _list.Items.Count > 0 Then
                    ' prefer the first function over "(3DxWare default)" when searching
                    _list.SelectedIndex = If(q <> "" AndAlso _list.Items.Count > 1 AndAlso TypeOf _list.Items(0) Is String, 1, 0)
                End If
            End Sub

            Private Sub OnSearchKey(sender As Object, e As KeyEventArgs)
                Select Case e.KeyCode
                    Case Keys.Down
                        If _list.SelectedIndex < _list.Items.Count - 1 Then _list.SelectedIndex += 1
                        e.Handled = True
                    Case Keys.Up
                        If _list.SelectedIndex > 0 Then _list.SelectedIndex -= 1
                        e.Handled = True
                    Case Keys.PageDown
                        _list.SelectedIndex = Math.Min(_list.Items.Count - 1, _list.SelectedIndex + 15)
                        e.Handled = True
                    Case Keys.PageUp
                        _list.SelectedIndex = Math.Max(0, _list.SelectedIndex - 15)
                        e.Handled = True
                    Case Keys.Enter
                        If _list.SelectedIndex >= 0 Then Pick(_list.SelectedItem)
                        e.Handled = True
                        e.SuppressKeyPress = True
                    Case Keys.Escape
                        Close()
                        e.Handled = True
                End Select
            End Sub

            Private Sub Pick(item As Object)
                Close()
                _owner.Choose(item)
                _owner.Focus()
            End Sub

            Private Sub OnDrawItem(sender As Object, e As DrawItemEventArgs)
                e.DrawBackground()
                If e.Index < 0 Then Return
                Dim selected = (e.State And DrawItemState.Selected) <> 0
                If _owner.Drawer IsNot Nothing Then _owner.Drawer.Invoke(e.Graphics, e.Bounds, _list.Items(e.Index), selected, True, _list.Font)
            End Sub
        End Class
    End Class

End Namespace
