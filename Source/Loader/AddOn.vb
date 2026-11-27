' KOCH SpaceMouse - Alibre Design add-on (loader)
' (c) 2026 KOCH Engineering - MIT License
'
' This small DLL is what Alibre loads and locks. It reads KochSpaceMouse.Core.dll into memory
' (so that file is never locked) and hands it the active Alibre context. After installing a new
' core, "Reload" in the menu picks it up without restarting Alibre Design.

Option Strict Off

Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms
Imports AlibreAddOn
Imports AlibreX

Namespace AlibreAddOnAssembly

    Public Module AlibreAddOn
        Private _root As IADRoot
        Private _addOn As SpaceMouseAddOn

        Public Sub AddOnLoad(ByVal hwnd As IntPtr, ByVal pAutomationHook As IAutomationHook, ByVal unused As IntPtr)
            _root = CType(pAutomationHook.Root, IADRoot)
            _addOn = New SpaceMouseAddOn(_root, hwnd)
        End Sub

        Public Sub AddOnUnload(ByVal hwnd As IntPtr, ByVal forceUnload As Boolean, ByRef cancel As Boolean, ByVal reserved1 As Integer, ByVal reserved2 As Integer)
            If _addOn IsNot Nothing Then _addOn.Shutdown()
            _addOn = Nothing
            _root = Nothing
        End Sub

        Public Function GetRoot() As IADRoot
            Return _root
        End Function

        Public Sub AddOnInvoke(ByVal hwnd As IntPtr, ByVal pAutomationHook As IntPtr, ByVal sessionName As String, ByVal isLicensed As Boolean, ByVal reserved1 As Integer, ByVal reserved2 As Integer)
        End Sub

        Public Function GetAddOnInterface() As IAlibreAddOn
            Return CType(_addOn, IAlibreAddOn)
        End Function
    End Module

    Public Class SpaceMouseAddOn
        Implements IAlibreAddOn

        Private Const ROOT_ID As Integer = 5800
        Private Const CMD_CONFIG As Integer = 5801
        Private Const CMD_RELOAD As Integer = 5802
        Private Const CMD_ABOUT As Integer = 5803

        Private ReadOnly _root As IADRoot
        Private ReadOnly _hwnd As IntPtr
        Private ReadOnly _folder As String
        Private _core As Object
        Private _coreVersion As String = "?"

        Public Sub New(root As IADRoot, hwnd As IntPtr)
            _root = root
            _hwnd = hwnd
            _folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            LoadCore()
        End Sub

        ' ---------------------------------------------------------------- core loading

        Private Sub LoadCore()
            Try
                Dim file = Path.Combine(_folder, "KochSpaceMouse.Core.dll")
                Dim asm = Assembly.Load(IO.File.ReadAllBytes(file))
                _coreVersion = asm.GetName().Version.ToString(3)
                Dim t = asm.GetType("KochSpaceMouse.Plugin", True)
                Dim getContext As Func(Of String) = AddressOf CurrentContext
                Dim logIt As Action(Of String) = AddressOf Log.Write
                _core = Activator.CreateInstance(t, New Object() {_folder, getContext, logIt})
            Catch ex As Exception
                _core = Nothing
                Log.Write("Loading the core failed: " & ex.ToString())
            End Try
        End Sub

        Private Sub UnloadCore()
            Try
                If _core IsNot Nothing Then CType(_core, IDisposable).Dispose()
            Catch ex As Exception
                Log.Write("Disposing the core failed: " & ex.Message)
            End Try
            _core = Nothing
        End Sub

        Public Sub Shutdown()
            UnloadCore()
        End Sub

        ' ---------------------------------------------------------------- active context

        ''' <summary>"Part", "Assembly", "Sketch", "Drawing" or "" for the active Alibre window.</summary>
        Private Function CurrentContext() As String
            Dim s As Object = Nothing
            Try
                s = _root.TopmostSession
            Catch
                Return ""
            End Try
            If s Is Nothing Then Return ""
            If TypeOf s Is IADDrawingSession Then Return "Drawing"
            If TypeOf s Is IADAssemblySession Then Return "Assembly"
            If TypeOf s Is IADPartSession Then
                If SketchActive(s) Then Return "Sketch"
                Return "Part"
            End If
            Return ""
        End Function

        Private Shared Function SketchActive(part As Object) As Boolean
            Try
                For Each sk In part.Sketches
                    If sk.IsActive Then Return True
                Next
            Catch
            End Try
            Try
                For Each sk In part.Sketches3D
                    If sk.IsActive Then Return True
                Next
            Catch
            End Try
            Return False
        End Function

        ' ---------------------------------------------------------------- IAlibreAddOn

        Public ReadOnly Property RootMenuItem As Integer Implements IAlibreAddOn.RootMenuItem
            Get
                Return ROOT_ID
            End Get
        End Property

        Public Function HasSubMenus(menuId As Integer) As Boolean Implements IAlibreAddOn.HasSubMenus
            Return menuId = ROOT_ID
        End Function

        Public Function SubMenuItems(menuId As Integer) As Array Implements IAlibreAddOn.SubMenuItems
            If menuId <> ROOT_ID Then Return Nothing
            Return New Integer() {CMD_CONFIG, CMD_ABOUT, CMD_RELOAD}
        End Function

        Public Function MenuItemText(menuId As Integer) As String Implements IAlibreAddOn.MenuItemText
            Select Case menuId
                Case ROOT_ID : Return "3Dconnexion"
                Case CMD_CONFIG : Return "3Dconnexion keys…"
                Case CMD_ABOUT : Return "About…"
                Case CMD_RELOAD : Return "Reload add-on"
                Case Else : Return String.Empty
            End Select
        End Function

        Public Function MenuItemToolTip(menuId As Integer) As String Implements IAlibreAddOn.MenuItemToolTip
            Select Case menuId
                Case CMD_CONFIG : Return "Choose the functions on the keys of your SpaceMouse Enterprise and Keyboard Pro"
                Case CMD_ABOUT : Return "KOCH Engineering · Henrik J. Koch · Denmark"
                Case CMD_RELOAD : Return "Load a newly installed version without restarting Alibre Design"
                Case Else : Return "KOCH 3Dconnexion Keys"
            End Select
        End Function

        Public Function MenuItemState(menuId As Integer, sessionIdentifier As String) As ADDONMenuStates Implements IAlibreAddOn.MenuItemState
            Return ADDONMenuStates.ADDON_MENU_ENABLED
        End Function

        Public Function MenuIcon(menuID As Integer) As String Implements IAlibreAddOn.MenuIcon
            If menuID = ROOT_ID Then Return String.Empty
            Return "SpaceMouse.ico"
        End Function

        Public Function PopupMenu(menuId As Integer) As Boolean Implements IAlibreAddOn.PopupMenu
            Return False
        End Function

        Public Function InvokeCommand(menuId As Integer, sessionIdentifier As String) As IAlibreAddOnCommand Implements IAlibreAddOn.InvokeCommand
            Dim owner As IWin32Window = New AlibreWindow(_hwnd)
            Try
                Select Case menuId
                    Case CMD_CONFIG, CMD_ABOUT
                        If _core Is Nothing Then LoadCore()
                        If _core Is Nothing Then
                            MessageBox.Show(owner, "The 3Dconnexion keys add-on could not start. See the log:" & vbCrLf & Log.FilePath, "3Dconnexion keys")
                        ElseIf menuId = CMD_ABOUT Then
                            _core.ShowAbout(owner)
                        Else
                            _core.ShowConfig(owner)
                        End If
                    Case CMD_RELOAD
                        Dim before = _coreVersion
                        UnloadCore()
                        LoadCore()
                        Log.Write("Reloaded core " & before & " -> " & _coreVersion)
                        MessageBox.Show(owner, If(_core Is Nothing, "Reload failed - see the log:" & vbCrLf & Log.FilePath,
                                                  "3Dconnexion keys add-on reloaded (version " & _coreVersion & ")."), "3Dconnexion keys")
                End Select
            Catch ex As Exception
                Log.Write("Command " & menuId & " failed: " & ex.ToString())
                MessageBox.Show(owner, "Something went wrong: " & ex.Message & vbCrLf & vbCrLf & "Log: " & Log.FilePath, "3Dconnexion keys")
            End Try
            Return Nothing
        End Function

        Public Function HasPersistentDataToSave(sessionIdentifier As String) As Boolean Implements IAlibreAddOn.HasPersistentDataToSave
            Return False
        End Function

        Public Sub SaveData(pCustomData As IStream, sessionIdentifier As String) Implements IAlibreAddOn.SaveData
        End Sub

        Public Sub LoadData(pCustomData As IStream, sessionIdentifier As String) Implements IAlibreAddOn.LoadData
        End Sub

        Public Function UseDedicatedRibbonTab() As Boolean Implements IAlibreAddOn.UseDedicatedRibbonTab
            Return False
        End Function

        Private Sub IAlibreAddOn_setIsAddOnLicensed(isLicensed As Boolean) Implements IAlibreAddOn.setIsAddOnLicensed
        End Sub
    End Class

    Friend Class AlibreWindow
        Implements IWin32Window
        Private ReadOnly _h As IntPtr
        Public Sub New(h As IntPtr)
            _h = h
        End Sub
        Public ReadOnly Property Handle As IntPtr Implements IWin32Window.Handle
            Get
                Return _h
            End Get
        End Property
    End Class

    Public Module Log
        Public ReadOnly FilePath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Alibre AddOns", "KochSpaceMouse", "addon.log")
        Public Sub Write(msg As String)
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath))
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "  " & msg & Environment.NewLine)
            Catch
            End Try
        End Sub
    End Module

End Namespace
