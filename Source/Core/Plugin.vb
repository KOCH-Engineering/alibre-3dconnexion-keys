' KOCH SpaceMouse - entry point called by the loader add-on
' (c) 2026 KOCH Engineering - MIT License
'
' The loader (KochSpaceMouse.dll) reads this assembly into memory, so the file is never
' locked and can be replaced while Alibre Design is running ("Reload" in the menu).

Imports System.Collections.Generic
Imports System.IO
Imports System.Windows.Forms

Namespace KochSpaceMouse

    Public Class Plugin
        Implements IDisposable

        Private ReadOnly _installFolder As String
        Private ReadOnly _getContext As Func(Of String)
        Private ReadOnly _log As Action(Of String)
        Private ReadOnly _timer As Timer
        Private _settings As Settings
        Private _applied As String = ""
        Private ReadOnly _runner As CommandRunner
        Private _hidden As New Dictionary(Of String, String)

        Public Shared Property AddOnFolder As String = ""

        Public Shared ReadOnly Property Version As String
            Get
                Return GetType(Plugin).Assembly.GetName().Version.ToString(3)
            End Get
        End Property

        ''' <param name="getContext">Returns "Part", "Assembly", "Sketch", "Drawing" or "" for the active Alibre window.</param>
        Public Sub New(installFolder As String, getContext As Func(Of String), log As Action(Of String))
            _installFolder = installFolder
            AddOnFolder = installFolder
            _getContext = getContext
            _log = log
            Diag.Log = log
            _settings = Settings.Load(installFolder)
            _log("Core " & Version & " loaded; " & _settings.Functions.Count & " functions")
            _runner = New CommandRunner(log)
            UpdateRunner()
            Application.AddMessageFilter(_runner)
            _timer = New Timer With {.Interval = 500}
            AddHandler _timer.Tick, AddressOf OnTick
            _timer.Start()
        End Sub

        Private _designTicks As Integer
        Private _sketchLogged As Boolean?
        Private _prewarmed As Boolean

        Private Sub OnTick(sender As Object, e As EventArgs)
            Dim ctx As String = ""
            Try
                ctx = _getContext()
            Catch
            End Try
            ' read Alibre's command list in the background ~5 s after the first design window, so the dialog opens fast
            If Not _prewarmed AndAlso ctx <> "" Then
                _designTicks += 1
                If _designTicks >= 10 Then
                    _prewarmed = True
                    Try
                        Dim n = AlibreCommands.ScanCached().Count
                        _log("Pre-read " & n & " Alibre commands: " & String.Join("; ", AlibreCommands.LastReport.Where(Function(l) l.StartsWith("Scan took") OrElse l.StartsWith("Commands found"))))
                    Catch ex As Exception
                        _log("Pre-read failed: " & ex.Message)
                    End Try
                End If
            End If
            If ctx <> "" Then _runner.Track(Form.ActiveForm)
            ' a part window with the sketch tools enabled is a sketch (the loader's API check does not see it)
            If ctx = "Part" AndAlso Form.ActiveForm IsNot Nothing Then
                Dim sk As Boolean? = Nothing
                Try
                    sk = AlibreCommands.SketchToolsEnabled(Form.ActiveForm)
                Catch
                End Try
                If sk.HasValue AndAlso sk.Value Then ctx = "Sketch"
                If sk.HasValue AndAlso (Not _sketchLogged.HasValue OrElse _sketchLogged.Value <> sk.Value) Then
                    _sketchLogged = sk
                    _log("Sketch tools " & If(sk.Value, "enabled -> Sketch", "disabled -> Part"))
                End If
            End If
            If Not _settings.AutoSwitch Then Return
            If ctx = "" OrElse ctx = _applied Then Return
            Apply(ctx)
        End Sub

        Private Sub UpdateRunner()
            _hidden = _settings.HiddenKeys()
            _runner.SetMap(_settings, _hidden)
            If _hidden.Count > 0 Then _log(_hidden.Count & " functions are run by the add-on")
        End Sub

        ''' <summary>The function as 3DxWare should send it: its own shortcut, or its invisible key.</summary>
        Private Function Effective(f As KeyFunction) As KeyFunction
            If f Is Nothing Then Return f
            Dim hk As String = Nothing
            If Not _hidden.TryGetValue(f.Id, hk) Then Return f
            ' Shortcut = what 3DxWare sends (the invisible key); the runner still knows the real shortcut from the catalog
            Return New KeyFunction With {.Id = f.Id, .Name = f.Name, .Shortcut = hk, .Category = f.Category,
                                         .Icon = f.Icon, .Command = f.Command}
        End Function

        ''' <summary>Writes the layout for this context into the 3DxWare profile.</summary>
        Public Sub Apply(ctx As String)
            Try
                Dim ids = _settings.Layout(ctx)
                Dim kbIds = _settings.Layout(ctx & Contexts.KeyboardSuffix)
                Dim rmIds = _settings.Layout(ctx & Contexts.RadialSuffix)
                Dim fs(Contexts.KeyCount - 1) As KeyFunction
                Dim kb(Contexts.KeyCount - 1) As KeyFunction
                Dim rm(Contexts.RadialSlots - 1) As KeyFunction
                For i = 0 To Contexts.KeyCount - 1
                    fs(i) = Effective(_settings.Find(ids(i)))
                    kb(i) = Effective(_settings.Find(kbIds(i)))
                    If i < Contexts.RadialSlots Then rm(i) = Effective(_settings.Find(rmIds(i)))
                Next
                Dim file = DxWareProfile.Write(fs, kb, rm, ctx, AddressOf Icons.LcdPng)
                _applied = ctx
                _log("Applied " & ctx & " layout to " & file)
            Catch ex As Exception
                _applied = ctx   ' don't retry every tick
                _log("Applying " & ctx & " failed: " & ex.ToString())
            End Try
        End Sub

        Public Sub ShowAbout(owner As IWin32Window)
            Using f As New AboutForm()
                f.ShowDialog(owner)
            End Using
        End Sub

        Public Sub ShowConfig(owner As IWin32Window)
            Dim ctx = ""
            Try
                ctx = _getContext()
            Catch
            End Try
            ' edit a copy, so Cancel really cancels
            Dim copy = Settings.FromXml(_settings.ToXml())
            Using f As New ConfigForm(copy, ctx)
                If f.ShowDialog(owner) <> DialogResult.OK Then Return
            End Using
            _settings = copy
            _settings.Save()
            UpdateRunner()
            Dim target = If(ctx = "", "Part", ctx)
            Apply(target)
            MessageBox.Show(owner, "Saved. The keys now show the " & target & " layout." &
                            If(_settings.AutoSwitch, vbCrLf & "They will follow the active Alibre window.", ""),
                            "3Dconnexion keys", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Stop()
            _timer.Dispose()
            Application.RemoveMessageFilter(_runner)
            _runner.Remove()
        End Sub
    End Class

End Namespace
