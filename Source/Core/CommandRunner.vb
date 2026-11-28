' KOCH SpaceMouse - runs Alibre commands that have no keyboard shortcut
' (c) 2026 KOCH Engineering - MIT License
'
' 3DxWare can only send key strokes. For a function without a shortcut the add-on gives it an
' invisible key (F13-F24, with Ctrl/Shift/Alt - no normal keyboard has these keys). A low-level
' keyboard hook catches that key wherever it lands - also when 3Dconnexion's radial menu window has
' the focus - swallows it and clicks the command's ribbon button in the last active Alibre window.
' A message filter inside Alibre is the fallback if the hook cannot be installed.

Imports System.Collections.Generic
Imports System.Runtime.InteropServices
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

        Private _lastForm As Form
        Private ReadOnly _invoker As Control
        Private _hook As IntPtr = IntPtr.Zero
        Private ReadOnly _proc As LowLevelKeyboardProc   ' kept alive while the hook is installed

        Public Sub New(log As Action(Of String))
            _log = log
            _invoker = New Control()
            _invoker.CreateControl()
            Dim h = _invoker.Handle
            _proc = AddressOf HookProc
            Try
                Using p = Diagnostics.Process.GetCurrentProcess(), m = p.MainModule
                    _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(m.ModuleName), 0)
                End Using
            Catch ex As Exception
                _log("Keyboard hook failed: " & ex.Message)
            End Try
            If _hook = IntPtr.Zero Then _log("Keyboard hook not installed (error " & Marshal.GetLastWin32Error() & "); using the message filter only")
        End Sub

        ' The design window the user works in (set by the plug-in timer while an Alibre design window is active)
        Public Sub Track(f As Form)
            If f IsNot Nothing AndAlso Not f.IsDisposed Then _lastForm = f
        End Sub

        Public Sub Remove()
            If _hook <> IntPtr.Zero Then UnhookWindowsHookEx(_hook)
            _hook = IntPtr.Zero
            _invoker.Dispose()
        End Sub

        ' ---------------------------------------------------------------- low-level hook

        Private Const WH_KEYBOARD_LL As Integer = 13
        Private Delegate Function LowLevelKeyboardProc(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr

        <StructLayout(LayoutKind.Sequential)>
        Private Structure KBDLLHOOKSTRUCT
            Public vkCode As Integer
            Public scanCode As Integer
            Public flags As Integer
            Public time As Integer
            Public dwExtraInfo As IntPtr
        End Structure

        <DllImport("user32.dll", SetLastError:=True)>
        Private Shared Function SetWindowsHookEx(idHook As Integer, lpfn As LowLevelKeyboardProc, hMod As IntPtr, dwThreadId As Integer) As IntPtr
        End Function
        <DllImport("user32.dll")>
        Private Shared Function UnhookWindowsHookEx(hhk As IntPtr) As Boolean
        End Function
        <DllImport("user32.dll")>
        Private Shared Function CallNextHookEx(hhk As IntPtr, nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
        End Function
        <DllImport("kernel32.dll", CharSet:=CharSet.Unicode)>
        Private Shared Function GetModuleHandle(name As String) As IntPtr
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetAsyncKeyState(vKey As Integer) As Short
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetForegroundWindow() As IntPtr
        End Function
        <DllImport("user32.dll")>
        Private Shared Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef pid As Integer) As Integer
        End Function

        Private ReadOnly _down As New HashSet(Of Integer)

        Private Function HookProc(nCode As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
            Try
                If nCode >= 0 Then
                    Dim k = Marshal.PtrToStructure(Of KBDLLHOOKSTRUCT)(lParam)
                    If k.vkCode >= Keys.F13 AndAlso k.vkCode <= Keys.F24 AndAlso ForUs() Then
                        Dim msg = wParam.ToInt32()
                        Dim isDown = (msg = WM_KEYDOWN OrElse msg = WM_SYSKEYDOWN)
                        Dim mods As Keys = Keys.None
                        If GetAsyncKeyState(&H11) < 0 Then mods = mods Or Keys.Control
                        If GetAsyncKeyState(&H10) < 0 Then mods = mods Or Keys.Shift
                        If GetAsyncKeyState(&H12) < 0 Then mods = mods Or Keys.Alt
                        Dim f As KeyFunction = Nothing
                        If _map.TryGetValue(CType(k.vkCode, Keys) Or mods, f) OrElse _down.Contains(k.vkCode) Then
                            If isDown Then
                                If Not _down.Contains(k.vkCode) AndAlso f IsNot Nothing Then
                                    _down.Add(k.vkCode)
                                    Dim fn = f
                                    _invoker.BeginInvoke(New MethodInvoker(Sub() RunInLastWindow(fn)))
                                End If
                            Else
                                _down.Remove(k.vkCode)
                            End If
                            Return New IntPtr(1)     ' swallow: nobody else sees the invisible key
                        End If
                    End If
                End If
            Catch
            End Try
            Return CallNextHookEx(_hook, nCode, wParam, lParam)
        End Function

        ' Only when Alibre (this process) or 3Dconnexion's menu window is in front - not for a second Alibre
        Private Shared Function ForUs() As Boolean
            Try
                Dim pid As Integer
                GetWindowThreadProcessId(GetForegroundWindow(), pid)
                If pid = Diagnostics.Process.GetCurrentProcess().Id Then Return True
                Using p = Diagnostics.Process.GetProcessById(pid)
                    Return p.ProcessName.StartsWith("3Dx", StringComparison.OrdinalIgnoreCase)
                End Using
            Catch
                Return False
            End Try
        End Function

        Private Sub RunInLastWindow(f As KeyFunction)
            Dim form As Form = Form.ActiveForm
            If form Is Nothing OrElse TypeOf form Is Toast Then form = _lastForm
            If form Is Nothing OrElse form.IsDisposed Then
                _log("'" & f.Name & "': no Alibre window to run it in")
                Return
            End If
            Run(f, form)
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
                    item = AlibreCommands.FindItem(cf, f.Command, f.Name, f.Shortcut)
                    If item IsNot Nothing Then Exit For
                Next
                If item IsNot Nothing Then
                    Dim why As String = ""
                    If AlibreCommands.Available(item, why) Then
                        AlibreCommands.Click(item)
                        _log("Ran '" & f.Name & "' in '" & form.Text & "' (" & why & ")")
                    Else
                        _log("'" & f.Name & "' is not available in '" & form.Text & "' (" & why & ")")
                        Toast.Show(f.Name & " is not available here")
                    End If
                ElseIf Not String.IsNullOrWhiteSpace(f.Shortcut) Then
                    ' not a ribbon command (e.g. an Alibre Script shortcut): type the shortcut
                    _log("'" & f.Name & "' not in the ribbon of '" & form.Text & "', typing " & f.Shortcut)
                    Dim keys = ToSendKeys(f.Shortcut)
                    If keys <> "" Then SendKeys.Send(keys)
                Else
                    _log("'" & f.Name & "' is not in the ribbon of '" & form.Text & "'")
                    Toast.Show(f.Name & " is not available in this window")
                End If
            Catch ex As Exception
                _log("Run '" & f.Name & "' failed: " & ex.Message)
            End Try
        End Sub

        ' "CTRL+ALT+B" -> "^%b" for SendKeys
        Private Shared Function ToSendKeys(shortcut As String) As String
            Dim mods = ""
            Dim key = ""
            Dim parts = shortcut.ToUpperInvariant().Split("+"c)
            For i = 0 To parts.Length - 1
                Dim p = parts(i).Trim()
                Select Case p
                    Case "CTRL", "CONTROL" : mods &= "^"
                    Case "ALT" : mods &= "%"
                    Case "SHIFT" : mods &= "+"
                    Case "" : If i = parts.Length - 1 Then key = "{+}"
                    Case "ENTER" : key = "{ENTER}"
                    Case "ESC" : key = "{ESC}"
                    Case "DELETE" : key = "{DEL}"
                    Case "BACKSPACE" : key = "{BS}"
                    Case "TAB" : key = "{TAB}"
                    Case "SPACE" : key = " "
                    Case "INSERT" : key = "{INS}"
                    Case "HOME", "END", "PGUP", "PGDN", "UP", "DOWN", "LEFT", "RIGHT" : key = "{" & p & "}"
                    Case Else
                        If p.Length > 1 AndAlso p(0) = "F"c Then
                            key = "{" & p & "}"
                        ElseIf p.Length = 1 AndAlso "+^%~(){}[]".Contains(p) Then
                            key = "{" & p & "}"
                        Else
                            key = p.ToLowerInvariant()
                        End If
                End Select
            Next
            If key = "" Then Return ""
            Return mods & key
        End Function
    End Class

End Namespace
