' KOCH SpaceMouse - runs Alibre commands that have no keyboard shortcut
' (c) 2026 KOCH Engineering - MIT License
'
' 3DxWare can only send key strokes. For a function without a shortcut the add-on gives it an
' invisible key (F13-F24, with Ctrl/Shift/Alt - no normal keyboard has these keys). This message
' filter sees that key inside Alibre, swallows it and clicks the command's ribbon button in the
' window that got the key.

Imports System.Collections.Generic
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class CommandRunner
        Implements IMessageFilter

        Private Const WM_KEYDOWN As Integer = &H100
        Private Const WM_KEYUP As Integer = &H101
        Private Const WM_SYSKEYDOWN As Integer = &H104
        Private Const WM_SYSKEYUP As Integer = &H105

        Private _map As New Dictionary(Of Keys, KeyFunction)
        Private ReadOnly _log As Action(Of String)

        Public Sub New(log As Action(Of String))
            _log = log
        End Sub

        ''' <summary>Which invisible key runs which function (from Settings.HiddenKeys).</summary>
        Public Sub SetMap(settings As Settings, hidden As Dictionary(Of String, String))
            Dim m As New Dictionary(Of Keys, KeyFunction)
            For Each kv In hidden
                Dim f = settings.Find(kv.Key)
                If f Is Nothing Then Continue For
                Dim k = ToKeys(kv.Value)
                If k <> Keys.None Then m(k) = f
            Next
            _map = m
        End Sub

        Private Shared Function ToKeys(text As String) As Keys
            Dim k As Keys = Keys.None
            For Each p In text.ToUpperInvariant().Split("+"c)
                Select Case p
                    Case "CTRL" : k = k Or Keys.Control
                    Case "SHIFT" : k = k Or Keys.Shift
                    Case "ALT" : k = k Or Keys.Alt
                    Case Else
                        Dim n As Integer
                        If p.StartsWith("F") AndAlso Integer.TryParse(p.Substring(1), n) AndAlso n >= 13 AndAlso n <= 24 Then
                            k = k Or CType(Keys.F13 + (n - 13), Keys)
                        Else
                            Return Keys.None
                        End If
                End Select
            Next
            Return k
        End Function

        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            If m.Msg <> WM_KEYDOWN AndAlso m.Msg <> WM_SYSKEYDOWN AndAlso m.Msg <> WM_KEYUP AndAlso m.Msg <> WM_SYSKEYUP Then Return False
            Dim vk = CInt(m.WParam.ToInt64() And &HFFFF)
            If vk < Keys.F13 OrElse vk > Keys.F24 Then Return False
            Dim f As KeyFunction = Nothing
            If Not _map.TryGetValue(CType(vk, Keys) Or Control.ModifierKeys, f) Then Return False
            Dim isDown = (m.Msg = WM_KEYDOWN OrElse m.Msg = WM_SYSKEYDOWN)
            Dim isRepeat = (m.LParam.ToInt64() And &H40000000L) <> 0
            If isDown AndAlso Not isRepeat Then
                Dim form As Form = Nothing
                Dim c = Control.FromChildHandle(m.HWnd)
                If c IsNot Nothing Then form = c.FindForm()
                If form Is Nothing Then form = Form.ActiveForm
                If form IsNot Nothing Then
                    Dim target = form
                    Dim fn = f
                    ' run after this key message is done, not inside it
                    form.BeginInvoke(New MethodInvoker(Sub() Run(fn, target)))
                End If
            End If
            Return True     ' Alibre never sees the invisible key
        End Function

        Private Sub Run(f As KeyFunction, form As Form)
            Try
                Dim item As Object = Nothing
                Dim candidates As New List(Of Form) From {form}
                If form.Owner IsNot Nothing Then candidates.Add(form.Owner)
                If form.MdiParent IsNot Nothing Then candidates.Add(form.MdiParent)
                For Each cf In candidates
                    item = AlibreCommands.FindItem(cf, f.Command, f.Name)
                    If item IsNot Nothing Then Exit For
                Next
                If item Is Nothing Then
                    _log("Run '" & f.Name & "': not in the ribbon of '" & form.Text & "'")
                    Media.SystemSounds.Beep.Play()
                ElseIf Not AlibreCommands.Click(item) Then
                    _log("Run '" & f.Name & "': the command is disabled here")
                    Media.SystemSounds.Beep.Play()
                Else
                    _log("Ran '" & f.Name & "' in '" & form.Text & "'")
                End If
            Catch ex As Exception
                _log("Run '" & f.Name & "' failed: " & ex.Message)
            End Try
        End Sub
    End Class

End Namespace
