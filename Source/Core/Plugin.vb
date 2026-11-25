' KOCH SpaceMouse - entry point called by the loader add-on
' (c) 2026 KOCH Engineering - MIT License
'
' The loader (KochSpaceMouse.dll) reads this assembly into memory, so the file is never
' locked and can be replaced while Alibre Design is running ("Reload" in the menu).

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

        Public Shared ReadOnly Property Version As String
            Get
                Return GetType(Plugin).Assembly.GetName().Version.ToString()
            End Get
        End Property

        ''' <param name="getContext">Returns "Part", "Assembly", "Sketch", "Drawing" or "" for the active Alibre window.</param>
        Public Sub New(installFolder As String, getContext As Func(Of String), log As Action(Of String))
            _installFolder = installFolder
            _getContext = getContext
            _log = log
            Diag.Log = log
            _settings = Settings.Load(installFolder)
            _log("Core " & Version & " loaded; " & _settings.Functions.Count & " functions")
            _timer = New Timer With {.Interval = 500}
            AddHandler _timer.Tick, AddressOf OnTick
            _timer.Start()
        End Sub

        Private Sub OnTick(sender As Object, e As EventArgs)
            If Not _settings.AutoSwitch Then Return
            Dim ctx As String = ""
            Try
                ctx = _getContext()
            Catch
            End Try
            If ctx = "" OrElse ctx = _applied Then Return
            Apply(ctx)
        End Sub

        ''' <summary>Writes the layout for this context into the 3DxWare profile.</summary>
        Public Sub Apply(ctx As String)
            Try
                Dim ids = _settings.Layout(ctx)
                Dim kbIds = _settings.Layout(ctx & Contexts.KeyboardSuffix)
                Dim fs(Contexts.KeyCount - 1) As KeyFunction
                Dim kb(Contexts.KeyCount - 1) As KeyFunction
                For i = 0 To Contexts.KeyCount - 1
                    fs(i) = _settings.Find(ids(i))
                    kb(i) = _settings.Find(kbIds(i))
                Next
                Dim file = DxWareProfile.Write(fs, kb, AddressOf Icons.LcdPng)
                _applied = ctx
                _log("Applied " & ctx & " layout to " & file)
            Catch ex As Exception
                _applied = ctx   ' don't retry every tick
                _log("Applying " & ctx & " failed: " & ex.ToString())
            End Try
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
            Dim target = If(ctx = "", "Part", ctx)
            Apply(target)
            MessageBox.Show(owner, "Saved. The keys now show the " & target & " layout." &
                            If(_settings.AutoSwitch, vbCrLf & "They will follow the active Alibre window.", ""),
                            "3Dconnexion keys", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Stop()
            _timer.Dispose()
        End Sub
    End Class

End Namespace
